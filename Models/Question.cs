namespace MathQuizLocker.Models
{
    /// <summary>A multiplication question: A × B.</summary>
    public class Question
    {
        public int A { get; set; }
        public int B { get; set; }

        public Question(int a, int b)
        {
            A = a;
            B = b;
        }
    }
}