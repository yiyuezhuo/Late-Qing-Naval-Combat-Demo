using System;
using System.Collections.Generic;
using System.Linq;

namespace YYZ.Ballistic
{
    public sealed class McCoyInput
    {
        public string DragName { get; set; } = "Example custom table";

        public List<DragPoint> DragTable { get; set; } = McCoy.DefaultDragTable();

        public string RangeUnit { get; set; } = "yards";

        public string Atmosphere { get; set; } = "standard";

        public string ProjectileId { get; set; } = "Example projectile";

        public double MuzzleVelocity { get; set; } = 2800;

        public double BallisticCoefficient { get; set; } = 0.3;

        public double SightHeight { get; set; } = 1.5;

        public double ElevationMinutes { get; set; } = 4;

        public double DensityRatio { get; set; } = 1;

        public double TemperatureF { get; set; } = 59;

        public double PrintInterval { get; set; } = 100;

        public double MaxRange { get; set; } = 1000;

        public double RangeWindMph { get; set; }

        public double CrossWindMph { get; set; }

        public double MatchRange { get; set; }

        public double MatchHeight { get; set; }
    }

    public sealed class McCoyResult
    {
        public List<TrajectoryPoint> Points { get; set; } = new List<TrajectoryPoint>();

        public double AdjustedElevationMinutes { get; set; }

        public bool DidAdjustElevation { get; set; }

        public bool DidReachSampledHeight { get; set; }

        public double SampledHeightRange { get; set; }

        public List<string> Warnings { get; set; } = new List<string>();

        public List<string> LegacyReport { get; set; } = new List<string>();
    }

    public static class McCoy
    {
        const double G = 32.174;
        const double Eps = 0.00001;
        const int MaxCorrectorIterations = 40;
        const int MaxElevationIterations = 20;

        sealed class AtmosphereConstants
        {
            public double Rh1;
            public double Rh2;
            public double Tk1;
            public double Tk2;
            public double Pir;
            public double Vv1;
        }

        sealed class Acceleration
        {
            public double Ax;
            public double Ay;
            public double Az;
        }

        sealed class TrajectoryRun
        {
            public List<TrajectoryPoint> Points = new List<TrajectoryPoint>();
            public double FinalHeightFeet;
            public bool ReachedStopHeight;
            public double StopHeightRangeUnits;
            public List<string> Warnings = new List<string>();
        }

        public static List<DragPoint> DefaultDragTable()
        {
            return new List<DragPoint>
            {
                new DragPoint { Mach = 0.0, Cd = 0.18 },
                new DragPoint { Mach = 0.5, Cd = 0.19 },
                new DragPoint { Mach = 0.8, Cd = 0.22 },
                new DragPoint { Mach = 0.95, Cd = 0.32 },
                new DragPoint { Mach = 1.05, Cd = 0.42 },
                new DragPoint { Mach = 1.2, Cd = 0.38 },
                new DragPoint { Mach = 1.5, Cd = 0.31 },
                new DragPoint { Mach = 2.0, Cd = 0.26 },
                new DragPoint { Mach = 3.0, Cd = 0.22 },
            };
        }

        public static McCoyInput DefaultInput()
        {
            return new McCoyInput();
        }

        public static string DragTableToText(IEnumerable<DragPoint> points)
        {
            return string.Join("\n", points.Select(point => $"{BallisticText.ToJsString(point.Mach)}, {BallisticText.ToJsString(point.Cd)}"));
        }

        public static List<DragPoint> ParseDragTable(string text)
        {
            var result = new List<DragPoint>();
            if (string.IsNullOrWhiteSpace(text))
            {
                return result;
            }

            var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (line.Length == 0)
                {
                    continue;
                }

                var parts = line.Split(new[] { ',', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2)
                {
                    continue;
                }

                if (double.TryParse(parts[0], System.Globalization.NumberStyles.Float, BallisticText.Invariant, out var mach) &&
                    double.TryParse(parts[1], System.Globalization.NumberStyles.Float, BallisticText.Invariant, out var cd) &&
                    BallisticMath.IsFinite(mach) &&
                    BallisticMath.IsFinite(cd))
                {
                    result.Add(new DragPoint { Mach = Math.Abs(mach), Cd = cd });
                }
            }

            result.Sort((left, right) => left.Mach.CompareTo(right.Mach));
            return result;
        }

