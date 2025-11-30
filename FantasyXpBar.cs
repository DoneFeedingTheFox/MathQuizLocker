using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.ComponentModel;


namespace MathQuizLocker
{
	public class FantasyXpBar : Control
	{
		private int _value = 0;
		private int _maximum = 100;

		[Browsable(false)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public int Value
		{
			get => _value;
			set
			{
				int v = Math.Max(0, Math.Min(value, _maximum));
				if (v == _value) return;
				_value = v;
				Invalidate();
			}
		}

		[Browsable(false)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
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


		public FantasyXpBar()
		{
			DoubleBuffered = true;
			Height = 18;
			Width = 220;
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			base.OnPaint(e);

			var g = e.Graphics;
			g.SmoothingMode = SmoothingMode.AntiAlias;

			Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);

			// Outer border (golden)
			using (GraphicsPath borderPath = GetRoundedRect(rect, 8))
			using (Pen borderPen = new Pen(Color.FromArgb(200, 160, 110), 2f))
			{
				g.FillPath(new SolidBrush(Color.FromArgb(40, 0, 0, 0)), borderPath); // subtle outer dark
				g.DrawPath(borderPen, borderPath);
			}

			// Inner area
			Rectangle inner = new Rectangle(rect.X + 3, rect.Y + 3, rect.Width - 6, rect.Height - 6);

			// Background
			using (GraphicsPath bgPath = GetRoundedRect(inner, 6))
			using (LinearGradientBrush bgBrush = new LinearGradientBrush(
					   inner,
					   Color.FromArgb(40, 40, 40),
					   Color.FromArgb(20, 20, 20),
					   LinearGradientMode.Vertical))
			{
				g.FillPath(bgBrush, bgPath);
			}

			// Fill based on Value
			if (_maximum > 0 && _value > 0)
			{
				float pct = _value / (float)_maximum;
				int fillWidth = (int)(inner.Width * pct);
				if (fillWidth < 2) fillWidth = 2;

				Rectangle fillRect = new Rectangle(inner.X, inner.Y, fillWidth, inner.Height);

				using (GraphicsPath fillPath = GetRoundedRect(fillRect, 6))
				using (LinearGradientBrush fillBrush = new LinearGradientBrush(
						   fillRect,
						   Color.FromArgb(60, 200, 90),   // greenish
						   Color.FromArgb(230, 210, 120), // gold
						   LinearGradientMode.Horizontal))
				{
					g.FillPath(fillBrush, fillPath);
				}

				// subtle highlight
				Rectangle highlight = new Rectangle(fillRect.X, fillRect.Y, fillRect.Width, fillRect.Height / 2);
				using (GraphicsPath hlPath = GetRoundedRect(highlight, 6))
				using (LinearGradientBrush hlBrush = new LinearGradientBrush(
						   highlight,
						   Color.FromArgb(80, Color.White),
						   Color.FromArgb(0, Color.White),
						   LinearGradientMode.Vertical))
				{
					g.FillPath(hlBrush, hlPath);
				}
			}

			// XP text overlay
			string text = $"{_value}/{_maximum}";
			using (var font = new Font("Segoe UI", 8, FontStyle.Bold))
			using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
			using (var textBrush = new SolidBrush(Color.FromArgb(230, 230, 230)))
			{
				g.DrawString(text, font, textBrush, inner, sf);
			}
		}

		private GraphicsPath GetRoundedRect(Rectangle r, int radius)
		{
			var path = new GraphicsPath();
			int d = radius * 2;

			if (d > r.Width) d = r.Width;
			if (d > r.Height) d = r.Height;

			path.AddArc(r.X, r.Y, d, d, 180, 90);
			path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
			path.AddArc(r.X, r.Bottom - d, d, d, 0, 90);
			path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
			path.CloseFigure();

			return path;
		}
	}
}
