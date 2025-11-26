using System;
using System.Runtime.InteropServices;

namespace MathQuizLocker
{
    public static class IdleHelper
    {
        [StructLayout(LayoutKind.Sequential)]
        struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        [DllImport("user32.dll")]
        static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        public static TimeSpan GetIdleTime()
        {
            LASTINPUTINFO lastIn = new LASTINPUTINFO();
            lastIn.cbSize = (uint)Marshal.SizeOf(lastIn);

            if (!GetLastInputInfo(ref lastIn))
                return TimeSpan.Zero;

            uint idleTicks = (uint)Environment.TickCount - lastIn.dwTime;
            return TimeSpan.FromMilliseconds(idleTicks);
        }
    }
}
