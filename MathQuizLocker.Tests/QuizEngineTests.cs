using MathQuizLocker;
using MathQuizLocker.Services;
using Xunit;

namespace MathQuizLocker.Tests
{
    /// <summary>Unit tests for QuizEngine: question range, answer validation, PromoteToNextLevel.</summary>
    public class QuizEngineTests
    {
        [Fact]
        public void GetNextQuestion_RespectsMaxFactorUnlocked()
        {
            var settings = new AppSettings { MaxFactorUnlocked = 3 };
            var rng = new Random(42);
            var engine = new QuizEngine(settings, rng);

            for (int i = 0; i < 50; i++)
            {
                var (a, b) = engine.GetNextQuestion();
                Assert.InRange(a, 1, 3);
                Assert.InRange(b, 1, 10);
            }
        }

        [Fact]
        public void SubmitAnswer_ReturnsTrue_WhenCorrect()
        {
            var settings = new AppSettings { MaxFactorUnlocked = 10 };
            var rng = new Random(123);
            var engine = new QuizEngine(settings, rng);
            var (a, b) = engine.GetNextQuestion();
            int correct = a * b;

            Assert.True(engine.SubmitAnswer(correct));
        }

        [Fact]
        public void SubmitAnswer_ReturnsFalse_WhenIncorrect()
        {
            var settings = new AppSettings { MaxFactorUnlocked = 5 };
            var rng = new Random(456);
            var engine = new QuizEngine(settings, rng);
            engine.GetNextQuestion();

            Assert.False(engine.SubmitAnswer(-1));
            Assert.False(engine.SubmitAnswer(0));
        }

        [Fact]
        public void PromoteToNextLevel_IncrementsUntil10()
        {
            var settings = new AppSettings { MaxFactorUnlocked = 9 };
            var engine = new QuizEngine(settings);

            engine.PromoteToNextLevel();
            Assert.Equal(10, settings.MaxFactorUnlocked);

            engine.PromoteToNextLevel();
            Assert.Equal(10, settings.MaxFactorUnlocked);
        }
    }
}
