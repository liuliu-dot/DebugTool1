using System;
using System.Drawing;
using System.Drawing.Drawing2D; // 引用绘图命名空间
using System.IO.Ports;
using System.Windows.Forms;

namespace DebugTool.UI.Controls.Common
{
    // 定义连接状态枚举
    public enum ConnectionStatus
    {
        Disconnected, // 断开 (灰)
        Connecting,   // 连接中 (黄)
        Connected,    // 已连接 (绿)
        Failed        // 失败 (红)
    }

    public class ConnectionArgs : EventArgs
    {
        public bool IsTcp { get; set; }
        public string PortName { get; set; }
        public int BaudRate { get; set; }
        public byte SlaveId { get; set; }
        public string IpAddress { get; set; }
        public int TcpPort { get; set; }
    }

    public partial class ConnectionPanel : UserControl
    {
        public event EventHandler<ConnectionArgs> ConnectRequest;
        public event EventHandler DisconnectRequest;

        // UI 控件
        private RadioButton radioSerial, radioTcp;
        private Label lblParam1, lblParam2;
        private ComboBox cmbPort, cmbBaud;
        private TextBox txtIp;
        private NumericUpDown numTcpPort, numSlaveId;
        private Button btnConnect, btnRefresh;
        private Label lblInfoVer, lblInfoName, lblInfoAddr;

        // ★★★ 新增：状态指示灯相关 ★★★
        private Panel pnlStatusLight;
        private Label lblStatusText; // 可选：显示文字状态
        private Color _lightColor = Color.DarkGray; // 默认灰色

        public ConnectionPanel()
        {
            InitializeCustomLayout();
            this.Load += (s, e) => RefreshPorts();
            LoadUserConfig();
            SetStatus(ConnectionStatus.Disconnected); // 初始状态
        }

        // ★★★ 新增：设置状态灯颜色 ★★★
        public void SetStatus(ConnectionStatus status)
        {
            if (this.InvokeRequired) { this.Invoke(new Action(() => SetStatus(status))); return; }

            switch (status)
            {
                case ConnectionStatus.Connected:
                    _lightColor = Color.FromArgb(46, 204, 113); // 鲜绿色
                    lblStatusText.Text = "已连接";
                    lblStatusText.ForeColor = _lightColor;
                    break;
                case ConnectionStatus.Connecting:
                    _lightColor = Color.Orange; // 橙黄色
                    lblStatusText.Text = "连接中...";
                    lblStatusText.ForeColor = _lightColor;
                    break;
                case ConnectionStatus.Failed:
                    _lightColor = Color.Red; // 红色
                    lblStatusText.Text = "连接失败";
                    lblStatusText.ForeColor = _lightColor;
                    break;
                case ConnectionStatus.Disconnected:
                default:
                    _lightColor = Color.DarkGray; // 灰色
                    lblStatusText.Text = "未连接";
                    lblStatusText.ForeColor = Color.Gray;
                    break;
            }

            // 触发重绘
            pnlStatusLight.Invalidate();
        }

        // 设置 "正在连接" 的 UI 锁定状态 (兼容旧代码调用)
        public void SetConnectingState()
        {
            if (this.InvokeRequired) { this.Invoke(new Action(SetConnectingState)); return; }

            SetStatus(ConnectionStatus.Connecting); // 更新灯光

            btnConnect.Text = "取消连接";
            btnConnect.BackColor = Color.Orange;
            SetControlsEnabled(false);
        }

        // 设置 "已连接/断开" 的 UI 状态
        public void SetConnectionState(bool isConnected)
        {
            if (this.InvokeRequired) { this.Invoke(new Action(() => SetConnectionState(isConnected))); return; }

            // 更新按钮状态
            btnConnect.Text = isConnected ? "断开连接" : "连接设备";
            btnConnect.BackColor = isConnected ? Color.FromArgb(231, 76, 60) : Color.FromArgb(46, 204, 113);

            // 如果断开，更新灯光为灰色 (连接成功的情况通常由外部显式调用 SetStatus(Connected))
            if (!isConnected)
            {
                SetStatus(ConnectionStatus.Disconnected);
                UpdateDeviceInfo("--", "--", "--");
            }

            SetControlsEnabled(!isConnected);
        }

