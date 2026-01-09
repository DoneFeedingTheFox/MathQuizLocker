using System;
using System.Collections.Generic;
using System.Linq;

namespace MathQuizLocker
{
    public enum AbilityType
    {
        SecondChance,
        HintPower,
        MathSight,
        SkipStone,
        PowerStrike,
        Valor,
        XpBoost
    }

    public class AbilityState
    {
        public AbilityType Type { get; set; }
        public int Charges { get; set; }
    }

    public class PlayerProgress
    {
        public int Level { get; set; } = 1;
        public int CurrentXp { get; set; } = 0;
        public int TotalXp { get; set; } = 0;
        public int CorrectAnswersCount { get; set; } = 0; // Fixed the missing property
        public int CheatTokens { get; set; } = 0;

        public List<AbilityState> UnlockedAbilities { get; set; }
            = new List<AbilityState>();

        public int StreakDays { get; set; } = 0;
        public DateTime? LastQuizDate { get; set; }
    }

    public static class KnightProgression
    {
        public static int GetKnightStageIndex(int level)
        {
            if (level < 1) level = 1;
            if (level > 20) level = 20;

            int stage = (level - 1) / 2;
            return Math.Clamp(stage, 0, 9);
        }
    }

    public static class XpSystem
    {
        // Reduced XP for correct answers significantly to favor Monster Slaying rewards
        public const int XpPerCorrectAnswer = 2;

        /// <summary>
        /// Increased Base XP and scaling factor.
        /// Level 1 -> 2: 150 XP
        /// Level 2 -> 3: 200 XP
        /// </summary>
        public static int GetXpRequiredForNextLevel(int currentLevel)
        {
            return 100 + (currentLevel * 50);
        }

        public static bool AddXp(PlayerProgress progress, int amount)
        {
            if (amount <= 0) return false;

            progress.CurrentXp += amount;
            progress.TotalXp += amount;

            bool leveledUp = false;

            while (true)
            {
                int xpNeeded = GetXpRequiredForNextLevel(progress.Level);
                if (progress.CurrentXp < xpNeeded)
                    break;

                progress.CurrentXp -= xpNeeded;
                progress.Level++;
                leveledUp = true;

                RewardSystem.ApplyLevelUpRewards(progress);
            }

            return leveledUp;
        }
    }

    public static class RewardSystem
    {
        public static void ApplyLevelUpRewards(PlayerProgress progress)
        {
            switch (progress.Level)
            {
                case 3:
                    UnlockAbility(progress, AbilityType.SecondChance);
                    break;
                case 5:
                    UnlockAbility(progress, AbilityType.HintPower);
                    break;
                case 7:
                    UnlockAbility(progress, AbilityType.MathSight);
                    break;
                case 10: // Pushed rewards slightly further back
                    progress.CheatTokens += 1;
                    break;
                case 12:
                    UnlockAbility(progress, AbilityType.SkipStone);
                    break;
                case 14:
                    UnlockAbility(progress, AbilityType.XpBoost);
                    break;
                case 16:
                    UnlockAbility(progress, AbilityType.PowerStrike);
                    break;
                case 18:
                    progress.CheatTokens += 2;
                    break;
                case 20:
                    progress.CheatTokens += 3;
                    break;
            }
        }

        private static void UnlockAbility(PlayerProgress progress, AbilityType type)
        {
            if (!progress.UnlockedAbilities.Any(a => a.Type == type))
            {
                progress.UnlockedAbilities.Add(new AbilityState
                {
                    Type = type,
                    Charges = 0
                });
            }
        }

        public static bool HasAbility(PlayerProgress progress, AbilityType type)
        {
            return progress.UnlockedAbilities.Any(a => a.Type == type);
        }
    }
}