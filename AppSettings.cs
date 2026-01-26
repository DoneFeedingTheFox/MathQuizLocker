using System.Text.Json;
using MathQuizLocker.Models;

namespace MathQuizLocker
{
    /// <summary>Persistence for app and player state. Stored under %LocalAppData%\MathQuizLocker\settings.json.</summary>
    public class AppSettings
    {
        // --- Core Settings ---
        /// <summary>Highest multiplication row unlocked (e.g. 2 = 1× and 2× table).</summary>
        public int MaxFactorUnlocked { get; set; } = 2;

        /// <summary>Language code for localization (e.g. "no", "en"). Empty = use default "no".</summary>
        public string LanguageCode { get; set; } = "";

        // --- Player Data ---
        public PlayerProgress PlayerProgress { get; set; } = new PlayerProgress();

        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MathQuizLocker",
            "settings.json");

        public AppSettings() { }

        /// <summary>
        /// Loads settings from the local app data folder or creates defaults.
        /// </summary>
        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    string json = File.ReadAllText(SettingsPath);
                    var loaded = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                    ValidateAndSanitize(loaded);
                    return loaded;
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error("Failed to load settings", ex);
            }

            return new AppSettings();
        }

        /// <summary>
        /// Saves the current state to settings.json.
        /// </summary>
        public static void Save(AppSettings settings)
        {
            try
            {
                string dir = Path.GetDirectoryName(SettingsPath)!;
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsPath, json);
            }
            catch (Exception ex)
            {
                AppLogger.Error("Failed to save settings", ex);
            }
        }

        /// <summary>Ensures loaded settings have valid ranges and non-null sub-objects.</summary>
        private static void ValidateAndSanitize(AppSettings s)
        {
            if (s.PlayerProgress == null)
                s.PlayerProgress = new PlayerProgress();
            s.PlayerProgress.Level = Math.Clamp(s.PlayerProgress.Level, 1, 999);
            s.PlayerProgress.CurrentXp = Math.Max(0, s.PlayerProgress.CurrentXp);
            s.PlayerProgress.TotalXp = Math.Max(0, s.PlayerProgress.TotalXp);
            s.PlayerProgress.EquippedKnightStage = Math.Clamp(s.PlayerProgress.EquippedKnightStage, 1, 10);
            s.MaxFactorUnlocked = Math.Clamp(s.MaxFactorUnlocked, 1, 10);
            s.LanguageCode ??= "";
        }

        /// <summary>
        /// Fully resets the player's journey back to Level 1.
        /// </summary>
        public void ResetProgress()
        {
            PlayerProgress = new PlayerProgress();
            MaxFactorUnlocked = 2;
       
            Save(this);
        }
    }

    /// <summary>Player progression: level, XP, and which knight sprite is equipped.</summary>
    public class PlayerProgress
    {
        public int Level { get; set; } = 1;
        /// <summary>XP within current level (resets on level-up).</summary>
        public int CurrentXp { get; set; } = 0;
        public int TotalXp { get; set; } = 0;
        /// <summary>Knight sprite stage 1–10 shown in combat.</summary>
        public int EquippedKnightStage { get; set; } = 1;
    }

}