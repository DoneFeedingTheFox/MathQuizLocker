using System.Drawing;
using System.Windows.Forms;

namespace MathQuizLocker
{
	internal sealed class DoubleBufferedPictureBox : PictureBox
	{
		public DoubleBufferedPictureBox()
		{
			SetStyle(ControlStyles.OptimizedDoubleBuffer |
					 ControlStyles.AllPaintingInWmPaint |
					 ControlStyles.UserPaint | // We handle painting
					 ControlStyles.SupportsTransparentBackColor, true);
			BackColor = Color.Transparent;
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			// Do NOT call base.OnPaint(e). 
			// This stops the control from drawing itself over the Form's OnPaint.
		}
	}
}
