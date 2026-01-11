using MathQuizLocker;
using System;
using System.Threading;
using System.Windows.Forms;
using Velopack;

namespace MathQuizLocker
{
	internal static class Program
	{
		// A unique name for your application mutex. 
		// Using "Global\\" allows it to work across different user sessions.
		private static Mutex _mutex = new Mutex(true, @"Global\MathQuizLocker-Unique-ID-99");

		[STAThread]
		static void Main()
		{
			// 1. VELOPACK HOOKS
			// Handles setup events (shortcuts, registry) and exits if this is a setup run.
			// The Run() method here intercepts special CLI arguments sent by the installer.
			VelopackApp.Build()
				.Run();

			// 2. SINGLE INSTANCE CHECK
			// If WaitOne returns false, another instance is already running.
			if (!_mutex.WaitOne(TimeSpan.Zero, true))
			{
				// Prevent multiple locker screens from stacking on top of each other.
				return;
			}

			try
			{
				ApplicationConfiguration.Initialize();

				// 3. START APPLICATION
				// Ensure your LockApplicationContext handles the QuizForm creation.
				Application.Run(new LockApplicationContext());
			}
			catch (Exception ex)
			{
				// Log critical startup failures to the event log or a file
				System.Diagnostics.Debug.WriteLine($"Startup Error: {ex.Message}");
			}
			finally
			{
				// Always release the mutex when the application closes to allow it to restart.
				if (_mutex != null)
				{
					_mutex.ReleaseMutex();
					_mutex.Dispose();
				}
			}
		}
	}
}