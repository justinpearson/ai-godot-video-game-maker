using System;
using System.Collections.Generic;
using System.Linq;

namespace TeaLeaves
{
    public record MathProblem(int A, int B, int Answer);

    public static class MathProblemGenerator
    {
        /// <summary>
        /// Generate a random addition problem where A and B are each between 1 and maxAddend.
        /// </summary>
        public static MathProblem Generate(int maxAddend = 9, Random? rng = null)
        {
            var r = rng ?? Random.Shared;
            int a = r.Next(1, maxAddend + 1);
            int b = r.Next(1, maxAddend + 1);
            return new MathProblem(a, b, a + b);
        }

        /// <summary>
        /// Generate <paramref name="count"/> distractor numbers that are NOT equal to
        /// <paramref name="correctAnswer"/>. Each between 0 and <paramref name="maxValue"/>
        /// inclusive, all unique, none equal to correctAnswer.
        /// </summary>
        public static List<int> GenerateDistractors(int correctAnswer, int count, int maxValue = 18, Random? rng = null)
        {
            var r = rng ?? Random.Shared;
            var distractors = new HashSet<int>();

            while (distractors.Count < count)
            {
                int candidate = r.Next(0, maxValue + 1);
                if (candidate != correctAnswer)
                {
                    distractors.Add(candidate);
                }
            }

            return distractors.ToList();
        }
    }
}
