using System;
using System.IO;
using System.Xml.Serialization;

namespace DebugTool.Utils
{
    // 1. 定义配置数据结构
    public class AppSettings
    {
        public string PortName { get; set; } = "COM1";
        public int BaudRate { get; set; } = 57600;
        public byte SlaveId { get; set; } = 1;
        public string IpAddress { get; set; } = "192.168.1.100";
        public int TcpPort { get; set; } = 502;
    }

    // 2. 管理器类
    public static class ConfigManager
    {
        private static readonly string ConfigFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.xml");

        public static void SaveSettings(string port, int baud, byte slaveId, string ip, int tcpPort)
        {
            try
            {
                var settings = new AppSettings
                {
                    PortName = port,
                    BaudRate = baud,
                    SlaveId = slaveId,
                    IpAddress = ip,
                    TcpPort = tcpPort
                };

                XmlSerializer serializer = new XmlSerializer(typeof(AppSettings));
                using (StreamWriter writer = new StreamWriter(ConfigFile))
                {
                    serializer.Serialize(writer, settings);
                }
            }
            catch (Exception ex)
            {
                // 保存失败不应崩溃，记录日志或忽略
                System.Diagnostics.Debug.WriteLine($"保存配置失败: {ex.Message}");
            }
        }

        public static AppSettings LoadSettings()
        {
            try
            {
                if (!File.Exists(ConfigFile))
                {
                    return new AppSettings(); // 返回默认值
                }

                XmlSerializer serializer = new XmlSerializer(typeof(AppSettings));
                using (StreamReader reader = new StreamReader(ConfigFile))
                {
                    return (AppSettings)serializer.Deserialize(reader);
                }
            }
            catch
            {
                return new AppSettings(); // 读取失败返回默认值
            }
        }
    }
}