        public static List<DragPoint> NormalizeDragTable(string text)
        {
            var parsed = ParseDragTable(text);
            return parsed.Count >= 2 ? parsed : DefaultDragTable();
        }

        public static McCoyResult Calculate(McCoyInput input)
        {
            return Calculate(input, true);
        }

        public static McCoyResult Calculate(McCoyInput input, bool includeReports)
        {
            var normalized = Normalize(input);
            var wantsMatch = normalized.MatchRange > 0 || normalized.MatchHeight != 0;
            var elevation = normalized.ElevationMinutes;
            var warnings = new List<string>();

            if (wantsMatch && normalized.MatchRange > 0)
            {
                var targetHeightFeet = normalized.MatchHeight / 12.0;
                var elevations = new List<double>();
                var heights = new List<double>();
                for (var index = 0; index < MaxElevationIterations; index += 1)
                {
                    var trial = RunTrajectory(normalized, elevation, normalized.MatchRange, false);
                    warnings.AddRange(trial.Warnings);
                    elevations.Add(elevation);
                    heights.Add(trial.FinalHeightFeet);
                    if (Math.Abs(trial.FinalHeightFeet - targetHeightFeet) < 0.00001)
                    {
                        break;
                    }

                    if (index == 0)
                    {
                        elevation += 2;
                    }
                    else
                    {
                        var h0 = heights[index - 1];
                        var h1 = heights[index];
                        var e0 = elevations[index - 1];
                        var e1 = elevations[index];
                        if (h0 == h1)
                        {
                            warnings.Add("Elevation iteration did not converge.");
                            break;
                        }

                        elevation = e1 + (targetHeightFeet - h1) * ((e0 - e1) / (h0 - h1));
                    }
                }
            }

            var trajectory = RunTrajectory(normalized, elevation, normalized.MaxRange, true);
            warnings.AddRange(trajectory.Warnings);
            var result = new McCoyResult
            {
                Points = trajectory.Points,
                AdjustedElevationMinutes = elevation,
                DidAdjustElevation = wantsMatch,
                Warnings = BallisticCollections.DistinctStrings(warnings),
            };
            if (includeReports)
            {
                result.LegacyReport = RenderLegacyReport(normalized, result);
            }

            return result;
        }

        public static McCoyResult CalculateFixedElevationToHeightAtRanges(
            McCoyInput input,
            double elevationMinutes,
            double targetHeightInches,
            double maxRange,
            IReadOnlyList<double> sampleRanges)
        {
            var normalized = Normalize(input);
            var ranges = (sampleRanges ?? Array.Empty<double>())
                .Where(range => BallisticMath.IsFinite(range) && range >= 0 && range <= maxRange + 1e-9)
                .Distinct()
                .OrderBy(range => range)
                .ToList();
            normalized.MatchRange = 0;
            normalized.MatchHeight = 0;
            normalized.MaxRange = Math.Max(maxRange, 1);
            normalized.PrintInterval = Math.Max(normalized.PrintInterval, 1);

            var trajectory = RunTrajectory(normalized, elevationMinutes, normalized.MaxRange, true, ranges, targetHeightInches / 12.0);
            var warnings = new List<string>(trajectory.Warnings);
            if (!trajectory.ReachedStopHeight)
            {
                warnings.Add("Fixed-elevation trajectory did not descend through the target height before the range limit.");
            }

            return new McCoyResult
            {
                Points = trajectory.Points,
                AdjustedElevationMinutes = elevationMinutes,
                DidAdjustElevation = false,
                DidReachSampledHeight = trajectory.ReachedStopHeight,
                SampledHeightRange = trajectory.StopHeightRangeUnits,
                Warnings = BallisticCollections.DistinctStrings(warnings),
            };
        }

