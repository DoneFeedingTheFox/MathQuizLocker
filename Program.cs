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
			VelopackApp.Build().Run();

			// 2. SINGLE INSTANCE CHECK
			// If WaitOne returns false, another instance is already running.
			if (!_mutex.WaitOne(TimeSpan.Zero, true))
			{
				// Optionally show a message: MessageBox.Show("Game is already running!");
				return; // Exit the second instance immediately
			}

			try
			{
				ApplicationConfiguration.Initialize();

				// 3. START APPLICATION
				Application.Run(new LockApplicationContext());
			}
			finally
			{
				// Always release the mutex when the application closes
				_mutex.ReleaseMutex();
				_mutex.Dispose();
			}
		}
	}
}