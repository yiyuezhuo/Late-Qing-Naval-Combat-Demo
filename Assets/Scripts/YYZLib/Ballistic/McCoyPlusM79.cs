using System;
using System.Collections.Generic;

namespace YYZ.Ballistic
{
    public sealed class McCoyPlusM79Input
    {
        public McCoyPlusInput McCoy { get; set; } = McCoyPlus.DefaultInput();

        public M79Input M79 { get; set; } = YYZ.Ballistic.M79.DefaultInput();
    }

    public sealed class McCoyPlusM79Row : McCoyPlusRow
    {
        public double? PenetrationInches { get; set; }

        public double? HorizontalPenetrationInches { get; set; }

        public double? M79NavyBallisticLimit { get; set; }

        public double M79Obliquity { get; set; }

        public M79PenetrationMode? PenetrationMode { get; set; }

        public double? RemainingVelocity { get; set; }
    }

    public sealed class McCoyPlusM79Result
    {
        public List<McCoyPlusM79Row> Rows { get; set; } = new List<McCoyPlusM79Row>();

        public List<McCoyPlusM79Row> ChartRows { get; set; } = new List<McCoyPlusM79Row>();

        public List<string> Warnings { get; set; } = new List<string>();
    }

    internal sealed class McCoyPlusM79SolvedThickness
    {
        public double? PenetrationInches;
        public double? M79NavyBallisticLimit;
        public double M79Obliquity;
        public M79PenetrationMode? PenetrationMode;
        public double? RemainingVelocity;
        public string Warning;
    }

    public static class McCoyPlusM79
    {
        const double MinThicknessDiameters = 0.001;
        const double MaxThicknessDiameters = 5.99999;
        const double ThicknessToleranceFps = 0.5;
        const int MaxSearchIterations = 42;

        sealed class M79AtThicknessResult
        {
            public M79Result Result;
            public double Obliquity;
        }

        public static McCoyPlusM79Input DefaultInput()
        {
            return new McCoyPlusM79Input();
        }

        public static McCoyPlusM79Result Calculate(McCoyPlusM79Input input)
        {
            var source = input ?? new McCoyPlusM79Input();
            var trajectory = McCoyPlus.CalculateParallel(source.McCoy);
            var warnings = new List<string>(trajectory.Warnings);
            var rows = new List<McCoyPlusM79Row>();
            foreach (var row in trajectory.Rows)
            {
                var solved = SolvePenetrationThicknessForRow(source.M79, row, row.FallAngleDegrees);
                var horizontalSolved = SolvePenetrationThicknessForRow(source.M79, row, 90 - row.FallAngleDegrees, true);
                if (solved.Warning != null)
                {
                    warnings.Add(solved.Warning);
                }

                rows.Add(new McCoyPlusM79Row
                {
                    Range = row.Range,
                    Time = row.Time,
                    ElevationDegrees = row.ElevationDegrees,
                    Velocity = row.Velocity,
                    FallAngleDegrees = row.FallAngleDegrees,
                    Trajectory = row.Trajectory,
                    PenetrationInches = solved.PenetrationInches,
                    HorizontalPenetrationInches = horizontalSolved.PenetrationInches,
                    M79NavyBallisticLimit = solved.M79NavyBallisticLimit,
                    M79Obliquity = solved.M79Obliquity,
                    PenetrationMode = solved.PenetrationMode,
                    RemainingVelocity = solved.RemainingVelocity,
                });
            }

            return new McCoyPlusM79Result
            {
                Rows = rows,
                ChartRows = McCoyPlus.SelectChartRows(rows),
                Warnings = BallisticCollections.DistinctStrings(warnings),
            };
        }

        static double ClampObliquity(double obliquity)
        {
            return Math.Min(Math.Max(obliquity, 0), 79.9999);
        }

