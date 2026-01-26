namespace MathQuizLocker.Models
{
    /// <summary>One monster entry from monsters.json: stats and sprite path (absolute after MonsterService.LoadConfig).</summary>
    public class MonsterConfig
    {
        public string Name { get; set; } = string.Empty;
        public int Level { get; set; }
        public int MaxHealth { get; set; }
        public int XpReward { get; set; }
        public bool IsBoss { get; set; }
        public string SpritePath { get; set; } = string.Empty;
        /// <summary>Seconds between monster attacks when the timer is used.</summary>
        public int AttackInterval { get; set; } = 5;
        /// <summary>Damage to the player on wrong answer or when the countdown reaches zero.</summary>
        public int AttackDamage { get; set; } = 10;
    }
}