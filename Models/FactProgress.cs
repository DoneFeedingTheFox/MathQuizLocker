using System;

namespace MathQuizLocker.Models
{
    public class FactProgress
    {
        public int A { get; set; }
        public int B { get; set; }
        public int CorrectCount { get; set; }
        public int IncorrectCount { get; set; }
        public int CurrentStreak { get; set; }
        public DateTime LastAsked { get; set; }
    }
}