        private void SetControlsEnabled(bool enable)
        {
            if (cmbPort != null) cmbPort.Enabled = enable;
            if (txtIp != null) txtIp.Enabled = enable;
            if (cmbBaud != null) cmbBaud.Enabled = enable;
            if (numTcpPort != null) numTcpPort.Enabled = enable;
            if (numSlaveId != null) numSlaveId.Enabled = enable;
            if (radioSerial != null) radioSerial.Enabled = enable;
            if (radioTcp != null) radioTcp.Enabled = enable;
            if (btnRefresh != null) btnRefresh.Enabled = enable;
        }

        public void UpdateDeviceInfo(string version, string name, string address)
        {
            if (this.InvokeRequired) { this.Invoke(new Action(() => UpdateDeviceInfo(version, name, address))); return; }
            lblInfoVer.Text = $"固件版本: {version}";
            lblInfoName.Text = $"设备名称: {name}";
            lblInfoAddr.Text = $"当前地址: {address}";
        }

        private void BtnConnect_Click(object sender, EventArgs e)
        {
            if (btnConnect.Text == "断开连接")
            {
                DisconnectRequest?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                var args = new ConnectionArgs
                {
                    IsTcp = radioTcp.Checked,
                    SlaveId = (byte)Math.Max(numSlaveId.Minimum, Math.Min(numSlaveId.Maximum, numSlaveId.Value))
                };

                if (args.IsTcp)
                {
                    args.IpAddress = txtIp.Text;
                    args.TcpPort = (int)numTcpPort.Value;
                }
                else
                {
                    string selectedPort = cmbPort.Text?.Trim();
                    if (string.IsNullOrWhiteSpace(selectedPort) || selectedPort.Contains("未检测"))
                    {
                        MessageBox.Show("请选择有效的串口！", "提示");
                        return;
                    }
                    args.PortName = selectedPort;
                    if (cmbBaud.SelectedItem != null) args.BaudRate = int.Parse(cmbBaud.SelectedItem.ToString());
                }

                DebugTool.Utils.ConfigManager.SaveSettings(args.PortName, args.BaudRate, args.SlaveId, txtIp.Text, (int)numTcpPort.Value);
                ConnectRequest?.Invoke(this, args);
            }
        }

        private void InitializeCustomLayout()
        {
            this.Height = 80;
            this.Dock = DockStyle.Top;
            this.BackColor = Color.White;
            this.Padding = new Padding(0);
            this.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = Color.LightGray });

            Panel pnlConfig = new Panel { Dock = DockStyle.Fill, Padding = new Padding(15, 10, 15, 10) };
            Panel pnlMode = new Panel { Height = 30, Dock = DockStyle.Top };
            radioSerial = new RadioButton { Text = "串口通讯 (Serial)", Checked = true, Location = new Point(0, 5), AutoSize = true, Font = new Font("微软雅黑", 9F, FontStyle.Bold) };
            radioTcp = new RadioButton { Text = "网络通讯 (TCP/IP)", Location = new Point(140, 5), AutoSize = true, Font = new Font("微软雅黑", 9F, FontStyle.Bold) };
            radioSerial.CheckedChanged += (s, e) => ToggleMode();
            pnlMode.Controls.AddRange(new Control[] { radioSerial, radioTcp });

