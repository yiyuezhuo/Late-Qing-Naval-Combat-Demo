using System;
using System.Collections.Generic;

namespace NavalCombatCore
{
    public static class RandomUtils
    {
        public static Random rand = new();
        public static float D100F() => (float)(rand.NextDouble() * 100);
        public static int D6() => rand.Next(6) + 1;
        public static float NextFloat() => (float)rand.NextDouble();
        public static float NextFloat(float low, float high) => (float)rand.NextDouble() * (high - low) + low;
        public static T Sample<T>(List<T> list) => list[rand.Next(list.Count)];
        
        // LLM Generated
        public static T Sample<T>(List<T> list, List<float> weights, Random random = null)
        {
            if (list == null || list.Count == 0)
                throw new ArgumentException("List cannot be null or empty");
            
            if (weights == null || weights.Count != list.Count)
                throw new ArgumentException("Weights list must have the same length as the input list");
            
            random ??= new Random();
            
            // Calculate total weight
            float totalWeight = 0f;
            foreach (float weight in weights)
            {
                if (weight < 0)
                    throw new ArgumentException("Weights cannot be negative");
                totalWeight += weight;
            }
            
            if (totalWeight <= 0)
                throw new ArgumentException("Total weight must be greater than 0");
            
            float randomValue = (float)random.NextDouble() * totalWeight;
            
            float cumulativeWeight = 0f;
            for (int i = 0; i < list.Count; i++)
            {
                cumulativeWeight += weights[i];
                if (randomValue <= cumulativeWeight)
                    return list[i];
            }
            
            return list[list.Count - 1];
        }

        public static int RandomRoundToInt(float x)
        {
            var floor = (int)Math.Floor(x);
            var r = x  - floor;
            var resid = NextFloat() <= r ? 1 : 0;
            return floor + resid;
        }
    }
}