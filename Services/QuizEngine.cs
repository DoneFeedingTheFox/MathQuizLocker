using System;
using System.Collections.Generic;
using System.Linq;
using MathQuizLocker.Models;

namespace MathQuizLocker.Services
{
    public class QuizEngine
    {
        private readonly AppSettings _settings;
        private readonly Random _rng = new Random();
        private FactProgress? _currentFact;
        private string _lastFactKey = string.Empty;

        public QuizEngine(AppSettings settings)
        {
            _settings = settings;
            InitializeForCurrentLevel();
        }

        public (int a, int b) GetNextQuestion()
        {
            var candidates = _settings.Progress.Values
                .Where(p => p.A <= _settings.MaxFactorUnlocked || p.B <= _settings.MaxFactorUnlocked).ToList();

            _currentFact = candidates.Count > 1
                ? candidates.Where(p => $"{p.A}x{p.B}" != _lastFactKey).ToList()[_rng.Next(candidates.Count - 1)]
                : candidates[0];

            _lastFactKey = $"{_currentFact.A}x{_currentFact.B}";
            return (_currentFact.A, _currentFact.B);
        }

        public bool SubmitAnswer(int userAnswer)
        {
            if (_currentFact == null) return false;
            bool isCorrect = userAnswer == (_currentFact.A * _currentFact.B);

            if (isCorrect) { _currentFact.CorrectCount++; _currentFact.CurrentStreak++; }
            else { _currentFact.IncorrectCount++; _currentFact.CurrentStreak = 0; }

            _currentFact.LastAsked = DateTime.Now;
            return isCorrect;
        }

        public void InitializeForCurrentLevel()
        {
            for (int a = 1; a <= 10; a++)
                for (int b = 1; b <= 10; b++)
                    if ((a <= _settings.MaxFactorUnlocked || b <= _settings.MaxFactorUnlocked) && !_settings.Progress.ContainsKey($"{a}x{b}"))
                        _settings.Progress[$"{a}x{b}"] = new FactProgress { A = a, B = b };
        }
    }
}