        public static List<string> RenderLegacyReport(McCoyInput input, McCoyResult result)
        {
            var lines = new List<string>();
            var unitLabel = input.RangeUnit == "yards" ? "(YARDS)" : "(METERS)";
            lines.Add(input.Atmosphere == "standard" ? "ARMY STANDARD METRO" : "ICAO STANDARD ATMOSPHERE");
            lines.Add("");
            lines.Add($"DRAG FUNCTION: {input.DragName}");
            lines.Add($"PROJECTILE IDENTIFICATION: {input.ProjectileId}");
            lines.Add("");
            lines.Add("MUZ VEL    C       H0      ELEV    DENSITY");
            lines.Add("(FT/SEC)   (LB/IN2)(INCHES)(MINUTES)RATIO");
            lines.Add($"{BallisticText.ToJsString(input.MuzzleVelocity)} {BallisticText.ToJsString(input.BallisticCoefficient)} {BallisticText.ToJsString(input.SightHeight)} {BallisticText.ToJsString(Math.Floor(1000 * result.AdjustedElevationMinutes + 0.5) / 1000)} {BallisticText.ToJsString(input.DensityRatio)}");
            lines.Add("");
            lines.Add("TEMP       RANGEWIND CROSSWIND RMATCH HMATCH");
            lines.Add($"(DEG,F)    (MPH)     (MPH)     {unitLabel} (INCHES)");
            lines.Add($"{BallisticText.ToJsString(Math.Floor(10 * input.TemperatureF) / 10)} {BallisticText.ToJsString(input.RangeWindMph)} {BallisticText.ToJsString(input.CrossWindMph)} {BallisticText.ToJsString(input.MatchRange)} {BallisticText.ToJsString(input.MatchHeight)}");
            lines.Add("");
            lines.Add($"RANGE {unitLabel}  HEIGHT(IN)  DEFL.(IN)  VEL(FPS)  TIME(SEC)  VX(FPS)  VY(FPS)  VZ(FPS)");
            foreach (var point in result.Points)
            {
                lines.Add(string.Join(" ", new[]
                {
                    BallisticText.Fixed(point.Range, 0).PadLeft(6),
                    BallisticText.Fixed(point.HeightInches, 1).PadLeft(9),
                    BallisticText.Fixed(point.DeflectionInches, 1).PadLeft(9),
                    BallisticText.Fixed(point.Velocity, 1).PadLeft(9),
                    BallisticText.Fixed(point.Time, 3).PadLeft(8),
                    BallisticText.Fixed(point.Vx, 1).PadLeft(8),
                    BallisticText.Fixed(point.Vy, 1).PadLeft(8),
                    BallisticText.Fixed(point.Vz, 1).PadLeft(8),
                }));
            }

            foreach (var warning in result.Warnings)
            {
                lines.Add(warning.ToUpperInvariant());
            }

            return lines;
        }

        static McCoyInput Normalize(McCoyInput input)
        {
            var source = input ?? new McCoyInput();
            return new McCoyInput
            {
                DragName = source.DragName,
                DragTable = source.DragTable != null && source.DragTable.Count >= 2 ? source.DragTable : DefaultDragTable(),
                RangeUnit = source.RangeUnit,
                Atmosphere = source.Atmosphere,
                ProjectileId = source.ProjectileId,
                MuzzleVelocity = Math.Max(source.MuzzleVelocity, 1),
                BallisticCoefficient = Math.Max(source.BallisticCoefficient, 0.0001),
                SightHeight = source.SightHeight,
                ElevationMinutes = source.ElevationMinutes,
                DensityRatio = Math.Max(source.DensityRatio, 0.0001),
                TemperatureF = source.TemperatureF,
                PrintInterval = Math.Max(source.PrintInterval, 1),
                MaxRange = Math.Max(source.MaxRange, 1),
                RangeWindMph = source.RangeWindMph,
                CrossWindMph = source.CrossWindMph,
                MatchRange = source.MatchRange,
                MatchHeight = source.MatchHeight,
            };
        }

        static double UnitToFeet(string unit)
        {
            return unit == "yards" ? 3 : 1 / 0.3048;
        }

