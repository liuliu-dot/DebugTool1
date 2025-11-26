using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using DebugTool.Models;
using DebugTool.Services;
using DebugTool.UI.Controls; // 引用 DataRowPanel

namespace DebugTool.UI.Controls.Load
{
    public partial class LoadMonitorView : UserControl
    {
        private DeviceService750_4_60A _service;
        private Label lblStatusInfo;

        // === 核心控件 ===
        private TabControl tabControl;

        // --- 1. 实时数据页 (Realtime) ---
        private Panel pnlRealtimeHeader;
        private FlowLayoutPanel flowRealtimeRows;

        // ★★★ 修复：定义所有 10 个数据行 ★★★
        private DataRowPanel rowVoltage, rowCurrent, rowPower, rowInput, rowDcDcStatus, rowStatus;
        private DataRowPanel rowWorkMode, rowVonPoint, rowLoadValue, rowDelay;

        // --- 2-5. 其他页面控件 (保持不变) ---
        private ComboBox cmbTargetCh, cmbMode, cmbBatchMode;
        private NumericUpDown numVon, numVal, numDelay;
        private NumericUpDown numBatchVon, numBatchVal, numBatchDelay;
        private NumericUpDown numPowerVolt;
        private CheckBox chk800V;
        private Panel[] channelCards;
        private Label[] channelStatusDots, channelVoltageLabels, channelCurrentLabels, channelPowerLabels, channelStatusLabels;
        private Label lblMonitorSummary;
        private Label lblInvSummary;
        private Panel pnlInvOverTemp, pnlInvAdFault, pnlInvOutVolt, pnlInvFan, pnlInvTimeout, pnlInvDcBus;
        private Label lblInvOverTemp, lblInvAdFault, lblInvOutVolt, lblInvFan, lblInvTimeout, lblInvDcBus;

        public LoadMonitorView()
        {
            InitializeCustomUI();
        }

        public void SetService(DeviceService750_4_60A service)
        {
            _service = service;
        }

        private void InitializeCustomUI()
        {
            this.Dock = DockStyle.Fill;
            this.BackColor = SystemColors.Control;

            // 1. 顶部状态条
            Panel topBar = new Panel { Dock = DockStyle.Top, Height = 30, BackColor = SystemColors.Control };
            lblStatusInfo = new Label { Text = "设备未连接", Location = new Point(10, 8), AutoSize = true, ForeColor = Color.DimGray };
            topBar.Controls.Add(lblStatusInfo);
            this.Controls.Add(topBar);

            // 2. 主 TabControl
            tabControl = new TabControl { Dock = DockStyle.Fill, Font = new Font("微软雅黑", 9F) };

            TabPage pageRealtime = new TabPage("实时数据");
            TabPage pageConfig = new TabPage("通道配置");
            TabPage pagePower = new TabPage("电源设置");
            TabPage pageMonitor = new TabPage("通道监控");
            TabPage pageInverter = new TabPage("逆变状态");

            InitializeRealtimePage(pageRealtime);
            InitializeConfigPage(pageConfig);
            InitializePowerPage(pagePower);
            InitializeMonitorPage(pageMonitor);
            InitializeInverterPage(pageInverter);

            tabControl.TabPages.Add(pageRealtime);
            tabControl.TabPages.Add(pageConfig);
            tabControl.TabPages.Add(pagePower);
            tabControl.TabPages.Add(pageMonitor);
            tabControl.TabPages.Add(pageInverter);

            this.Controls.Add(tabControl);
            topBar.SendToBack();
            tabControl.BringToFront();
        }

        // ==================== 1. 实时数据页 (修复：包含所有行 + 解决遮挡) ====================
        private void InitializeRealtimePage(TabPage page)
        {
            page.BackColor = SystemColors.Control;

            // 1. 表头
            pnlRealtimeHeader = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = SystemColors.ControlLight };

            // 左上角标题 (保持不变)
            Label lblTitle = new Label { Text = "数据项", Location = new Point(0, 0), Size = new Size(120, 40), TextAlign = ContentAlignment.MiddleCenter, Font = new Font("微软雅黑", 9F, FontStyle.Bold) };
            pnlRealtimeHeader.Controls.Add(lblTitle);

