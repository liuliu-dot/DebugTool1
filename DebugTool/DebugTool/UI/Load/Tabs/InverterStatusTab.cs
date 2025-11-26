using System;
using System.Drawing;
using System.Windows.Forms;
using DebugTool.Models;

namespace DebugTool.UI.Load.Tabs
{
    /// <summary>
    /// 逆变器状态 Tab - 显示逆变器6个状态指标
    /// </summary>
    public class InverterStatusTab : UserControl
    {
        // 顶部摘要
        private Label _lblSummary;

        // 6个状态卡片
        private Panel _pnlOverTemp;
        private Panel _pnlAdFault;
        private Panel _pnlOutVolt;
        private Panel _pnlFan;
        private Panel _pnlTimeout;
        private Panel _pnlDcBus;

        // 对应的值标签
        private Label _lblOverTemp;
        private Label _lblAdFault;
        private Label _lblOutVolt;
        private Label _lblFan;
        private Label _lblTimeout;
        private Label _lblDcBus;

        public InverterStatusTab()
        {
            InitializeUI();
        }

        private void InitializeUI()
        {
            this.Dock = DockStyle.Fill;
            this.BackColor = SystemColors.Control;

            // 顶部摘要
            _lblSummary = new Label
            {
                Text = "逆变器状态: 未知",
                Dock = DockStyle.Top,
                Height = 40,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("微软雅黑", 12, FontStyle.Bold),
                BackColor = SystemColors.ControlLight
            };
            this.Controls.Add(_lblSummary);

            // 卡片容器
            FlowLayoutPanel flowPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(20),
                AutoScroll = true
            };

            // 创建6个状态卡片
            CreateStatusCard(flowPanel, "逆变过温", out _pnlOverTemp, out _lblOverTemp);
            CreateStatusCard(flowPanel, "AD采样故障", out _pnlAdFault, out _lblAdFault);
            CreateStatusCard(flowPanel, "输出电压", out _pnlOutVolt, out _lblOutVolt);
            CreateStatusCard(flowPanel, "风扇状态", out _pnlFan, out _lblFan);
            CreateStatusCard(flowPanel, "通信超时", out _pnlTimeout, out _lblTimeout);
            CreateStatusCard(flowPanel, "母线电压", out _pnlDcBus, out _lblDcBus);

            this.Controls.Add(flowPanel);
            _lblSummary.BringToFront();
        }

        private void CreateStatusCard(FlowLayoutPanel parent, string title, out Panel panel, out Label valueLabel)
        {
            panel = new Panel
            {
                Size = new Size(300, 100),
                BackColor = Color.White,
                Margin = new Padding(10),
                BorderStyle = BorderStyle.FixedSingle
            };

            Label lblTitle = new Label
            {
                Text = title,
                Location = new Point(10, 10),
                AutoSize = true,
                Font = new Font("微软雅黑", 10, FontStyle.Bold)
            };

            valueLabel = new Label
            {
                Text = "--",
                Dock = DockStyle.Bottom,
                Height = 50,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("微软雅黑", 14, FontStyle.Bold),
                ForeColor = Color.Gray
            };

            panel.Controls.Add(lblTitle);
            panel.Controls.Add(valueLabel);
            parent.Controls.Add(panel);
        }

        /// <summary>
        /// 更新逆变器状态显示
        /// </summary>
        public void UpdateStatus(InverterStatus inv)
        {
            if (inv == null) return;

            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => UpdateStatus(inv)));
                return;
            }

            // 更新总体状态
            bool hasFault = inv.HasFault();
            _lblSummary.Text = hasFault ? "逆变器状态: 异常" : "逆变器状态: 正常";
            _lblSummary.ForeColor = hasFault ? Color.Red : Color.Green;

            // 更新各状态卡片
            SetCardStatus(_pnlOverTemp, _lblOverTemp, inv.IsOverTemp, "温度正常", "过温保护");
            SetCardStatus(_pnlAdFault, _lblAdFault, inv.IsAdFault, "采样正常", "AD故障");
            SetCardStatus(_pnlFan, _lblFan, inv.IsFanFault, "风扇正常", "风扇故障");
            SetCardStatus(_pnlTimeout, _lblTimeout, inv.IsTimeout, "通信正常", "超时");

            // 输出电压状态（三态）
            string outVoltText = inv.OutputVoltageStatus == 1 ? "电压正常" :
                                 inv.OutputVoltageStatus == 0 ? "输出欠压" : "输出过压";
            SetCardStatus(_pnlOutVolt, _lblOutVolt, inv.OutputVoltageStatus != 1, "电压正常", outVoltText);

            // 母线电压状态（三态）
            string dcBusText = inv.DcBusVoltageStatus == 1 ? "电压正常" :
                               inv.DcBusVoltageStatus == 0 ? "母线欠压" : "母线过压";
            SetCardStatus(_pnlDcBus, _lblDcBus, inv.DcBusVoltageStatus != 1, "电压正常", dcBusText);
        }

        private void SetCardStatus(Panel panel, Label label, bool isError, string normalText, string errorText)
        {
            if (isError)
            {
                panel.BackColor = Color.MistyRose;
                label.Text = errorText;
                label.ForeColor = Color.Red;
            }
            else
            {
                panel.BackColor = Color.White;
                label.Text = normalText;
                label.ForeColor = Color.Green;
            }
        }
    }
}
