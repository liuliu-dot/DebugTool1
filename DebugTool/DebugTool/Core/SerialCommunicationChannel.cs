using System;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;

namespace DebugTool.Core
{
    public class SerialCommunicationChannel : ICommunicationChannel
    {
        private SerialPort _serialPort;

        public SerialCommunicationChannel(string portName, int baudRate)
        {
            _serialPort = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One);
            _serialPort.ReadTimeout = 2000;
            _serialPort.WriteTimeout = 1000;
        }

        public bool IsConnected
        {
            get { return _serialPort != null && _serialPort.IsOpen; }
        }

        // C# 7.3 兼容签名
        public async Task<bool> ConnectAsync(CancellationToken token = default(CancellationToken))
        {
            try
            {
                await Task.Run(() =>
                {
                    if (_serialPort.IsOpen) _serialPort.Close();
                    _serialPort.Open();
                }, token);

                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task DisconnectAsync()
        {
            if (_serialPort != null && _serialPort.IsOpen)
            {
                _serialPort.Close();
            }
            await Task.CompletedTask;
        }

        public async Task<byte[]> SendAndReceiveAsync(byte[] frame, CancellationToken token = default(CancellationToken))
        {
            return await Task.Run(() =>
            {
                if (token.IsCancellationRequested) throw new OperationCanceledException();

                byte[] crcFrame = new byte[frame.Length + 2];
                Array.Copy(frame, crcFrame, frame.Length);
                ushort crc = CalculateCrc(frame);
                crcFrame[frame.Length] = (byte)(crc & 0xFF);
                crcFrame[frame.Length + 1] = (byte)(crc >> 8);

                _serialPort.DiscardInBuffer();
                _serialPort.Write(crcFrame, 0, crcFrame.Length);

                int waitTime = 0;
                while (_serialPort.BytesToRead == 0)
                {
                    if (token.IsCancellationRequested) throw new OperationCanceledException();

                    System.Threading.Thread.Sleep(10);
                    waitTime += 10;
                    if (waitTime > _serialPort.ReadTimeout) throw new TimeoutException("串口接收超时");
                }

                System.Threading.Thread.Sleep(20);
                int bytesToRead = _serialPort.BytesToRead;
                byte[] buffer = new byte[bytesToRead];
                _serialPort.Read(buffer, 0, bytesToRead);

                if (buffer.Length < 3) throw new Exception("响应数据过短");

                byte[] dataPayload = new byte[buffer.Length - 2];
                Array.Copy(buffer, dataPayload, buffer.Length - 2);

                ushort calculatedCrc = CalculateCrc(dataPayload);
                byte recvCrcLo = buffer[buffer.Length - 2];
                byte recvCrcHi = buffer[buffer.Length - 1];

                if (recvCrcLo != (byte)(calculatedCrc & 0xFF) || recvCrcHi != (byte)(calculatedCrc >> 8))
                {
                    throw new Exception("CRC 校验失败 (Serial)");
                }

                return dataPayload;

            }, token);
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
            if (_serialPort != null)
            {
                if (_serialPort.IsOpen) _serialPort.Close();
                _serialPort.Dispose();
            }
        }
    }
}