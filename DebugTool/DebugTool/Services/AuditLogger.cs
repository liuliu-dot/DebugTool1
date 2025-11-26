using System;
using System.IO;

namespace DebugTool.Services
{
    public static class AuditLogger
    {
        private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");

        // ★★★ 新增：日志事件，供界面订阅 ★★★
        public static event Action<string> LogAdded;

        static AuditLogger()
        {
            if (!Directory.Exists(LogPath))
                Directory.CreateDirectory(LogPath);
        }

        public static void Log(string action, string details, bool isSuccess = true)
        {
            string status = isSuccess ? "SUCCESS" : "FAILED";
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string logLine = $"{timestamp} | [{status}] | {action} | {details}";

            // 1. 写文件 (原有逻辑)
            string fileName = $"Audit_{DateTime.Now:yyyyMMdd}.log";
            string fullPath = Path.Combine(LogPath, fileName);

            try
            {
                File.AppendAllText(fullPath, logLine + Environment.NewLine);
            }
            catch { /* 忽略文件写入错误 */ }

            // 2. ★★★ 触发事件，通知界面更新 ★★★
            // 使用 ?.Invoke 确保只有在有订阅者时才调用
            // 注意：这里可能会在非UI线程触发，界面层需要处理 Invoke
            LogAdded?.Invoke(logLine);
        }
    }
}