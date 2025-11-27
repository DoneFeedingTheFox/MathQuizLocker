using MathQuizLocker.Models;
using System;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;

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

        /// <summary>
        /// Highest factor currently unlocked (1 = only 1x1..1x1, 2 = 1x1..2x2, etc.)
        /// </summary>
        public int MaxFactorUnlocked { get; set; } = 1;

        /// <summary>
        /// Progress for each multiplication fact, key like "2x3".
        /// </summary>
        public Dictionary<string, FactProgress> Progress { get; set; }
            = new Dictionary<string, FactProgress>();


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
                if (settings.MaxFactorUnlocked < 1)
                    settings.MaxFactorUnlocked = 1;
                if (settings.MaxFactorUnlocked > 10)
                    settings.MaxFactorUnlocked = 10;


                return settings;
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
