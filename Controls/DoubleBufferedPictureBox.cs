using System.Drawing;
using System.Windows.Forms;

namespace MathQuizLocker.Controls
{
    public class DoubleBufferedPictureBox : PictureBox
    {
        public DoubleBufferedPictureBox()
        {
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }

        // We leave OnPaint empty because the Form is drawing for us!
        protected override void OnPaint(PaintEventArgs pe) { }
    }
}