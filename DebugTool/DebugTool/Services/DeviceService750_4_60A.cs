using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading; // ★★★ 必须引用 ★★★
using System.Threading.Tasks;
using System.Timers;
using DebugTool.Core;
using DebugTool.Models;

namespace DebugTool.Services
{
    public class DeviceService750_4_60A
    {
        private readonly ModbusClient750_4_60A _client;

        // 显式指定命名空间，解决与 System.Threading.Timer 的冲突
        private System.Timers.Timer _pollTimer;

        private bool _isPolling = false;
        private WatchdogMonitor _watchdog;

        public event Action<DeviceRealTimeData> DataUpdated;
        public event Action<string> ErrorOccurred;
        public DeviceRealTimeData LastData { get; private set; }

        public bool IsConnected => _client.IsConnected;

        public DeviceService750_4_60A()
        {
            _client = new ModbusClient750_4_60A();

            _pollTimer = new System.Timers.Timer(1000);
            _pollTimer.AutoReset = true;
            _pollTimer.Elapsed += async (s, e) => await PollDataAsync();

            _watchdog = new WatchdogMonitor(5, (msg) => ErrorOccurred?.Invoke($"[Load] {msg}"));
        }

        #region 连接管理

        public bool Connect(string portName, int baudRate = 9600)
        {
            if (_client.ConnectSerial(portName, baudRate))
            {
                _watchdog.Start();
                StartPolling();
                return true;
            }
            return false;
        }

        // ★★★ 核心修复：增加连接后的“握手验证” ★★★
        public async Task<bool> ConnectTcpAsync(string ip, int port, CancellationToken token = default)
        {
            // 1. 尝试建立 TCP 物理连接
            if (await _client.ConnectTcpAsync(ip, port, token))
            {
                // 2. 【新增】应用层握手：尝试读取版本号
                // 使用默认地址 1 进行测试，如果设备不在线，这里会超时抛出异常
                try
                {
                    AuditLogger.Log("连接", "[GJDD-750] 正在验证设备响应(握手)...");
                    await ReadVersionAsync(1);
                    AuditLogger.Log("连接", "[GJDD-750] 设备握手验证成功");

                    // 3. 握手成功，才启动轮询
                    _watchdog.Start();
                    StartPolling();
                    return true;
                }
                catch (Exception ex)
                {
                    // 4. 握手失败，强制断开连接
                    AuditLogger.Log("连接", $"[GJDD-750] 连接失败: Socket已通但设备无响应 ({ex.Message})", false);
                    Disconnect(); // 关掉刚才打开的 TCP 连接
                    return false; // 告诉 UI 连接失败
                }
            }
            return false;
        }

        public void Disconnect()
        {
            _watchdog.Stop();
            StopPolling();
            _client.Disconnect();
        }

        #endregion

        #region 轮询逻辑
        private void StartPolling() { if (!_pollTimer.Enabled) _pollTimer.Start(); }
        private void StopPolling() { if (_pollTimer.Enabled) _pollTimer.Stop(); }

        private async Task PollDataAsync()
        {
            if (_isPolling || !IsConnected) return;
            _isPolling = true;
            try
            {
                byte addr = 1;
                var data = await ReadRealTimeStatusAsync(addr);
                _watchdog.Feed();
                LastData = data;
                DataUpdated?.Invoke(data);
            }
            catch { /* 忽略轮询错误 */ }
            finally { _isPolling = false; }
        }
        #endregion

        #region 业务指令
        public async Task<string> ReadVersionAsync(byte addr)
        {
            byte[] response = await _client.SendAndReceiveAsync(addr, 0x09, new byte[0]);
            if (response.Length >= 2)
                return System.Text.Encoding.ASCII.GetString(response, 0, Math.Min(response.Length, 10)).TrimEnd('\0');
            return "未知";
        }

        public async Task SetSingleChannelConfigAsync(byte addr, ChannelLoadConfig config, bool saveToEEPROM)
        {
            if (config.ChannelIndex < 1 || config.ChannelIndex > 8) throw new ArgumentOutOfRangeException("通道号错误");
            byte[] info = new byte[7];
            info[0] = (byte)config.ChannelIndex;
            byte[] paramsBytes = ConvertConfigToBytes(config);
            Array.Copy(paramsBytes, 0, info, 1, 6);
            byte cmd = saveToEEPROM ? (byte)0x12 : (byte)0x11;
            await _client.SendAndReceiveAsync(addr, cmd, info);
        }

        public async Task SetAllChannelsConfigAsync(byte addr, List<ChannelLoadConfig> configs, bool saveToEEPROM)
        {
            var sortedConfigs = configs.OrderBy(c => c.ChannelIndex).ToList();
            byte[] info = new byte[48];
            for (int i = 0; i < 8; i++)
            {
                Array.Copy(ConvertConfigToBytes(sortedConfigs[i]), 0, info, i * 6, 6);
            }
            byte cmd = saveToEEPROM ? (byte)0x41 : (byte)0x40;
            await _client.SendAndReceiveAsync(addr, cmd, info);
        }

