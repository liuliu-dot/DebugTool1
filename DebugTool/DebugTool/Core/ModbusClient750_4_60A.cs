using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using DebugTool.Services;

namespace DebugTool.Core
{
    public class ModbusClient750_4_60A : IDisposable
    {
        private const byte SOI = 0xEE;
        private const byte EOI = 0xEF;
        private const byte ROI = 0xED;

        private SerialPort _serialPort;
        private TcpClient _tcpClient;
        private NetworkStream _networkStream;
        private bool _isTcpMode = false;

        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
        private const int ReceiveTimeout = 3000;

        public bool IsConnected
        {
            get
            {
                if (_isTcpMode) return _tcpClient != null && _tcpClient.Connected;
                return _serialPort != null && _serialPort.IsOpen;
            }
        }

        public ModbusClient750_4_60A()
        {
            _serialPort = new SerialPort();
        }

        #region 连接管理

        public bool ConnectSerial(string portName, int baudRate)
        {
            Disconnect();
            _isTcpMode = false;
            try
            {
                AuditLogger.Log("通信", $"[GJDD-750] 开始连接串口: {portName}, 波特率: {baudRate}");
                _serialPort.PortName = portName;
                _serialPort.BaudRate = baudRate;
                _serialPort.DataBits = 8;
                _serialPort.Parity = Parity.None;
                _serialPort.StopBits = StopBits.One;
                _serialPort.ReadTimeout = 2000;
                _serialPort.WriteTimeout = 1000;
                _serialPort.Open();
                AuditLogger.Log("通信", $"[GJDD-750] 串口连接成功: {portName}");
                return true;
            }
            catch (Exception ex)
            {
                AuditLogger.Log("通信", $"[GJDD-750] 串口连接失败: {ex.Message}", false);
                return false;
            }
        }

        public async Task<bool> ConnectTcpAsync(string ip, int port, CancellationToken token = default(CancellationToken))
        {
            Disconnect();
            _isTcpMode = true;
            try
            {
                AuditLogger.Log("通信", $"[GJDD-750] 开始连接TCP: {ip}:{port}");
                _tcpClient = new TcpClient();
                _tcpClient.ReceiveTimeout = ReceiveTimeout;
                _tcpClient.SendTimeout = 1000;

                var connectTask = _tcpClient.ConnectAsync(ip, port);
                var cancelTask = Task.Delay(-1, token);

                var completedTask = await Task.WhenAny(connectTask, cancelTask);

                if (completedTask == connectTask)
                {
                    await connectTask;
                    _networkStream = _tcpClient.GetStream();
                    AuditLogger.Log("通信", $"[GJDD-750] TCP连接成功");
                    return true;
                }
                else
                {
                    _tcpClient.Close();
                    throw new OperationCanceledException();
                }
            }
            catch (OperationCanceledException)
            {
                AuditLogger.Log("通信", "[GJDD-750] TCP连接被用户取消");
                return false;
            }
            catch (Exception ex)
            {
                AuditLogger.Log("通信", $"[GJDD-750] TCP连接失败: {ex.Message}", false);
                return false;
            }
        }

        public void Disconnect()
        {
            try
            {
                if (_tcpClient != null) { _networkStream?.Close(); _tcpClient.Close(); _tcpClient = null; AuditLogger.Log("通信", "[GJDD-750] TCP连接已关闭"); }
                if (_serialPort != null && _serialPort.IsOpen) { _serialPort.Close(); AuditLogger.Log("通信", "[GJDD-750] 串口已关闭"); }
            }
            catch { }
        }

        #endregion

        #region 通信核心

        public async Task<byte[]> SendAndReceiveAsync(byte addr, byte cid, byte[] info, CancellationToken token = default(CancellationToken))
        {
            if (!IsConnected) throw new Exception("设备未连接");

            byte[] frame = BuildFrame(addr, cid, info);
            string txHex = BitConverter.ToString(frame).Replace("-", " ");
            AuditLogger.Log("通信", $"[GJDD-750] TX: {txHex}");

            await _lock.WaitAsync(token);
            try
            {
                if (_isTcpMode)
                {
                    if (_networkStream.DataAvailable)
                    {
                        byte[] trash = new byte[_tcpClient.Available];
                        await _networkStream.ReadAsync(trash, 0, trash.Length, token);
                    }
                    await _networkStream.WriteAsync(frame, 0, frame.Length, token);
                }
                else
                {
                    _serialPort.DiscardInBuffer();
                    // 在 4.6.1 中 Stream.WriteAsync 可能不支持 CancellationToken，如果报错请去掉 token 参数
                    await _serialPort.BaseStream.WriteAsync(frame, 0, frame.Length);
                }

                if (addr == 0x00 && (cid == 0x0E || cid == 0x11 || cid == 0x40)) return new byte[0];

                return await ReadResponseAsync(token);
            }
            catch (Exception ex)
            {
                // ★★★ 修复 C# 7.3 语法：替换 'is not' 为 '!(... is ...)' ★★★
                if (!(ex is OperationCanceledException))
                    AuditLogger.Log("通信", $"[GJDD-750] 错误: {ex.Message}", false);
                else
                    AuditLogger.Log("通信", "[GJDD-750] 操作已取消");

                throw;
            }
            finally
            {
                _lock.Release();
            }
        }

