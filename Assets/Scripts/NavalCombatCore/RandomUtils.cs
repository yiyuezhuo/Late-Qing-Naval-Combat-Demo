using System;
using System.Collections.Generic;

namespace NavalCombatCore
{
    public static class RandomUtils
    {
        public static Random rand = new();
        public static float D100F() => (float)(rand.NextDouble() * 100);
        public static float NextFloat() => (float)rand.NextDouble();
        public static T Sample<T>(List<T> list) => list[rand.Next(list.Count)];
    }
}