            Panel pnlParams = new Panel { Height = 35, Dock = DockStyle.Bottom };
            lblParam1 = new Label { Text = "端口:", Location = new Point(0, 8), AutoSize = true };
            cmbPort = new ComboBox { Location = new Point(45, 5), Width = 90, DropDownStyle = ComboBoxStyle.DropDownList };
            btnRefresh = new Button { Text = "↻", Location = new Point(140, 4), Width = 28, Height = 25, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
            btnRefresh.FlatAppearance.BorderSize = 0;
            btnRefresh.Click += (s, e) => RefreshPorts();
            txtIp = new TextBox { Location = new Point(45, 5), Width = 120, Visible = false, Text = "192.168.1.100" };
            lblParam2 = new Label { Text = "波特率:", Location = new Point(180, 8), AutoSize = true };
            cmbBaud = new ComboBox { Location = new Point(235, 5), Width = 80, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbBaud.Items.AddRange(new object[] { "9600", "19200", "38400", "57600", "115200" });
            cmbBaud.SelectedItem = "57600";
            numTcpPort = new NumericUpDown { Location = new Point(235, 5), Width = 70, Minimum = 1, Maximum = 65535, Value = 502, Visible = false };
            Label lblAddr = new Label { Text = "地址:", Location = new Point(330, 8), AutoSize = true };
            numSlaveId = new NumericUpDown { Location = new Point(370, 5), Width = 50, Minimum = 1, Maximum = 247, Value = 1 };
            btnConnect = new Button { Text = "连接设备", Location = new Point(450, 2), Size = new Size(100, 30), BackColor = Color.FromArgb(46, 204, 113), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Font = new Font("微软雅黑", 10F, FontStyle.Bold) };
            btnConnect.FlatAppearance.BorderSize = 0;
            btnConnect.Click += BtnConnect_Click;

            // ★★★ 指示灯控件初始化 ★★★
            // 1. 圆形指示灯 Panel
            pnlStatusLight = new Panel
            {
                Size = new Size(18, 18),
                Location = new Point(570, 8) // 放在连接按钮右侧
            };
            // 使用 Paint 事件绘制圆形
            pnlStatusLight.Paint += (s, paintArgs) =>
            {
                paintArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (Brush brush = new SolidBrush(_lightColor))
                {
                    paintArgs.Graphics.FillEllipse(brush, 1, 1, 15, 15); // 留1px边距
                }
            };

            // 2. 状态文字 Label
            lblStatusText = new Label
            {
                Text = "未连接",
                Location = new Point(595, 9),
                AutoSize = true,
                Font = new Font("微软雅黑", 9F, FontStyle.Bold),
                ForeColor = Color.Gray
            };

            pnlParams.Controls.AddRange(new Control[] { lblParam1, cmbPort, btnRefresh, txtIp, lblParam2, cmbBaud, numTcpPort, lblAddr, numSlaveId, btnConnect, pnlStatusLight, lblStatusText });
            pnlConfig.Controls.Add(pnlParams);
            pnlConfig.Controls.Add(pnlMode);

            Panel pnlInfo = new Panel { Dock = DockStyle.Right, Width = 450, BackColor = Color.FromArgb(250, 250, 250) };
            pnlInfo.Controls.Add(new Panel { Dock = DockStyle.Left, Width = 1, BackColor = Color.LightGray });
            Label lblInfoTitle = new Label { Text = "设备状态面板", Location = new Point(15, 10), Font = new Font("微软雅黑", 9F, FontStyle.Bold), ForeColor = Color.DimGray, AutoSize = true };
            lblInfoVer = new Label { Text = "固件版本: --", Location = new Point(15, 45), Font = new Font("Consolas", 9F), AutoSize = true };
            lblInfoName = new Label { Text = "设备名称: --", Location = new Point(150, 45), Font = new Font("Consolas", 9F), AutoSize = true };
            lblInfoAddr = new Label { Text = "当前地址: --", Location = new Point(300, 45), Font = new Font("Consolas", 9F), AutoSize = true };
            pnlInfo.Controls.AddRange(new Control[] { lblInfoTitle, lblInfoVer, lblInfoName, lblInfoAddr });

            this.Controls.Add(pnlConfig);
            this.Controls.Add(pnlInfo);
        }

        private void ToggleMode()
        {
            bool isSerial = radioSerial.Checked;
            cmbPort.Visible = isSerial; btnRefresh.Visible = isSerial; cmbBaud.Visible = isSerial;
            txtIp.Visible = !isSerial; numTcpPort.Visible = !isSerial;
            lblParam1.Text = isSerial ? "端口:" : "IP地址:";
            lblParam2.Text = isSerial ? "波特率:" : "端口:";
        }

        private void RefreshPorts()
        {
            if (cmbPort == null) return;
            cmbPort.Items.Clear();
            try { string[] ports = SerialPort.GetPortNames(); if (ports != null && ports.Length > 0) { cmbPort.Items.AddRange(ports); cmbPort.SelectedIndex = 0; } else { cmbPort.Items.Add("未检测到串口"); cmbPort.SelectedIndex = 0; } } catch { cmbPort.Items.Add("检测失败"); }
        }

        private void LoadUserConfig()
        {
            var s = DebugTool.Utils.ConfigManager.LoadSettings();
            if (cmbPort.Items.Contains(s.PortName)) cmbPort.SelectedItem = s.PortName;
            if (cmbBaud.Items.Contains(s.BaudRate.ToString())) cmbBaud.SelectedItem = s.BaudRate.ToString();
            numSlaveId.Value = Math.Max(numSlaveId.Minimum, Math.Min(numSlaveId.Maximum, (decimal)s.SlaveId));
            txtIp.Text = s.IpAddress;
            numTcpPort.Value = Math.Max(numTcpPort.Minimum, Math.Min(numTcpPort.Maximum, (decimal)s.TcpPort));
        }
    }
}