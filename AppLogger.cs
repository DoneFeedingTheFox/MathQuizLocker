using System.Diagnostics;

namespace MathQuizLocker
{
    /// <summary>
    /// Simple logger for errors and diagnostics. Writes to Debug and optionally to a log file.
    /// </summary>
    internal static class AppLogger
    {
        private static readonly string LogFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MathQuizLocker");
        private static readonly string LogPath = Path.Combine(LogFolder, "mathquizlocker.log");
        private static readonly object _lock = new object();

        /// <summary>Log an error with optional exception; appears in Debug output and mathquizlocker.log.</summary>
        public static void Error(string message, Exception? ex = null)
        {
            string line = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] ERROR: {message}";
            if (ex != null)
                line += $" | {ex.GetType().Name}: {ex.Message}";

            Debug.WriteLine(line);
            TryWriteToFile(line);
        }

        /// <summary>Log a warning; appears in Debug output and mathquizlocker.log.</summary>
        public static void Warn(string message)
        {
            string line = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] WARN: {message}";
            Debug.WriteLine(line);
            TryWriteToFile(line);
        }

        private static void TryWriteToFile(string line)
        {
            try
            {
                lock (_lock)
                {
                    if (!Directory.Exists(LogFolder))
                        Directory.CreateDirectory(LogFolder);
                    File.AppendAllText(LogPath, line + Environment.NewLine);
                }
            }
            catch
            {
                // Best-effort logging only
            }
        }
    }
}
