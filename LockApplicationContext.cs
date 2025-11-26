using System;
using System.Windows.Forms;
using Microsoft.Win32;

namespace MathQuizLocker
{
    public class LockApplicationContext : ApplicationContext
    {
        // Fully qualified type to avoid ambiguity
        private readonly System.Windows.Forms.Timer _timer;
        private bool _lockPendingFromIdle = false;
        private bool _quizOpen = false;
        private bool _resumeTriggered = false;
        private readonly AppSettings _settings;

        public LockApplicationContext()
        {
            _settings = AppSettings.Load();

            _timer = new System.Windows.Forms.Timer();
            _timer.Interval = 3000; // check every 3 seconds
            _timer.Tick += Timer_Tick;
            _timer.Start();

            SystemEvents.PowerModeChanged += SystemEvents_PowerModeChanged;

            if (_settings.ShowQuizOnStartup)
            {
                ShowQuiz();
            }
        }

        private void SystemEvents_PowerModeChanged(object? sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode == PowerModes.Resume && _settings.LockOnWakeFromSleep)
            {
                _resumeTriggered = true;
            }
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            // Handle wake from sleep
            if (_resumeTriggered && !_quizOpen)
            {
                _resumeTriggered = false;
                ShowQuiz();
                return;
            }

            // Handle inactivity
            if (_settings.IdleMinutesBeforeLock <= 0)
                return; // disabled

            var idle = IdleHelper.GetIdleTime();
            var threshold = TimeSpan.FromMinutes(_settings.IdleMinutesBeforeLock);

            if (idle > threshold && !_lockPendingFromIdle && !_quizOpen)
            {
                _lockPendingFromIdle = true;
            }
            else if (idle < TimeSpan.FromSeconds(5) && _lockPendingFromIdle && !_quizOpen)
            {
                _lockPendingFromIdle = false;
                ShowQuiz();
            }
        }

        private void ShowQuiz()
        {
            _quizOpen = true;

            using (var quiz = new QuizForm(_settings))
            {
                quiz.FormClosed += (s, e) =>
                {
                    _quizOpen = false;
                };

                quiz.ShowDialog();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _timer?.Dispose();
                SystemEvents.PowerModeChanged -= SystemEvents_PowerModeChanged;
            }
            base.Dispose(disposing);
        }
    }
}