        static AtmosphereConstants AtmosphereFor(string kind)
        {
            if (kind == "icao")
            {
                return new AtmosphereConstants
                {
                    Rh1 = -0.00002926,
                    Rh2 = -0.0000000001,
                    Tk1 = -0.000006858,
                    Tk2 = -0.00000000002776,
                    Pir = -0.000208551,
                    Vv1 = 49.0223,
                };
            }

            return new AtmosphereConstants
            {
                Rh1 = -0.00003158,
                Rh2 = 0,
                Tk1 = -0.000006015,
                Tk2 = 0,
                Pir = -0.0002048757,
                Vv1 = 49.19,
            };
        }

        static double InterpolateCd(double mach, List<DragPoint> table)
        {
            if (table.Count < 2)
            {
                throw new InvalidOperationException("Drag table needs at least two points.");
            }

            if (mach < table[0].Mach || mach > table[table.Count - 1].Mach)
            {
                throw new InvalidOperationException($"Mach {BallisticText.Fixed(mach, 3)} is outside the drag table range.");
            }

            for (var index = 0; index < table.Count - 1; index += 1)
            {
                var left = table[index];
                var right = table[index + 1];
                if (mach <= right.Mach)
                {
                    var slope = (right.Cd - left.Cd) / (right.Mach - left.Mach);
                    return left.Cd + slope * (mach - left.Mach);
                }
            }

            return table[table.Count - 1].Cd;
        }

