using MathQuizLocker;
using Xunit;

namespace MathQuizLocker.Tests
{
    /// <summary>Unit tests for XpSystem: required XP formula and AddXp behavior.</summary>
    public class XpSystemTests
    {
        [Theory]
        [InlineData(1, 150)]
        [InlineData(2, 200)]
        [InlineData(10, 600)]
        public void GetXpRequiredForNextLevel_ReturnsExpected(int currentLevel, int expected)
        {
            Assert.Equal(expected, XpSystem.GetXpRequiredForNextLevel(currentLevel));
        }

        [Fact]
        public void AddXp_IncreasesCurrentAndTotal()
        {
            var progress = new PlayerProgress { CurrentXp = 20, TotalXp = 100 };
            XpSystem.AddXp(progress, 30);

            Assert.Equal(50, progress.CurrentXp);
            Assert.Equal(130, progress.TotalXp);
        }

        [Fact]
        public void AddXp_IgnoresZeroOrNegative()
        {
            var progress = new PlayerProgress { CurrentXp = 10, TotalXp = 10 };
            XpSystem.AddXp(progress, 0);
            XpSystem.AddXp(progress, -5);

            Assert.Equal(10, progress.CurrentXp);
            Assert.Equal(10, progress.TotalXp);
        }
    }
}
