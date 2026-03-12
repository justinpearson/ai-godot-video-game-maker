using System;
using GdUnit4;
using static GdUnit4.Assertions;
using TeaLeaves;

namespace TeaLeaves.Tests
{
    [TestSuite]
    public class MathProblemGeneratorTests
    {
        [TestCase]
        public void Generate_ReturnsValidProblem()
        {
            var rng = new Random(42);
            var problem = MathProblemGenerator.Generate(9, rng);

            AssertThat(problem).IsNotNull();
            AssertInt(problem.A).IsBetween(1, 9);
            AssertInt(problem.B).IsBetween(1, 9);
            AssertInt(problem.Answer).IsBetween(2, 18);
        }

        [TestCase]
        public void Generate_AnswerIsSumOfAAndB()
        {
            var rng = new Random(123);
            for (int i = 0; i < 50; i++)
            {
                var problem = MathProblemGenerator.Generate(9, rng);
                AssertInt(problem.Answer).IsEqual(problem.A + problem.B);
            }
        }

        [TestCase]
        public void Generate_RespectsMaxAddend()
        {
            var rng = new Random(99);
            int maxAddend = 5;

            for (int i = 0; i < 50; i++)
            {
                var problem = MathProblemGenerator.Generate(maxAddend, rng);
                AssertInt(problem.A).IsBetween(1, maxAddend);
                AssertInt(problem.B).IsBetween(1, maxAddend);
                AssertInt(problem.Answer).IsBetween(2, maxAddend * 2);
            }
        }

        [TestCase]
        public void GenerateDistractors_ReturnsCorrectCount()
        {
            var rng = new Random(42);
            var distractors = MathProblemGenerator.GenerateDistractors(7, 3, 18, rng);

            AssertInt(distractors.Count).IsEqual(3);
        }

        [TestCase]
        public void GenerateDistractors_NoneEqualCorrectAnswer()
        {
            var rng = new Random(77);
            int correctAnswer = 10;
            var distractors = MathProblemGenerator.GenerateDistractors(correctAnswer, 5, 18, rng);

            foreach (int d in distractors)
            {
                AssertInt(d).IsNotEqual(correctAnswer);
            }
        }

        [TestCase]
        public void GenerateDistractors_AllUnique()
        {
            var rng = new Random(55);
            var distractors = MathProblemGenerator.GenerateDistractors(8, 4, 18, rng);
            var uniqueSet = new System.Collections.Generic.HashSet<int>(distractors);

            AssertInt(uniqueSet.Count).IsEqual(distractors.Count);
        }
    }
}
