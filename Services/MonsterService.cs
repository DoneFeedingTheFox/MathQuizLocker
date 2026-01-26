using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using MathQuizLocker;
using MathQuizLocker.Models;

namespace MathQuizLocker.Services
{
	/// <summary>Loads monsters from monsters.json and resolves them by name or by player level (and boss flag).</summary>
	public class MonsterService
	{
		private List<MonsterConfig> _monsters = new();

		public MonsterService()
		{
			LoadConfig();
		}

		/// <summary>Loads and validates monsters.json from the app base directory; sprite paths are made absolute.</summary>
		public void LoadConfig()
		{
			string baseDir = AppDomain.CurrentDomain.BaseDirectory;
			string jsonPath = Path.Combine(baseDir, "monsters.json");

			if (File.Exists(jsonPath))
			{
				try
				{
					string json = File.ReadAllText(jsonPath);
					var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
					var rawList = JsonSerializer.Deserialize<List<MonsterConfig>>(json, options) ?? new();

					// Validate and sanitize: keep only entries with valid data
					rawList = rawList
						.Where(m => m != null && !string.IsNullOrWhiteSpace(m.Name) && m.Level >= 1 && m.MaxHealth > 0)
						.Select(m =>
						{
							m!.AttackInterval = m.AttackInterval > 0 ? m.AttackInterval : 5;
							m.AttackDamage = m.AttackDamage > 0 ? m.AttackDamage : 10;
							m.SpritePath = Path.Combine(baseDir, "Assets", (m.SpritePath ?? "").Trim());
							return m;
						})
						.ToList();

					_monsters = rawList;
				}
				catch (Exception ex)
				{
					AppLogger.Error("Failed to load monsters.json", ex);
					_monsters = new List<MonsterConfig>();
				}
			}
		}

        public MonsterConfig GetMonsterByLevel(int playerLevel, bool wantBoss)
        {
       
            var monster = _monsters.FirstOrDefault(m => m.Level == playerLevel && m.IsBoss == wantBoss);
            monster ??= _monsters.FirstOrDefault(m => m.Level == playerLevel);

            return monster ?? (_monsters.Count > 0 ? _monsters[0] : new MonsterConfig
            {
                Name = "Default",
                MaxHealth = 50,
                Level = 1,
                SpritePath = "Monsters/goblin"
            });
        }

        /// <summary>Finds a monster by name (case-insensitive). Falls back to first monster or a safe default.</summary>
        public MonsterConfig GetMonster(string name)
		{
			var monster = _monsters.FirstOrDefault(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
			return monster ?? (_monsters.Count > 0 ? _monsters[0] : new MonsterConfig { Name = "Unknown", MaxHealth = 10 });
		}
	}
}