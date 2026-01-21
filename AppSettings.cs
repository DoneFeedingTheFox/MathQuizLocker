using System.Text.Json;
using MathQuizLocker.Models;

namespace MathQuizLocker
{
    public class AppSettings
    {
        // --- Core Settings ---
   
        public bool LockOnWakeFromSleep { get; set; } = true;
        public bool ShowQuizOnStartup { get; set; } = true;
        public int RequiredCorrectAnswers { get; set; } = 10;
        public int MaxFactorUnlocked { get; set; } = 2;

        // --- Player Data ---
        public PlayerProgress PlayerProgress { get; set; } = new PlayerProgress();

        // --- Spaced Repetition Data ---
        // Dictionary key format: "AxB" (e.g., "2x5")
    

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
                    return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load settings: {ex.Message}");
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
                System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex.Message}");
            }
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

    public class PlayerProgress
    {
        public int Level { get; set; } = 1;
        public int CurrentXp { get; set; } = 0;
        public int TotalXp { get; set; } = 0;
        public int EquippedKnightStage { get; set; } = 1;
    
    }

}