using System;
using System.Threading;
using System.Windows.Forms;
using Velopack;
using MathQuizLocker.Services;
using Microsoft.Win32;

namespace MathQuizLocker
{
    internal static class Program
    {
     
        private static Mutex _mutex = new Mutex(true, @"Global\MathQuizLocker-Unique-ID-99");


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
                            // Setter banen til den kjørbare filen (.exe)
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
                System.Diagnostics.Debug.WriteLine("Kunne ikke sette autostart: " + ex.Message);
            }
#endif
        }

        [STAThread]
        static void Main()
        {
            VelopackApp.Build().Run();

            if (!_mutex.WaitOne(TimeSpan.Zero, true))
            {
                return; // Already running
            }

            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                // 1. Pre-load settings here instead of inside a context constructor
                var settings = AppSettings.Load();

                using (var updateForm = new UpdateForm())
                {
                    if (updateForm.ShowDialog() == DialogResult.OK)
                    {
                        // 2. FIX: Launch QuizForm directly. 
                        // This eliminates the 75.27% LockApplicationContext CPU hang.
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
                System.Diagnostics.Debug.WriteLine($"Startup Error: {ex.Message}");

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