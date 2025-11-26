using System;
using System.Drawing;
using System.Windows.Forms;
using DebugTool.Models;
using DebugTool.UI.Controls;

namespace DebugTool.UI.Load.Tabs
{
    /// <summary>
    /// 实时数据 Tab - 显示8通道的实时电压、电流、功率等数据
    /// </summary>
    public class RealTimeDataTab : UserControl
    {
        // 表头面板
        private Panel _headerPanel;
        private FlowLayoutPanel _rowsContainer;

        // 10个数据行
        private DataRowPanel _rowVoltage;
        private DataRowPanel _rowCurrent;
        private DataRowPanel _rowPower;
        private DataRowPanel _rowInput;
        private DataRowPanel _rowDcDcStatus;
        private DataRowPanel _rowStatus;
        private DataRowPanel _rowWorkMode;
        private DataRowPanel _rowVonPoint;
        private DataRowPanel _rowLoadValue;
        private DataRowPanel _rowDelay;

        public RealTimeDataTab()
        {
            InitializeUI();
        }

        private void InitializeUI()
        {
            this.Dock = DockStyle.Fill;
            this.BackColor = SystemColors.Control;

            // 1. 表头
            _headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 40,
                BackColor = SystemColors.ControlLight
            };

            Label lblTitle = new Label
            {
                Text = "数据项",
                Location = new Point(0, 0),
                Size = new Size(120, 40),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("微软雅黑", 9F, FontStyle.Bold)
            };
            _headerPanel.Controls.Add(lblTitle);

            // 8个通道标题
            for (int i = 0; i < 8; i++)
            {
                Label lblCh = new Label
                {
                    Text = $"CH {i + 1:D2}",
                    AutoSize = false,
                    Location = new Point(130 + i * 168, 0),
                    Size = new Size(160, 40),
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font("微软雅黑", 10F, FontStyle.Bold)
                };
                _headerPanel.Controls.Add(lblCh);
            }
            this.Controls.Add(_headerPanel);

            // 2. 数据行容器
            _rowsContainer = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(0, 45, 0, 0) // 避免遮挡表头
            };

            // 实例化10个数据行
            _rowVoltage = new DataRowPanel("电压 (V)");
            _rowCurrent = new DataRowPanel("电流 (A)");
            _rowPower = new DataRowPanel("功率 (W)");
            _rowInput = new DataRowPanel("输入电压 (V)");
            _rowDcDcStatus = new DataRowPanel("DC-DC状态");
            _rowStatus = new DataRowPanel("状态标志");
            _rowWorkMode = new DataRowPanel("工作模式");
            _rowVonPoint = new DataRowPanel("Von点 (V)");
            _rowLoadValue = new DataRowPanel("设定值");
            _rowDelay = new DataRowPanel("延迟 (s)");

            _rowsContainer.Controls.AddRange(new Control[]
            {
                _rowVoltage, _rowCurrent, _rowPower, _rowInput, _rowDcDcStatus,
                _rowStatus, _rowWorkMode, _rowVonPoint, _rowLoadValue, _rowDelay
            });

            this.Controls.Add(_rowsContainer);
            _headerPanel.BringToFront();
        }

        /// <summary>
        /// 更新实时数据（电压、电流、功率、状态等）
        /// </summary>
        public void UpdateRealTimeData(int channelIndex, ChannelRealTimeStatus chData)
        {
            if (chData == null || channelIndex < 0 || channelIndex >= 8) return;

            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => UpdateRealTimeData(channelIndex, chData)));
                return;
            }

            bool isOnline = chData.IsOnline;
            string alarmText = GetAlarmText(chData.StatusBits);
            string statusDisplay = isOnline ? "在线" : "待机";
            if (!string.IsNullOrEmpty(alarmText)) statusDisplay = alarmText;
            Color statusColor = isOnline ? (!string.IsNullOrEmpty(alarmText) ? Color.Red : Color.Green) : Color.Gray;

            _rowVoltage.UpdateChannelValue(channelIndex, $"{chData.RealVoltage:F2}", Color.Blue);
            _rowCurrent.UpdateChannelValue(channelIndex, $"{chData.RealCurrent:F2}", Color.OrangeRed);
            _rowPower.UpdateChannelValue(channelIndex, $"{(chData.RealVoltage * chData.RealCurrent):F2}", Color.Purple);
            _rowInput.UpdateChannelValue(channelIndex, $"{chData.LlcVoltage:F2}", Color.Black);
            _rowDcDcStatus.UpdateChannelValue(channelIndex, isOnline ? "ON" : "OFF", isOnline ? Color.Green : Color.Gray);
            _rowStatus.UpdateChannelValue(channelIndex, statusDisplay, statusColor);
        }

        /// <summary>
        /// 更新配置数据（工作模式、Von点、设定值、延迟）
        /// </summary>
        public void UpdateConfigData(int channelIndex, ChannelLoadConfig cfg)
        {
            if (cfg == null || channelIndex < 0 || channelIndex >= 8) return;

            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => UpdateConfigData(channelIndex, cfg)));
                return;
            }

            string modeStr = cfg.Mode.ToString().Replace("CC_Slow", "CC慢").Replace("CC_Fast", "CC快");
            _rowWorkMode.UpdateChannelValue(channelIndex, modeStr, Color.DarkBlue);
            _rowVonPoint.UpdateChannelValue(channelIndex, $"{cfg.VonVoltage:F1}", Color.Black);

            string unit = (cfg.Mode == LoadMode.CV) ? "V" : ((cfg.Mode == LoadMode.CP) ? "W" : "A");
            _rowLoadValue.UpdateChannelValue(channelIndex, $"{cfg.LoadValue:F2}{unit}", Color.DarkGreen);
            _rowDelay.UpdateChannelValue(channelIndex, $"{cfg.AdditionalParam / 10.0:F1}", Color.Black);
        }

        private string GetAlarmText(ushort statusBits)
        {
            if ((statusBits & 0x02) != 0) return "LLC过压";
            if ((statusBits & 0x10) != 0) return "超功率";
            if ((statusBits & 0x20) != 0) return "超温";
            return "";
        }
    }
}
