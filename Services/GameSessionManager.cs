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
        private PlayerProgress _progress;

        private int _monsterHealth;
        private int _playerHealth;
        private const int MaxPlayerHealth = 100;
        private int _maxMonsterHealth;

        // Public Properties for the UI to read
        public int CurrentPlayerHealth => _playerHealth;
        public int CurrentMonsterHealth => _monsterHealth;
        public int MaxMonsterHealth => _maxMonsterHealth;

        public int CorrectCount { get; private set; }
        public int TotalKills { get; private set; } // Track kills for Game Over screen
        public bool XpBoostActive { get; set; }

        public GameSessionManager(AppSettings settings, QuizEngine engine)
        {
            _settings = settings;
            _quizEngine = engine;

            // CRITICAL: Set player health to 100 BEFORE starting anything else
            // This prevents the "Grey Overlay" in OnPaint on launch.
            _playerHealth = MaxPlayerHealth;

            StartNewBattle();
        }

        public void StartNewBattle()
        {
            _progress = _settings.PlayerProgress;

            // Reset Monster Health based on Tier
            int tier = Math.Max(1, _settings.MaxFactorUnlocked);
            _maxMonsterHealth = 15 + (tier * 15);
            _monsterHealth = _maxMonsterHealth;

            _playerHealth = MaxPlayerHealth;

            Console.WriteLine($"Battle Start: Monster HP {_monsterHealth}, Player HP {_playerHealth}");
        }

        public void ApplyDamage(int damage)
        {
            _monsterHealth -= damage;
            if (_monsterHealth <= 0)
            {
                _monsterHealth = 0;
                TotalKills++; // Increment kill counter
            }
            Console.WriteLine($"Monster took {damage} dmg. Remaining: {_monsterHealth}");
        }

        public void ApplyPlayerDamage(int damage)
        {
            _playerHealth -= damage;
            if (_playerHealth < 0) _playerHealth = 0;

            Console.WriteLine($"Knight took {damage} dmg. Health left: {_playerHealth}");
        }

        public QuizResult ProcessAnswer(int answer, int a, int b)
        {
            bool isCorrect = _quizEngine.SubmitAnswer(answer);
            var result = new QuizResult { IsCorrect = isCorrect };

            if (isCorrect)
            {
                CorrectCount++;

                AppSettings.Save(_settings);
                result.Message = result.LeveledUp ? $"LEVEL UP! Level {_settings.PlayerProgress.Level}!" : $"Correct!";
                result.MessageColor = result.LeveledUp ? Color.Gold : Color.LimeGreen;
            }
            else
            {
                result.Message = $"Incorrect. {a} × {b} = {a * b}";
                result.MessageColor = Color.Tomato;
            }
            return result;
        }

        public bool IsSessionComplete() => CorrectCount >= (_settings.RequiredCorrectAnswers > 0 ? _settings.RequiredCorrectAnswers : 10);
    }
}