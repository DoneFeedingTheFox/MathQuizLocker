using MathQuizLocker.Models;
using MathQuizLocker.Services;
using Xunit;

namespace MathQuizLocker.Tests
{
    public class GameSessionManagerTests
    {
        [Fact]
        public void StartNewBattle_ExposesCurrentBattleXpReward()
        {
            var settings = new AppSettings();
            var session = new GameSessionManager(settings, new QuizEngine(settings, new Random(1)));
            var monster = new MonsterConfig { MaxHealth = 40, XpReward = 75, AttackDamage = 12, AttackInterval = 5 };

            session.StartNewBattle(monster);

            Assert.Equal(75, session.CurrentBattleXpReward);
        }

        [Fact]
        public void ApplyDamage_SetsLeveledUpWhenXpThresholdIsReached()
        {
            var progress = new PlayerProgress { Level = 1, CurrentXp = 140, TotalXp = 140 };
            var settings = new AppSettings { PlayerProgress = progress };
            var session = new GameSessionManager(settings, new QuizEngine(settings, new Random(2)));
            var monster = new MonsterConfig { MaxHealth = 10, XpReward = 20, AttackDamage = 5, AttackInterval = 5 };

            session.StartNewBattle(monster);
            bool defeated = session.ApplyDamage(10, out int xpGained, out bool leveledUp);

            Assert.True(defeated);
            Assert.Equal(20, xpGained);
            Assert.True(leveledUp);
            Assert.Equal(160, progress.CurrentXp);
        }

        [Fact]
        public void ProcessAnswer_ValidatesProvidedQuestionValues()
        {
            var settings = new AppSettings();
            var engine = new QuizEngine(settings, new Random(3));
            var session = new GameSessionManager(settings, engine);

            // Deliberately skip QuizEngine.GetNextQuestion; ProcessAnswer should still work.
            var correct = session.ProcessAnswer(12, 3, 4);
            var incorrect = session.ProcessAnswer(13, 3, 4);

            Assert.True(correct.IsCorrect);
            Assert.False(incorrect.IsCorrect);
        }
    }
}
