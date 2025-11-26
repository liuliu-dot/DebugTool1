using System;
using System.Drawing;
using System.Windows.Forms;
using DebugTool.Models;

namespace DebugTool.UI.Load.Tabs
{
    /// <summary>
    /// 通道监控 Tab - 8个通道的卡片式监控视图
    /// </summary>
    public class ChannelMonitorTab : UserControl
    {
        // 8个通道的监控卡片
        private Panel[] _channelCards;
        private Label[] _statusDots;
        private Label[] _voltageLabels;
        private Label[] _currentLabels;
        private Label[] _powerLabels;
        private Label[] _statusLabels;

        // 顶部摘要
        private Label _lblSummary;

        public ChannelMonitorTab()
        {
            InitializeUI();
        }

        private void InitializeUI()
        {
            this.Dock = DockStyle.Fill;
            this.BackColor = SystemColors.Control;
            this.AutoScroll = true;

            // 顶部摘要栏
            _lblSummary = new Label
            {
                Text = "等待数据...",
                Dock = DockStyle.Top,
                Height = 30,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0),
                Font = new Font("微软雅黑", 9F),
                BackColor = SystemColors.ControlLight
            };
            this.Controls.Add(_lblSummary);

            // 卡片容器
            FlowLayoutPanel flowPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                AutoScroll = true
            };

            // 初始化数组
            _channelCards = new Panel[8];
            _statusDots = new Label[8];
            _voltageLabels = new Label[8];
            _currentLabels = new Label[8];
            _powerLabels = new Label[8];
            _statusLabels = new Label[8];

            // 创建8个通道卡片
            for (int i = 0; i < 8; i++)
            {
                CreateChannelCard(flowPanel, i);
            }

            this.Controls.Add(flowPanel);
            _lblSummary.BringToFront();
        }

        private void CreateChannelCard(FlowLayoutPanel parent, int index)
        {
            // 卡片面板
            Panel card = new Panel
            {
                Size = new Size(300, 150),
                BackColor = Color.White,
                Margin = new Padding(10),
                BorderStyle = BorderStyle.FixedSingle
            };

            // 通道号标题
            Label lblIndex = new Label
            {
                Text = $"CH-{index + 1:00}",
                Location = new Point(10, 10),
                AutoSize = true,
                Font = new Font("微软雅黑", 12, FontStyle.Bold)
            };

            // 状态指示点
            Label dot = new Label
            {
                Text = "●",
                Location = new Point(270, 5),
                AutoSize = true,
                Font = new Font("微软雅黑", 14),
                ForeColor = Color.LightGray
            };

            // 电压显示（大字体）
            Label lblVoltage = new Label
            {
                Text = "-- V",
                Location = new Point(10, 40),
                Size = new Size(280, 30),
                Font = new Font("Consolas", 18, FontStyle.Bold),
                ForeColor = Color.Blue,
                TextAlign = ContentAlignment.MiddleRight
            };

            // 电流显示
            Label lblCurrent = new Label
            {
                Text = "-- A",
                Location = new Point(10, 80),
                Size = new Size(130, 25),
                Font = new Font("Consolas", 11),
                TextAlign = ContentAlignment.MiddleLeft
            };

            // 功率显示
            Label lblPower = new Label
            {
                Text = "-- W",
                Location = new Point(150, 80),
                Size = new Size(140, 25),
                Font = new Font("Consolas", 11),
                TextAlign = ContentAlignment.MiddleRight
            };

            // 底部状态栏
            Label lblStatus = new Label
            {
                Text = "离线",
                Dock = DockStyle.Bottom,
                Height = 25,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.LightGray,
                ForeColor = Color.DimGray
            };

            card.Controls.AddRange(new Control[] { lblIndex, dot, lblVoltage, lblCurrent, lblPower, lblStatus });
            parent.Controls.Add(card);

            // 保存引用
            _channelCards[index] = card;
            _statusDots[index] = dot;
            _voltageLabels[index] = lblVoltage;
            _currentLabels[index] = lblCurrent;
            _powerLabels[index] = lblPower;
            _statusLabels[index] = lblStatus;
        }

        /// <summary>
        /// 更新单个通道的监控数据
        /// </summary>
        public void UpdateChannel(int channelIndex, ChannelRealTimeStatus chData)
        {
            if (chData == null || channelIndex < 0 || channelIndex >= 8) return;

            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => UpdateChannel(channelIndex, chData)));
                return;
            }

            bool isOnline = chData.IsOnline;
            string alarmText = GetAlarmText(chData.StatusBits);
            bool hasAlarm = !string.IsNullOrEmpty(alarmText);

            // 更新状态点
            _statusDots[channelIndex].ForeColor = isOnline ? Color.LimeGreen : Color.Gray;

            // 更新数值
            _voltageLabels[channelIndex].Text = isOnline ? $"{chData.RealVoltage:F2} V" : "-- V";
            _currentLabels[channelIndex].Text = isOnline ? $"{chData.RealCurrent:F2} A" : "-- A";
            _powerLabels[channelIndex].Text = isOnline ? $"{(chData.RealVoltage * chData.RealCurrent):F2} W" : "-- W";

            // 更新状态栏
            if (hasAlarm)
            {
                _statusLabels[channelIndex].Text = $"⚠️ {alarmText}";
                _statusLabels[channelIndex].BackColor = Color.MistyRose;
                _statusLabels[channelIndex].ForeColor = Color.Red;
            }
            else
            {
                _statusLabels[channelIndex].Text = isOnline ? "正常" : "离线";
                _statusLabels[channelIndex].BackColor = isOnline ? Color.LightGreen : Color.LightGray;
                _statusLabels[channelIndex].ForeColor = isOnline ? Color.DarkGreen : Color.DimGray;
            }
        }

        /// <summary>
        /// 更新顶部摘要信息
        /// </summary>
        public void UpdateSummary(int onlineCount, double totalPower, int alarmCount)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => UpdateSummary(onlineCount, totalPower, alarmCount)));
                return;
            }

            _lblSummary.Text = $"在线: {onlineCount}/8 | 总功率: {totalPower:F2}W | 通道告警: {alarmCount}";
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
