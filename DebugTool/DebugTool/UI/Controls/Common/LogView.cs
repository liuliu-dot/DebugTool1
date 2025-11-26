using DebugTool.Services;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace DebugTool.UI.Controls.Common
{
    public partial class LogView : UserControl
    {
        private TextBox txtLog;
        private Button btnClear;

        public LogView()
        {
            InitializeCustomLayout();

            // 订阅日志事件
            AuditLogger.LogAdded += OnNewLog;
        }

        private void InitializeCustomLayout()
        {
            this.Dock = DockStyle.Fill;
            this.BackColor = Color.White;
            this.Padding = new Padding(10);

            // 顶部工具栏
            Panel topPanel = new Panel { Dock = DockStyle.Top, Height = 40 };
            Label title = new Label { Text = "📋 系统操作日志", Location = new Point(0, 10), AutoSize = true, Font = new Font("微软雅黑", 12, FontStyle.Bold) };

            btnClear = new Button { Text = "清空屏幕", Location = new Point(150, 5), Width = 80, Height = 30, Cursor = Cursors.Hand };
            btnClear.Click += (s, e) => txtLog.Clear();

            topPanel.Controls.Add(title);
            topPanel.Controls.Add(btnClear);
            this.Controls.Add(topPanel);

            // 日志文本框
            txtLog = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.FromArgb(30, 30, 30), // 深色背景
                ForeColor = Color.LimeGreen,                // 极客绿文字
                Font = new Font("Consolas", 10F),
                Text = "--- 日志系统已就绪 ---\r\n"
            };
            this.Controls.Add(txtLog);
            txtLog.BringToFront();
        }

        private void OnNewLog(string logMsg)
        {
            // 确保在 UI 线程更新
            if (txtLog.InvokeRequired)
            {
                txtLog.Invoke(new Action(() => OnNewLog(logMsg)));
                return;
            }

            // 追加文本并滚动到底部
            txtLog.AppendText(logMsg + "\r\n");

            // 限制日志长度，防止内存溢出 (保留最近 2000 行)
            if (txtLog.Lines.Length > 2000)
            {
                // 简单清空，或者你可以写更复杂的截断逻辑
                txtLog.Clear();
                txtLog.AppendText("--- 日志过长，已自动清理 ---\r\n" + logMsg + "\r\n");
            }
        }
    }
}