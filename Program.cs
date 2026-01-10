using MathQuizLocker;
using System;
using System.Windows.Forms;
using Velopack; // Ensure you have run: dotnet add package Velopack

namespace MathQuizLocker
{
	internal static class Program
	{
		[STAThread]
		static void Main()
		{
			// 1. Start the Velopack runner. 
			// This handles setup events (shortcuts, registry) and then exits if needed.
			VelopackApp.Build().Run();

			ApplicationConfiguration.Initialize();

			// 2. Start the application context
			Application.Run(new LockApplicationContext());
		}
	}
}