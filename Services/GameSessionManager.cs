using System;
using System.Drawing;
using MathQuizLocker.Models;

namespace MathQuizLocker.Services
{
    /// <summary>Result of processing one player answer: correctness, message and color for UI.</summary>
    public class QuizResult
    {
        public bool IsCorrect { get; set; }
        public string Message { get; set; } = "";
        public Color MessageColor { get; set; }
    }

    /// <summary>Runs a single combat: monster/player HP, damage, XP, and answer validation.</summary>
    public class GameSessionManager
    {
        private readonly AppSettings _settings;
        private readonly QuizEngine _quizEngine;
        private PlayerProgress _progress;

        private int _monsterHealth;
        private int _playerHealth;
        private const int MaxPlayerHealth = 100;
        private int _maxMonsterHealth;
		private int _currentMonsterXpReward;

		public int CurrentBattleXpReward { get; private set; }




		// Public Properties for the UI to read
		public int CurrentPlayerHealth => _playerHealth;
		public int CurrentMonsterHealth => _monsterHealth;
        public int MaxMonsterHealth => _maxMonsterHealth;

        private int _currentMonsterAttackDamage;
        public int CurrentMonsterAttackInterval { get; private set; }

        public GameSessionManager(AppSettings settings, QuizEngine engine)
        {		
			_settings = settings;
            _quizEngine = engine;                  
            _playerHealth = MaxPlayerHealth;   
        }

        /// <summary>Starts a new fight with the given monster; resets monster/player HP and attack timer.</summary>
        public void StartNewBattle(MonsterConfig config)
        {
			_progress = _settings.PlayerProgress;
			_maxMonsterHealth = config.MaxHealth;
			_monsterHealth = config.MaxHealth;			
			_currentMonsterXpReward = config.XpReward;

            _currentMonsterAttackDamage = config.AttackDamage;
            CurrentMonsterAttackInterval = config.AttackInterval;

            _playerHealth = MaxPlayerHealth;

			Console.WriteLine($"Battle Start: {config.MaxHealth} HP, {config.XpReward} XP Reward");
		}

        /// <summary>Damage the player takes when the monster timer expires.</summary>
        public int GetTimerDamage() => _currentMonsterAttackDamage;

        /// <summary>Applies damage to the monster. If it dies, adds XP to progress and returns true.</summary>
        public bool ApplyDamage(int damage, out int xpGained, out bool leveledUp)
		{
			xpGained = 0;
			leveledUp = false;

			_monsterHealth -= damage;

			if (_monsterHealth <= 0)
			{
				_monsterHealth = 0;

				// Use the XP amount we got from the JSON
				xpGained = _currentMonsterXpReward;

				// Use your XpSystem to update progress
				XpSystem.AddXp(_progress, _currentMonsterXpReward);

				AppSettings.Save(_settings); // Save progress immediately on kill
				return true; // Monster defeated
			}

			return false; // Monster still alive
		}
        

        /// <summary>Reduces player HP (e.g. wrong answer). Clamped to 0.</summary>
        public void ApplyPlayerDamage(int damage)
        {
            _playerHealth -= damage;
            if (_playerHealth < 0) _playerHealth = 0;

            Console.WriteLine($"Knight took {damage} dmg. Health left: {_playerHealth}");
        }

        /// <summary>Checks the answer for the given (a,b) question and returns a result for the UI.</summary>
        public QuizResult ProcessAnswer(int answer, int a, int b)
        {
            bool isCorrect = _quizEngine.SubmitAnswer(answer);
            var result = new QuizResult { IsCorrect = isCorrect };

            if (isCorrect)
            {
                AppSettings.Save(_settings);
                result.Message = "Correct!";
                result.MessageColor = Color.LimeGreen;
            }
            else
            {
                result.Message = $"Incorrect. {a} × {b} = {a * b}";
                result.MessageColor = Color.Tomato;
            }
            return result;
        }
    }
}