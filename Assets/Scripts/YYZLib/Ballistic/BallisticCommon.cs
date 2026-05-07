using System;
using System.Collections.Generic;
using System.Globalization;

namespace YYZ.Ballistic
{
    public static class BallisticText
    {
        public static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        public static string Fixed(double value, int digits)
        {
            return BallisticMath.IsFinite(value)
                ? value.ToString("F" + digits, Invariant)
                : "NaN";
        }

        public static string ToJsString(double value)
        {
            return BallisticMath.IsFinite(value)
                ? value.ToString("G15", Invariant)
                : "NaN";
        }

        public static double RoundHalfUp(double value)
        {
            return Math.Floor(value + 0.5);
        }
    }

    public static class BallisticMath
    {
        public const double Deg = Math.PI / 180.0;

        public static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        public static double Positive(double value, double fallback)
        {
            return IsFinite(value) && value > 0 ? value : fallback;
        }

        public static double NonNegative(double value)
        {
            return IsFinite(value) && value > 0 ? value : 0;
        }
    }

    public sealed class DragPoint
    {
        public double Mach { get; set; }

        public double Cd { get; set; }
    }

    public sealed class TrajectoryPoint
    {
        public double Range { get; set; }

        public double HeightInches { get; set; }

        public double DeflectionInches { get; set; }

        public double Velocity { get; set; }

        public double Time { get; set; }

        public double Vx { get; set; }

        public double Vy { get; set; }

        public double Vz { get; set; }
    }

    internal static class BallisticCollections
    {
        public static List<string> DistinctStrings(IEnumerable<string> values)
        {
            var seen = new HashSet<string>();
            var result = new List<string>();
            foreach (var value in values)
            {
                if (seen.Add(value))
                {
                    result.Add(value);
                }
            }

            return result;
        }
    }
}
