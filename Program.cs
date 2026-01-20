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
        // Mutex sikrer at kun én instans kjører av gangen
        private static Mutex _mutex = new Mutex(true, @"Global\MathQuizLocker-Unique-ID-99");

        /// <summary>
        /// Registrerer eller fjerner applikasjonen fra Windows autostart.
        /// Bruker #if !DEBUG for å hindre at dette skjer på utviklingsmaskinen.
        /// </summary>
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
            // 1. VELOPACK STARTUP HOOKS
            VelopackApp.Build().Run();

            // 2. SINGLE INSTANCE CHECK
            if (!_mutex.WaitOne(TimeSpan.Zero, true))
            {
                return; // Allerede i gang
            }

            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                // 3. START UPDATER (Splash Screen)
                using (var updateForm = new UpdateForm())
                {
                    if (updateForm.ShowDialog() == DialogResult.OK)
                    {
                        // 4. START MAIN APPLICATION
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
                // Fallback: Prøv å kjøre spillet uansett hvis oppdateringen krasjer
                Application.Run(new LockApplicationContext());
            }
            finally
            {
                // Rydd opp ressurser
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