using DebugTool.Services;
using DebugTool.UI.Controls.Common;
using DebugTool.UI.Controls.Load;
using DebugTool.UI.Controls.Vdc32;
using DebugTool.Utils;
using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace DebugTool
{
    public partial class MainForm : Form
    {
        private readonly ConnectionManager _connectionManager;
        private CancellationTokenSource _connectCts;

        // 界面控件
        private ConnectionPanel _connectionPanel;
        private Vdc32View _vdc32View;
        private LoadMonitorView _loadView;
        private LogView _logView;

        // 布局容器
        private Panel _menuPanel;
        private Panel _contentPanel;

        // ★★★ 新增：底部状态栏控件 ★★★
        private Panel _bottomPanel;
        private Label _lblLog;
        private LinkLabel _lnkViewAll;

        private string _currentView = "VDC32";

        public MainForm()
        {
            _connectionManager = new ConnectionManager();

            InitializeLayout();
            InitializeViews(); // 初始化视图

            // ★★★ 新增：订阅全局日志事件 ★★★
            AuditLogger.LogAdded += OnLogAdded;

            SwitchView("VDC32");
        }

        private void InitializeLayout()
        {
            this.Size = new Size(1380, 850);
            this.Text = "冠佳电子多功能调试工具 (Pro)";
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("微软雅黑", 9F);

            // 左侧菜单
            _menuPanel = new Panel { Dock = DockStyle.Left, Width = 200, BackColor = Color.FromArgb(45, 45, 48) };

            // 右侧内容容器
            _contentPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.WhiteSmoke };

            // 1. 顶部连接面板
            _connectionPanel = new ConnectionPanel();
            _connectionPanel.ConnectRequest += OnConnectRequest;
            _connectionPanel.DisconnectRequest += OnDisconnectRequest;
            _connectionPanel.Dock = DockStyle.Top;

            // 2. ★★★ 新增：底部状态栏面板 ★★★
            _bottomPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 30,
                BackColor = Color.FromArgb(0, 122, 204), // 使用专业的主题色 (VS蓝)
                Padding = new Padding(10, 0, 10, 0)
            };

            // 查看全部链接
            _lnkViewAll = new LinkLabel
            {
                Text = "查看全部 >",
                Dock = DockStyle.Right,
                AutoSize = true,
                LinkColor = Color.White,
                TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(0, 7, 0, 0) // 垂直居中微调
            };
            _lnkViewAll.Click += (s, e) => SwitchView("LOG");

            // 日志文本标签
            _lblLog = new Label
            {
                Text = "就绪",
                Dock = DockStyle.Fill, // 填满剩余空间
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Consolas", 9F) // 等宽字体显示日志更合适
            };

            _bottomPanel.Controls.Add(_lblLog);
            _bottomPanel.Controls.Add(_lnkViewAll); // 先添加 Right，再添加 Fill

            // 将控件加入主容器
            // 注意添加顺序影响布局：先加的在Z轴底部，但对于Dock布局，
            // 我们希望 Top 和 Bottom 面板优先占据空间，中间 Fill 的视图最后占据剩余空间。
            // 在 WinForms 中，Z-Index 大的（后添加的）会被 Z-Index 小的（先添加的）挤占空间？
            // 其实是：Controls 集合中索引大的（底层）优先布局。
            // 简单做法：添加完所有 Panel 后，确保 Views 使用 BringToFront() 即可填满中间。

            _contentPanel.Controls.Add(_connectionPanel);
            _contentPanel.Controls.Add(_bottomPanel);

            this.Controls.Add(_contentPanel);
            this.Controls.Add(_menuPanel);

            // 添加菜单按钮
            AddMenuButton("VDC-32 检测板", 0, (s, e) => SwitchView("VDC32"));
            AddMenuButton("GJDD-750 负载", 50, (s, e) => SwitchView("LOAD"));
            AddMenuButton("📋 查看日志", 100, (s, e) => SwitchView("LOG"));

            // 工具按钮
            AddMenuButton("📊 导出当前数据", 160, (s, e) => ExportCurrentData());
            AddMenuButton("📂 打开文件目录", 210, (s, e) => System.Diagnostics.Process.Start(AppDomain.CurrentDomain.BaseDirectory));
        }

        private void InitializeViews()
        {
            // VDC32 视图
            _vdc32View = new Vdc32View();
            _vdc32View.SetService(_connectionManager.Vdc32);
            _vdc32View.Dock = DockStyle.Fill;
            _vdc32View.OnDeviceInfoUpdated += (ver, name, addr) => _connectionPanel.UpdateDeviceInfo(ver, name, addr);
            _contentPanel.Controls.Add(_vdc32View);

            // Load 视图
            _loadView = new LoadMonitorView();
            _loadView.SetService(_connectionManager.Load);
            _loadView.Dock = DockStyle.Fill;
            _connectionManager.Load.DataUpdated += (data) => _loadView.UpdateData(data);
            _contentPanel.Controls.Add(_loadView);

            // 日志视图
            _logView = new LogView();
            _logView.Dock = DockStyle.Fill;
            _contentPanel.Controls.Add(_logView);
        }

        // ★★★ 新增：日志更新处理 ★★★
        private void OnLogAdded(string logMsg)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => OnLogAdded(logMsg)));
                return;
            }

            // === 过滤逻辑 ===
            // 如果日志包含通信数据的关键词 (TX, RX, 发送完成)，则不更新到底部状态栏
            // 这样状态栏就只会显示 "连接成功"、"导出完成"、"错误" 等重要状态
            if (logMsg.Contains("TX:") ||
                logMsg.Contains("RX:") ||
                logMsg.Contains("发送完成"))
            {
                return;
            }

            // 截取时间戳之后的内容，让底部显示更简洁
            // 假设格式: "2025-05-27 10:00:00 | [SUCCESS] | Action | Details"
            // 我们只显示 "[SUCCESS] | Action | Details"
            int firstPipeIndex = logMsg.IndexOf('|');
            string displayMsg = firstPipeIndex > 0 ? logMsg.Substring(firstPipeIndex + 1).Trim() : logMsg;

            _lblLog.Text = displayMsg;
        }

        private void SwitchView(string viewName)
        {
            _currentView = viewName;

            // 隐藏所有视图
            if (_vdc32View != null) _vdc32View.Visible = false;
            if (_loadView != null) _loadView.Visible = false;
            if (_logView != null) _logView.Visible = false;

            // 显示目标视图并置顶 (填满中间区域)
            UserControl targetView = null;
            switch (viewName)
            {
                case "VDC32": targetView = _vdc32View; break;
                case "LOAD": targetView = _loadView; break;
                case "LOG": targetView = _logView; break;
            }

            if (targetView != null)
            {
                targetView.Visible = true;
                targetView.BringToFront();
            }
        }

        private void AddMenuButton(string text, int top, EventHandler onClick)
        {
            Button btn = new Button
            {
                Text = text,
                Top = top,
                Left = 0,
                Width = 200,
                Height = 50,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(20, 0, 0, 0),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.Click += onClick;
            _menuPanel.Controls.Add(btn);
        }

        // === 连接逻辑处理 ===
        private async void OnConnectRequest(object sender, ConnectionArgs e)
        {
            if (_connectCts != null && !_connectCts.IsCancellationRequested)
            {
                AuditLogger.Log("连接", "正在取消连接...");
                _connectCts.Cancel();
                return;
            }

            try
            {
                _connectCts = new CancellationTokenSource();

                // 1. 设置状态为：连接中 (黄色灯)
                _connectionPanel.SetConnectingState();
                // 注意：SetConnectingState 内部现在会自动调用 SetStatus(ConnectionStatus.Connecting)

                AuditLogger.Log("连接", $"正在连接设备 ({_currentView})...");

                if (_currentView == "VDC32")
                {
                    if (e.IsTcp)
                        await _connectionManager.ConnectVdc32TcpAsync(e.IpAddress, e.TcpPort, e.SlaveId, _connectCts.Token);
                    else
                        await _connectionManager.ConnectVdc32Async(e.PortName, e.BaudRate, e.SlaveId, _connectCts.Token);

                    // 2. 连接成功 (绿色灯)
                    _connectionPanel.SetConnectionState(_connectionManager.Vdc32.IsConnected);
                    if (_connectionManager.Vdc32.IsConnected)
                    {
                        // ★★★ 显式设置绿色状态 ★★★
                        _connectionPanel.SetStatus(ConnectionStatus.Connected);
                        _ = _connectionManager.Vdc32.PollAllDataAsync();
                        AuditLogger.Log("连接", "VDC-32 连接成功");
                    }
                }
                else if (_currentView == "LOAD")
                {
                    if (e.IsTcp)
                        await _connectionManager.ConnectLoadTcpAsync(e.IpAddress, e.TcpPort, _connectCts.Token);
                    else
                        await _connectionManager.ConnectLoadAsync(e.PortName, e.BaudRate);

                    _connectionPanel.SetConnectionState(_connectionManager.Load.IsConnected);
                    if (_connectionManager.Load.IsConnected)
                    {
                        // ★★★ 显式设置绿色状态 ★★★
                        _connectionPanel.SetStatus(ConnectionStatus.Connected);
                        AuditLogger.Log("连接", "负载设备连接成功");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                AuditLogger.Log("连接", "连接已取消");
                _connectionPanel.SetConnectionState(false);
                // ★★★ 取消后设置为灰色 ★★★
                _connectionPanel.SetStatus(ConnectionStatus.Disconnected);
            }
            catch (Exception ex)
            {
                string err = $"连接失败: {ex.Message}";
                MessageBox.Show(err, "连接错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _connectionPanel.SetConnectionState(false);
                AuditLogger.Log("连接", err, false);

                // ★★★ 失败后设置为红色 ★★★
                _connectionPanel.SetStatus(ConnectionStatus.Failed);
            }
            finally
            {
                _connectCts?.Dispose();
                _connectCts = null;
            }
        }

        private async void OnDisconnectRequest(object sender, EventArgs e)
        {
            await _connectionManager.DisconnectAllAsync();
            _connectionPanel.SetConnectionState(false);
            // ★★★ 断开后设置为灰色 ★★★
            _connectionPanel.SetStatus(ConnectionStatus.Disconnected);
            AuditLogger.Log("连接", "设备已断开");
        }

        private void ExportCurrentData()
        {
            try
            {
                if (_currentView == "VDC32")
                {
                    CsvExporter.ExportVdc32Data(_connectionManager.Vdc32.LastData);
                    AuditLogger.Log("数据导出", "VDC-32 数据已导出");
                }
                else if (_currentView == "LOAD")
                {
                    CsvExporter.ExportLoadData(_connectionManager.Load.LastData);
                    AuditLogger.Log("数据导出", "负载设备数据已导出");
                }
            }
            catch (Exception ex)
            {
                AuditLogger.Log("数据导出", $"导出失败: {ex.Message}", false);
            }
        }
    }
}