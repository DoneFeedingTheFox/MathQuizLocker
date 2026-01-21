using MathQuizLocker;

public class QuizEngine
{
    private readonly AppSettings _settings;
    private (int a, int b) _currentQuestion;

    public QuizEngine(AppSettings settings)
    {
        _settings = settings;
    }

    // You can keep this here or use the one in your QuizForm.Helpers
    public (int a, int b) GetNextQuestion()
    {
        Random rng = new Random();
        int limit = _settings.MaxFactorUnlocked;

        // Pure random generation
        _currentQuestion = (rng.Next(1, limit + 1), rng.Next(1, 11));
        return _currentQuestion;
    }

    public bool SubmitAnswer(int userAnswer)
    {
        // Simple verification without saving to a dictionary
        return userAnswer == (_currentQuestion.a * _currentQuestion.b);
    }

    // This is still useful for game progression!
    public void PromoteToNextLevel()
    {
        if (_settings.MaxFactorUnlocked < 10)
        {
            _settings.MaxFactorUnlocked++;
            AppSettings.Save(_settings);
        }
    }
}