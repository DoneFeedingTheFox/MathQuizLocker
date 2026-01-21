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
            _settings = AppSettings.Load();

            // Hindre autostart-registrering når du kjører fra Visual Studio (Debug)
#if !DEBUG
    SetAutostart(true);
#endif

            SystemEvents.PowerModeChanged += (s, e) =>
            {
                if (e.Mode == PowerModes.Resume && _settings.LockOnWakeFromSleep)
                {
                    if (!_quizOpen) ShowQuiz();
                }
            };

            ShowQuiz();
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