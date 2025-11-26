using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DebugTool.UI.Controls.Common  // <--- 修正命名空间，非常重要！
{
    /// <summary>
    /// 状态指示灯控件
    /// 绿色 = 开启/正常
    /// 灰色 = 关闭/未激活
    /// 红色 = 异常/错误
    /// </summary>
    public class StatusIndicator : Control
    {
        private IndicatorState _state = IndicatorState.Off;
        private string _label = "";

        public enum IndicatorState
        {
            Off,        // 灰色 - 关闭/未激活
            On,         // 绿色 - 开启/正常
            Error       // 红色 - 异常/错误
        }

        public StatusIndicator()
        {
            this.SetStyle(ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.UserPaint |
                         ControlStyles.OptimizedDoubleBuffer, true);
            this.Size = new Size(120, 24);
            this.Font = new Font("微软雅黑", 9F);
        }

        /// <summary>
        /// 指示灯状态
        /// </summary>
        public IndicatorState State
        {
            get => _state;
            set
            {
                if (_state != value)
                {
                    _state = value;
                    this.Invalidate();
                }
            }
        }

        /// <summary>
        /// 指示灯标签文本
        /// </summary>
        public string Label
        {
            get => _label;
            set
            {
                if (_label != value)
                {
                    _label = value;
                    this.Invalidate();
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // 指示灯圆形大小和位置
            int ledSize = Math.Min(this.Height - 4, 18);
            int ledY = (this.Height - ledSize) / 2;
            Rectangle ledRect = new Rectangle(2, ledY, ledSize, ledSize);

            // 根据状态选择颜色
            Color ledColor;
            Color ledBorder;
            switch (_state)
            {
                case IndicatorState.On:
                    ledColor = Color.FromArgb(0, 220, 0);      // 亮绿色
                    ledBorder = Color.FromArgb(0, 150, 0);     // 深绿色边框
                    break;
                case IndicatorState.Error:
                    ledColor = Color.FromArgb(255, 50, 50);    // 亮红色
                    ledBorder = Color.FromArgb(180, 0, 0);     // 深红色边框
                    break;
                case IndicatorState.Off:
                default:
                    ledColor = Color.FromArgb(160, 160, 160);  // 灰色
                    ledBorder = Color.FromArgb(100, 100, 100); // 深灰色边框
                    break;
            }

            // 绘制发光效果（仅在On或Error状态）
            if (_state != IndicatorState.Off)
            {
                using (GraphicsPath path = new GraphicsPath())
                {
                    path.AddEllipse(ledRect);
                    using (PathGradientBrush brush = new PathGradientBrush(path))
                    {
                        brush.CenterColor = ledColor;
                        brush.SurroundColors = new Color[] { Color.FromArgb(50, ledColor) };
                        g.FillEllipse(brush, ledRect);
                    }
                }
            }
            else
            {
                // 关闭状态使用纯色填充
                using (SolidBrush brush = new SolidBrush(ledColor))
                {
                    g.FillEllipse(brush, ledRect);
                }
            }

            // 绘制边框
            using (Pen pen = new Pen(ledBorder, 1.5f))
            {
                g.DrawEllipse(pen, ledRect);
            }

            // 绘制高光效果
            if (_state != IndicatorState.Off)
            {
                int highlightSize = ledSize / 3;
                Rectangle highlightRect = new Rectangle(
                    ledRect.X + ledSize / 4,
                    ledRect.Y + ledSize / 6,
                    highlightSize,
                    highlightSize);

                using (GraphicsPath path = new GraphicsPath())
                {
                    path.AddEllipse(highlightRect);
                    using (PathGradientBrush brush = new PathGradientBrush(path))
                    {
                        brush.CenterColor = Color.FromArgb(200, Color.White);
                        brush.SurroundColors = new Color[] { Color.FromArgb(0, Color.White) };
                        g.FillEllipse(brush, highlightRect);
                    }
                }
            }

            // 绘制标签文本
            if (!string.IsNullOrEmpty(_label))
            {
                Rectangle textRect = new Rectangle(
                    ledRect.Right + 6,
                    0,
                    this.Width - ledRect.Right - 8,
                    this.Height);

                TextFormatFlags flags = TextFormatFlags.Left |
                                       TextFormatFlags.VerticalCenter |
                                       TextFormatFlags.NoPrefix;

                TextRenderer.DrawText(g, _label, this.Font, textRect, this.ForeColor, flags);
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            this.Invalidate();
        }
    }
}