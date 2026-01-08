using System;
using System.Drawing;
using MathQuizLocker.Models;

namespace MathQuizLocker.Services
{
    public class QuizResult
    {
        public bool IsCorrect { get; set; }
        public int XPFirstGained { get; set; }
        public bool LeveledUp { get; set; }
        public string Message { get; set; } = "";
        public Color MessageColor { get; set; }
    }

    public class GameSessionManager
    {
        private readonly AppSettings _settings;
        private readonly QuizEngine _quizEngine;

        public int CorrectCount { get; private set; }
        public int CorrectStreak { get; private set; }
        public bool PowerStrikeQueued { get; set; }
        public bool XpBoostActive { get; set; }

        public GameSessionManager(AppSettings settings, QuizEngine engine)
        {
            _settings = settings;
            _quizEngine = engine;
        }

        public QuizResult ProcessAnswer(int answer, int a, int b)
        {
            bool isCorrect = _quizEngine.SubmitAnswer(answer);
            var result = new QuizResult { IsCorrect = isCorrect };

            if (isCorrect)
            {
                CorrectCount++;
                CorrectStreak++;

                int xpGain = XpSystem.XpPerCorrectAnswer;
                if (XpBoostActive) xpGain *= 2;
                if (PowerStrikeQueued) { xpGain *= 2; PowerStrikeQueued = false; }

                result.XPFirstGained = xpGain;
                result.LeveledUp = XpSystem.AddXp(_settings.PlayerProgress, xpGain);

                if (CorrectStreak >= 5) { _settings.PlayerProgress.CheatTokens++; CorrectStreak -= 5; }

                AppSettings.Save(_settings);
                result.Message = result.LeveledUp ? $"LEVEL UP! Level {_settings.PlayerProgress.Level}!" : $"Correct!";
                result.MessageColor = result.LeveledUp ? Color.Gold : Color.LimeGreen;
            }
            else
            {
                CorrectStreak = 0;
                result.Message = $"Incorrect. {a} × {b} = {a * b}";
                result.MessageColor = Color.Tomato;
            }
            return result;
        }

        public bool IsSessionComplete() => CorrectCount >= (_settings.RequiredCorrectAnswers > 0 ? _settings.RequiredCorrectAnswers : 10);
    }
}