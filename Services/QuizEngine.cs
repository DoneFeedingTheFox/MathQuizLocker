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
			var candidates = _settings.Progress.Values.ToList(); //

			// Create a weighted list: 
			// - Give wrong answers/low streaks more "tickets" in the lottery
			// - Give mastered answers (streak > 3) fewer "tickets"
			List<FactProgress> weightedPool = new List<FactProgress>();

			foreach (var p in candidates)
			{
				int weight = 10; // Base weight
				weight += (p.IncorrectCount * 5); // Add weight for mistakes
				weight -= (p.CurrentStreak * 2);  // Reduce weight for success

				if (weight < 1) weight = 1; // Minimum weight of 1

				for (int i = 0; i < weight; i++)
				{
					weightedPool.Add(p);
				}
			}

			// Pick from the weighted pool (excluding the last key if possible)
			var filteredPool = weightedPool.Where(p => $"{p.A}x{p.B}" != _lastFactKey).ToList();
			_currentFact = filteredPool.Count > 0
				? filteredPool[_rng.Next(filteredPool.Count)]
				: weightedPool[_rng.Next(weightedPool.Count)];

			_lastFactKey = $"{_currentFact.A}x{_currentFact.B}"; //
			return (_currentFact.A, _currentFact.B); //
		}

		public bool SubmitAnswer(int userAnswer)
		{
			if (_currentFact == null) return false;
			bool isCorrect = userAnswer == (_currentFact.A * _currentFact.B);

			if (isCorrect)
			{
				_currentFact.CorrectCount++;
				_currentFact.CurrentStreak++;
			}
			else
			{
				_currentFact.CurrentStreak = 0; // Mistakes reset the streak
			}

			_currentFact.LastAsked = DateTime.Now;

			// --- AUTOMATIC POOL EXPANSION ---
			var pool = _settings.Progress.Values.ToList();
			// Check how many unique facts have been "learned" (streak of 3+)
			int masteredCount = pool.Count(p => p.CurrentStreak >= 3);
			double masteryPercent = (double)masteredCount / pool.Count;

			// If 60% of the current pool is mastered, unlock the next number
			if (masteryPercent > 0.60 && _settings.MaxFactorUnlocked < 10)
			{
				_settings.MaxFactorUnlocked++;
				ExpandPool(); // Adds the 5s, 6s, etc.

				// Save immediately so progress isn't lost if the app closes
				AppSettings.Save(_settings);
			}

			return isCorrect;
		}
		private void ExpandPool()
		{
			int currentMax = _settings.MaxFactorUnlocked;

			// We want to add all combinations for the NEWLY unlocked number
			// Example: If currentMax is 5, add 5x1 through 5x10 AND 1x5 through 10x5
			for (int i = 1; i <= 10; i++)
			{
				// Add Row (5x1, 5x2...)
				string key1 = $"{currentMax}x{i}";
				if (!_settings.Progress.ContainsKey(key1))
					_settings.Progress[key1] = new FactProgress { A = currentMax, B = i };

				// Add Column (1x5, 2x5...)
				string key2 = $"{i}x{currentMax}";
				if (!_settings.Progress.ContainsKey(key2))
					_settings.Progress[key2] = new FactProgress { A = i, B = currentMax };
			}
		}

		public void InitializeForCurrentLevel()
        {
            // If the dictionary is empty, start with the base pool (1s and 2s)
            if (_settings.Progress.Count == 0)
            {
                _settings.MaxFactorUnlocked = 2; //
                ExpandPool();
            }
        }
    }
}