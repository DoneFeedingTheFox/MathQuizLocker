using MathQuizLocker.Models;
using System.Text.Json;


namespace MathQuizLocker
{
    public class AppSettings
    {
        public int RequiredCorrectAnswers { get; set; } = 10;
        public int MinOperand { get; set; } = 1;
        public int MaxOperand { get; set; } = 10;
        public int IdleMinutesBeforeLock { get; set; } = 5;
        public bool ShowQuizOnStartup { get; set; } = true;
        public bool LockOnWakeFromSleep { get; set; } = true;
        public bool EnableDeveloperHotkey { get; set; } = true;

        public int MaxFactorUnlocked { get; set; } = 1;

        public Dictionary<string, FactProgress> Progress { get; set; }
            = new Dictionary<string, FactProgress>();

        public PlayerProgress PlayerProgress { get; set; } = new PlayerProgress();

        public static string GetConfigPath()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(baseDir, "mathlock.settings.json");
        }

        public static AppSettings Load()
        {
            var path = GetConfigPath();

            if (!File.Exists(path))
            {
                var defaults = new AppSettings();
                Save(defaults);
                return defaults;
            }

            try
            {
                var json = File.ReadAllText(path);
                AppSettings? settings = JsonSerializer.Deserialize<AppSettings>(
                    json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                settings ??= new AppSettings();
                settings.Progress ??= new Dictionary<string, FactProgress>();

                if (settings.MaxFactorUnlocked < 1) settings.MaxFactorUnlocked = 1;
                if (settings.MaxFactorUnlocked > 10) settings.MaxFactorUnlocked = 10;

                settings.PlayerProgress ??= new PlayerProgress();

                return settings;
            }
            catch
            {
                return new AppSettings();
            }
        }

        public void ResetProgress()
        {
            // 1. Reset the multiplication table difficulty
            this.MaxFactorUnlocked = 1;
            this.Progress = new Dictionary<string, FactProgress>();

            // 2. Reset the knight / XP progress values
            // I have removed TotalCorrectAnswers because it does not exist in your PlayerProgress class
            this.PlayerProgress.Level = 1;
            this.PlayerProgress.CurrentXp = 0;
            this.PlayerProgress.TotalXp = 0; 
            this.PlayerProgress.CheatTokens = 0;
            this.PlayerProgress.UnlockedAbilities.Clear();

            // 3. Save these changes
            Save(this);
        }

        public static void Save(AppSettings settings)
        {
            var path = GetConfigPath();
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(path, json);
        }
    }
}