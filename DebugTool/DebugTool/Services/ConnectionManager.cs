using System.Threading; // ★★★ 必须引用
using System.Threading.Tasks;

namespace DebugTool.Services
{
    public class ConnectionManager
    {
        public DeviceServiceVDC_32 Vdc32 { get; private set; }
        public DeviceService750_4_60A Load { get; private set; }

        public ConnectionManager()
        {
            Vdc32 = new DeviceServiceVDC_32();
            Load = new DeviceService750_4_60A();
        }

        // ★★★ 更新签名: 增加 CancellationToken token = default ★★★
        public async Task ConnectVdc32Async(string port, int baud, byte slaveId, CancellationToken token = default)
        {
            if (Load.IsConnected) Load.Disconnect();
            if (Vdc32.IsConnected) await Vdc32.DisconnectAsync();
            bool success = await Vdc32.ConnectAsync(port, baud, slaveId, token);
            if (!success) throw new System.Exception("VDC-32 串口打开失败");
        }

        public async Task ConnectVdc32TcpAsync(string ip, int port, byte slaveId, CancellationToken token = default)
        {
            if (Load.IsConnected) Load.Disconnect();
            if (Vdc32.IsConnected) await Vdc32.DisconnectAsync();
            bool success = await Vdc32.ConnectTcpAsync(ip, port, slaveId, token);
            if (!success) throw new System.Exception($"VDC-32 TCP 连接失败 ({ip}:{port})");
        }

        public async Task ConnectLoadAsync(string port, int baud, CancellationToken token = default)
        {
            if (Vdc32.IsConnected) await Vdc32.DisconnectAsync();
            if (Load.IsConnected) Load.Disconnect();
            // 串口通常不阻塞太久，但为了接口一致可以预留 token
            bool success = Load.Connect(port, baud);
            if (!success) throw new System.Exception("负载设备 串口打开失败");
            await Task.CompletedTask;
        }

        public async Task ConnectLoadTcpAsync(string ip, int port, CancellationToken token = default)
        {
            if (Vdc32.IsConnected) await Vdc32.DisconnectAsync();
            if (Load.IsConnected) Load.Disconnect();
            bool success = await Load.ConnectTcpAsync(ip, port, token);
            if (!success) throw new System.Exception($"负载设备 TCP 连接失败 ({ip}:{port})");
        }

        public async Task DisconnectAllAsync()
        {
            if (Vdc32.IsConnected) await Vdc32.DisconnectAsync();
            if (Load.IsConnected) Load.Disconnect();
        }
    }
}