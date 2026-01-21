using System;
using System.Collections.Generic;
using System.Linq;

namespace MathQuizLocker
{

	public static class XpSystem
	{
		public static int GetXpRequiredForNextLevel(int currentLevel)
		{
			return 100 + (currentLevel * 50);
		}

		public static void AddXp(PlayerProgress progress, int amount)
		{
			if (amount <= 0) return;

			progress.CurrentXp += amount;
			progress.TotalXp += amount;

		}
	}
}