using System;
using System.Threading;
using System.Threading.Tasks;
using DebugTool.Services;

namespace DebugTool.Core
{
    public class ModbusClientVDC_32 : IDisposable
    {
        private ICommunicationChannel _channel;
        private readonly SemaphoreSlim _communicationLock = new SemaphoreSlim(1, 1);

        public event Action OnConnectionLost;

        public ModbusClientVDC_32() { }

        public bool IsConnected
        {
            get { return _channel != null && _channel.IsConnected; }
        }

        #region 连接管理

        public async Task<bool> ConnectAsync(string portName, int baudRate, CancellationToken token = default(CancellationToken))
        {
            await _communicationLock.WaitAsync(token);
            try
            {
                if (_channel != null) await DisconnectInternalAsync();

                _channel = new SerialCommunicationChannel(portName, baudRate);
                bool success = await _channel.ConnectAsync(token);

                if (success)
                    AuditLogger.Log("通信", $"[VDC-32] 串口连接成功: {portName}, 波特率: {baudRate}");
                else
                    AuditLogger.Log("通信", $"[VDC-32] 串口连接失败: {portName}", false);

                return success;
            }
            finally
            {
                _communicationLock.Release();
            }
        }

        public async Task<bool> ConnectTcpAsync(string ip, int port, CancellationToken token = default(CancellationToken))
        {
            await _communicationLock.WaitAsync(token);
            try
            {
                if (_channel != null) await DisconnectInternalAsync();

                _channel = new TcpCommunicationChannel(ip, port);
                bool success = await _channel.ConnectAsync(token);

                if (success)
                    AuditLogger.Log("通信", $"[VDC-32] TCP连接成功: {ip}:{port}");
                else
                    AuditLogger.Log("通信", $"[VDC-32] TCP连接失败: {ip}:{port}", false);

                return success;
            }
            finally
            {
                _communicationLock.Release();
            }
        }

        public async Task DisconnectAsync()
        {
            await _communicationLock.WaitAsync();
            try
            {
                await DisconnectInternalAsync();
                AuditLogger.Log("通信", "[VDC-32] 连接已手动断开");
            }
            finally
            {
                _communicationLock.Release();
            }
        }

        private async Task DisconnectInternalAsync()
        {
            if (_channel != null)
            {
                try
                {
                    await _channel.DisconnectAsync();
                    _channel.Dispose();
                }
                catch { }
                finally
                {
                    _channel = null;
                }
            }
        }

        #endregion

        public async Task<ushort[]> ReadHoldingRegistersAsync(byte slaveId, ushort startAddress, ushort numRegisters, CancellationToken token = default(CancellationToken))
        {
            byte[] frame = new byte[5];
            frame[0] = slaveId;
            frame[1] = 0x03;
            frame[2] = (byte)(startAddress >> 8);
            frame[3] = (byte)(startAddress & 0xFF);
            frame[4] = (byte)numRegisters;

            byte[] response = await SendAndReceiveAsync(frame, token);

            if (response.Length < 3 || response[1] != 0x03) throw new Exception("读取失败");
            int byteCount = response[2];
            ushort[] result = new ushort[byteCount / 2];
            for (int i = 0; i < result.Length; i++) result[i] = (ushort)((response[3 + i * 2] << 8) | response[4 + i * 2]);
            return result;
        }

        public async Task WriteMultipleRegistersAsync(byte slaveId, ushort startAddress, ushort[] data)
        {
            byte byteCount = (byte)(data.Length * 2);
            byte[] frame = new byte[7 + byteCount];
            frame[0] = slaveId; frame[1] = 0x10;
            frame[2] = (byte)(startAddress >> 8); frame[3] = (byte)(startAddress & 0xFF);
            frame[4] = (byte)(data.Length >> 8); frame[5] = (byte)(data.Length & 0xFF);
            frame[6] = byteCount;
            for (int i = 0; i < data.Length; i++)
            {
                frame[7 + i * 2] = (byte)(data[i] >> 8);
                frame[8 + i * 2] = (byte)(data[i] & 0xFF);
            }
            // 这里不强制传入 token，使用默认值
            await SendAndReceiveAsync(frame);
        }

        private async Task<byte[]> SendAndReceiveAsync(byte[] frame, CancellationToken token = default(CancellationToken))
        {
            if (_channel == null || !_channel.IsConnected) throw new Exception("未连接");

            await _communicationLock.WaitAsync(token);
            try
            {
                if (_channel == null || !_channel.IsConnected) throw new Exception("连接已断开");

                string txHex = BitConverter.ToString(frame).Replace("-", " ");
                AuditLogger.Log("通信", $"[VDC-32] TX: {txHex}");

                byte[] result = await _channel.SendAndReceiveAsync(frame, token);

                string rxHex = BitConverter.ToString(result).Replace("-", " ");
                AuditLogger.Log("通信", $"[VDC-32] RX: {rxHex}");

                return result;
            }
            catch (Exception ex)
            {
                // ★★★ 修复 C# 7.3 语法 ★★★
                bool isCancel = ex is OperationCanceledException;
                AuditLogger.Log("通信", $"[VDC-32] {(isCancel ? "操作已取消" : "错误")}: {ex.Message}", isCancel);

                if (!isCancel) OnConnectionLost?.Invoke();
                throw;
            }
            finally
            {
                _communicationLock.Release();
            }
        }

        public void Dispose()
        {
            try
            {
                if (_channel != null)
                {
                    _channel.DisconnectAsync().Wait(1000);
                    _channel.Dispose();
                }
            }
            catch { }
            _communicationLock?.Dispose();
        }
    }
}