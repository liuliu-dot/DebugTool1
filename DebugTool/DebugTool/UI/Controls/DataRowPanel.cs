using System;
using System.Drawing;
using System.Windows.Forms;

namespace DebugTool.UI.Controls
{
    /// <summary>
    /// 数据行面板 - 用于横向表格布局（左侧标签 + 8个通道数据单元格）
    /// </summary>
    public class DataRowPanel : UserControl
    {
        private Label lblRowHeader;
        private Label[] lblChannelCells;

        public string RowTitle { get; set; }

        public DataRowPanel(string rowTitle)
        {
            this.RowTitle = rowTitle;
            // 手动调用初始化，因为我们没有 Designer 文件
            InitializeCustomComponent();
        }

        // 为了避免与系统生成的 InitializeComponent 冲突，我们改个名字
        private void InitializeCustomComponent()
        {
            // 1. 调整总宽度：从 1480 减小到 1100，防止出现横向滚动条
            this.Size = new Size(1100, 50);
            this.Margin = new Padding(0, 0, 0, 5);
            this.BackColor = Color.Transparent;
            this.Padding = new Padding(0);

            // 左侧行标题 (保持不变)
            lblRowHeader = new Label
            {
                Text = RowTitle,
                AutoSize = false,
                Font = new Font("微软雅黑", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(80, 80, 80),
                BackColor = Color.FromArgb(235, 235, 235),
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(120, 48),
                Location = new Point(0, 0)
            };

            // 8个通道数据单元格
            lblChannelCells = new Label[8];
            for (int i = 0; i < 8; i++)
            {
                // === 核心修改 ===
                // 原逻辑: 130 + i * 168 (太宽)
                // 新逻辑: 130 + i * 120 (更紧凑，刚好放入屏幕)
                int xPos = 130 + i * 120;

                lblChannelCells[i] = new Label
                {
                    Text = "--",
                    AutoSize = false,
                    Font = new Font("Consolas", 10F, FontStyle.Bold),
                    ForeColor = Color.FromArgb(60, 60, 60),
                    BackColor = Color.White,
                    TextAlign = ContentAlignment.MiddleCenter,
                    // === 核心修改 ===
                    // 宽度从 160 改为 115，留 5px 间距
                    Size = new Size(115, 48),
                    Location = new Point(xPos, 0),
                    BorderStyle = BorderStyle.None
                };

                // 添加圆角边框效果（通过Paint事件）
                int index = i;
                lblChannelCells[i].Paint += (s, e) =>
                {
                    using (Pen pen = new Pen(Color.FromArgb(200, 200, 200), 1))
                    {
                        e.Graphics.DrawRectangle(pen, 0, 0,
                            lblChannelCells[index].Width - 1,
                            lblChannelCells[index].Height - 1);
                    }
                };

                this.Controls.Add(lblChannelCells[i]);
            }

            this.Controls.Add(lblRowHeader);
        }

        /// <summary>
        /// 更新某个通道的数据
        /// </summary>
        public void UpdateChannelValue(int channelIndex, string value, Color textColor)
        {
            if (InvokeRequired)
            {
                this.BeginInvoke(new Action(() => UpdateChannelValue(channelIndex, value, textColor)));
                return;
            }

            if (channelIndex >= 0 && channelIndex < 8)
            {
                lblChannelCells[channelIndex].Text = value;
                lblChannelCells[channelIndex].ForeColor = textColor;
            }
        }

        /// <summary>
        /// 设置某个通道单元格的背景色
        /// </summary>
        public void SetChannelBackColor(int channelIndex, Color backColor)
        {
            if (channelIndex >= 0 && channelIndex < 8)
            {
                lblChannelCells[channelIndex].BackColor = backColor;
            }
        }
    }
}