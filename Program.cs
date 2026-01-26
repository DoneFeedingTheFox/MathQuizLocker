using System;
using System.Threading;
using System.Windows.Forms;
using Velopack;
using MathQuizLocker.Services;
using Microsoft.Win32;

namespace MathQuizLocker
{
    /// <summary>Application entry point: single-instance guard, update check, then main quiz/lock form.</summary>
    internal static class Program
    {
        /// <summary>Ensures only one instance runs; shared name so it works across sessions.</summary>
        private static Mutex _mutex = new Mutex(true, @"Global\MathQuizLocker-Unique-ID-99");

        /// <summary>Adds or removes the app from Windows "Run at startup" (Current User Run key). Only in Release build.</summary>
        public static void SetExternalAutostart(bool enable)
        {
#if !DEBUG
            try
            {
                string runKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(runKey, true))
                {
                    if (key != null)
                    {
                        if (enable)
                        {
                            // Store path to the executable so Windows starts us at login
                            key.SetValue("MathQuizLocker", Application.ExecutablePath);
                        }
                        else
                        {
                            key.DeleteValue("MathQuizLocker", false);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warn("Could not set autostart: " + ex.Message);
            }
#endif
        }

        [STAThread]
        static void Main()
        {
            VelopackApp.Build().Run();

            // Single-instance: if another instance holds the mutex, exit immediately
            if (!_mutex.WaitOne(TimeSpan.Zero, true))
                return;

            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                var settings = AppSettings.Load();

                // UpdateForm checks for updates; on OK we proceed to the main game form
                using (var updateForm = new UpdateForm())
                {
                    if (updateForm.ShowDialog() == DialogResult.OK)
                    {
                        var quiz = new QuizForm(settings);
                        Application.Run(quiz);
                    }
                    else
                    {
                        Application.Exit();
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error("Startup error", ex);

                // Fallback launch if update check fails
                var settings = AppSettings.Load();
                Application.Run(new QuizForm(settings));
            }
            finally
            {
                AssetCache.DisposeAll();
                if (_mutex != null)
                {
                    try { _mutex.ReleaseMutex(); } catch { }
                    _mutex.Dispose();
                }
            }
        }
    }
}