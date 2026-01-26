using MathQuizLocker;

namespace MathQuizLocker.Services
{
    /// <summary>Generates multiplication questions and checks answers. Tracks current question for validation.</summary>
    public class QuizEngine
    {
        private readonly AppSettings _settings;
        private readonly Random _rng;
        private (int a, int b) _currentQuestion;

        /// <summary>Optional RNG for reproducible tests; if null, a new Random is used.</summary>
        public QuizEngine(AppSettings settings, Random? rng = null)
        {
            _settings = settings;
            _rng = rng ?? new Random();
        }

        /// <summary>Picks a new question: first factor from 1..MaxFactorUnlocked, second from 1..10.</summary>
        public (int a, int b) GetNextQuestion()
        {
            int limit = _settings.MaxFactorUnlocked;
            _currentQuestion = (_rng.Next(1, limit + 1), _rng.Next(1, 11));
            return _currentQuestion;
        }

        /// <summary>Returns true if the given answer matches the current question's product.</summary>
        public bool SubmitAnswer(int userAnswer)
        {
            return userAnswer == (_currentQuestion.a * _currentQuestion.b);
        }

        /// <summary>Unlocks the next row of the multiplication table (MaxFactorUnlocked, max 10) and saves.</summary>
        public void PromoteToNextLevel()
        {
            if (_settings.MaxFactorUnlocked < 10)
            {
                _settings.MaxFactorUnlocked++;
                AppSettings.Save(_settings);
            }
        }
    }
}
