using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using MathQuizLocker;
using MathQuizLocker.Models;

namespace MathQuizLocker.Services
{
	/// <summary>Loads transition graphics from transition_graphics.json and resolves them by biome transitions.</summary>
	public class TransitionGraphicService
	{
		private List<TransitionGraphicConfig> _transitionGraphics = new();

		public TransitionGraphicService()
		{
			LoadConfig();
		}

		/// <summary>Loads and validates transition_graphics.json from the app base directory; graphic paths are made absolute.</summary>
		public void LoadConfig()
		{
			string baseDir = AppDomain.CurrentDomain.BaseDirectory;
			string jsonPath = Path.Combine(baseDir, "transition_graphics.json");

			if (File.Exists(jsonPath))
			{
				try
				{
					string json = File.ReadAllText(jsonPath);
					var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
					var rawList = JsonSerializer.Deserialize<List<TransitionGraphicConfig>>(json, options) ?? new();

					// Validate and sanitize: keep only entries with valid data
					rawList = rawList
						.Where(tg => tg != null && !string.IsNullOrWhiteSpace(tg.FromBiome) && !string.IsNullOrWhiteSpace(tg.ToBiome) && !string.IsNullOrWhiteSpace(tg.GraphicPath))
						.Select(tg =>
						{
							// Normalize path separators and combine with Assets folder
							string relativePath = (tg.GraphicPath ?? "").Trim().Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
							tg!.GraphicPath = Path.Combine(baseDir, "Assets", relativePath);
							return tg;
						})
						.ToList();

					_transitionGraphics = rawList;
					System.Diagnostics.Debug.WriteLine($"[TRANSITION GRAPHIC SERVICE] Loaded {_transitionGraphics.Count} transition graphics from {jsonPath}");
					foreach (var tg in _transitionGraphics)
					{
						System.Diagnostics.Debug.WriteLine($"  - {tg.FromBiome} -> {tg.ToBiome}: {tg.GraphicPath}");
					}
				}
				catch (Exception ex)
				{
					AppLogger.Error("Failed to load transition_graphics.json", ex);
					System.Diagnostics.Debug.WriteLine($"[TRANSITION GRAPHIC SERVICE] Error loading config: {ex.Message}");
					_transitionGraphics = new List<TransitionGraphicConfig>();
				}
			}
			else
			{
				System.Diagnostics.Debug.WriteLine($"[TRANSITION GRAPHIC SERVICE] JSON file not found at: {jsonPath}");
			}
		}

		/// <summary>Gets the transition graphic path for a biome transition. Returns null if not found.</summary>
		public string? GetTransitionGraphic(string fromBiome, string toBiome)
		{
			if (string.IsNullOrWhiteSpace(fromBiome) || string.IsNullOrWhiteSpace(toBiome))
			{
				System.Diagnostics.Debug.WriteLine($"[TRANSITION GRAPHIC SERVICE] Invalid parameters: fromBiome='{fromBiome}', toBiome='{toBiome}'");
				return null;
			}

			System.Diagnostics.Debug.WriteLine($"[TRANSITION GRAPHIC SERVICE] Looking up: '{fromBiome}' -> '{toBiome}'");
			System.Diagnostics.Debug.WriteLine($"[TRANSITION GRAPHIC SERVICE] Loaded {_transitionGraphics.Count} transition graphics");

			var config = _transitionGraphics.FirstOrDefault(tg =>
				tg.FromBiome.Equals(fromBiome, StringComparison.OrdinalIgnoreCase) &&
				tg.ToBiome.Equals(toBiome, StringComparison.OrdinalIgnoreCase));

			if (config != null)
			{
				System.Diagnostics.Debug.WriteLine($"[TRANSITION GRAPHIC SERVICE] Found: {config.GraphicPath}");
				return config.GraphicPath;
			}
			else
			{
				System.Diagnostics.Debug.WriteLine($"[TRANSITION GRAPHIC SERVICE] Not found. Available transitions:");
				foreach (var tg in _transitionGraphics)
				{
					System.Diagnostics.Debug.WriteLine($"  - {tg.FromBiome} -> {tg.ToBiome}: {tg.GraphicPath}");
				}
				return null;
			}
		}
	}
}
