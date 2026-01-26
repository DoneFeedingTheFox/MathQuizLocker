using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using MathQuizLocker;

namespace MathQuizLocker.Services
{
	/// <summary>Loads story text from Assets/StoryText/strings_{langCode}.json and serves it by level.</summary>
	public static class LocalizationService
	{
		private static Dictionary<string, string> _storyText = new();

		/// <summary>Loads strings from Assets/StoryText/strings_{langCode}.json. Keys like "level_1", "level_2".</summary>
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
					AppLogger.Warn("Localization load failed: " + ex.Message);
				}
			}
		}

		/// <summary>Returns the story text for the given level, or a default line if missing.</summary>
		public static string GetStory(int level)
		{
			string key = $"level_{level}";
			return _storyText.TryGetValue(key, out string? text) ? text : "Your legend grows...";
		}
	}
}