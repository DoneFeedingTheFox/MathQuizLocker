using System;
using System.Windows.Forms;
using Microsoft.Win32;
using MathQuizLocker.Services;

namespace MathQuizLocker
{
    public class LockApplicationContext : ApplicationContext
    {
        private readonly System.Windows.Forms.Timer _timer;
        private bool _quizOpen = false;
        private readonly AppSettings _settings;

        public LockApplicationContext()
        {
            _settings = AppSettings.Load(); //

            _timer = new System.Windows.Forms.Timer { Interval = 3000 };
            _timer.Tick += (s, e) => CheckIdle();
            _timer.Start();

            // The event subscription
            SystemEvents.PowerModeChanged += (s, e) =>
            {
                if (e.Mode == PowerModes.Resume && _settings.LockOnWakeFromSleep)
                {
                    // Only trigger if a quiz isn't already visible
                    if (!_quizOpen)
                    {
                        ShowQuiz();
                    }
                }
            };

            // Initial launch for debugging
            ShowQuiz();
        }

        private void CheckIdle()
        {
            if (_quizOpen) return;
            var idle = IdleHelper.GetIdleTime();
            if (idle > TimeSpan.FromMinutes(_settings.IdleMinutesBeforeLock)) ShowQuiz();
        }

        private void ShowQuiz()
        {
            _quizOpen = true;
            using (var quiz = new QuizForm(_settings))
            {
                quiz.ShowDialog();
                _quizOpen = false;
            }
        }
    }
}