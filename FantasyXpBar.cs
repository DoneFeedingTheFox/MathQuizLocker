using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MathQuizLocker
{
    public class FantasyXpBar : Control
    {
        private int _maximum = 100;
        private int _value = 0;

        [DefaultValue(100)]
        public int Maximum
        {
            get => _maximum;
            set
            {
                _maximum = Math.Max(1, value);
                if (_value > _maximum) _value = _maximum;
                Invalidate();
            }
        }

        [DefaultValue(0)]
        public int Value
        {
            get => _value;
            set
            {
                _value = Math.Max(0, Math.Min(_maximum, value));
                Invalidate();
            }
        }

        public FantasyXpBar()
        {
            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);

            BackColor = Color.Transparent;
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9f, FontStyle.Bold);

            Size = new Size(260, 22);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var outer = new Rectangle(0, 0, Width - 1, Height - 1);

            // safe inset so rounded edges never clip
            int inset = 2;
            var inner = Rectangle.Inflate(outer, -inset, -inset);

            int radius = Math.Max(6, inner.Height / 2);

            // Track + border colors
            Color trackFill = Color.FromArgb(40, 40, 40);
            Color trackBorder = Color.FromArgb(220, 220, 220);

            // Fill colors
            Color fillA = Color.FromArgb(120, 230, 140); // top
            Color fillB = Color.FromArgb(40, 180, 90);   // bottom

            // 1) Track
            using (var trackPath = CreateRoundRect(inner, radius))
            using (var trackBrush = new SolidBrush(trackFill))
            using (var borderPen = new Pen(trackBorder, 2f))
            {
                g.FillPath(trackBrush, trackPath);
                g.DrawPath(borderPen, trackPath);
            }

            // 2) Fill
            float pct = (_maximum <= 0) ? 0f : (float)_value / _maximum;
            pct = Math.Max(0f, Math.Min(1f, pct));

            // keep fill inside track border
            var fillRect = Rectangle.Inflate(inner, -2, -2);
            fillRect.Width = (int)Math.Round(fillRect.Width * pct);

            if (fillRect.Width > 0)
            {
                // Ensure fill still has a rounded cap when very small
                int minWidth = Math.Min(fillRect.Height, 16);
                if (fillRect.Width < minWidth) fillRect.Width = minWidth;

                using (var fillPath = CreateRoundRect(fillRect, fillRect.Height / 2))
                using (var fillBrush = new LinearGradientBrush(fillRect, fillA, fillB, LinearGradientMode.Vertical))
                {
                    g.FillPath(fillBrush, fillPath);
                }
            }

            // 3) Text — properly centered, DPI-safe
            string text = $"{_value}/{_maximum}";
            TextRenderer.DrawText(
                g,
                text,
                Font,
                inner,
                ForeColor,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding |
                TextFormatFlags.SingleLine
            );
        }

        private static GraphicsPath CreateRoundRect(Rectangle r, int radius)
        {
            var path = new GraphicsPath();

            if (radius <= 0)
            {
                path.AddRectangle(r);
                path.CloseFigure();
                return path;
            }

            int d = radius * 2;
            if (d > r.Width) d = r.Width;
            if (d > r.Height) d = r.Height;

            var arc = new Rectangle(r.Location, new Size(d, d));

            // TL
            path.AddArc(arc, 180, 90);
            // TR
            arc.X = r.Right - d;
            path.AddArc(arc, 270, 90);
            // BR
            arc.Y = r.Bottom - d;
            path.AddArc(arc, 0, 90);
            // BL
            arc.X = r.Left;
            path.AddArc(arc, 90, 90);

            path.CloseFigure();
            return path;
        }
    }
}
