using System;
using System.Collections.Generic;
using System.Linq;

namespace MathQuizLocker
{
    // All the different powers your knight can unlock
    public enum AbilityType
    {
        SecondChance,   // Retry one wrong question
        HintPower,      // Show first digit of the answer (our new hint ability)
        MathSight,      // (You can use this later for something else if you want)
        SkipStone,      // Skip a question
        PowerStrike,    // One answer counts as 2
        Valor,          // Auto-solve last question if conditions are met
        XpBoost         // Temporary XP boost
    }

    // State for abilities that may have limited uses / charges
    public class AbilityState
    {
        public AbilityType Type { get; set; }
        public int Charges { get; set; } // For token-like abilities, 0 for passive
    }

    // Main player progression state, this will be saved in settings
    public class PlayerProgress
    {
        public int Level { get; set; } = 1;
        public int CurrentXp { get; set; } = 0;   // XP towards next level
        public int TotalXp { get; set; } = 0;     // Optional: for lifetime stats

        public int CheatTokens { get; set; } = 0; // Generic “cheat points”

        // All abilities the player has unlocked
        public List<AbilityState> UnlockedAbilities { get; set; }
            = new List<AbilityState>();

        // Optional streak system (can be used later)
        public int StreakDays { get; set; } = 0;
        public DateTime? LastQuizDate { get; set; }
    }

    /// <summary>
    /// Maps level → knight sprite index.
    /// 10 images, levels 1–20.
    /// Stage 0 = level 1–2, stage 1 = 3–4, ... stage 9 = 19–20.
    /// </summary>
    public static class KnightProgression
    {
        public static int GetKnightStageIndex(int level)
        {
            if (level < 1) level = 1;
            if (level > 20) level = 20;

            int stage = (level - 1) / 2; // integer division
            return Math.Clamp(stage, 0, 9);
        }
    }

    /// <summary>
    /// Handles XP gain and level-up logic.
    /// </summary>
    public static class XpSystem
    {
        public const int XpPerCorrectAnswer = 10;

        /// <summary>
        /// XP needed to go from currentLevel → currentLevel+1.
        /// Formula: 50 + N * 10
        /// </summary>
        public static int GetXpRequiredForNextLevel(int currentLevel)
        {
            return 50 + currentLevel * 10;
        }

        /// <summary>
        /// Add XP and check for level-ups.
        /// Returns true if the player leveled up at least once.
        /// </summary>
        public static bool AddXp(PlayerProgress progress, int amount)
        {
            if (amount <= 0)
                return false;

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

                // Grant rewards for the new level
                RewardSystem.ApplyLevelUpRewards(progress);
            }

            return leveledUp;
        }
    }

    /// <summary>
    /// Applies rewards when the player levels up: abilities, cheat tokens, etc.
    /// </summary>
    public static class RewardSystem
    {
        public static void ApplyLevelUpRewards(PlayerProgress progress)
        {
            // This switch is where you define what each level gives.
            switch (progress.Level)
            {
                case 3:
                    // Stage 2: Light armor – unlock Second Chance
                    UnlockAbility(progress, AbilityType.SecondChance);
                    break;

                case 5:
                    // Stage 3: Helmet + sword – unlock HintPower
                    UnlockAbility(progress, AbilityType.HintPower);
                    break;

                case 7:
                    // Stage 4: Better chestplate
                    UnlockAbility(progress, AbilityType.MathSight);
                    break;

                case 9:
                    // Stage 5: Full basic knight – first cheat token
                    progress.CheatTokens += 1;
                    break;

                case 11:
                    // Stage 6: Polished armor + cape
                    UnlockAbility(progress, AbilityType.SkipStone);
                    break;

                case 13:
                    // Stage 7: Silver armor
                    UnlockAbility(progress, AbilityType.XpBoost);
                    break;

                case 15:
                    // Stage 8: Gold-trimmed armor
                    UnlockAbility(progress, AbilityType.PowerStrike);
                    break;

                case 17:
                    // Stage 9: Aura knight – more cheat tokens
                    progress.CheatTokens += 2;
                    break;

                case 19:
                    // Stage 10: Legendary knight – extra tokens instead of Valor
                    progress.CheatTokens += 3;
                    break;

                default:
                    // Other levels: no special reward, just stats
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
                    Charges = 0 // default; set >0 for token-style abilities
                });
            }
        }

        /// <summary>
        /// Helper to check if an ability is unlocked.
        /// </summary>
        public static bool HasAbility(PlayerProgress progress, AbilityType type)
        {
            return progress.UnlockedAbilities.Any(a => a.Type == type);
        }
    }
}
