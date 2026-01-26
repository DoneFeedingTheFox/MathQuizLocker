using System;
using System.Runtime.InteropServices;

namespace MathQuizLocker
{
    /// <summary>Uses Windows GetLastInputInfo to report how long the user has been idle (no keyboard/mouse).</summary>
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

        /// <summary>Returns the time since last user input. Safe across 32-bit tick wraparound (~49 days).</summary>
        public static TimeSpan GetIdleTime()
        {
            LASTINPUTINFO lastIn = new LASTINPUTINFO();
            lastIn.cbSize = (uint)Marshal.SizeOf(lastIn);

            if (!GetLastInputInfo(ref lastIn))
                return TimeSpan.Zero;

            // Use TickCount64 to avoid overflow after ~49 days; lastIn.dwTime is 32-bit.
            long now64 = Environment.TickCount64;
            long last64 = (long)(uint)lastIn.dwTime;
            long diffMs = now64 - last64;
            if (diffMs < 0)
                diffMs += 4294967296L; // 32-bit wraparound
            diffMs = Math.Clamp(diffMs, 0, 4294967296L);
            return TimeSpan.FromMilliseconds(diffMs);
        }
    }
}
