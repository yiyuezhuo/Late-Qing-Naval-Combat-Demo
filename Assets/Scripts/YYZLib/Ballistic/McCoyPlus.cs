using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace YYZ.Ballistic
{
    public sealed class McCoyPlusDragPreset
    {
        public string Id { get; set; } = string.Empty;

        public string Label { get; set; } = string.Empty;

        public string Source { get; set; } = string.Empty;

        public List<DragPoint> Points { get; set; } = new List<DragPoint>();
    }

    public sealed class McCoyPlusInput
    {
        public string DragName { get; set; } = McCoyPlus.DragPresets()[0].Label;

        public List<DragPoint> DragTable { get; set; } = McCoyPlus.DragPresets()[0].Points;

        public string RangeUnit { get; set; } = "yards";

        public string Atmosphere { get; set; } = "standard";

        public string ProjectileId { get; set; } = "Example projectile";

        public double MuzzleVelocity { get; set; } = 2800;

        public double BallisticCoefficient { get; set; } = 0.3;

        public double MaxRange { get; set; } = McCoyPlus.SweepLimit;

        public double DensityRatio { get; set; } = 1;

        public double TemperatureF { get; set; } = 59;

        public double MatchHeight { get; set; }
    }

    public class McCoyPlusRow
    {
        public double Range { get; set; }

        public double Time { get; set; }

        public double ElevationDegrees { get; set; }

        public double Velocity { get; set; }

        public double FallAngleDegrees { get; set; }

        public List<TrajectoryPoint> Trajectory { get; set; } = new List<TrajectoryPoint>();
    }

    public sealed class McCoyPlusResult
    {
        public List<McCoyPlusRow> Rows { get; set; } = new List<McCoyPlusRow>();

        public List<McCoyPlusRow> ChartRows { get; set; } = new List<McCoyPlusRow>();

        public List<string> Warnings { get; set; } = new List<string>();
    }

    public static class McCoyPlus
    {
        const double RangeStep = 1000;
        public const double SweepLimit = 100000;

        static readonly Lazy<List<McCoyPlusDragPreset>> Presets = new Lazy<List<McCoyPlusDragPreset>>(BuildDragPresets);

        public static List<McCoyPlusDragPreset> DragPresets()
        {
            return Presets.Value.Select(ClonePreset).ToList();
        }

        public static McCoyPlusInput DefaultInput()
        {
            return new McCoyPlusInput();
        }

        public static string DragPresetToText(string presetId)
        {
            var selected = Presets.Value.FirstOrDefault(item => item.Id == presetId) ?? Presets.Value[0];
            return McCoy.DragTableToText(selected.Points);
        }

        public static McCoyPlusResult Calculate(McCoyPlusInput input)
        {
            var source = input ?? new McCoyPlusInput();
            var rows = new List<McCoyPlusRow>();
            var warnings = new List<string>();
            var dragTable = source.DragTable != null && source.DragTable.Count >= 2 ? source.DragTable : Presets.Value[0].Points;

            foreach (var targetRange in SweepTargets(source.MaxRange))
            {
                var solved = SolveRow(source, dragTable, targetRange);

                if (solved.Row == null)
                {
                    warnings.Add(solved.Warning);
                    break;
                }

                rows.Add(solved.Row);
            }

            return BuildResult(source, rows, warnings);
        }

        public static McCoyPlusResult CalculateParallel(McCoyPlusInput input, int workerCount = 8)
        {
            var source = input ?? new McCoyPlusInput();
            var targetRanges = SweepTargets(source.MaxRange).ToList();
            var dragTable = source.DragTable != null && source.DragTable.Count >= 2 ? source.DragTable : Presets.Value[0].Points;
            var solvedRows = new SolvedRow[targetRanges.Count];
            var workers = Math.Max(1, Math.Min(workerCount, 64));

            Parallel.For(0, targetRanges.Count, new ParallelOptions { MaxDegreeOfParallelism = workers }, index =>
            {
                solvedRows[index] = SolveRow(source, dragTable, targetRanges[index]);
            });

            var rows = new List<McCoyPlusRow>();
            var warnings = new List<string>();
            foreach (var solved in solvedRows)
            {
                if (solved.Row == null)
                {
                    warnings.Add(solved.Warning);
                    break;
                }

                rows.Add(solved.Row);
            }

            return BuildResult(source, rows, warnings);
        }

        static McCoyPlusResult BuildResult(McCoyPlusInput source, List<McCoyPlusRow> rows, List<string> warnings)
        {
            if (rows.Count > 0 && rows[rows.Count - 1].Range >= SweepLimit)
            {
                warnings.Add($"Stopped at {BallisticText.ToJsString(SweepLimit)} {source.RangeUnit}: safety sweep limit reached.");
            }

            return new McCoyPlusResult
            {
                Rows = rows,
                ChartRows = ChartRows(rows),
                Warnings = BallisticCollections.DistinctStrings(warnings),
            };
        }

        sealed class SolvedRow
        {
            public McCoyPlusRow Row;
            public string Warning;
        }

        static SolvedRow SolveRow(McCoyPlusInput source, List<DragPoint> dragTable, double targetRange)
        {
            var printInterval = Math.Max(1, Math.Floor(targetRange / 100));
            var targetInput = ToMcCoyInput(source, dragTable, targetRange, printInterval);
            var result = McCoy.Calculate(targetInput, false);
            var point = LastPointAtRange(result.Points, targetRange);
            var angle = point != null ? FallAngleDegrees(point) : double.NaN;

            if (point != null &&
                result.Warnings.Count == 0 &&
                BallisticMath.IsFinite(result.AdjustedElevationMinutes) &&
                BallisticMath.IsFinite(point.Time) &&
                BallisticMath.IsFinite(point.Velocity) &&
                BallisticMath.IsFinite(angle))
            {
                return new SolvedRow
                {
                    Row = new McCoyPlusRow
                    {
                        Range = targetRange,
                        Time = point.Time,
                        ElevationDegrees = result.AdjustedElevationMinutes / 60,
                        Velocity = point.Velocity,
                        FallAngleDegrees = angle,
                        Trajectory = result.Points,
                    },
                };
            }

            var reason = result.Warnings.Count > 0 ? string.Join("; ", result.Warnings) : "solver did not return a finite target-range solution";
            return new SolvedRow
            {
                Warning = $"Stopped at {BallisticText.ToJsString(targetRange)} {source.RangeUnit}: {reason}.",
            };
        }

        static McCoyInput ToMcCoyInput(McCoyPlusInput input, List<DragPoint> dragTable, double targetRange, double printInterval)
        {
            return new McCoyInput
            {
                DragName = input.DragName,
                DragTable = dragTable,
                RangeUnit = input.RangeUnit,
                Atmosphere = input.Atmosphere,
                ProjectileId = input.ProjectileId,
                MuzzleVelocity = input.MuzzleVelocity,
                BallisticCoefficient = input.BallisticCoefficient,
                SightHeight = 0,
                ElevationMinutes = new McCoyInput().ElevationMinutes,
                DensityRatio = input.DensityRatio,
                TemperatureF = input.TemperatureF,
                PrintInterval = printInterval,
                MaxRange = targetRange,
                RangeWindMph = 0,
                CrossWindMph = 0,
                MatchRange = targetRange,
                MatchHeight = input.MatchHeight,
            };
        }

        static TrajectoryPoint LastPointAtRange(List<TrajectoryPoint> points, double targetRange)
        {
            if (points == null || points.Count == 0)
            {
                return null;
            }

            var last = points[points.Count - 1];
            return Math.Abs(last.Range - targetRange) > 1e-9 ? null : last;
        }

        static double FallAngleDegrees(TrajectoryPoint point)
        {
            return Math.Atan2(-point.Vy, point.Vx) * 180 / Math.PI;
        }

        static List<T> ChartRows<T>(List<T> rows) where T : McCoyPlusRow
        {
            if (rows.Count <= 3)
            {
                return new List<T>(rows);
            }

            var selected = new List<T>
            {
                rows[0],
                rows[(int)Math.Floor((rows.Count - 1) / 2.0)],
                rows[rows.Count - 1],
            };
            var result = new List<T>();
            foreach (var row in selected)
            {
                if (result.All(candidate => candidate.Range != row.Range))
                {
                    result.Add(row);
                }
            }

            return result;
        }

        internal static List<T> SelectChartRows<T>(List<T> rows) where T : McCoyPlusRow
        {
            return ChartRows(rows);
        }

        static IEnumerable<double> SweepTargets(double maxRange)
        {
            var normalizedMaxRange = Math.Min(Math.Max(maxRange, 1), SweepLimit);
            var targets = new List<double>();
            for (var targetRange = RangeStep; targetRange <= normalizedMaxRange; targetRange += RangeStep)
            {
                targets.Add(targetRange);
            }

            if (targets.Count == 0 || Math.Abs(targets[targets.Count - 1] - normalizedMaxRange) > 1e-9)
            {
                targets.Add(normalizedMaxRange);
            }

            return targets;
        }

        static McCoyPlusDragPreset ClonePreset(McCoyPlusDragPreset preset)
        {
            return new McCoyPlusDragPreset
            {
                Id = preset.Id,
                Label = preset.Label,
                Source = preset.Source,
                Points = preset.Points.Select(point => new DragPoint { Mach = point.Mach, Cd = point.Cd }).ToList(),
            };
        }

        static McCoyPlusDragPreset Preset(string id, string label, string source, string text)
        {
            return new McCoyPlusDragPreset
            {
                Id = id,
                Label = label,
                Source = source,
                Points = McCoy.ParseDragTable(text),
            };
        }

        static List<McCoyPlusDragPreset> BuildDragPresets()
        {
            return new List<McCoyPlusDragPreset>
            {
                Preset("g1", "G1 standard projectile", "References/JBM/mcg1.txt", JbmMcg1),
                Preset("g2", "G2 standard projectile", "References/JBM/mcg2.txt", JbmMcg2),
                Preset("g5", "G5 standard projectile", "References/JBM/mcg5.txt", JbmMcg5),
                Preset("g6", "G6 standard projectile", "References/JBM/mcg6.txt", JbmMcg6),
                Preset("g7", "G7 standard projectile", "References/JBM/mcg7.txt", JbmMcg7),
                Preset("g8", "G8 standard projectile", "References/JBM/mcg8.txt", JbmMcg8),
                Preset("gi", "GI standard projectile", "References/JBM/mcgi.txt", JbmMcgi),
                Preset("gs", "GS sphere, 9/16 inch", "References/JBM/mcgs.txt", JbmMcgs),
                Preset("ra4", "RA4 drag function", "References/JBM/ra4.txt", JbmRa4),
            };
        }

        const string JbmMcg1 = @"0.00 0.2629
0.05 0.2558
0.10 0.2487
0.15 0.2413
0.20 0.2344
0.25 0.2278
0.30 0.2214
0.35 0.2155
0.40 0.2104
0.45 0.2061
0.50 0.2032
0.55 0.2020
0.60 0.2034
0.70 0.2165
0.725 0.2230
0.75 0.2313
0.775 0.2417
0.80 0.2546
0.825 0.2706
0.85 0.2901
0.875 0.3136
0.90 0.3415
0.925 0.3734
0.95 0.4084
0.975 0.4448
1.0 0.4805
1.025 0.5136
1.05 0.5427
1.075 0.5677
1.10 0.5883
1.125 0.6053
1.15 0.6191
1.20 0.6393
1.25 0.6518
1.30 0.6589
1.35 0.6621
1.40 0.6625
1.45 0.6607
1.50 0.6573
1.55 0.6528
1.60 0.6474
1.65 0.6413
1.70 0.6347
1.75 0.6280
1.80 0.6210
1.85 0.6141
1.90 0.6072
1.95 0.6003
2.00 0.5934
2.05 0.5867
2.10 0.5804
2.15 0.5743
2.20 0.5685
2.25 0.5630
2.30 0.5577
2.35 0.5527
2.40 0.5481
2.45 0.5438
2.50 0.5397
2.60 0.5325
2.70 0.5264
2.80 0.5211
2.90 0.5168
3.00 0.5133
3.10 0.5105
3.20 0.5084
3.30 0.5067
3.40 0.5054
3.50 0.5040
3.60 0.5030
3.70 0.5022
3.80 0.5016
3.90 0.5010
4.00 0.5006
4.20 0.4998
4.40 0.4995
4.60 0.4992
4.80 0.4990
5.00 0.4988";

        const string JbmMcg2 = @"0.00 0.2303
0.05 0.2298
0.10 0.2287
0.15 0.2271
0.20 0.2251
0.25 0.2227
0.30 0.2196
0.35 0.2156
0.40 0.2107
0.45 0.2048
0.50 0.1980
0.55 0.1905
0.60 0.1828
0.65 0.1758
0.70 0.1702
0.75 0.1669
0.775 0.1664
0.80 0.1667
0.825 0.1682
0.85 0.1711
0.875 0.1761
0.90 0.1831
0.925 0.2004
0.95 0.2589
0.975 0.3492
1.0 0.3983
1.025 0.4075
1.05 0.4103
1.075 0.4114
1.10 0.4106
1.125 0.4089
1.15 0.4068
1.175 0.4046
1.20 0.4021
1.25 0.3966
1.30 0.3904
1.35 0.3835
1.40 0.3759
1.45 0.3678
1.50 0.3594
1.55 0.3512
1.60 0.3432
1.65 0.3356
1.70 0.3282
1.75 0.3213
1.80 0.3149
1.85 0.3089
1.90 0.3033
1.95 0.2982
2.00 0.2933
2.05 0.2889
2.10 0.2846
2.15 0.2806
2.20 0.2768
2.25 0.2731
2.30 0.2696
2.35 0.2663
2.40 0.2632
2.45 0.2602
2.50 0.2572
2.55 0.2543
2.60 0.2515
2.65 0.2487
2.70 0.2460
2.75 0.2433
2.80 0.2408
2.85 0.2382
2.90 0.2357
2.95 0.2333
3.00 0.2309
3.10 0.2262
3.20 0.2217
3.30 0.2173
3.40 0.2132
3.50 0.2091
3.60 0.2052
3.70 0.2014
3.80 0.1978
3.90 0.1944
4.00 0.1912
4.20 0.1851
4.40 0.1794
4.60 0.1741
4.80 0.1693
5.00 0.1648";

        const string JbmMcg5 = @"0.00 0.1710
0.05 0.1719
0.10 0.1727
0.15 0.1732
0.20 0.1734
0.25 0.1730
0.30 0.1718
0.35 0.1696
0.40 0.1668
0.45 0.1637
0.50 0.1603
0.55 0.1566
0.60 0.1529
0.65 0.1497
0.70 0.1473
0.75 0.1463
0.80 0.1489
0.85 0.1583
0.875 0.1672
0.90 0.1815
0.925 0.2051
0.95 0.2413
0.975 0.2884
1.0 0.3379
1.025 0.3785
1.05 0.4032
1.075 0.4147
1.10 0.4201
1.15 0.4278
1.20 0.4338
1.25 0.4373
1.30 0.4392
1.35 0.4403
1.40 0.4406
1.45 0.4401
1.50 0.4386
1.55 0.4362
1.60 0.4328
1.65 0.4286
1.70 0.4237
1.75 0.4182
1.80 0.4121
1.85 0.4057
1.90 0.3991
1.95 0.3926
2.00 0.3861
2.05 0.3800
2.10 0.3741
2.15 0.3684
2.20 0.3630
2.25 0.3578
2.30 0.3529
2.35 0.3481
2.40 0.3435
2.45 0.3391
2.50 0.3349
2.60 0.3269
2.70 0.3194
2.80 0.3125
2.90 0.3060
3.00 0.2999
3.10 0.2942
3.20 0.2889
3.30 0.2838
3.40 0.2790
3.50 0.2745
3.60 0.2703
3.70 0.2662
3.80 0.2624
3.90 0.2588
4.00 0.2553
4.20 0.2488
4.40 0.2429
4.60 0.2376
4.80 0.2326
5.00 0.2280";

        const string JbmMcg6 = @"0.00 0.2617
0.05 0.2553
0.10 0.2491
0.15 0.2432
0.20 0.2376
0.25 0.2324
0.30 0.2278
0.35 0.2238
0.40 0.2205
0.45 0.2177
0.50 0.2155
0.55 0.2138
0.60 0.2126
0.65 0.2121
0.70 0.2122
0.75 0.2132
0.80 0.2154
0.85 0.2194
0.875 0.2229
0.90 0.2297
0.925 0.2449
0.95 0.2732
0.975 0.3141
1.0 0.3597
1.025 0.3994
1.05 0.4261
1.075 0.4402
1.10 0.4465
1.125 0.4490
1.15 0.4497
1.175 0.4494
1.20 0.4482
1.225 0.4464
1.25 0.4441
1.30 0.4390
1.35 0.4336
1.40 0.4279
1.45 0.4221
1.50 0.4162
1.55 0.4102
1.60 0.4042
1.65 0.3981
1.70 0.3919
1.75 0.3855
1.80 0.3788
1.85 0.3721
1.90 0.3652
1.95 0.3583
2.00 0.3515
2.05 0.3447
2.10 0.3381
2.15 0.3314
2.20 0.3249
2.25 0.3185
2.30 0.3122
2.35 0.3060
2.40 0.3000
2.45 0.2941
2.50 0.2883
2.60 0.2772
2.70 0.2668
2.80 0.2574
2.90 0.2487
3.00 0.2407
3.10 0.2333
3.20 0.2265
3.30 0.2202
3.40 0.2144
3.50 0.2089
3.60 0.2039
3.70 0.1991
3.80 0.1947
3.90 0.1905
4.00 0.1866
4.20 0.1794
4.40 0.1730
4.60 0.1673
4.80 0.1621
5.00 0.1574";

        const string JbmMcg7 = @"0.00 0.1198
0.05 0.1197
0.10 0.1196
0.15 0.1194
0.20 0.1193
0.25 0.1194
0.30 0.1194
0.35 0.1194
0.40 0.1193
0.45 0.1193
0.50 0.1194
0.55 0.1193
0.60 0.1194
0.65 0.1197
0.70 0.1202
0.725 0.1207
0.75 0.1215
0.775 0.1226
0.80 0.1242
0.825 0.1266
0.85 0.1306
0.875 0.1368
0.90 0.1464
0.925 0.1660
0.95 0.2054
0.975 0.2993
1.0 0.3803
1.025 0.4015
1.05 0.4043
1.075 0.4034
1.10 0.4014
1.125 0.3987
1.15 0.3955
1.20 0.3884
1.25 0.3810
1.30 0.3732
1.35 0.3657
1.40 0.3580
1.50 0.3440
1.55 0.3376
1.60 0.3315
1.65 0.3260
1.70 0.3209
1.75 0.3160
1.80 0.3117
1.85 0.3078
1.90 0.3042
1.95 0.3010
2.00 0.2980
2.05 0.2951
2.10 0.2922
2.15 0.2892
2.20 0.2864
2.25 0.2835
2.30 0.2807
2.35 0.2779
2.40 0.2752
2.45 0.2725
2.50 0.2697
2.55 0.2670
2.60 0.2643
2.65 0.2615
2.70 0.2588
2.75 0.2561
2.80 0.2533
2.85 0.2506
2.90 0.2479
2.95 0.2451
3.00 0.2424
3.10 0.2368
3.20 0.2313
3.30 0.2258
3.40 0.2205
3.50 0.2154
3.60 0.2106
3.70 0.2060
3.80 0.2017
3.90 0.1975
4.00 0.1935
4.20 0.1861
4.40 0.1793
4.60 0.1730
4.80 0.1672
5.00 0.1618";

        const string JbmMcg8 = @"0.00 0.2105
0.05 0.2105
0.10 0.2104
0.15 0.2104
0.20 0.2103
0.25 0.2103
0.30 0.2103
0.35 0.2103
0.40 0.2103
0.45 0.2102
0.50 0.2102
0.55 0.2102
0.60 0.2102
0.65 0.2102
0.70 0.2103
0.75 0.2103
0.80 0.2104
0.825 0.2104
0.85 0.2105
0.875 0.2106
0.90 0.2109
0.925 0.2183
0.95 0.2571
0.975 0.3358
1.0 0.4068
1.025 0.4378
1.05 0.4476
1.075 0.4493
1.10 0.4477
1.125 0.4450
1.15 0.4419
1.20 0.4353
1.25 0.4283
1.30 0.4208
1.35 0.4133
1.40 0.4059
1.45 0.3986
1.50 0.3915
1.55 0.3845
1.60 0.3777
1.65 0.3710
1.70 0.3645
1.75 0.3581
1.80 0.3519
1.85 0.3458
1.90 0.3400
1.95 0.3343
2.00 0.3288
2.05 0.3234
2.10 0.3182
2.15 0.3131
2.20 0.3081
2.25 0.3032
2.30 0.2983
2.35 0.2937
2.40 0.2891
2.45 0.2845
2.50 0.2802
2.60 0.2720
2.70 0.2642
2.80 0.2569
2.90 0.2499
3.00 0.2432
3.10 0.2368
3.20 0.2308
3.30 0.2251
3.40 0.2197
3.50 0.2147
3.60 0.2101
3.70 0.2058
3.80 0.2019
3.90 0.1983
4.00 0.1950
4.20 0.1890
4.40 0.1837
4.60 0.1791
4.80 0.1750
5.00 0.1713";

        const string JbmMcgi = @"0.00 0.2282
0.05 0.2282
0.10 0.2282
0.15 0.2282
0.20 0.2282
0.25 0.2282
0.30 0.2282
0.35 0.2282
0.40 0.2282
0.45 0.2282
0.50 0.2282
0.55 0.2282
0.60 0.2282
0.65 0.2282
0.70 0.2282
0.725 0.2353
0.75 0.2434
0.775 0.2515
0.80 0.2596
0.825 0.2677
0.85 0.2759
0.875 0.2913
0.90 0.3170
0.925 0.3442
0.95 0.3728
1.0 0.4349
1.05 0.5034
1.075 0.5402
1.10 0.5756
1.125 0.5887
1.15 0.6018
1.175 0.6149
1.20 0.6279
1.225 0.6418
1.25 0.6423
1.30 0.6423
1.35 0.6423
1.40 0.6423
1.45 0.6423
1.50 0.6423
1.55 0.6423
1.60 0.6423
1.625 0.6407
1.65 0.6378
1.70 0.6321
1.75 0.6266
1.80 0.6213
1.85 0.6163
1.90 0.6113
1.95 0.6066
2.00 0.6020
2.05 0.5976
2.10 0.5933
2.15 0.5891
2.20 0.5850
2.25 0.5811
2.30 0.5773
2.35 0.5733
2.40 0.5679
2.45 0.5626
2.50 0.5576
2.60 0.5478
2.70 0.5386
2.80 0.5298
2.90 0.5215
3.00 0.5136
3.10 0.5061
3.20 0.4989
3.30 0.4921
3.40 0.4855
3.50 0.4792
3.60 0.4732
3.70 0.4674
3.80 0.4618
3.90 0.4564
4.00 0.4513
4.20 0.4415
4.40 0.4323
4.60 0.4238
4.80 0.4157
5.00 0.4082";

        const string JbmMcgs = @"0.00 0.4662
0.05 0.4689
0.10 0.4717
0.15 0.4745
0.20 0.4772
0.25 0.4800
0.30 0.4827
0.35 0.4852
0.40 0.4882
0.45 0.4920
0.50 0.4970
0.55 0.5080
0.60 0.5260
0.65 0.5590
0.70 0.5920
0.75 0.6258
0.80 0.6610
0.85 0.6985
0.90 0.7370
0.95 0.7757
1.0 0.8140
1.05 0.8512
1.10 0.8870
1.15 0.9210
1.20 0.9510
1.25 0.9740
1.30 0.9910
1.35 0.9990
1.40 1.0030
1.45 1.0060
1.50 1.0080
1.55 1.0090
1.60 1.0090
1.65 1.0090
1.70 1.0090
1.75 1.0080
1.80 1.0070
1.85 1.0060
1.90 1.0040
1.95 1.0025
2.00 1.0010
2.05 0.9990
2.10 0.9970
2.15 0.9956
2.20 0.9940
2.25 0.9916
2.30 0.9890
2.35 0.9869
2.40 0.9850
2.45 0.9830
2.50 0.9810
2.55 0.9790
2.60 0.9770
2.65 0.9750
2.70 0.9730
2.75 0.9710
2.80 0.9690
2.85 0.9670
2.90 0.9650
2.95 0.9630
3.00 0.9610
3.05 0.9589
3.10 0.9570
3.15 0.9555
3.20 0.9540
3.25 0.9520
3.30 0.9500
3.35 0.9485
3.40 0.9470
3.45 0.9450
3.50 0.9430
3.55 0.9414
3.60 0.9400
3.65 0.9385
3.70 0.9370
3.75 0.9355
3.80 0.9340
3.85 0.9325
3.90 0.9310
3.95 0.9295
4.00 0.9280";

        const string JbmRa4 = @"0.000, 0.2283
0.050, 0.2283
0.100, 0.2282
0.150, 0.2281
0.200, 0.2281
0.250, 0.2281
0.300, 0.2281
0.350, 0.2281
0.400, 0.2281
0.450, 0.2281
0.500, 0.2281
0.550, 0.2281
0.600, 0.2281
0.650, 0.2281
0.700, 0.2288
0.725, 0.2296
0.750, 0.2307
0.775, 0.2320
0.800, 0.2334
0.825, 0.2359
0.850, 0.2389
0.875, 0.2480
0.900, 0.2604
0.925, 0.2819
0.950, 0.3111
0.975, 0.3496
1.000, 0.3975
1.025, 0.4530
1.050, 0.5010
1.075, 0.5476
1.100, 0.5719
1.125, 0.5895
1.150, 0.5943
1.175, 0.5933
1.200, 0.5881
1.225, 0.5810
1.250, 0.5736
1.275, 0.5690
1.300, 0.5651
1.325, 0.5629
1.350, 0.5609
1.375, 0.5591
1.400, 0.5575
1.425, 0.5558
1.450, 0.5543
1.475, 0.5527
1.500, 0.5513
1.525, 0.5499
1.550, 0.5485
1.575, 0.5472
1.600, 0.5460
1.625, 0.5449
1.650, 0.5438
1.675, 0.5428
1.700, 0.5419
1.725, 0.5410
1.750, 0.5401
1.775, 0.5393
1.800, 0.5385
1.825, 0.5377
1.850, 0.5369
1.875, 0.5361
1.900, 0.5354
1.925, 0.5346
1.950, 0.5338
2.000, 0.5323
2.100, 0.5294
2.200, 0.5267
2.300, 0.5240
2.400, 0.5216
2.500, 0.5193
2.600, 0.5170
2.650, 0.5160
2.700, 0.5149
2.800, 0.5129
2.900, 0.5109
3.000, 0.5091
3.100, 0.5074
3.200, 0.5058
3.300, 0.5043
3.400, 0.5029
3.500, 0.5017
3.600, 0.5006
3.700, 0.4995
3.800, 0.4986
3.900, 0.4977
4.000, 0.4969";
    }
}
