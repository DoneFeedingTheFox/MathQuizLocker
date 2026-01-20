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

        public (int a, int b) GetNextQuestion(bool isBoss = false)
        {
            var allCandidates = _settings.Progress.Values.ToList();
            List<FactProgress> filteredCandidates;

            if (isBoss)
            {
             
                filteredCandidates = allCandidates;
            }
            else
            {
                
                filteredCandidates = allCandidates
                    .Where(p => p.A == _settings.MaxFactorUnlocked || p.B == _settings.MaxFactorUnlocked)
                    .ToList();
            }


            List<FactProgress> weightedPool = new List<FactProgress>();
            foreach (var p in filteredCandidates)
            {
                int weight = 10;
                weight += (p.IncorrectCount * 5);
                weight -= (p.CurrentStreak * 2);
                if (weight < 1) weight = 1;

                for (int i = 0; i < weight; i++) weightedPool.Add(p);
            }

            var finalSelection = weightedPool.Where(p => $"{p.A}x{p.B}" != _lastFactKey).ToList();
            _currentFact = finalSelection.Count > 0
                ? finalSelection[_rng.Next(finalSelection.Count)]
                : weightedPool[_rng.Next(weightedPool.Count)];

            _lastFactKey = $"{_currentFact.A}x{_currentFact.B}";
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