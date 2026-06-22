using System.Runtime.InteropServices;

namespace MathQuizLocker
{
    /// <summary>Reports how long the user has been idle (no keyboard/mouse input).</summary>
    public static class IdleHelper
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct LastInputInfo
        {
            public uint cbSize;
            public uint dwTime;
        }

        [DllImport("user32.dll")]
        private static extern bool GetLastInputInfo(ref LastInputInfo plii);

        public static TimeSpan GetIdleTime()
        {
            var lastIn = new LastInputInfo { cbSize = (uint)Marshal.SizeOf<LastInputInfo>() };

            if (!GetLastInputInfo(ref lastIn))
                return TimeSpan.Zero;

            uint idleTicks = unchecked((uint)Environment.TickCount - lastIn.dwTime);
            return TimeSpan.FromMilliseconds(idleTicks);
        }
    }
}
