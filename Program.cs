using System;
using System.Threading;
using System.Windows.Forms;
using Velopack;
using MathQuizLocker.Services;

namespace MathQuizLocker
{
    internal static class Program
    {
        // Mutex ensures only one instance of the locker runs at a time
        private static Mutex _mutex = new Mutex(true, @"Global\MathQuizLocker-Unique-ID-99");

        [STAThread]
        static void Main()
        {
            // 1. VELOPACK STARTUP HOOKS
            // Must run first to handle installation/uninstallation events
            VelopackApp.Build().Run();

            // 2. SINGLE INSTANCE CHECK
            if (!_mutex.WaitOne(TimeSpan.Zero, true))
            {
                return; // Already running
            }

            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                // 3. START UPDATER (Splash Screen)
                // This blocks execution until the update check is finished
                using (var updateForm = new UpdateForm())
                {
                    if (updateForm.ShowDialog() == DialogResult.OK)
                    {
                        // 4. START MAIN APPLICATION
                        // Runs the background context that monitors idle time
                        Application.Run(new LockApplicationContext());
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
                // Fallback: Try to run the game anyway if the updater crashes
                Application.Run(new LockApplicationContext());
            }
            finally
            {
                // Clean up resources
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