        private async Task<byte[]> ReadResponseAsync(CancellationToken token = default(CancellationToken))
        {
            byte[] buffer = new byte[1024];
            int totalBytesRead = 0;

            using (var timeoutCts = new CancellationTokenSource(ReceiveTimeout))
            using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, token))
            {
                try
                {
                    while (true)
                    {
                        if (linkedCts.Token.IsCancellationRequested)
                            linkedCts.Token.ThrowIfCancellationRequested();

                        int read = 0;
                        if (_isTcpMode)
                        {
                            if (_networkStream.DataAvailable)
                                read = await _networkStream.ReadAsync(buffer, totalBytesRead, buffer.Length - totalBytesRead, linkedCts.Token);
                        }
                        else
                        {
                            if (_serialPort.BytesToRead > 0)
                                read = await _serialPort.BaseStream.ReadAsync(buffer, totalBytesRead, buffer.Length - totalBytesRead, linkedCts.Token);
                        }

                        if (read > 0)
                        {
                            totalBytesRead += read;
                            if (buffer[totalBytesRead - 1] == EOI) break;
                        }
                        else
                        {
                            await Task.Delay(20, linkedCts.Token);
                        }
                    }

                    byte[] receivedData = new byte[totalBytesRead];
                    Array.Copy(buffer, receivedData, totalBytesRead);
                    string rxHex = BitConverter.ToString(receivedData).Replace("-", " ");
                    AuditLogger.Log("通信", $"[GJDD-750] RX: {rxHex}");

                    return ParseFrame(buffer, totalBytesRead);
                }
                catch (OperationCanceledException)
                {
                    if (token.IsCancellationRequested) throw new OperationCanceledException("用户取消");
                    throw new TimeoutException("接收超时");
                }
            }
        }

        #endregion

        #region 协议封装

        private byte[] BuildFrame(byte addr, byte cid, byte[] info)
        {
            byte len = (byte)(3 + (info?.Length ?? 0));
            List<byte> payload = new List<byte> { addr, cid, len };
            if (info != null) payload.AddRange(info);

            ushort checksum = CalculateChecksum(payload.ToArray());
            byte chk_H = (byte)(checksum >> 8);
            byte chk_L = (byte)(checksum & 0xFF);

            List<byte> frame = new List<byte> { SOI };
            foreach (byte b in payload) AddEscaped(frame, b);
            AddEscaped(frame, chk_H);
            AddEscaped(frame, chk_L);
            frame.Add(EOI);

            return frame.ToArray();
        }

        private void AddEscaped(List<byte> frame, byte b)
        {
            if (b == SOI || b == EOI || b == ROI) { frame.Add(ROI); frame.Add(b); }
            else frame.Add(b);
        }

        private byte[] ParseFrame(byte[] frame, int totalLength)
        {
            if (totalLength < 7) throw new Exception("数据帧过短");
            if (frame[0] != SOI) throw new Exception("帧头错误");
            if (frame[totalLength - 1] != EOI) throw new Exception("帧尾错误");

            List<byte> raw = new List<byte>();
            for (int i = 1; i < totalLength - 1; i++)
            {
                byte b = frame[i];
                if (b == ROI) continue;
                raw.Add(b);
            }

            if (raw.Count < 5) throw new Exception("反转义数据过短");

            int payloadLen = raw.Count - 2;
            byte[] payloadToCheck = new byte[payloadLen];
            raw.CopyTo(0, payloadToCheck, 0, payloadLen);

            ushort calculatedSum = CalculateChecksum(payloadToCheck);
            ushort receivedChecksum = (ushort)((raw[raw.Count - 2] << 8) | raw[raw.Count - 1]);

            if (receivedChecksum != calculatedSum) throw new Exception("校验和错误");

            byte lengthField = raw[2];
            int infoLen = lengthField - 3;
            if (infoLen < 0) return new byte[0];

            byte[] info = new byte[infoLen];
            raw.CopyTo(3, info, 0, infoLen);

            byte rtnCode = raw[1];
            if (rtnCode >= 0xF1 && rtnCode <= 0xF4) throw new Exception($"设备返回错误码: 0x{rtnCode:X2}");

            return info;
        }

        private ushort CalculateChecksum(byte[] data)
        {
            int sum = 0;
            foreach (byte b in data) sum += b;
            return (ushort)(sum % 0x10000);
        }

        public void Dispose() { Disconnect(); }

        #endregion
    }
}