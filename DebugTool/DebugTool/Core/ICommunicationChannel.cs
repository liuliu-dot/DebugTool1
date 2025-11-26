using System.Threading;
using System.Threading.Tasks;

namespace DebugTool.Core
{
    public interface ICommunicationChannel : System.IDisposable
    {
        bool IsConnected { get; }

        // C# 7.3 兼容写法：接口中定义默认参数
        Task<bool> ConnectAsync(CancellationToken token = default(CancellationToken));

        Task DisconnectAsync();

        Task<byte[]> SendAndReceiveAsync(byte[] frame, CancellationToken token = default(CancellationToken));
    }
}