using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using MathQuizLocker.Models; // Important: This lets the service see MonsterConfig

namespace MathQuizLocker.Services
{
	public class MonsterService
	{
		private List<MonsterConfig> _monsters = new();

		public MonsterService()
		{
			LoadConfig();
		}

		// Inside MonsterService.cs
		public void LoadConfig()
		{
			// Use the base directory so it works regardless of where the app is launched
			string baseDir = AppDomain.CurrentDomain.BaseDirectory;
			string jsonPath = Path.Combine(baseDir, "monsters.json");

			if (File.Exists(jsonPath))
			{
				string json = File.ReadAllText(jsonPath);
				var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
				var rawList = JsonSerializer.Deserialize<List<MonsterConfig>>(json, options) ?? new();

				// FIX: Pre-pend the base directory to every SpritePath so AssetCache can find them
				foreach (var monster in rawList)
				{
					// This turns "monsters/goblin" into "C:/Users/.../bin/Debug/Resources/monsters/goblin"
					monster.SpritePath = Path.Combine(baseDir, "Resources", monster.SpritePath);
				}
				_monsters = rawList;
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

        public MonsterConfig GetMonster(string name)
		{
			var monster = _monsters.FirstOrDefault(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

			// Fallback: Return the first monster if name not found, or a default object to prevent crashes
			return monster ?? (_monsters.Count > 0 ? _monsters[0] : new MonsterConfig { Name = "Unknown", MaxHealth = 10 });
		}

		public List<MonsterConfig> GetAllMonsters() => _monsters;
	}
}