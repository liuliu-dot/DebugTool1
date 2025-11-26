using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using DebugTool.Core;
using DebugTool.Models;
using System.Text;

namespace DebugTool.Services
{
    public class DeviceServiceVDC_32
    {
        private readonly ModbusClientVDC_32 _modbusClient;
        private byte _slaveId;
        private WatchdogMonitor _watchdog;
        private System.Timers.Timer _pollTimer;
        private bool _isPolling = false;

        public event Action<List<ChannelData>> DataUpdated;
        public event Action<IoStatus> IoStatusUpdated;
        public event Action<string> ErrorOccurred;
        public event Action ConnectionLost;
        public List<ChannelData> LastData { get; private set; }

        public byte SlaveId => _slaveId;
        public bool IsConnected => _modbusClient.IsConnected;

        public DeviceServiceVDC_32()
        {
            _modbusClient = new ModbusClientVDC_32();
            _modbusClient.OnConnectionLost += () =>
            {
                StopPolling();
                _watchdog.Stop();
                ConnectionLost?.Invoke();
            };
            _watchdog = new WatchdogMonitor(5, (msg) => ErrorOccurred?.Invoke(msg));
            _pollTimer = new System.Timers.Timer(500);
            _pollTimer.AutoReset = true;
            _pollTimer.Elapsed += async (s, e) => await PollAllDataAsync();
        }

        #region 连接管理

        public async Task<bool> ConnectAsync(string portName, int baudRate, byte slaveId, CancellationToken token = default)
        {
            _slaveId = slaveId;
            bool success = await _modbusClient.ConnectAsync(portName, baudRate, token);
            if (success)
            {
                // 串口通常不需要额外握手，但也建议加上
                _watchdog.Start();
                StartPolling();
            }
            return success;
        }

        // ★★★ 核心修复：增加连接后的“握手验证” ★★★
        public async Task<bool> ConnectTcpAsync(string ip, int port, byte slaveId, CancellationToken token = default)
        {
            _slaveId = slaveId;

            // 1. 尝试建立 TCP 物理连接
            bool success = await _modbusClient.ConnectTcpAsync(ip, port, token);

            if (success)
            {
                // 2. 【新增】应用层握手：尝试读取一次版本号
                // 如果设备不存在（物理断开），这里会等待超时（3秒）并抛出异常
                try
                {
                    AuditLogger.Log("连接", "[VDC-32] 正在验证设备响应(握手)...");
                    await GetVersionAsync(); // 发送 03 指令读取版本
                    AuditLogger.Log("连接", "[VDC-32] 设备握手验证成功");

                    // 3. 握手成功，才启动轮询
                    _watchdog.Start();
                    StartPolling();
                }
                catch (Exception ex)
                {
                    // 4. 握手失败，强制断开连接，并返回 false
                    AuditLogger.Log("连接", $"[VDC-32] 连接失败: Socket已通但设备无响应 ({ex.Message})", false);
                    await DisconnectAsync(); // 关掉刚才打开的 TCP 连接
                    return false; // 告诉 UI 连接失败
                }
            }
            return success;
        }

        public async Task DisconnectAsync()
        {
            StopPolling();
            _watchdog.Stop();
            await _modbusClient.DisconnectAsync();
        }

        public void Disconnect()
        {
            StopPolling();
            _watchdog.Stop();
            _modbusClient.DisconnectAsync().Wait();
        }

        private void StartPolling() { if (!_pollTimer.Enabled) _pollTimer.Start(); }
        private void StopPolling() { if (_pollTimer.Enabled) _pollTimer.Stop(); }
        #endregion

        #region 数据轮询 (保持不变)
        public async Task PollAllDataAsync()
        {
            if (!IsConnected || _isPolling) return;
            _isPolling = true;
            try
            {
                var channels = await GetAllChannelDataAsync();
                LastData = channels;
                DataUpdated?.Invoke(channels);
                var ioStatus = await GetRealTimeStatusAsync();
                IoStatusUpdated?.Invoke(ioStatus);
                _watchdog.Feed();
            }
            catch (Exception ex) { if (IsConnected) ErrorOccurred?.Invoke(ex.Message); }
            finally { _isPolling = false; }
        }

        public async Task<List<ChannelData>> GetAllChannelDataAsync()
        {
            var v = _modbusClient.ReadHoldingRegistersAsync(_slaveId, 0x8010, 32);
            var t = _modbusClient.ReadHoldingRegistersAsync(_slaveId, 0x8030, 32);
            var s = _modbusClient.ReadHoldingRegistersAsync(_slaveId, 0x8005, 2);
            await Task.WhenAll(v, t, s);
            ushort[] vs = await v; ushort[] ts = await t; ushort[] ss = await s;
            List<ChannelData> r = new List<ChannelData>();
            for (int i = 0; i < 32; i++)
            {
                double vol = (vs[i] > 0x8000) ? (vs[i] - 0x8000) / 100.0 : vs[i] / 1000.0;
                int ri = i / 16; int bi = i % 16; int sb = (ss[ri] >> bi) & 1;
                ChannelDropStatus stat = (sb == 0) ? ChannelDropStatus.OK : (ts[i] == 0 ? ChannelDropStatus.DROPPED : ChannelDropStatus.FLAGGED);
                r.Add(new ChannelData { Channel = i + 1, Voltage = vol, Status = stat, RecoveryTime = ts[i] });
            }
            return r;
        }