        public async Task SetOutputVoltageAsync(byte addr, double voltage)
        {
            if (voltage < 300 || voltage > 800) throw new ArgumentOutOfRangeException("电压必须在 300-800V 之间");
            ushort val = (ushort)(voltage * 20);
            byte[] info = new byte[2];
            info[0] = (byte)(val >> 8);
            info[1] = (byte)(val & 0xFF);
            await _client.SendAndReceiveAsync(addr, 0x0E, info);
        }

        public async Task<List<ChannelLoadConfig>> ReadAllChannelsConfigAsync(byte addr)
        {
            byte[] response = await _client.SendAndReceiveAsync(addr, 0x06, new byte[0]);
            if (response.Length < 48) throw new Exception($"读取配置失败，响应长度不足 (收到{response.Length}字节)");
            List<ChannelLoadConfig> results = new List<ChannelLoadConfig>();
            for (int i = 0; i < 8; i++)
            {
                byte[] chunk = new byte[6];
                Array.Copy(response, i * 6, chunk, 0, 6);
                results.Add(ConvertBytesToConfig(i + 1, chunk));
            }
            return results;
        }

        public async Task<DeviceRealTimeData> ReadRealTimeStatusAsync(byte addr)
        {
            byte[] response = await _client.SendAndReceiveAsync(addr, 0x0F, new byte[0]);
            if (response.Length < 65) return null;

            var result = new DeviceRealTimeData();
            for (int i = 0; i < 8; i++)
            {
                int baseIdx = i * 8;
                var ch = new ChannelRealTimeStatus
                {
                    ChannelIndex = i + 1,
                    RealVoltage = ((response[baseIdx] << 8) | response[baseIdx + 1]) / 20.0,
                    RealCurrent = ((response[baseIdx + 2] << 8) | response[baseIdx + 3]) / 100.0,
                    LlcVoltage = ((response[baseIdx + 4] << 8) | response[baseIdx + 5]) / 20.0,
                    StatusBits = (ushort)((response[baseIdx + 6] << 8) | response[baseIdx + 7]),
                    IsOnline = (response[baseIdx + 7] & 0x01) != 0
                };
                result.Channels.Add(ch);
            }

            byte b = response[64];
            var inv = result.Inverter;
            inv.StatusByte = b;
            inv.IsOverTemp = (b & 0x01) != 0;
            inv.IsAdFault = (b & 0x02) != 0;
            inv.OutputVoltageStatus = (b >> 2) & 0x03;
            inv.IsFanFault = (b & 0x10) != 0;
            inv.IsTimeout = (b & 0x20) != 0;
            inv.DcBusVoltageStatus = (b >> 6) & 0x03;

            return result;
        }

        private byte[] ConvertConfigToBytes(ChannelLoadConfig config)
        {
            byte[] data = new byte[6];
            data[0] = (byte)config.Mode;
            ushort vonVal = (ushort)(config.VonVoltage * 20);
            data[1] = (byte)(vonVal >> 8);
            data[2] = (byte)(vonVal & 0xFF);
            ushort loadVal = 0;
            switch (config.Mode)
            {
                case LoadMode.CC_Slow: case LoadMode.CC_Fast: loadVal = (ushort)(config.LoadValue * 100); break;
                case LoadMode.CV: loadVal = (ushort)(config.LoadValue * 20); break;
                case LoadMode.CP: case LoadMode.CR: loadVal = (ushort)(config.LoadValue * 10); break;
            }
            data[3] = (byte)(loadVal >> 8);
            data[4] = (byte)(loadVal & 0xFF);
            data[5] = (byte)config.AdditionalParam;
            return data;
        }

        private ChannelLoadConfig ConvertBytesToConfig(int channelIndex, byte[] data)
        {
            var config = new ChannelLoadConfig(channelIndex);
            config.Mode = (LoadMode)data[0];
            ushort vonRaw = (ushort)((data[1] << 8) | data[2]);
            config.VonVoltage = vonRaw / 20.0;
            ushort loadRaw = (ushort)((data[3] << 8) | data[4]);
            switch (config.Mode)
            {
                case LoadMode.CC_Slow: case LoadMode.CC_Fast: config.LoadValue = loadRaw / 100.0; break;
                case LoadMode.CV: config.LoadValue = loadRaw / 20.0; break;
                case LoadMode.CP: case LoadMode.CR: config.LoadValue = loadRaw / 10.0; break;
            }
            config.AdditionalParam = data[5];
            return config;
        }
        #endregion
    }
}