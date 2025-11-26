using DebugTool.Models;
using DebugTool.Services;
using DebugTool.UI.Controls.Common;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DebugTool.UI.Controls.Vdc32
{
    public partial class Vdc32View : UserControl
    {
        private DeviceServiceVDC_32 _service;

        // ★★★ 新增：定义事件，将设备信息传给主界面 ★★★
        public event Action<string, string, string> OnDeviceInfoUpdated;

        private bool _hasLoadedInfo = false;

        // === 控件定义 ===
        private TabControl tabControl;
        private DataGridView dgvChannels;

        // 监测页控件
        private StatusIndicator indS1, indWaterSelf, indWaterPar, indJig, indContactor, indFan, indAcOnDep;
        private System.Windows.Forms.Button btnExportCsv, btnReadTh, btnWriteTh, btnSetTh, btnClearFlags;
        private System.Windows.Forms.TextBox txtThreshold;

        // 控制页控件
        private ToggleSwitch togglePtc, toggleAc, togglePson, toggleFan;
        private System.Windows.Forms.CheckBox[] chkIoDirs;
        private System.Windows.Forms.Button btnReadIoDir, btnWriteIoDir;
        private System.Windows.Forms.Button btnReadEnv;
        private System.Windows.Forms.Label lblTemp, lblFanCurrent;

        // 配置页控件
        private System.Windows.Forms.TextBox txtSn, txtAddress;
        private System.Windows.Forms.ComboBox cmbBaudRate;
        private System.Windows.Forms.CheckBox chkAcDep;

        public Vdc32View()
        {
            InitializeCustomUI();
        }

        public void SetService(DeviceServiceVDC_32 service)
        {
            _service = service;
            _service.DataUpdated += OnDataUpdated;
            _service.IoStatusUpdated += OnIoStatusUpdated;
        }

        private void InitializeCustomUI()
        {
            this.Dock = DockStyle.Fill;
            this.BackColor = SystemColors.Control;

            // 1. 顶部标题栏 (★★★ 已移除旧的 GroupBox，只保留标题 ★★★)
            Panel topPanel = new Panel { Dock = DockStyle.Top, Height = 50, Padding = new Padding(10) };

            Label lblState = new Label
            {
                Text = "VDC-32 检测界面",
                Location = new Point(10, 10),
                AutoSize = true,
                Font = new Font("微软雅黑", 16F, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 51, 153) // 深蓝色
            };
            topPanel.Controls.Add(lblState);
            this.Controls.Add(topPanel);

            // 2. 主 TabControl
            tabControl = new TabControl { Dock = DockStyle.Fill, Font = new Font("微软雅黑", 9F) };

            TabPage pageMonitor = new TabPage("监测");
            TabPage pageControl = new TabPage("控制");
            TabPage pageConfig = new TabPage("配置");

            InitializeMonitorPage(pageMonitor);
            InitializeControlPage(pageControl);
            InitializeConfigPage(pageConfig);

            tabControl.TabPages.Add(pageMonitor);
            tabControl.TabPages.Add(pageControl);
            tabControl.TabPages.Add(pageConfig);

            this.Controls.Add(tabControl);
            topPanel.SendToBack();
            tabControl.BringToFront();
        }

        // ==================== 1. 监测页 (保持全屏优化) ====================
        private void InitializeMonitorPage(TabPage page)
        {
            page.BackColor = SystemColors.Control;
            page.Padding = new Padding(5);

            // A. 顶部 IO 状态
            GroupBox grpStatus = new GroupBox { Text = "I/O 实时状态监测", Dock = DockStyle.Top, Height = 80 };
            FlowLayoutPanel flowStatus = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = false, Padding = new Padding(5) };

            indS1 = CreateIndicator("S1开关");
            indWaterSelf = CreateIndicator("自水信号");
            indWaterPar = CreateIndicator("并水信号");
            indJig = CreateIndicator("治具到位");
            indContactor = CreateIndicator("接触器");
            indFan = CreateIndicator("风扇状态");
            indAcOnDep = CreateIndicator("AC依赖");

            flowStatus.Controls.AddRange(new Control[] { indS1, indWaterSelf, indWaterPar, indJig, indContactor, indFan, indAcOnDep });
            grpStatus.Controls.Add(flowStatus);
            page.Controls.Add(grpStatus);

            // B. 中间数据表格
            GroupBox grpMain = new GroupBox { Text = "32通道电压监测与跌落检测", Dock = DockStyle.Fill };

            // 底部按钮区域
            Panel pnlBottom = new Panel { Dock = DockStyle.Bottom, Height = 50 };
            btnClearFlags = CreateBtn("清除标志", 10, 10, 100);
            btnExportCsv = CreateBtn("导出表格", 120, 10, 100);
            btnReadTh = CreateBtn("读门限", 250, 10, 80);
            btnWriteTh = CreateBtn("写门限", 340, 10, 80);
            System.Windows.Forms.Label lblTh = new System.Windows.Forms.Label { Text = "设置值:", Location = new Point(440, 15), AutoSize = true };
            txtThreshold = new System.Windows.Forms.TextBox { Text = "1.5", Location = new Point(490, 12), Width = 50 };
            btnSetTh = CreateBtn("批量设置", 550, 10, 80);
            pnlBottom.Controls.AddRange(new Control[] { btnClearFlags, btnExportCsv, btnReadTh, btnWriteTh, lblTh, txtThreshold, btnSetTh });
            grpMain.Controls.Add(pnlBottom);

            // DataGridView 配置
            dgvChannels = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.White,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToResizeRows = false,
                AllowUserToResizeColumns = false,
                RowHeadersVisible = false,
                ColumnHeadersHeight = 35,
                BorderStyle = BorderStyle.None,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                ScrollBars = ScrollBars.None,
                SelectionMode = DataGridViewSelectionMode.CellSelect,
                GridColor = SystemColors.ControlLight
            };

            DataGridViewCellStyle headerStyle = new DataGridViewCellStyle
            {
                Alignment = DataGridViewContentAlignment.MiddleCenter,
                BackColor = SystemColors.ControlLight,
                Font = new Font("微软雅黑", 9F, FontStyle.Bold)
            };
            dgvChannels.ColumnHeadersDefaultCellStyle = headerStyle;

            DataGridViewCellStyle cellStyle = new DataGridViewCellStyle
            {
                Alignment = DataGridViewContentAlignment.MiddleCenter,
                Font = new Font("Consolas", 10F)
            };
            dgvChannels.DefaultCellStyle = cellStyle;

            for (int i = 0; i < 4; i++)
            {
                dgvChannels.Columns.Add($"ch{i}", "CH");
                dgvChannels.Columns.Add($"vol{i}", "Volt(V)");
                dgvChannels.Columns[i * 2].FillWeight = 40;
                dgvChannels.Columns[i * 2 + 1].FillWeight = 60;
            }

            // 填充默认数据
            for (int i = 0; i < 8; i++)
            {
                int rowIndex = dgvChannels.Rows.Add();
                var row = dgvChannels.Rows[rowIndex];
                row.Cells[0].Value = i + 1; row.Cells[1].Value = "--";
                row.Cells[2].Value = i + 9; row.Cells[3].Value = "--";
                row.Cells[4].Value = i + 17; row.Cells[5].Value = "--";
                row.Cells[6].Value = i + 25; row.Cells[7].Value = "--";

                Color labelColor = Color.FromArgb(240, 240, 240);
                row.Cells[0].Style.BackColor = labelColor;
                row.Cells[2].Style.BackColor = labelColor;
                row.Cells[4].Style.BackColor = labelColor;
                row.Cells[6].Style.BackColor = labelColor;
            }

            // 动态调整行高
            dgvChannels.SizeChanged += (s, e) =>
            {
                if (dgvChannels.Height > 0 && dgvChannels.Rows.Count > 0)
                {
                    int h = (dgvChannels.Height - dgvChannels.ColumnHeadersHeight) / dgvChannels.Rows.Count;
                    if (h < 20) h = 20;
                    foreach (DataGridViewRow row in dgvChannels.Rows) row.Height = h;
                }
            };

            grpMain.Controls.Add(dgvChannels);
            dgvChannels.BringToFront();

            page.Controls.Add(grpMain);
            grpMain.BringToFront();
        }

        // ==================== 2. 控制页 (保持不变) ====================
        private void InitializeControlPage(TabPage page)
        {
            page.BackColor = SystemColors.Control;
            FlowLayoutPanel flow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(10) };

            GroupBox grpOut = new GroupBox { Text = "IO 输出控制", Size = new Size(700, 100) };
            FlowLayoutPanel flowOut = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(10) };
            togglePtc = CreateToggle("PTC 加热", IoCommand.PtcOn, IoCommand.PtcOff);
            toggleAc = CreateToggle("AC 联锁", IoCommand.AcOn, IoCommand.AcOff);
            togglePson = CreateToggle("PSON", IoCommand.PsonOn, IoCommand.PsonOff);
            toggleFan = CreateToggle("风扇", IoCommand.FanOn, IoCommand.FanOff);
            flowOut.Controls.AddRange(new Control[] { togglePtc, toggleAc, togglePson, toggleFan });
            grpOut.Controls.Add(flowOut);
            flow.Controls.Add(grpOut);

            GroupBox grpDir = new GroupBox { Text = "IO 方向配置 (勾选=输出)", Size = new Size(700, 80) };
            FlowLayoutPanel flowDir = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(10) };
            chkIoDirs = new System.Windows.Forms.CheckBox[8];
            for (int i = 0; i < 8; i++)
            {
                chkIoDirs[i] = new System.Windows.Forms.CheckBox { Text = $"IO{i + 1}", Width = 50 };
                flowDir.Controls.Add(chkIoDirs[i]);
            }
            btnReadIoDir = CreateBtn("读取", 0, 0, 60);
            btnWriteIoDir = CreateBtn("写入", 0, 0, 60);
            flowDir.Controls.Add(btnReadIoDir);
            flowDir.Controls.Add(btnWriteIoDir);
            grpDir.Controls.Add(flowDir);
            flow.Controls.Add(grpDir);

            btnReadIoDir.Click += async (s, e) => await ReadIoDirAsync();
            btnWriteIoDir.Click += async (s, e) => await WriteIoDirAsync();

            GroupBox grpEnv = new GroupBox { Text = "环境监测", Size = new Size(700, 80) };
            btnReadEnv = CreateBtn("读取数据", 20, 30, 100);
            btnReadEnv.Click += async (s, e) => await ReadEnvAsync();
            lblTemp = new System.Windows.Forms.Label { Text = "温度: -- ℃", Location = new Point(150, 35), AutoSize = true };
            lblFanCurrent = new System.Windows.Forms.Label { Text = "风扇电流: -- mA", Location = new Point(300, 35), AutoSize = true };
            grpEnv.Controls.AddRange(new Control[] { btnReadEnv, lblTemp, lblFanCurrent });
            flow.Controls.Add(grpEnv);

            page.Controls.Add(flow);
        }

        // ==================== 3. 配置页 (保持不变) ====================
        private void InitializeConfigPage(TabPage page)
        {
            page.BackColor = SystemColors.Control;
            GroupBox grpCfg = new GroupBox { Text = "设备参数管理", Dock = DockStyle.Top, Height = 250, Padding = new Padding(20) };

            int y = 30; int dy = 40;

            AddConfigRow(grpCfg, "序列号 SN:", ref y, out txtSn, "读 SN", async (s, e) => await ReadSnAsync(), "写 SN", async (s, e) => await WriteSnAsync());
            txtSn.Width = 200;

            AddConfigRow(grpCfg, "从机地址:", ref y, out txtAddress, "读取", null, "修改地址", async (s, e) => await SetAddressAsync());
            txtAddress.Text = "1"; txtAddress.Width = 60;

            System.Windows.Forms.Label lblBaud = new System.Windows.Forms.Label { Text = "波特率:", Location = new Point(20, y), AutoSize = true };
            cmbBaudRate = new System.Windows.Forms.ComboBox { Location = new Point(120, y - 3), Width = 100, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbBaudRate.Items.AddRange(new object[] { "9600", "19200", "38400", "57600", "115200" });
            System.Windows.Forms.Button btnBaud = CreateBtn("广播设置", 240, y - 5, 100);
            btnBaud.Click += async (s, e) => await SetBaudAsync();
            grpCfg.Controls.AddRange(new Control[] { lblBaud, cmbBaudRate, btnBaud });
            y += dy;

            chkAcDep = new System.Windows.Forms.CheckBox { Text = "AC 依赖治具到位信号", Location = new Point(20, y), AutoSize = true };
            System.Windows.Forms.Button btnSync = CreateBtn("同步状态", 240, y - 5, 100);
            btnSync.Click += async (s, e) => await SyncAcDepAsync();
            grpCfg.Controls.AddRange(new Control[] { chkAcDep, btnSync });

            page.Controls.Add(grpCfg);
        }

        private System.Windows.Forms.Button CreateBtn(string text, int x, int y, int w)
        {
            return new System.Windows.Forms.Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(w, 28),
                UseVisualStyleBackColor = true
            };
        }

        private void AddConfigRow(GroupBox g, string label, ref int y, out System.Windows.Forms.TextBox txt, string btn1Text, EventHandler h1, string btn2Text, EventHandler h2)
        {
            System.Windows.Forms.Label lbl = new System.Windows.Forms.Label { Text = label, Location = new Point(20, y), AutoSize = true };
            txt = new System.Windows.Forms.TextBox { Location = new Point(120, y - 3) };
            System.Windows.Forms.Button b1 = CreateBtn(btn1Text, 340, y - 5, 80);
            if (h1 != null) b1.Click += h1;
            System.Windows.Forms.Button b2 = CreateBtn(btn2Text, 430, y - 5, 80);
            if (h2 != null) b2.Click += h2;

            if (h1 == null) b1.Visible = false;
            g.Controls.AddRange(new Control[] { lbl, txt, b1, b2 });
            y += 40;
        }

        // 业务逻辑
        private async Task ReadIoDirAsync() { if (!Check()) return; try { ushort d = await _service.GetIoDirectionsAsync(); for (int i = 0; i < 8; i++) chkIoDirs[i].Checked = ((d >> i) & 1) == 1; Msg("读取成功"); } catch (Exception ex) { Msg(ex.Message); } }
        private async Task WriteIoDirAsync() { if (!Check()) return; try { ushort d = 0; for (int i = 0; i < 8; i++) if (chkIoDirs[i].Checked) d |= (ushort)(1 << i); await _service.SetIoDirectionsAsync(d); Msg("写入成功"); } catch (Exception ex) { Msg(ex.Message); } }
        private async Task SyncAcDepAsync() { if (!Check()) return; try { chkAcDep.Checked = await _service.GetAcOnDependencyAsync(); Msg("同步成功"); } catch (Exception ex) { Msg(ex.Message); } }
        private async Task SetBaudAsync() { if (!Check()) return; if (cmbBaudRate.Text != "") try { await _service.SetBaudRateBroadcastAsync(int.Parse(cmbBaudRate.Text)); Msg("指令已发送"); } catch (Exception ex) { Msg(ex.Message); } }
        private async Task ReadEnvAsync() { if (!Check()) return; try { var t = await _service.GetCurrentTemperatureAsync(); var f = await _service.GetFanCurrentAsync(); lblTemp.Text = $"温度:{t}℃"; lblFanCurrent.Text = $"风扇:{f}mA"; } catch (Exception ex) { Msg(ex.Message); } }
        private async Task SetThresholdAsync() { if (!Check()) return; if (double.TryParse(txtThreshold.Text, out double v)) try { await _service.SetAllThresholdsAsync(Enumerable.Repeat(v, 32).ToArray()); Msg("设置成功"); } catch (Exception ex) { Msg(ex.Message); } }
        private async Task ClearFlagsAsync() { if (!Check()) return; try { await _service.ClearAllDropFlagsAsync(); Msg("清除成功"); } catch (Exception ex) { Msg(ex.Message); } }
        private async Task ReadSnAsync() { if (!Check()) return; try { txtSn.Text = await _service.GetSerialNumberAsync(); } catch (Exception ex) { Msg(ex.Message); } }
        private async Task WriteSnAsync() { if (!Check()) return; try { await _service.SetSerialNumberAsync(txtSn.Text); Msg("写入成功"); } catch (Exception ex) { Msg(ex.Message); } }
        private async Task SetAddressAsync() { if (!Check()) return; if (byte.TryParse(txtAddress.Text, out byte a)) try { await _service.SetSlaveId(a); Msg("修改成功"); } catch (Exception ex) { Msg(ex.Message); } }
        private async Task ReadThresholds() { Msg("读取功能需固件支持"); await Task.CompletedTask; }
        private async Task WriteThresholds() { await SetThresholdAsync(); }

        private bool Check() => _service != null && _service.IsConnected;
        private void Msg(string m) => MessageBox.Show(m);

        private StatusIndicator CreateIndicator(string label) { return new StatusIndicator { Label = label, Size = new Size(90, 30), Margin = new Padding(5) }; }
        private ToggleSwitch CreateToggle(string text, IoCommand cmdOn, IoCommand cmdOff)
        {
            var toggle = new ToggleSwitch { Text = text, Size = new Size(150, 30), Margin = new Padding(5) };
            toggle.CheckedChanged += async (s, e) => { if (toggle.Tag != null) return; try { var cmd = toggle.Checked ? cmdOn : cmdOff; if (_service != null && _service.IsConnected) await _service.SetIoOutputAsync(cmd); } catch { toggle.Tag = "UP"; toggle.Checked = !toggle.Checked; toggle.Tag = null; } };
            return toggle;
        }

        private void OnDataUpdated(List<ChannelData> channels)
        {
            if (this.InvokeRequired) { this.Invoke(new Action(() => OnDataUpdated(channels))); return; }
            for (int i = 0; i < channels.Count; i++)
            {
                int colGroup = i / 8; int row = i % 8;
                dgvChannels.Rows[row].Cells[colGroup * 2].Value = channels[i].Channel;
                var cell = dgvChannels.Rows[row].Cells[colGroup * 2 + 1];
                cell.Value = channels[i].VoltageText;
                cell.Style.ForeColor = channels[i].Status == ChannelDropStatus.OK ? Color.Black : Color.Red;
            }
            // ★★★ 修复：如果还没加载过设备信息，触发异步读取 ★★★
            if (!_hasLoadedInfo) UpdateDeviceInfoAsync();
        }

        private async void UpdateDeviceInfoAsync()
        {
            try
            {
                string ver = await _service.GetVersionAsync();
                string name = "VDC-32 Standard";
                string addr = _service.SlaveId.ToString();

                // ★★★ 触发事件通知外部 (ConnectionPanel) 更新 ★★★
                OnDeviceInfoUpdated?.Invoke(ver, name, addr);

                // 标记已加载，避免重复读取造成闪烁
                _hasLoadedInfo = true;
            }
            catch { }
        }

        private void OnIoStatusUpdated(IoStatus status)
        {
            if (this.InvokeRequired) { this.Invoke(new Action(() => OnIoStatusUpdated(status))); return; }
            UpdateToggle(togglePtc, status.Io0OutputLow);
            UpdateToggle(toggleAc, status.Io1OutputLow);
            UpdateToggle(togglePson, status.Io2OutputLow);
            UpdateToggle(toggleFan, status.Io3OutputLow);
            indS1.State = status.S1Switch ? StatusIndicator.IndicatorState.On : StatusIndicator.IndicatorState.Off;
            indJig.State = status.JigInPlace ? StatusIndicator.IndicatorState.On : StatusIndicator.IndicatorState.Off;
            indWaterSelf.State = status.WaterLeakSelf ? StatusIndicator.IndicatorState.Error : StatusIndicator.IndicatorState.Off;
            indWaterPar.State = status.WaterLeakParallel ? StatusIndicator.IndicatorState.Error : StatusIndicator.IndicatorState.Off;
            indContactor.State = status.ContactorSignal ? StatusIndicator.IndicatorState.On : StatusIndicator.IndicatorState.Off;
            indFan.State = status.FanStatus ? StatusIndicator.IndicatorState.On : StatusIndicator.IndicatorState.Off;
            indAcOnDep.State = status.AcOnDependsOnJig ? StatusIndicator.IndicatorState.On : StatusIndicator.IndicatorState.Off;
        }
        private void UpdateToggle(ToggleSwitch toggle, bool isChecked) { toggle.Tag = "UPDATING"; if (toggle.Checked != isChecked) toggle.Checked = isChecked; toggle.Tag = null; }
    }
}