        public async Task<IoStatus> GetRealTimeStatusAsync()
        {
            ushort[] d = await _modbusClient.ReadHoldingRegistersAsync(_slaveId, 0x8004, 1);
            ushort r = d[0];
            return new IoStatus
            {
                Io0OutputLow = (r & 1) != 0,
                Io1OutputLow = (r & 2) != 0,
                Io2OutputLow = (r & 4) != 0,
                Io3OutputLow = (r & 8) != 0,
                S1Switch = (r & 0x100) != 0,
                WaterLeakSelf = (r & 0x200) != 0,
                WaterLeakParallel = (r & 0x400) != 0,
                JigInPlace = (r & 0x800) != 0,
                ContactorSignal = (r & 0x1000) != 0,
                FanStatus = (r & 0x2000) != 0,
                AcOnDependsOnJig = (r & 0x4000) != 0
            };
        }
        #endregion

        #region 业务指令 (保持不变)
        public async Task SetIoOutputAsync(IoCommand c) => await _modbusClient.WriteMultipleRegistersAsync(_slaveId, 0x8004, new ushort[] { (ushort)c });
        public async Task<string> GetVersionAsync() { var d = await _modbusClient.ReadHoldingRegistersAsync(_slaveId, 0x8001, 1); return (d[0] / 10.0).ToString("F1"); }
        public async Task SetSlaveId(byte id) { await _modbusClient.WriteMultipleRegistersAsync(_slaveId, 0x8000, new ushort[] { id }); _slaveId = id; }
        public async Task SetAllThresholdsAsync(double[] th) { ushort[] d = new ushort[th.Length]; for (int i = 0; i < th.Length; i++) d[i] = (ushort)(th[i] * 100); await _modbusClient.WriteMultipleRegistersAsync(_slaveId, 0x8050, d); }
        public async Task ClearAllDropFlagsAsync() => await _modbusClient.WriteMultipleRegistersAsync(_slaveId, 0x8005, new ushort[] { 0, 0 });
        public async Task<string> GetSerialNumberAsync()
        {
            ushort[] d = await _modbusClient.ReadHoldingRegistersAsync(_slaveId, 0x8820, 31);
            List<byte> b = new List<byte>(); foreach (var u in d) { b.Add((byte)(u >> 8)); b.Add((byte)(u & 0xFF)); }
            return Encoding.ASCII.GetString(b.ToArray()).TrimEnd('\0');
        }
        public async Task SetSerialNumberAsync(string sn)
        {
            if (string.IsNullOrEmpty(sn)) return;
            byte[] b = Encoding.ASCII.GetBytes(sn); if (b.Length % 2 != 0) { Array.Resize(ref b, b.Length + 1); b[b.Length - 1] = 0; }
            ushort[] d = new ushort[b.Length / 2]; for (int i = 0; i < d.Length; i++) d[i] = (ushort)((b[i * 2] << 8) | b[i * 2 + 1]);
            ushort[] f = new ushort[31]; Array.Copy(d, f, d.Length);
            await _modbusClient.WriteMultipleRegistersAsync(_slaveId, 0x8820, f);
        }
        public async Task<double> GetCurrentTemperatureAsync() { var d = await _modbusClient.ReadHoldingRegistersAsync(_slaveId, 0x8008, 1); return d[0]; }
        public async Task<int> GetFanCurrentAsync() { var d = await _modbusClient.ReadHoldingRegistersAsync(_slaveId, 0x8009, 1); return d[0]; }
        public async Task<ushort> GetIoDirectionsAsync() { var d = await _modbusClient.ReadHoldingRegistersAsync(_slaveId, 0x8003, 1); return d[0]; }
        public async Task SetIoDirectionsAsync(ushort d) => await _modbusClient.WriteMultipleRegistersAsync(_slaveId, 0x8003, new ushort[] { d });
        public async Task<bool> GetAcOnDependencyAsync() { var d = await _modbusClient.ReadHoldingRegistersAsync(_slaveId, 0x800A, 1); return d[0] == 1; }
        public async Task SetAcOnDependencyAsync(bool e) => await _modbusClient.WriteMultipleRegistersAsync(_slaveId, 0x800A, new ushort[] { (ushort)(e ? 1 : 0) });
        public async Task<int> GetBaudRateAsync() { var d = await _modbusClient.ReadHoldingRegistersAsync(_slaveId, 0x8002, 1); return d[0]; }
        public async Task SetBaudRateBroadcastAsync(int b)
        {
            try { await _modbusClient.WriteMultipleRegistersAsync(0x00, 0x8002, new ushort[] { (ushort)b }); } catch (TimeoutException) { }
        }
        #endregion
    }
}