        static M79AtThicknessResult M79AtThickness(M79Input baseInput, McCoyPlusRow row, double thickness, double obliquity)
        {
            var result = M79.Calculate(new M79Input
            {
                ProjectileDiameter = baseInput.ProjectileDiameter,
                ProjectileWeight = baseInput.ProjectileWeight,
                PlateThickness = thickness,
                PlateQuality = baseInput.PlateQuality,
                Obliquity = obliquity,
                StrikingVelocity = row.Velocity,
                Elongation = baseInput.Elongation,
            }, false);
            return new M79AtThicknessResult { Result = result, Obliquity = obliquity };
        }

        internal static McCoyPlusM79SolvedThickness SolvePenetrationThicknessForRow(M79Input baseInput, McCoyPlusRow row, double impactObliquity, bool rejectAboveLimit = false)
        {
            if (rejectAboveLimit && impactObliquity > 79.9999)
            {
                return new McCoyPlusM79SolvedThickness
                {
                    PenetrationInches = null,
                    M79NavyBallisticLimit = null,
                    M79Obliquity = impactObliquity,
                    PenetrationMode = null,
                    RemainingVelocity = null,
                    Warning = null,
                };
            }

            var obliquity = ClampObliquity(impactObliquity);
            var projectileDiameter = Math.Max(baseInput.ProjectileDiameter, 0.001);
            var low = projectileDiameter * MinThicknessDiameters;
            var high = projectileDiameter * MaxThicknessDiameters;
            var lowResult = M79AtThickness(baseInput, row, low, obliquity);
            var highResult = M79AtThickness(baseInput, row, high, obliquity);

            if (lowResult.Result.NavyBallisticLimit > row.Velocity)
            {
                return new McCoyPlusM79SolvedThickness
                {
                    PenetrationInches = null,
                    M79NavyBallisticLimit = lowResult.Result.NavyBallisticLimit,
                    M79Obliquity = lowResult.Obliquity,
                    PenetrationMode = null,
                    RemainingVelocity = null,
                    Warning = $"No M79 thickness at or above {BallisticText.Fixed(low, 3)} in is matched by {BallisticText.Fixed(row.Velocity, 0)} ft/s at {BallisticText.ToJsString(row.Range)}.",
                };
            }

            if (highResult.Result.NavyBallisticLimit < row.Velocity)
            {
                return new McCoyPlusM79SolvedThickness
                {
                    PenetrationInches = null,
                    M79NavyBallisticLimit = highResult.Result.NavyBallisticLimit,
                    M79Obliquity = highResult.Obliquity,
                    PenetrationMode = highResult.Result.PenetrationMode,
                    RemainingVelocity = highResult.Result.RemainingVelocity,
                    Warning = $"No M79 thickness up to {BallisticText.Fixed(high, 2)} in ({BallisticText.ToJsString(MaxThicknessDiameters)}D) matched {BallisticText.Fixed(row.Velocity, 0)} ft/s at {BallisticText.ToJsString(row.Range)}.",
                };
            }

            var bestThickness = high;
            var bestResult = highResult.Result;
            var bestObliquity = highResult.Obliquity;

            for (var index = 0; index < MaxSearchIterations; index += 1)
            {
                var mid = (low + high) / 2;
                var midResult = M79AtThickness(baseInput, row, mid, obliquity);
                bestThickness = mid;
                bestResult = midResult.Result;
                bestObliquity = midResult.Obliquity;

                if (Math.Abs(midResult.Result.NavyBallisticLimit - row.Velocity) <= ThicknessToleranceFps)
                {
                    break;
                }

                if (midResult.Result.NavyBallisticLimit < row.Velocity)
                {
                    low = mid;
                }
                else
                {
                    high = mid;
                }
            }

            return new McCoyPlusM79SolvedThickness
            {
                PenetrationInches = bestThickness,
                M79NavyBallisticLimit = bestResult.NavyBallisticLimit,
                M79Obliquity = bestObliquity,
                PenetrationMode = bestResult.PenetrationMode,
                RemainingVelocity = bestResult.RemainingVelocity,
                Warning = null,
            };
        }
    }
}
