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
                return; // Allerede i gang
            }

            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

             
                using (var updateForm = new UpdateForm())
                {
                    if (updateForm.ShowDialog() == DialogResult.OK)
                    {
                      
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
               
                Application.Run(new LockApplicationContext());
            }
			finally
			{
				AssetCache.DisposeAll();

				if (_mutex != null)
				{
					try
					{
						// This is the important part: explicitly release before disposing
						_mutex.ReleaseMutex();
					}
					catch (Exception) { /* Already released or not owned */ }

					_mutex.Dispose();
					_mutex = null;
				}
			}
		}
    }
}