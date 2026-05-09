using System;
using System.Collections.Generic;

namespace YYZ.Ballistic
{
    public sealed class McCoyPlusFacehardM79Input
    {
        public McCoyPlusInput McCoy { get; set; } = McCoyPlus.DefaultInput();

        public FacehardBridgeInput Facehard { get; set; } = new FacehardBridgeInput();

        public M79Input M79 { get; set; } = YYZ.Ballistic.M79.DefaultInput();
    }

    public sealed class McCoyPlusFacehardM79Row : McCoyPlusRow
    {
        public double? PenetrationInches { get; set; }

        public double? HorizontalPenetrationInches { get; set; }

        public double? FacehardNavyBl { get; set; }

        public double FacehardObliquity { get; set; }

        public double? M79NavyBallisticLimit { get; set; }

        public double M79Obliquity { get; set; }

        public M79PenetrationMode? PenetrationMode { get; set; }

        public double? RemainingVelocity { get; set; }
    }

    public sealed class McCoyPlusFacehardM79Result
    {
        public List<McCoyPlusFacehardM79Row> Rows { get; set; } = new List<McCoyPlusFacehardM79Row>();

        public List<McCoyPlusFacehardM79Row> ChartRows { get; set; } = new List<McCoyPlusFacehardM79Row>();

        public List<string> Warnings { get; set; } = new List<string>();
    }

    public static class McCoyPlusFacehardM79
    {
        public static McCoyPlusFacehardM79Input DefaultInput()
        {
            return new McCoyPlusFacehardM79Input();
        }

        public static McCoyPlusFacehardM79Result Calculate(McCoyPlusFacehardM79Input input)
        {
            return Calculate(input, null);
        }

        public static McCoyPlusFacehardM79Result Calculate(McCoyPlusFacehardM79Input input, IEnumerable<double> targetRanges)
        {
            if (McCoyPlusFacehard.FacehardCalculator == null)
            {
                return new McCoyPlusFacehardM79Result
                {
                    Warnings = new List<string>
                    {
                        "Facehard calculator delegate is not configured; McCoyPlusFacehardM79 cannot solve vertical penetration thickness in this scoped port.",
                    },
                };
            }

            var source = input ?? new McCoyPlusFacehardM79Input();
            var trajectory = targetRanges == null
                ? McCoyPlus.CalculateParallel(source.McCoy)
                : McCoyPlus.CalculateTargetsParallel(source.McCoy, targetRanges);
            var warnings = new List<string>(trajectory.Warnings);
            var rows = new List<McCoyPlusFacehardM79Row>();
            foreach (var row in trajectory.Rows)
            {
                var verticalSolved = McCoyPlusFacehard.SolvePenetrationThicknessForRow(source.Facehard, row, row.FallAngleDegrees);
                var horizontalSolved = McCoyPlusM79.SolvePenetrationThicknessForRow(source.M79, row, 90 - row.FallAngleDegrees, true);
                if (verticalSolved.Warning != null)
                {
                    warnings.Add(verticalSolved.Warning);
                }
                if (horizontalSolved.Warning != null)
                {
                    warnings.Add(horizontalSolved.Warning);
                }

                rows.Add(new McCoyPlusFacehardM79Row
                {
                    Range = row.Range,
                    Time = row.Time,
                    ElevationDegrees = row.ElevationDegrees,
                    Velocity = row.Velocity,
                    FallAngleDegrees = row.FallAngleDegrees,
                    Trajectory = row.Trajectory,
                    PenetrationInches = verticalSolved.PenetrationInches,
                    HorizontalPenetrationInches = horizontalSolved.PenetrationInches,
                    FacehardNavyBl = verticalSolved.FacehardNavyBl,
                    FacehardObliquity = verticalSolved.FacehardObliquity,
                    M79NavyBallisticLimit = horizontalSolved.M79NavyBallisticLimit,
                    M79Obliquity = horizontalSolved.M79Obliquity,
                    PenetrationMode = horizontalSolved.PenetrationMode,
                    RemainingVelocity = horizontalSolved.RemainingVelocity,
                });
            }

            return new McCoyPlusFacehardM79Result
            {
                Rows = rows,
                ChartRows = McCoyPlus.SelectChartRows(rows),
                Warnings = BallisticCollections.DistinctStrings(warnings),
            };
        }
    }
}
