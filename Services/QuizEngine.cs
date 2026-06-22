using MathQuizLocker;

namespace MathQuizLocker.Services
{
    /// <summary>Generates multiplication questions and checks answers. Tracks current question for validation.</summary>
    public class QuizEngine
    {
        private readonly AppSettings _settings;
        private readonly Random _rng;
        private (int a, int b) _currentQuestion;
        private (int a, int b) _lastQuestion;
        private bool _hasLastQuestion;

        /// <summary>Optional RNG for reproducible tests; if null, a new Random is used.</summary>
        public QuizEngine(AppSettings settings, Random? rng = null)
        {
            _settings = settings;
            _rng = rng ?? new Random();
        }

        /// <summary>Picks a new question: first factor from 1..MaxFactorUnlocked, second from 1..10. Avoids repeating the previous fact (including commutative pairs).</summary>
        public (int a, int b) GetNextQuestion()
        {
            int limit = Math.Clamp(_settings.MaxFactorUnlocked, 1, 10);

            (int a, int b) next;
            int attempts = 0;
            do
            {
                next = (_rng.Next(1, limit + 1), _rng.Next(1, 11));
                attempts++;
            }
            while (_hasLastQuestion && IsSameFact(next, _lastQuestion) && attempts < 50);

            _lastQuestion = next;
            _hasLastQuestion = true;
            _currentQuestion = next;
            return _currentQuestion;
        }

        private static bool IsSameFact((int a, int b) left, (int a, int b) right) =>
            (left.a == right.a && left.b == right.b) || (left.a == right.b && left.b == right.a);

        /// <summary>Returns true if the given answer matches the current question's product.</summary>
        public bool SubmitAnswer(int userAnswer)
        {
            return userAnswer == (_currentQuestion.a * _currentQuestion.b);
        }

        /// <summary>Unlocks the next row of the multiplication table (MaxFactorUnlocked, max 10) and saves.</summary>
        public void PromoteToNextLevel()
        {
            int expectedUnlockForLevel = Math.Clamp(_settings.PlayerProgress.Level + 1, 1, 10);

            if (_settings.MaxFactorUnlocked < expectedUnlockForLevel)
            {
                _settings.MaxFactorUnlocked = expectedUnlockForLevel;
            }
        }
    }
}