        static TrajectoryRun RunTrajectory(
            McCoyInput input,
            double elevationMinutes,
            double stopRange,
            bool collectOutput,
            IReadOnlyList<double> outputRanges = null,
            double? descendingStopHeightFeet = null)
        {
            var table = input.DragTable;
            var constants = AtmosphereFor(input.Atmosphere);
            var unitFeet = UnitToFeet(input.RangeUnit);
            const double stepUnits = 1;
            var stepFeet = stepUnits * unitFeet;
            var printStep = Math.Max(input.PrintInterval, 1);
            var run = new TrajectoryRun();
            var sortedOutputRanges = outputRanges?
                .Where(range => BallisticMath.IsFinite(range) && range >= 0 && range <= stopRange + 1e-9)
                .Distinct()
                .OrderBy(range => range)
                .ToList();
            var outputRangeIndex = 0;

            var vx = input.MuzzleVelocity * Math.Cos(elevationMinutes / 3437.74677);
            var vy = input.MuzzleVelocity * Math.Sin(elevationMinutes / 3437.74677);
            var vz = 0.0;
            var rangeFeet = 0.0;
            var rangeUnits = 0.0;
            var heightFeet = -input.SightHeight / 12.0;
            var deflectionFeet = 0.0;
            var time = 0.0;
            var nextPrintRange = printStep;
            var rangeWind = (22 * input.RangeWindMph) / 15.0;
            var crossWind = (22 * input.CrossWindMph) / 15.0;
            var c3 = (constants.Pir * input.DensityRatio) / input.BallisticCoefficient;

            void PushPoint(double sampleRangeUnits, double sampleHeightFeet, double sampleDeflectionFeet, double sampleVelocityX, double sampleVelocityY, double sampleVelocityZ, double sampleTime)
            {
                run.Points.Add(new TrajectoryPoint
                {
                    Range = sampleRangeUnits,
                    HeightInches = 12 * sampleHeightFeet,
                    DeflectionInches = 12 * sampleDeflectionFeet,
                    Velocity = Math.Sqrt(sampleVelocityX * sampleVelocityX + sampleVelocityY * sampleVelocityY + sampleVelocityZ * sampleVelocityZ),
                    Time = sampleTime,
                    Vx = sampleVelocityX,
                    Vy = sampleVelocityY,
                    Vz = sampleVelocityZ,
                });
            }

            void PushCurrentPoint()
            {
                PushPoint(rangeUnits, heightFeet, deflectionFeet, vx, vy, vz, time);
            }

            void PushInterpolatedPoint(
                double sampleRangeUnits,
                double previousRangeUnits,
                double previousHeightFeet,
                double previousDeflectionFeet,
                double previousVx,
                double previousVy,
                double previousVz,
                double previousTime)
            {
                var t = Math.Abs(rangeUnits - previousRangeUnits) < 1e-12
                    ? 0
                    : (sampleRangeUnits - previousRangeUnits) / (rangeUnits - previousRangeUnits);
                t = Math.Min(Math.Max(t, 0), 1);
                PushPoint(
                    sampleRangeUnits,
                    previousHeightFeet + (heightFeet - previousHeightFeet) * t,
                    previousDeflectionFeet + (deflectionFeet - previousDeflectionFeet) * t,
                    previousVx + (vx - previousVx) * t,
                    previousVy + (vy - previousVy) * t,
                    previousVz + (vz - previousVz) * t,
                    previousTime + (time - previousTime) * t);
            }

            TrajectoryPoint CreateInterpolatedPoint(
                double sampleRangeUnits,
                double previousRangeUnits,
                double previousHeightFeet,
                double previousDeflectionFeet,
                double previousVx,
                double previousVy,
                double previousVz,
                double previousTime)
            {
                var t = Math.Abs(rangeUnits - previousRangeUnits) < 1e-12
                    ? 0
                    : (sampleRangeUnits - previousRangeUnits) / (rangeUnits - previousRangeUnits);
                t = Math.Min(Math.Max(t, 0), 1);
                var sampleHeightFeet = previousHeightFeet + (heightFeet - previousHeightFeet) * t;
                var sampleDeflectionFeet = previousDeflectionFeet + (deflectionFeet - previousDeflectionFeet) * t;
                var sampleVx = previousVx + (vx - previousVx) * t;
                var sampleVy = previousVy + (vy - previousVy) * t;
                var sampleVz = previousVz + (vz - previousVz) * t;
                return new TrajectoryPoint
                {
                    Range = sampleRangeUnits,
                    HeightInches = 12 * sampleHeightFeet,
                    DeflectionInches = 12 * sampleDeflectionFeet,
                    Velocity = Math.Sqrt(sampleVx * sampleVx + sampleVy * sampleVy + sampleVz * sampleVz),
                    Time = previousTime + (time - previousTime) * t,
                    Vx = sampleVx,
                    Vy = sampleVy,
                    Vz = sampleVz,
                };
            }

            Acceleration AccelerationFor(double localVx, double localVy, double localVz, double localHeightFeet)
            {
                var relativeSpeed = Math.Sqrt(Math.Pow(localVx - rangeWind, 2) + localVy * localVy + Math.Pow(localVz - crossWind, 2));
                var localTempF = (input.TemperatureF + 459.67) * Math.Exp((constants.Tk1 + constants.Tk2 * localHeightFeet) * localHeightFeet) - 459.67;
                var soundSpeed = constants.Vv1 * Math.Sqrt(localTempF + 459.67);
                var cd = InterpolateCd(relativeSpeed / soundSpeed, table);
                var drag = (c3 * cd * relativeSpeed * Math.Exp((constants.Rh1 + constants.Rh2 * localHeightFeet) * localHeightFeet)) / localVx;
                return new Acceleration
                {
                    Ax = drag * (localVx - rangeWind),
                    Ay = drag * localVy - G / localVx,
                    Az = drag * (localVz - crossWind),
                };
            }

            if (collectOutput)
            {
                if (sortedOutputRanges == null)
                {
                    PushCurrentPoint();
                }
                else
                {
                    while (outputRangeIndex < sortedOutputRanges.Count && sortedOutputRanges[outputRangeIndex] <= 1e-9)
                    {
                        PushCurrentPoint();
                        outputRangeIndex += 1;
                    }
                }
            }

            try
            {
                while (rangeUnits < stopRange)
                {
                    var a1 = AccelerationFor(vx, vy, vz, heightFeet);
                    var oldVx = vx;
                    var oldVy = vy;
                    var oldVz = vz;
                    var oldHeight = heightFeet;
                    var oldDeflection = deflectionFeet;
                    var oldTime = time;
                    var oldRangeUnits = rangeUnits;

                    var predictedVx = oldVx + a1.Ax * stepFeet;
                    var predictedVy = oldVy + a1.Ay * stepFeet;
                    var predictedVz = oldVz + a1.Az * stepFeet;
                    var previousSpeed = Math.Sqrt(predictedVx * predictedVx + predictedVy * predictedVy + predictedVz * predictedVz);

                    for (var iteration = 0; iteration < MaxCorrectorIterations; iteration += 1)
                    {
                        var a2 = AccelerationFor(predictedVx, predictedVy, predictedVz, oldHeight);
                        predictedVx = oldVx + 0.5 * (a1.Ax + a2.Ax) * stepFeet;
                        predictedVy = oldVy + 0.5 * (a1.Ay + a2.Ay) * stepFeet;
                        predictedVz = oldVz + 0.5 * (a1.Az + a2.Az) * stepFeet;
                        var speed = Math.Sqrt(predictedVx * predictedVx + predictedVy * predictedVy + predictedVz * predictedVz);
                        if (Math.Abs((speed - previousSpeed) / speed) <= Eps)
                        {
                            break;
                        }

                        previousSpeed = speed;
                    }

                    rangeFeet += stepFeet;
                    rangeUnits += stepUnits;
                    heightFeet = oldHeight + ((oldVy + predictedVy) / (oldVx + predictedVx)) * stepFeet;
                    deflectionFeet = oldDeflection + ((oldVz + predictedVz) / (oldVx + predictedVx)) * stepFeet;
                    time = oldTime + (2 * stepFeet) / (oldVx + predictedVx);
                    vx = predictedVx;
                    vy = predictedVy;
                    vz = predictedVz;

                    if (collectOutput && sortedOutputRanges == null && rangeUnits >= nextPrintRange - 1e-9)
                    {
                        PushCurrentPoint();
                        nextPrintRange += printStep;
                    }

                    if (collectOutput && sortedOutputRanges != null)
                    {
                        while (outputRangeIndex < sortedOutputRanges.Count && sortedOutputRanges[outputRangeIndex] <= rangeUnits + 1e-9)
                        {
                            var sampleRange = sortedOutputRanges[outputRangeIndex];
                            if (sampleRange >= oldRangeUnits - 1e-9)
                            {
                                PushInterpolatedPoint(sampleRange, oldRangeUnits, oldHeight, oldDeflection, oldVx, oldVy, oldVz, oldTime);
                            }
                            outputRangeIndex += 1;
                        }
                    }

                    if (descendingStopHeightFeet.HasValue &&
                        oldRangeUnits > 0 &&
                        oldHeight >= descendingStopHeightFeet.Value &&
                        heightFeet <= descendingStopHeightFeet.Value &&
                        heightFeet <= oldHeight)
                    {
                        var denominator = heightFeet - oldHeight;
                        var t = Math.Abs(denominator) < 1e-12
                            ? 1
                            : (descendingStopHeightFeet.Value - oldHeight) / denominator;
                        t = Math.Min(Math.Max(t, 0), 1);
                        var stopRangeUnits = oldRangeUnits + (rangeUnits - oldRangeUnits) * t;
                        var stopPoint = CreateInterpolatedPoint(stopRangeUnits, oldRangeUnits, oldHeight, oldDeflection, oldVx, oldVy, oldVz, oldTime);
                        if (collectOutput && (run.Points.Count == 0 || Math.Abs(run.Points[run.Points.Count - 1].Range - stopPoint.Range) > 1e-9))
                        {
                            run.Points.Add(stopPoint);
                        }
                        run.ReachedStopHeight = true;
                        run.StopHeightRangeUnits = stopRangeUnits;
                        heightFeet = descendingStopHeightFeet.Value;
                        break;
                    }

                    if (rangeFeet < 0 || vx <= 0)
                    {
                        throw new InvalidOperationException("Trajectory cannot reach the specified range.");
                    }
                }
            }
            catch (Exception error)
            {
                run.Warnings.Add(error.Message);
            }

            run.FinalHeightFeet = heightFeet;
            return run;
        }
    }
}
