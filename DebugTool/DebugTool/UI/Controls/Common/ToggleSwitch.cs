using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DebugTool.UI.Controls.Common // <--- 修正命名空间，非常重要！
{
    /// <summary>
    /// A modern, animated toggle switch control for Windows Forms.
    /// </summary>
    [DefaultEvent("CheckedChanged")]
    public class ToggleSwitch : Control
    {
        private bool _checked = false;
        private Color _onBackColor = Color.FromArgb(76, 175, 80);
        private Color _onToggleColor = Color.WhiteSmoke;
        private Color _offBackColor = Color.Gray;
        private Color _offToggleColor = Color.Gainsboro;
        private bool _solidStyle = true;
        private int _animationSpeed = 10; // Milliseconds for each animation step
        private float _animationProgress = 0; // 0 for off, 100 for on
        private Timer _animationTimer;

        public event EventHandler CheckedChanged;

        [DefaultValue(false)]
        public bool Checked
        {
            get => _checked;
            set
            {
                if (_checked != value)
                {
                    _checked = value;
                    StartAnimation();
                    CheckedChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        [Category("Appearance")]
        public Color OnBackColor
        {
            get => _onBackColor;
            set { _onBackColor = value; Invalidate(); }
        }

        [Category("Appearance")]
        public Color OnToggleColor
        {
            get => _onToggleColor;
            set { _onToggleColor = value; Invalidate(); }
        }

        [Category("Appearance")]
        public Color OffBackColor
        {
            get => _offBackColor;
            set { _offBackColor = value; Invalidate(); }
        }

        [Category("Appearance")]
        public Color OffToggleColor
        {
            get => _offToggleColor;
            set { _offToggleColor = value; Invalidate(); }
        }

        [DefaultValue(true)]
        public bool SolidStyle
        {
            get => _solidStyle;
            set { _solidStyle = value; Invalidate(); }
        }

        public override string Text { get; set; }

        public ToggleSwitch()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            MinimumSize = new Size(45, 22);
            _animationTimer = new Timer { Interval = 1 };
            _animationTimer.Tick += (sender, args) =>
            {
                float target = _checked ? 100 : 0;
                if (_animationProgress != target)
                {
                    float step = (100f / (_animationSpeed / (float)_animationTimer.Interval));
                    if (_checked)
                    {
                        _animationProgress += step;
                        if (_animationProgress > 100) _animationProgress = 100;
                    }
                    else
                    {
                        _animationProgress -= step;
                        if (_animationProgress < 0) _animationProgress = 0;
                    }
                    Invalidate();
                }
                else
                {
                    _animationTimer.Stop();
                }
            };
            _animationProgress = _checked ? 100 : 0;
        }

        private void StartAnimation()
        {
            _animationTimer.Start();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            float toggleSize = Height - 4;
            RectangleF rect = new RectangleF(0, 0, Width, Height);

            float progress = _animationProgress / 100f;

            Color currentBackColor = InterpolateColor(_offBackColor, _onBackColor, progress);
            Color currentToggleColor = InterpolateColor(_offToggleColor, _onToggleColor, progress);

            // Draw background
            using (var path = GetFigurePath(rect, rect.Height / 2))
            using (var brush = new SolidBrush(currentBackColor))
            {
                e.Graphics.FillPath(brush, path);
            }

            // Draw toggle
            float toggleX = 2 + (_animationProgress / 100f) * (Width - toggleSize - 4);
            RectangleF toggleRect = new RectangleF(toggleX, 2, toggleSize, toggleSize);

            using (var path = GetFigurePath(toggleRect, toggleRect.Height / 2))
            using (var brush = new SolidBrush(currentToggleColor))
            {
                e.Graphics.FillPath(brush, path);
            }
        }

        private static GraphicsPath GetFigurePath(RectangleF rect, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            float diameter = radius * 2;
            if (diameter > rect.Width) diameter = rect.Width;
            if (diameter > rect.Height) diameter = rect.Height;

            path.StartFigure();
            path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        private Color InterpolateColor(Color color1, Color color2, float fraction)
        {
            byte r = (byte)(color1.R + (color2.R - color1.R) * fraction);
            byte g = (byte)(color1.G + (color2.G - color1.G) * fraction);
            byte b = (byte)(color1.B + (color2.B - color1.B) * fraction);
            return Color.FromArgb(r, g, b);
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            Checked = !Checked;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _animationTimer?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}