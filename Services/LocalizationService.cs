using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace MathQuizLocker.Services
{
	public static class LocalizationService
	{
		private static Dictionary<string, string> _storyText = new();

		// Try this more robust reading method in LocalizationService.cs
		public static void LoadLanguage(string langCode)
		{
			string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "StoryText", $"strings_{langCode}.json");

			if (File.Exists(path))
			{
				try
				{
					// This version detects the encoding automatically based on the file content
					string json;
					using (var reader = new StreamReader(path, System.Text.Encoding.UTF8, true))
					{
						json = reader.ReadToEnd();
					}

					using var doc = JsonDocument.Parse(json);
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