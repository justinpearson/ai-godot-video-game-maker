using System;
using System.Collections.Generic;
using System.Linq;

namespace TeaLeaves
{
    public record MathProblem(int A, int B, int Answer);

    public static class MathProblemGenerator
    {
        /// <summary>
        /// Generate a random addition problem whose answer equals the target value.
        /// </summary>
        public static MathProblem GenerateForAnswer(int targetAnswer, Random? rng = null)
        {
            var r = rng ?? Random.Shared;
            // Both addends must be single-digit (1–9)
            int maxA = Math.Min(targetAnswer - 1, 9);
            int minA = Math.Max(1, targetAnswer - 9);
            int a = r.Next(minA, maxA + 1);
            int b = targetAnswer - a;
            // Randomly swap so it's not always ascending
            if (r.Next(2) == 0)
                (a, b) = (b, a);
            return new MathProblem(a, b, targetAnswer);
        }

        /// <summary>
        /// Generate a set of unique box values (answers) for the game.
        /// Each value must be >= 2 (so we can split into a + b where both > 0).
        /// </summary>
        public static List<int> GenerateBoxValues(int count, int minValue = 2, int maxValue = 9, Random? rng = null)
        {
            var r = rng ?? Random.Shared;
            var values = new HashSet<int>();
            while (values.Count < count)
            {
                values.Add(r.Next(minValue, maxValue + 1));
            }
            var list = values.ToList();
            // Shuffle
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = r.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
            return list;
        }
    }
}
