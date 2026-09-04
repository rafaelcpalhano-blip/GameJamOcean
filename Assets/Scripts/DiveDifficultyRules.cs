using System;

namespace GameJamOcean.Diving
{
    public static class DiveDifficultyRules
    {
        public static int SelectTier(int points, int[] thresholds)
        {
            int selected = -1, best = -1;
            for (int i = 0; i < thresholds.Length; i++)
                if (thresholds[i] >= 0 && thresholds[i] <= points && thresholds[i] > best)
                { selected = i; best = thresholds[i]; }
            return selected;
        }
        // Largest remainder allocation: quotas always add up to the fixed session budget.
        public static int[] Allocate(int total, float[] weights)
        {
            if (total < 0 || weights == null || weights.Length == 0) throw new ArgumentException("Invalid budget or weights");
            double sum = 0;
            foreach (float w in weights)
            {
                if (float.IsNaN(w) || float.IsInfinity(w) || w < 0) throw new ArgumentException("Invalid weight");
                sum += w;
            }
            if (sum <= 0) throw new ArgumentException("Weights must have a positive sum");
            var result = new int[weights.Length];
            var fractions = new double[weights.Length];
            int assigned = 0;
            for (int i = 0; i < weights.Length; i++)
            {
                double exact = total * (double)weights[i] / sum;
                result[i] = (int)Math.Floor(exact);
                fractions[i] = exact - result[i]; assigned += result[i];
            }
            while (assigned++ < total)
            {
                int best = 0;
                for (int i = 1; i < weights.Length; i++) if (fractions[i] > fractions[best]) best = i;
                result[best]++; fractions[best] = -1;
            }
            return result;
        }
    }
}