            // 8个通道标题 (必须与 DataRowPanel 的布局完全一致)
            for (int i = 0; i < 8; i++)
            {
                // === 核心修改 ===
                // 位置: 130 + i * 168 -> 130 + i * 120
                int xPos = 130 + i * 120;

                Label lblCh = new Label
                {
                    Text = $"CH {i + 1:D2}",
                    AutoSize = false,
                    Location = new Point(xPos, 0),
                    // 宽度: 160 -> 115
                    Size = new Size(115, 40),
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font("微软雅黑", 10F, FontStyle.Bold)
                };
                pnlRealtimeHeader.Controls.Add(lblCh);
            }
            page.Controls.Add(pnlRealtimeHeader);

            // 2. 数据行容器
            flowRealtimeRows = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(0, 45, 0, 0)
            };

            // 实例化所有 10 个数据行 (保持不变)
            rowVoltage = new DataRowPanel("电压 (V)");
            rowCurrent = new DataRowPanel("电流 (A)");
            rowPower = new DataRowPanel("功率 (W)");
            rowInput = new DataRowPanel("输入电压 (V)");
            rowDcDcStatus = new DataRowPanel("DC-DC状态");
            rowStatus = new DataRowPanel("状态标志");
            rowWorkMode = new DataRowPanel("工作模式");
            rowVonPoint = new DataRowPanel("Von点 (V)");
            rowLoadValue = new DataRowPanel("设定值");
            rowDelay = new DataRowPanel("延迟 (s)");

            flowRealtimeRows.Controls.AddRange(new Control[] {
        rowVoltage, rowCurrent, rowPower, rowInput, rowDcDcStatus,
        rowStatus, rowWorkMode, rowVonPoint, rowLoadValue, rowDelay
    });

