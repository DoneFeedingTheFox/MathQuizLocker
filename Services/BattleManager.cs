using MathQuizLocker.Services;

public class BattleManager
{
    public int MonsterHealth { get; private set; }
    public int MaxMonsterHealth { get; private set; }
    public (int d1, int d2) CurrentDice { get; private set; }

    public void SpawnMonster(int level)
    {
        MaxMonsterHealth = 50 + (level * 20); // Scale health with level
        MonsterHealth = MaxMonsterHealth;
    }

    public void RollDice(QuizEngine engine)
    {
        CurrentDice = engine.GetNextQuestion(); // Reusing your existing engine for the dice numbers
    }

    public bool ApplyAttack(int answer, out int damage)
    {
        damage = 0;
        int correctResult = CurrentDice.d1 * CurrentDice.d2;

        if (answer == correctResult)
        {
            damage = answer; // Damage is the math result as requested
            MonsterHealth -= damage;
            return true;
        }
        return false;
    }
}