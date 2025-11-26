using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace DebugTool.Core
{
    public class TcpCommunicationChannel : ICommunicationChannel
    {
        private TcpClient _tcpClient;
        private NetworkStream _networkStream;
        private readonly string _ip;
        private readonly int _port;
        private const int ReceiveTimeout = 3000;

        public TcpCommunicationChannel(string ip, int port)
        {
            _ip = ip;
            _port = port;
        }

        public bool IsConnected
        {
            get { return _tcpClient != null && _tcpClient.Connected; }
        }

        // C# 7.3 修复：显式使用 default(CancellationToken)
        public async Task<bool> ConnectAsync(CancellationToken token = default(CancellationToken))
        {
            if (IsConnected) await DisconnectAsync();

            try
            {
                _tcpClient = new TcpClient();
                _tcpClient.ReceiveTimeout = ReceiveTimeout;

                var connectTask = _tcpClient.ConnectAsync(_ip, _port);
                var delayTask = Task.Delay(3000, token);

                var completedTask = await Task.WhenAny(connectTask, delayTask);

                if (completedTask == connectTask)
                {
                    await connectTask;
                    _networkStream = _tcpClient.GetStream();
                    return true;
                }
                else
                {
                    _tcpClient.Close();
                    if (token.IsCancellationRequested)
                        throw new OperationCanceledException();

                    return false;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return false;
            }
        }

        public async Task DisconnectAsync()
        {
            try
            {
                if (_networkStream != null) _networkStream.Close();
                if (_tcpClient != null) _tcpClient.Close();
            }
            catch { }
            finally
            {
                _tcpClient = null;
                _networkStream = null;
            }
            await Task.CompletedTask;
        }

        public async Task<byte[]> SendAndReceiveAsync(byte[] frame, CancellationToken token = default(CancellationToken))
        {
            if (!IsConnected) throw new Exception("未连接 (Not Connected)");

            byte[] crcFrame = new byte[frame.Length + 2];
            Array.Copy(frame, crcFrame, frame.Length);

            ushort crc = CalculateCrc(frame);
            crcFrame[frame.Length] = (byte)(crc & 0xFF);
            crcFrame[frame.Length + 1] = (byte)(crc >> 8);

            using (var timeoutCts = new CancellationTokenSource(ReceiveTimeout))
            using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, token))
            {
                try
                {
                    if (_networkStream.DataAvailable)
                    {
                        byte[] trash = new byte[_tcpClient.Available];
                        await _networkStream.ReadAsync(trash, 0, trash.Length, linkedCts.Token);
                    }

                    await _networkStream.WriteAsync(crcFrame, 0, crcFrame.Length, linkedCts.Token);

                    byte[] buffer = new byte[1024];
                    int totalBytesRead = 0;

                    while (totalBytesRead == 0)
                    {
                        if (linkedCts.Token.IsCancellationRequested)
                            linkedCts.Token.ThrowIfCancellationRequested();

                        if (_networkStream.DataAvailable)
                        {
                            int read = await _networkStream.ReadAsync(buffer, totalBytesRead, buffer.Length - totalBytesRead, linkedCts.Token);
                            if (read == 0) throw new Exception("连接已断开");
                            totalBytesRead += read;

                            await Task.Delay(20, linkedCts.Token);
                            while (_networkStream.DataAvailable)
                            {
                                read = await _networkStream.ReadAsync(buffer, totalBytesRead, buffer.Length - totalBytesRead, linkedCts.Token);
                                totalBytesRead += read;
                            }
                        }
                        else
                        {
                            await Task.Delay(10, linkedCts.Token);
                        }
                    }

                    if (totalBytesRead < 3) throw new Exception("响应数据过短");

                    byte recvCrcLo = buffer[totalBytesRead - 2];
                    byte recvCrcHi = buffer[totalBytesRead - 1];

                    byte[] dataPayload = new byte[totalBytesRead - 2];
                    Array.Copy(buffer, dataPayload, totalBytesRead - 2);
                    ushort calculatedCrc = CalculateCrc(dataPayload);

                    if (recvCrcLo != (byte)(calculatedCrc & 0xFF) || recvCrcHi != (byte)(calculatedCrc >> 8))
                    {
                        throw new Exception("CRC 校验失败");
                    }

                    return dataPayload;
                }
                catch (OperationCanceledException)
                {
                    if (token.IsCancellationRequested)
                        throw new OperationCanceledException("用户取消操作");

                    throw new TimeoutException($"TCP 接收超时 ({ReceiveTimeout}ms)");
                }
                catch (System.IO.IOException ex)
                {
                    throw new Exception($"TCP IO错误: {ex.Message}");
                }
            }
        }

        private ushort CalculateCrc(byte[] data)
        {
            ushort crc = 0xFFFF;
            foreach (byte b in data)
            {
                crc ^= b;
                for (int i = 0; i < 8; i++)
                {
                    if ((crc & 0x0001) != 0) { crc >>= 1; crc ^= 0xA001; }
                    else crc >>= 1;
                }
            }
            return crc;
        }

        public void Dispose()
        {
            DisconnectAsync().Wait();
        }
    }
}