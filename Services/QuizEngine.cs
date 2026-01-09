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

        // We want the full "lille gangetabell"
        private const int MaxRowFactor = 10; // rows: A = 1..10
        private const int MaxColFactor = 10; // cols: B = 1..10

        public QuizEngine(AppSettings settings)
        {
            _settings = settings;
            if (_settings.MaxFactorUnlocked < 1)
                _settings.MaxFactorUnlocked = 1;
            if (_settings.MaxFactorUnlocked > MaxRowFactor)
                _settings.MaxFactorUnlocked = MaxRowFactor;

            EnsureFactsForCurrentLevel();
        }

        private static string GetKey(int a, int b) => $"{a}x{b}";

        /// <summary>
        /// Make sure Progress contains entries for all unlocked facts.
        /// Unlocks by row: 1×1..1×10, then 2×1..2×10, etc.
        /// </summary>
        private void EnsureFactsForCurrentLevel()
        {
            for (int a = 1; a <= _settings.MaxFactorUnlocked && a <= MaxRowFactor; a++)
            {
                for (int b = 1; b <= MaxColFactor; b++)
                {
                    string key = GetKey(a, b);
                    if (!_settings.Progress.ContainsKey(key))
                    {
                        _settings.Progress[key] = new FactProgress
                        {
                            A = a,
                            B = b,
                            CorrectCount = 0,
                            IncorrectCount = 0,
                            CurrentStreak = 0,
                            LastAsked = DateTime.MinValue
                        };
                    }
                }
            }
        }

        /// <summary>
        /// Returns the next question (a, b).
        /// </summary>
        public (int a, int b) GetNextQuestion()
        {
            var fact = PickNextFact();
            _currentFact = fact;
            return (fact.A, fact.B);
        }

        /// <summary>
        /// Submit an answer for the current question. Returns true if correct.
        /// </summary>
        public bool SubmitAnswer(int userAnswer)
        {
            if (_currentFact == null)
                throw new InvalidOperationException("No active question has been generated.");

            bool isCorrect = userAnswer == _currentFact.A * _currentFact.B;

            RegisterAnswer(_currentFact, isCorrect);
            MaybeUnlockNextLevel();

            return isCorrect;
        }

        private void RegisterAnswer(FactProgress fact, bool isCorrect)
        {
            if (isCorrect)
            {
                fact.CorrectCount++;
                fact.CurrentStreak++;
            }
            else
            {
                fact.IncorrectCount++;
                fact.CurrentStreak = 0;
            }

            fact.LastAsked = DateTime.Now;
        }

		/// <summary>
		/// Unlock next row when user has mastered enough of current rows.
		/// For example:
		///  - Start: MaxFactorUnlocked = 1 -> 1×1..1×10
		///  - After mastery: MaxFactorUnlocked = 2 -> 1×1..2×10
		/// </summary>
		private bool ShouldUnlockNextLevel()
		{
			var allFacts = _settings.Progress.Values
				.Where(p => p.A <= _settings.MaxFactorUnlocked && p.A <= MaxRowFactor && p.B <= MaxColFactor)
				.ToList();

			if (allFacts.Count == 0)
				return false;

			const int requiredStreak = 3;
			const double requiredRatio = 0.6; // 60% of facts in current level

			int mastered = allFacts.Count(p =>
				p.CurrentStreak >= requiredStreak &&
				(p.CorrectCount + p.IncorrectCount) > 0  // has been asked at least once
			);

			double ratio = (double)mastered / allFacts.Count;

			return ratio >= requiredRatio && _settings.MaxFactorUnlocked < MaxRowFactor;
		}


		private void MaybeUnlockNextLevel()
        {
            if (ShouldUnlockNextLevel())
            {
                _settings.MaxFactorUnlocked++;
                EnsureFactsForCurrentLevel();
            }
        }

        /// <summary>
        /// Weighted random selection of the next fact.
        /// Only uses unlocked rows (A <= MaxFactorUnlocked) and full B = 1..10.
        /// </summary>
        private FactProgress PickNextFact()
        {
            var candidates = _settings.Progress.Values
                .Where(p => p.A <= _settings.MaxFactorUnlocked && p.A <= MaxRowFactor && p.B <= MaxColFactor)
                .ToList();

            if (candidates.Count == 0)
                throw new InvalidOperationException("No facts available. Did you call EnsureFactsForCurrentLevel?");

            var weights = candidates.Select(GetPriorityWeight).ToList();
            double total = weights.Sum();

            double r = _rng.NextDouble() * total;
            double cumulative = 0.0;

            for (int i = 0; i < candidates.Count; i++)
            {
                cumulative += weights[i];
                if (r <= cumulative)
                {
                    return candidates[i];
                }
            }

            // Fallback
            return candidates[^1];
        }

        public void InitializeForCurrentLevel()
        {
            // Optional: Clear existing progress to ensure a truly fresh start
            _settings.Progress.Clear();

            // Ensure settings are within valid bounds
            if (_settings.MaxFactorUnlocked < 1) _settings.MaxFactorUnlocked = 1;

            // Repopulate with Level 1 facts
            EnsureFactsForCurrentLevel();
        }

        /// <summary>
        /// Higher weight = more likely to be asked.
        /// New or wrong facts are prioritized; mastered facts appear less often.
        /// </summary>
        private double GetPriorityWeight(FactProgress fact)
        {
            double weight = 1.0;

            bool neverSeen = fact.CorrectCount == 0 && fact.IncorrectCount == 0;
            if (neverSeen)
            {
                weight += 5.0; // new facts are very attractive
            }

            // Errors count a lot
            weight += fact.IncorrectCount * 3.0;

            // Correct streak reduces weight
            int cappedStreak = Math.Min(fact.CurrentStreak, 5);
            weight -= cappedStreak * 0.8;

            // Recency: not asked for a while => small boost
            if (fact.LastAsked != DateTime.MinValue)
            {
                var daysSince = (DateTime.Now - fact.LastAsked).TotalDays;
                weight += Math.Min(daysSince, 5) * 0.3;
            }

            if (weight < 0.2)
                weight = 0.2;

            return weight;
        }
    }
}
