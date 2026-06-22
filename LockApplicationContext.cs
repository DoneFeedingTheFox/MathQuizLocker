using System;
using System.Windows.Forms;
using Microsoft.Win32;

namespace MathQuizLocker
{
    /// <summary>Runs in the background and shows the quiz when idle, on wake, or at startup.</summary>
    public class LockApplicationContext : ApplicationContext
    {
        private readonly System.Windows.Forms.Timer _timer;
        private readonly AppSettings _settings;
        private bool _quizOpen;
        private readonly PowerModeChangedEventHandler _powerModeHandler;

        public LockApplicationContext(AppSettings settings)
        {
            _settings = settings;

            _timer = new System.Windows.Forms.Timer { Interval = 3000 };
            _timer.Tick += (_, _) => CheckIdle();
            _timer.Start();

            _powerModeHandler = OnPowerModeChanged;
            SystemEvents.PowerModeChanged += _powerModeHandler;

            if (_settings.ShowQuizOnStartup)
                ShowQuiz();
        }

        private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode == PowerModes.Resume && _settings.LockOnWakeFromSleep && !_quizOpen)
                ShowQuiz();
        }

        private void CheckIdle()
        {
            if (_quizOpen)
                return;

            var idle = IdleHelper.GetIdleTime();
            if (idle > TimeSpan.FromMinutes(_settings.IdleMinutesBeforeLock))
                ShowQuiz();
        }

        private void ShowQuiz()
        {
            _quizOpen = true;
            try
            {
                using var quiz = new QuizForm(_settings);
                quiz.ShowDialog();
            }
            finally
            {
                _quizOpen = false;
            }
        }

        protected override void ExitThreadCore()
        {
            _timer.Stop();
            _timer.Dispose();
            SystemEvents.PowerModeChanged -= _powerModeHandler;
            base.ExitThreadCore();
        }
    }
}
