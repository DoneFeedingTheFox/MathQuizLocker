public class MonsterConfig
{
	public string Name { get; set; } = string.Empty;
    public int Level { get; set; }
    public int MaxHealth { get; set; }
	public int XpReward { get; set; }
	public bool IsBoss { get; set; }
	public string SpritePath { get; set; } = string.Empty;
    public int AttackInterval { get; set; } = 5; // Default fallback
    public int AttackDamage { get; set; } = 10;   // Default fallback
}