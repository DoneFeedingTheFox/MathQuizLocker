using MathQuizLocker;
using System;
using System.Threading;
using System.Windows.Forms;
using Velopack;

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
            // This must run first to handle installation/uninstallation events
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
                // We show this as a dialog. It will block execution until it closes.
                using (var updateForm = new UpdateForm())
                {
                    // .ShowDialog() returns only when the update check is finished
                    // or if no update was found.
                    if (updateForm.ShowDialog() == DialogResult.OK)
                    {
                        // 4. START MAIN APPLICATION
                        // If we reach here, either the app is up to date or update failed/skipped.
                        Application.Run(new LockApplicationContext());
                    }
                    else
                    {
                        // If ShowDialog didn't return OK (e.g., user forced close
                        // or updater logic failed), we exit to be safe.
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
                if (_mutex != null)
                {
                    try { _mutex.ReleaseMutex(); } catch { }
                    _mutex.Dispose();
                }
            }
        }
    }
}
