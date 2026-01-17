using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace MathQuizLocker.Services
{
	public static class LocalizationService
	{
		private static Dictionary<string, string> _storyText = new();

		public static void LoadLanguage(string langCode)
		{
			// Update this path to match your exact folder structure
			string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
									   "Assets", "StoryText", $"strings_{langCode}.json");

			if (File.Exists(path))
			{
				try
				{
					var json = File.ReadAllText(path);
					using var doc = JsonDocument.Parse(json);

					// Navigate to the "stories" section of your JSON
					var stories = doc.RootElement.GetProperty("stories");

					_storyText.Clear();
					foreach (var property in stories.EnumerateObject())
					{
						_storyText[property.Name] = property.Value.GetString() ?? "";
					}
				}
				catch (Exception ex)
				{
					System.Diagnostics.Debug.WriteLine($"Localization Error: {ex.Message}");
				}
			}
		}

		public static string GetStory(int level)
		{
			string key = $"level_{level}";
			return _storyText.TryGetValue(key, out string? text) ? text : "Your legend grows...";
		}
	}
}