            page.Controls.Add(flowRealtimeRows);
            pnlRealtimeHeader.BringToFront();
        }

        // ==================== 2. 通道配置页 ====================
        private void InitializeConfigPage(TabPage page)
        {
            page.BackColor = SystemColors.Control;

            // 1. 单通道设置
            GroupBox grpSingle = new GroupBox { Text = "单通道负载配置", Location = new Point(20, 20), Size = new Size(450, 280) };
            int y = 30, dy = 40;

            AddLabel(grpSingle, "通道:", 20, y);
            cmbTargetCh = new ComboBox { Location = new Point(80, y - 3), Width = 80, DropDownStyle = ComboBoxStyle.DropDownList };
            for (int i = 1; i <= 8; i++) cmbTargetCh.Items.Add($"CH{i}"); cmbTargetCh.SelectedIndex = 0;

            AddLabel(grpSingle, "模式:", 180, y);
            cmbMode = new ComboBox { Location = new Point(230, y - 3), Width = 120, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbMode.Items.AddRange(new object[] { "CC 慢速", "CV 恒压", "CP 恒功率", "CR 恒阻", "CC 快速" }); cmbMode.SelectedIndex = 0;

            y += dy;
            AddLabel(grpSingle, "Von(V):", 20, y);
            numVon = CreateNum(80, y - 3, 0, 60, 2, 0);

            AddLabel(grpSingle, "设定值:", 180, y);
            numVal = CreateNum(240, y - 3, 0, 750, 2, 0);

            y += dy;
            AddLabel(grpSingle, "延时(s):", 20, y);
            numDelay = CreateNum(80, y - 3, 0, 60, 1, 0);

            y += dy + 10;
            Button btnRead = CreateBtn("读取配置", 20, y, 100, async (s, e) => await ReadSingleConfig());
            Button btnApply = CreateBtn("应用设置", 130, y, 100, async (s, e) => await ApplyConfig(false));
            Button btnSave = CreateBtn("保存配置", 240, y, 100, async (s, e) => await ApplyConfig(true));

            grpSingle.Controls.AddRange(new Control[] { cmbTargetCh, cmbMode, numVon, numVal, numDelay, btnRead, btnApply, btnSave });
            page.Controls.Add(grpSingle);

            // 2. 批量设置
            GroupBox grpBatch = new GroupBox { Text = "批量配置 (所有8通道)", Location = new Point(500, 20), Size = new Size(450, 280) };
            y = 30;

            AddLabel(grpBatch, "统一模式:", 20, y);
            cmbBatchMode = new ComboBox { Location = new Point(100, y - 3), Width = 120, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbBatchMode.Items.AddRange(new object[] { "CC 慢速", "CV 恒压", "CP 恒功率", "CR 恒阻", "CC 快速" }); cmbBatchMode.SelectedIndex = 0;

            y += dy;
            AddLabel(grpBatch, "Von:", 20, y);
            numBatchVon = CreateNum(60, y - 3, 0, 60, 2, 0);

            AddLabel(grpBatch, "值:", 160, y);
            numBatchVal = CreateNum(190, y - 3, 0, 750, 2, 0);

            y += dy;
            AddLabel(grpBatch, "延时:", 20, y);
            numBatchDelay = CreateNum(60, y - 3, 0, 60, 1, 0);

            y += dy + 10;
            Button btnBApply = CreateBtn("批量应用", 20, y, 120, async (s, e) => await ApplyBatchConfig(false));
            Button btnBSave = CreateBtn("批量保存", 160, y, 120, async (s, e) => await ApplyBatchConfig(true));

            grpBatch.Controls.AddRange(new Control[] { cmbBatchMode, numBatchVon, numBatchVal, numBatchDelay, btnBApply, btnBSave });
            page.Controls.Add(grpBatch);
        }

        // ==================== 3. 电源设置页 ====================
        private void InitializePowerPage(TabPage page)
        {
            page.BackColor = SystemColors.Control;
            GroupBox grpPower = new GroupBox { Text = "800V 电源设置", Location = new Point(20, 20), Size = new Size(400, 200) };

            chk800V = new CheckBox { Text = "启用 800V 设备模式", Location = new Point(30, 40), AutoSize = true };
            chk800V.CheckedChanged += (s, e) => { numPowerVolt.Enabled = chk800V.Checked; };

            Label l = new Label { Text = "输出电压 (300-800V):", Location = new Point(30, 80), AutoSize = true };
            numPowerVolt = CreateNum(180, 77, 300, 800, 0, 400);
            numPowerVolt.Enabled = false;

            Button btnSet = CreateBtn("设置整机电压", 30, 130, 150, BtnSetPower_Click);
            grpPower.Controls.AddRange(new Control[] { chk800V, l, numPowerVolt, btnSet });
            page.Controls.Add(grpPower);
        }

        // ==================== 4. 通道监控页 ====================
        private void InitializeMonitorPage(TabPage page)
        {
            page.BackColor = SystemColors.Control;
            page.AutoScroll = true;
            lblMonitorSummary = new Label { Text = "等待数据...", Dock = DockStyle.Top, Height = 30, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(10, 0, 0, 0) };
            page.Controls.Add(lblMonitorSummary);
            FlowLayoutPanel flow = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(10), AutoScroll = true };
            channelCards = new Panel[8];
            channelStatusDots = new Label[8];
            channelVoltageLabels = new Label[8];
            channelCurrentLabels = new Label[8];
            channelPowerLabels = new Label[8];
            channelStatusLabels = new Label[8];
            for (int i = 0; i < 8; i++)
            {
                Panel card = new Panel { Size = new Size(300, 150), BackColor = Color.White, Margin = new Padding(10), BorderStyle = BorderStyle.FixedSingle };
                Label idx = new Label { Text = $"CH-{i + 1:00}", Location = new Point(10, 10), AutoSize = true, Font = new Font("微软雅黑", 12, FontStyle.Bold) };
                Label dot = new Label { Text = "●", Location = new Point(270, 5), AutoSize = true, Font = new Font("微软雅黑", 14), ForeColor = Color.LightGray };
                Label lblV = new Label { Text = "0.00 V", Location = new Point(10, 40), Size = new Size(280, 30), Font = new Font("Consolas", 18, FontStyle.Bold), ForeColor = Color.Blue, TextAlign = ContentAlignment.MiddleRight };
                Label lblA = new Label { Text = "0.00 A", Location = new Point(10, 80), Size = new Size(130, 25), Font = new Font("Consolas", 11), TextAlign = ContentAlignment.MiddleLeft };
                Label lblW = new Label { Text = "0.00 W", Location = new Point(150, 80), Size = new Size(140, 25), Font = new Font("Consolas", 11), TextAlign = ContentAlignment.MiddleRight };
                Label status = new Label { Text = "离线", Dock = DockStyle.Bottom, Height = 25, TextAlign = ContentAlignment.MiddleCenter, BackColor = Color.LightGray };
                card.Controls.AddRange(new Control[] { idx, dot, lblV, lblA, lblW, status });
                flow.Controls.Add(card);
                channelCards[i] = card; channelStatusDots[i] = dot; channelVoltageLabels[i] = lblV; channelCurrentLabels[i] = lblA; channelPowerLabels[i] = lblW; channelStatusLabels[i] = status;
            }
            page.Controls.Add(flow);
            lblMonitorSummary.BringToFront();
        }

        // ==================== 5. 逆变状态页 ====================
        private void InitializeInverterPage(TabPage page)
        {
            page.BackColor = SystemColors.Control;
            lblInvSummary = new Label { Text = "逆变器状态: 未知", Dock = DockStyle.Top, Height = 40, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("微软雅黑", 12, FontStyle.Bold) };
            page.Controls.Add(lblInvSummary);
            FlowLayoutPanel flow = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), AutoScroll = true };
            CreateInvCard(flow, "逆变过温", out pnlInvOverTemp, out lblInvOverTemp);
            CreateInvCard(flow, "AD采样故障", out pnlInvAdFault, out lblInvAdFault);
            CreateInvCard(flow, "输出电压", out pnlInvOutVolt, out lblInvOutVolt);
            CreateInvCard(flow, "风扇状态", out pnlInvFan, out lblInvFan);
            CreateInvCard(flow, "通信超时", out pnlInvTimeout, out lblInvTimeout);
            CreateInvCard(flow, "母线电压", out pnlInvDcBus, out lblInvDcBus);
            page.Controls.Add(flow);
            lblInvSummary.BringToFront();
        }

        // === 核心数据更新逻辑 (修复) ===
        public void UpdateData(DeviceRealTimeData realData)
        {
            if (realData == null) return;
            if (this.InvokeRequired) { this.Invoke(new Action(() => UpdateData(realData))); return; }

            int onlineCount = 0;
            double totalPower = 0;
            int alarmCount = 0;

            for (int i = 0; i < 8; i++)
            {
                var chData = realData.Channels.FirstOrDefault(c => c.ChannelIndex == (i + 1));
                if (chData == null) continue;

                bool isOnline = chData.IsOnline;
                if (isOnline) { onlineCount++; totalPower += (chData.RealVoltage * chData.RealCurrent); }

                string alarmText = GetAlarmText(chData.StatusBits);
                if (!string.IsNullOrEmpty(alarmText)) alarmCount++;

                string statusDisplay = isOnline ? "在线" : "待机";
                if (!string.IsNullOrEmpty(alarmText)) statusDisplay = alarmText;
                Color statusColor = isOnline ? (!string.IsNullOrEmpty(alarmText) ? Color.Red : Color.Green) : Color.Gray;

                // 1. 更新 DataRowPanel (实时数据页)
                rowVoltage.UpdateChannelValue(i, $"{chData.RealVoltage:F2}", Color.Blue);
                rowCurrent.UpdateChannelValue(i, $"{chData.RealCurrent:F2}", Color.OrangeRed);
                rowPower.UpdateChannelValue(i, $"{(chData.RealVoltage * chData.RealCurrent):F2}", Color.Purple);
                rowInput.UpdateChannelValue(i, $"{chData.LlcVoltage:F2}", Color.Black);
                rowDcDcStatus.UpdateChannelValue(i, isOnline ? "ON" : "OFF", isOnline ? Color.Green : Color.Gray);
                rowStatus.UpdateChannelValue(i, statusDisplay, statusColor);

                // 由于配置数据是低频刷新的，这里不更新配置行，仅更新监控页

                // 2. 更新监控卡片
                UpdateSingleCard(i, isOnline, !string.IsNullOrEmpty(alarmText), alarmText, chData);
            }

            lblMonitorSummary.Text = $"在线: {onlineCount}/8 | 总功率: {totalPower:F2}W | 通道告警: {alarmCount}";
            lblStatusInfo.Text = $"刷新: {DateTime.Now:HH:mm:ss}";

            // 3. 更新逆变器状态
            if (realData.Inverter != null) UpdateInverterUI(realData.Inverter);
        }

        // === 业务逻辑 (修复：读取配置回显到表格) ===
        private async Task ReadAndDisplayLoadConfigAsync()
        {
            if (_service == null || !_service.IsConnected)
            {
                MessageBox.Show("请先连接设备");
                return;
            }

            try
            {
                lblStatusInfo.Text = "正在读取配置...";
                var configs = await _service.ReadAllChannelsConfigAsync(1); // 假设地址为1

                for (int i = 0; i < 8; i++)
                {
                    var cfg = configs.FirstOrDefault(c => c.ChannelIndex == i + 1);
                    if (cfg != null)
                    {
                        // ★★★ 更新配置行：模式、Von点、设定值、延迟 ★★★
                        string modeStr = cfg.Mode.ToString().Replace("CC_Slow", "CC慢").Replace("CC_Fast", "CC快");
                        rowWorkMode.UpdateChannelValue(i, modeStr, Color.DarkBlue);
                        rowVonPoint.UpdateChannelValue(i, $"{cfg.VonVoltage:F1}", Color.Black);

                        string unit = (cfg.Mode == LoadMode.CV) ? "V" : ((cfg.Mode == LoadMode.CP) ? "W" : "A");
                        rowLoadValue.UpdateChannelValue(i, $"{cfg.LoadValue:F2}{unit}", Color.DarkGreen);

                        rowDelay.UpdateChannelValue(i, $"{cfg.AdditionalParam / 10.0:F1}", Color.Black);
                    }
                }

                lblStatusInfo.Text = "配置读取成功";
                MessageBox.Show("读取成功，已更新到列表！");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"读取失败: {ex.Message}");
                lblStatusInfo.Text = "配置读取失败";
            }
        }

        // ... (保留后面的辅助方法和控制逻辑) ...
        // 为节省篇幅，这里只保留方法签名，请把之前代码中的具体逻辑复制进来

        private void AddLabel(Control p, string t, int x, int y) { p.Controls.Add(new Label { Text = t, Location = new Point(x, y), AutoSize = true }); }
        private NumericUpDown CreateNum(int x, int y, decimal min, decimal max, int decimals, decimal val) { return new NumericUpDown { Location = new Point(x, y), Width = 70, Minimum = min, Maximum = max, DecimalPlaces = decimals, Value = val }; }
        private Button CreateBtn(string text, int x, int y, int w, EventHandler onClick) { var btn = new Button { Text = text, Location = new Point(x, y), Size = new Size(w, 28), UseVisualStyleBackColor = true }; if (onClick != null) btn.Click += onClick; return btn; }

        private void CreateInvCard(FlowLayoutPanel p, string t, out Panel pnl, out Label lbl)
        {
            pnl = new Panel { Size = new Size(300, 100), BackColor = Color.White, Margin = new Padding(10), BorderStyle = BorderStyle.FixedSingle };
            Label lt = new Label { Text = t, Location = new Point(10, 10), AutoSize = true, Font = new Font("微软雅黑", 10, FontStyle.Bold) };
            lbl = new Label { Text = "--", Dock = DockStyle.Bottom, Height = 50, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("微软雅黑", 14, FontStyle.Bold), ForeColor = Color.Gray };
            pnl.Controls.Add(lt); pnl.Controls.Add(lbl); p.Controls.Add(pnl);
        }
        private void UpdateInverterUI(InverterStatus inv)
        {
            bool fault = inv.HasFault(); lblInvSummary.Text = fault ? "异常" : "正常"; lblInvSummary.ForeColor = fault ? Color.Red : Color.Green;
            void Set(Panel p, Label l, bool err, string norm, string fail) { p.BackColor = err ? Color.MistyRose : Color.White; l.Text = err ? fail : norm; l.ForeColor = err ? Color.Red : Color.Green; }
            Set(pnlInvOverTemp, lblInvOverTemp, inv.IsOverTemp, "温度正常", "过温保护");
            Set(pnlInvAdFault, lblInvAdFault, inv.IsAdFault, "采样正常", "AD故障");
            Set(pnlInvFan, lblInvFan, inv.IsFanFault, "风扇正常", "风扇故障");
            Set(pnlInvTimeout, lblInvTimeout, inv.IsTimeout, "通信正常", "超时");
            Set(pnlInvOutVolt, lblInvOutVolt, inv.OutputVoltageStatus != 1, "电压正常", inv.OutputVoltageStatus == 0 ? "输出欠压" : "输出过压");
            Set(pnlInvDcBus, lblInvDcBus, inv.DcBusVoltageStatus != 1, "电压正常", inv.DcBusVoltageStatus == 0 ? "母线欠压" : "母线过压");
        }
        private async Task ApplyConfig(bool save) { if (_service == null || !_service.IsConnected) { MessageBox.Show("请连接"); return; } try { var cfg = new ChannelLoadConfig { ChannelIndex = cmbTargetCh.SelectedIndex + 1, Mode = (LoadMode)cmbMode.SelectedIndex, VonVoltage = (double)numVon.Value, LoadValue = (double)numVal.Value, AdditionalParam = (int)(numDelay.Value * 10) }; await _service.SetSingleChannelConfigAsync(1, cfg, save); MessageBox.Show("成功"); } catch (Exception ex) { MessageBox.Show(ex.Message); } }
        private async Task ApplyBatchConfig(bool save) { if (_service == null || !_service.IsConnected) { MessageBox.Show("请连接"); return; } try { var list = new List<ChannelLoadConfig>(); for (int i = 1; i <= 8; i++) list.Add(new ChannelLoadConfig { ChannelIndex = i, Mode = (LoadMode)cmbBatchMode.SelectedIndex, VonVoltage = (double)numBatchVon.Value, LoadValue = (double)numBatchVal.Value, AdditionalParam = (int)(numBatchDelay.Value * 10) }); await _service.SetAllChannelsConfigAsync(1, list, save); MessageBox.Show("成功"); } catch (Exception ex) { MessageBox.Show(ex.Message); } }
        private async void BtnSetPower_Click(object sender, EventArgs e) { if (_service == null || !_service.IsConnected) { MessageBox.Show("请连接"); return; } if (MessageBox.Show($"设置 {numPowerVolt.Value}V?", "确认", MessageBoxButtons.YesNo) == DialogResult.Yes) try { await _service.SetOutputVoltageAsync(1, (double)numPowerVolt.Value); MessageBox.Show("成功"); } catch (Exception ex) { MessageBox.Show(ex.Message); } }
        private async Task ReadSingleConfig()
        {
            if (_service == null || !_service.IsConnected) { MessageBox.Show("请连接"); return; }
            try
            {
                var configs = await _service.ReadAllChannelsConfigAsync(1);
                int idx = cmbTargetCh.SelectedIndex + 1;
                var cfg = configs.FirstOrDefault(c => c.ChannelIndex == idx);
                if (cfg != null) { cmbMode.SelectedIndex = (int)cfg.Mode; numVon.Value = (decimal)cfg.VonVoltage; numVal.Value = (decimal)cfg.LoadValue; numDelay.Value = (decimal)(cfg.AdditionalParam / 10.0); MessageBox.Show("读取成功"); }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void UpdateSingleCard(int i, bool isOnline, bool hasAlarm, string alarmText, ChannelRealTimeStatus chData)
        {
            if (channelCards[i] == null) return;
            channelStatusDots[i].ForeColor = isOnline ? Color.LimeGreen : Color.Gray;
            channelVoltageLabels[i].Text = isOnline ? $"{chData.RealVoltage:F2} V" : "-- V";
            channelCurrentLabels[i].Text = isOnline ? $"{chData.RealCurrent:F2} A" : "-- T";
            channelPowerLabels[i].Text = isOnline ? $"{(chData.RealVoltage * chData.RealCurrent):F2} W" : "-- W";
            if (hasAlarm) { channelStatusLabels[i].Text = $"⚠️ {alarmText}"; channelStatusLabels[i].ForeColor = Color.Red; }
            else { channelStatusLabels[i].Text = isOnline ? "正常" : "离线"; channelStatusLabels[i].ForeColor = Color.Gray; }
        }
        private string GetAlarmText(ushort s) { if ((s & 0x02) != 0) return "LLC过压"; if ((s & 0x10) != 0) return "超功率"; if ((s & 0x20) != 0) return "超温"; return ""; }
    }
}