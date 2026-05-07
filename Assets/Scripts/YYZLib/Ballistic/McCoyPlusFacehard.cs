using System;
using System.Collections.Generic;

namespace YYZ.Ballistic
{
    public sealed class McCoyPlusFacehardInput
    {
        public McCoyPlusInput McCoy { get; set; } = McCoyPlus.DefaultInput();

        public FacehardBridgeInput Facehard { get; set; } = new FacehardBridgeInput();
    }

    public sealed class FacehardBridgeInput
    {
        public double ProjectileDiameter { get; set; } = 8;

        public double PlateThickness { get; set; } = 8;

        public double StrikingVelocity { get; set; } = 1500;

        public double Obliquity { get; set; }
    }

    public sealed class FacehardBridgeResult
    {
        public double NavyBl { get; set; }
    }

    public sealed class McCoyPlusFacehardRow : McCoyPlusRow
    {
        public double? PenetrationInches { get; set; }

        public double? HorizontalPenetrationInches { get; set; }

        public double? FacehardNavyBl { get; set; }

        public double FacehardObliquity { get; set; }
    }

    public sealed class McCoyPlusFacehardResult
    {
        public List<McCoyPlusFacehardRow> Rows { get; set; } = new List<McCoyPlusFacehardRow>();

        public List<McCoyPlusFacehardRow> ChartRows { get; set; } = new List<McCoyPlusFacehardRow>();

        public List<string> Warnings { get; set; } = new List<string>();
    }

    public static class McCoyPlusFacehard
    {
        const double MaxThicknessInches = 80;
        const double ThicknessToleranceFps = 0.5;
        const int MaxSearchIterations = 42;

        public static Func<FacehardBridgeInput, FacehardBridgeResult> FacehardCalculator { get; set; }

        sealed class SolvedThickness
        {
            public double? PenetrationInches;
            public double? FacehardNavyBl;
            public double FacehardObliquity;
            public string Warning;
        }

        sealed class NavyBlResult
        {
            public double NavyBl;
            public double Obliquity;
        }

        public static McCoyPlusFacehardInput DefaultInput()
        {
            return new McCoyPlusFacehardInput();
        }

        public static McCoyPlusFacehardResult Calculate(McCoyPlusFacehardInput input)
        {
            if (FacehardCalculator == null)
            {
                return new McCoyPlusFacehardResult
                {
                    Warnings = new List<string>
                    {
                        "Facehard calculator delegate is not configured; McCoyPlusFacehard cannot solve penetration thickness in this scoped port.",
                    },
                };
            }

            var source = input ?? new McCoyPlusFacehardInput();
            var trajectory = McCoyPlus.Calculate(source.McCoy);
            var warnings = new List<string>(trajectory.Warnings);
            var rows = new List<McCoyPlusFacehardRow>();
            foreach (var row in trajectory.Rows)
            {
                var solved = SolvePenetrationThickness(source.Facehard, row, row.FallAngleDegrees);
                var horizontalSolved = SolvePenetrationThickness(source.Facehard, row, 90 - row.FallAngleDegrees, true);
                if (solved.Warning != null)
                {
                    warnings.Add(solved.Warning);
                }

                rows.Add(new McCoyPlusFacehardRow
                {
                    Range = row.Range,
                    Time = row.Time,
                    ElevationDegrees = row.ElevationDegrees,
                    Velocity = row.Velocity,
                    FallAngleDegrees = row.FallAngleDegrees,
                    Trajectory = row.Trajectory,
                    PenetrationInches = solved.PenetrationInches,
                    HorizontalPenetrationInches = horizontalSolved.PenetrationInches,
                    FacehardNavyBl = solved.FacehardNavyBl,
                    FacehardObliquity = solved.FacehardObliquity,
                });
            }

            return new McCoyPlusFacehardResult
            {
                Rows = rows,
                ChartRows = McCoyPlus.SelectChartRows(rows),
                Warnings = BallisticCollections.DistinctStrings(warnings),
            };
        }

        static NavyBlResult FacehardNavyBlAtThickness(FacehardBridgeInput baseInput, McCoyPlusRow row, double thickness, double obliquity)
        {
            var result = FacehardCalculator(new FacehardBridgeInput
            {
                ProjectileDiameter = baseInput.ProjectileDiameter,
                PlateThickness = thickness,
                StrikingVelocity = row.Velocity,
                Obliquity = obliquity,
            });
            return new NavyBlResult { NavyBl = result.NavyBl, Obliquity = obliquity };
        }

        static SolvedThickness SolvePenetrationThickness(FacehardBridgeInput baseInput, McCoyPlusRow row, double impactObliquity, bool rejectAboveLimit = false)
        {
            if (rejectAboveLimit && impactObliquity > 80)
            {
                return new SolvedThickness
                {
                    PenetrationInches = null,
                    FacehardNavyBl = null,
                    FacehardObliquity = impactObliquity,
                    Warning = null,
                };
            }

            var obliquity = Math.Min(Math.Max(impactObliquity, 0), 80);
            var low = 0.1;
            var high = Math.Max(Math.Max(baseInput.ProjectileDiameter * 2, baseInput.PlateThickness), 1);
            var highResult = FacehardNavyBlAtThickness(baseInput, row, high, obliquity);

            while (highResult.NavyBl < row.Velocity && high < MaxThicknessInches)
            {
                low = high;
                high = Math.Min(high * 1.5, MaxThicknessInches);
                highResult = FacehardNavyBlAtThickness(baseInput, row, high, obliquity);
            }

            if (highResult.NavyBl < row.Velocity)
            {
                return new SolvedThickness
                {
                    PenetrationInches = null,
                    FacehardNavyBl = highResult.NavyBl,
                    FacehardObliquity = highResult.Obliquity,
                    Warning = $"No Facehard thickness up to {BallisticText.ToJsString(MaxThicknessInches)} in matched {BallisticText.Fixed(row.Velocity, 0)} ft/s at {BallisticText.ToJsString(row.Range)}.",
                };
            }

            var bestThickness = high;
            var bestNavyBl = highResult.NavyBl;
            var bestObliquity = highResult.Obliquity;

            for (var index = 0; index < MaxSearchIterations; index += 1)
            {
                var mid = (low + high) / 2;
                var midResult = FacehardNavyBlAtThickness(baseInput, row, mid, obliquity);
                bestThickness = mid;
                bestNavyBl = midResult.NavyBl;
                bestObliquity = midResult.Obliquity;

                if (Math.Abs(midResult.NavyBl - row.Velocity) <= ThicknessToleranceFps)
                {
                    break;
                }

                if (midResult.NavyBl < row.Velocity)
                {
                    low = mid;
                }
                else
                {
                    high = mid;
                }
            }

            return new SolvedThickness
            {
                PenetrationInches = bestThickness,
                FacehardNavyBl = bestNavyBl,
                FacehardObliquity = bestObliquity,
                Warning = null,
            };
        }
    }
}
