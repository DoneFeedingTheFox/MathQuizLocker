using MathQuizLocker;
using System;
using System.Windows.Forms;

namespace MathQuizLocker
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new LockApplicationContext());
        }
    }
}
