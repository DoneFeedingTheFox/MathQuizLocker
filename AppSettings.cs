using System;
using System.IO;
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

                return settings ?? new AppSettings();
            }
            catch
            {
                return new AppSettings();
            }
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
