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

		// To prevent immediate repetition
		private int _lastA = -1;
		private int _lastB = -1;

		public QuizEngine(AppSettings settings)
        {
            _settings = settings;
            InitializeForCurrentLevel();
        }

		public (int a, int b) GetNextQuestion(bool isBoss = false)
		{
			var allCandidates = _settings.Progress.Values.ToList();
			int currentLevel = _settings.MaxFactorUnlocked;

			List<FactProgress> filteredCandidates;

			if (isBoss)
			{
				// BOSS: Should ask ANY question currently in the unlocked pool.
				// If you are Level 2, the pool contains all 1x and 2x tables (up to 10).
				filteredCandidates = allCandidates;
			}
			else
			{
				// NORMAL: Strictly the current table (e.g., only 2x questions at Level 2)
				filteredCandidates = allCandidates
					.Where(p => p.A == currentLevel || p.B == currentLevel)
					.ToList();
			}

			// --- Anti-Repeat Logic ---
			var poolWithoutRepeat = filteredCandidates
				.Where(p => p.A != _lastA || p.B != _lastB)
				.ToList();

			var sourcePool = poolWithoutRepeat.Count > 0 ? poolWithoutRepeat : filteredCandidates;

			_currentFact = sourcePool[_rng.Next(sourcePool.Count)];
			_lastA = _currentFact.A;
			_lastB = _currentFact.B;

			return (_currentFact.A, _currentFact.B);
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
                _currentFact.CurrentStreak = 0;
                _currentFact.IncorrectCount++; 
            }

            _currentFact.LastAsked = DateTime.Now;

            
            AppSettings.Save(_settings);

            return isCorrect;
        }

        public void PromoteToNextLevel()
        {
            // Denne kalles kun fra QuizForm når bossen er beseiret
            if (_settings.MaxFactorUnlocked < 10)
            {
                _settings.MaxFactorUnlocked++;
                ExpandPool();
                AppSettings.Save(_settings);
            }
        }

		private void ExpandPool()
		{
			AddFactorToPool(_settings.MaxFactorUnlocked);
		}

		public void InitializeForCurrentLevel()
		{
			// If the dictionary is empty, we need to build the initial pool
			if (_settings.Progress.Count == 0)
			{
				// If the game starts at Level 2, we need to add Level 1 AND Level 2
				int targetFactor = _settings.MaxFactorUnlocked > 0 ? _settings.MaxFactorUnlocked : 2;

				// Loop through every number from 1 to the target level
				for (int i = 1; i <= targetFactor; i++)
				{
					AddFactorToPool(i);
				}

				_settings.MaxFactorUnlocked = targetFactor;
				AppSettings.Save(_settings);
			}
		}

		// Helper method to make the code cleaner
		private void AddFactorToPool(int factor)
		{
			for (int i = 1; i <= 10; i++)
			{
				// Add 2x1, 2x2...
				string key1 = $"{factor}x{i}";
				if (!_settings.Progress.ContainsKey(key1))
					_settings.Progress[key1] = new FactProgress { A = factor, B = i };

				// Add 1x2, 2x2...
				string key2 = $"{i}x{factor}";
				if (!_settings.Progress.ContainsKey(key2))
					_settings.Progress[key2] = new FactProgress { A = i, B = factor };
			}
		}
	}
}