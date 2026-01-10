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
		private string _lastFactKey = string.Empty; // Track last asked question

		private const int MaxRowFactor = 10;
		private const int MaxColFactor = 10;

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
		/// Unlocks facts in both directions (e.g., 1x5 AND 5x1)
		/// </summary>
		private void EnsureFactsForCurrentLevel()
		{
			int max = _settings.MaxFactorUnlocked;

			for (int a = 1; a <= MaxRowFactor; a++)
			{
				for (int b = 1; b <= MaxColFactor; b++)
				{
					// Logic: Unlock if EITHER factor is within the unlocked range
					if (a <= max || b <= max)
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
		}

		public (int a, int b) GetNextQuestion()
		{
			var fact = PickNextFact();
			_currentFact = fact;
			_lastFactKey = GetKey(fact.A, fact.B); // Remember this key
			return (fact.A, fact.B);
		}

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

		private bool ShouldUnlockNextLevel()
		{
			var allFacts = _settings.Progress.Values
				.Where(p => (p.A <= _settings.MaxFactorUnlocked || p.B <= _settings.MaxFactorUnlocked)
							&& p.A <= MaxRowFactor && p.B <= MaxColFactor)
				.ToList();

			if (allFacts.Count == 0) return false;

			const int requiredStreak = 3;
			const double requiredRatio = 0.6;

			int mastered = allFacts.Count(p =>
				p.CurrentStreak >= requiredStreak &&
				(p.CorrectCount + p.IncorrectCount) > 0
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

		private FactProgress PickNextFact()
		{
			var candidates = _settings.Progress.Values
				.Where(p => (p.A <= _settings.MaxFactorUnlocked || p.B <= _settings.MaxFactorUnlocked))
				.ToList();

			if (candidates.Count == 0)
				throw new InvalidOperationException("No facts available.");

			// Filter out the exact same question that was just asked
			var selectionPool = candidates.Count > 1
				? candidates.Where(p => GetKey(p.A, p.B) != _lastFactKey).ToList()
				: candidates;

			var weights = selectionPool.Select(GetPriorityWeight).ToList();
			double total = weights.Sum();

			double r = _rng.NextDouble() * total;
			double cumulative = 0.0;

			for (int i = 0; i < selectionPool.Count; i++)
			{
				cumulative += weights[i];
				if (r <= cumulative) return selectionPool[i];
			}

			return selectionPool[^1];
		}

		public void InitializeForCurrentLevel()
		{
			_settings.Progress.Clear();
			if (_settings.MaxFactorUnlocked < 1) _settings.MaxFactorUnlocked = 1;
			EnsureFactsForCurrentLevel();
		}

		private double GetPriorityWeight(FactProgress fact)
		{
			double weight = 1.0;

			// 1. Reduced boost for new facts to allow older facts back in the mix
			if (fact.CorrectCount == 0 && fact.IncorrectCount == 0)
				weight += 2.0;

			// 2. High priority for mistakes
			weight += fact.IncorrectCount * 4.0;

			// 3. Streak reduces priority (Mastery)
			weight -= fact.CurrentStreak * 1.5;

			// 4. Anti-Repetition: Significantly reduce weight of recently asked questions
			if (fact.LastAsked != DateTime.MinValue)
			{
				var secondsSince = (DateTime.Now - fact.LastAsked).TotalSeconds;
				if (secondsSince < 45) // Penalize if seen in the last 45 seconds
					weight *= 0.1;

				var minutesSince = (DateTime.Now - fact.LastAsked).TotalMinutes;
				weight += Math.Min(minutesSince, 10) * 0.5;
			}

			return Math.Max(weight, 0.1);
		}
	}
}