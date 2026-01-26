using System;
using System.Collections.Generic;
using System.Linq;

namespace MathQuizLocker
{
	/// <summary>XP and level progression: how much XP is needed for the next level and applying gained XP.</summary>
	public static class XpSystem
	{
		/// <summary>XP required to level up from currentLevel (formula: 100 + currentLevel * 50).</summary>
		public static int GetXpRequiredForNextLevel(int currentLevel)
		{
			return 100 + (currentLevel * 50);
		}

		/// <summary>Adds XP to both CurrentXp and TotalXp. No-op if amount &lt;= 0.</summary>
		public static void AddXp(PlayerProgress progress, int amount)
		{
			if (amount <= 0) return;

			progress.CurrentXp += amount;
			progress.TotalXp += amount;

		}
	}
}