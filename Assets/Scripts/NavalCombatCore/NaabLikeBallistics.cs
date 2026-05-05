using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NavalCombatCore
{
    public enum NaabLikeDragFunction
    {
        G1,
        G2,
        G5,
        G6,
        G7,
        G8,
        G9,
        GS,
        GL
    }

    public sealed class NaabLikeProjectile
    {
        public string name = "";
        public float diameterInches = 5f;
        public float totalWeightPounds = 50f;
        public float bodyWeightPounds = 50f;
        public float windscreenWeightPounds;
        public float apCapWeightPounds;

        /// <summary>
        /// ProjInfo cap family selector:
        /// 0 - None
        /// 1 - Hard cap
        /// 2 - Medium cap
        /// 3 - Soft cap
        /// 4 - Hood
        /// </summary>
        public int hcwclcrCapType;
        
        // Former nation/shellClass selector for windscreen/AP-cap NBL addends: <=3in -> 1;
        // nation 1 -> 0.75; nation 2 class 16 -> 0.05; nation 3 class 13-16,
        // nation 4 class 8, and nation 5 class 7-12 -> 0.33; otherwise -> 1.
        public float windscreenNblAddendMultiplier = 1f;
        public float highObliquityWindscreenNblAddendMultiplier = 0.1f; // Former nation 6 class 8/9 branch above 45 deg.
        public float highObliquityThresholdDeg;

        public float muzzleVelocityFeetPerSecond = 3000f;
        public float maxRangeYards = 22600f;
        public float maxElevationDeg = 20f;
        public NaabLikeDragFunction dragFunction = NaabLikeDragFunction.G5;
        public float ballisticCoefficient = 1.9307f;
        public float dragCoefficientAdjust = 14f;
        public float effectiveShellQuality = 0.575f;

        public static NaabLikeProjectile CreateDefaultMetaProjectile()
        {
            return new NaabLikeProjectile
            {
                name = "Palliser Chilled Cast Iron Shot (GB) 6''",
                diameterInches = 6f,
                totalWeightPounds = 100f,
                bodyWeightPounds = 100f,
                windscreenWeightPounds = 0f,
                apCapWeightPounds = 0f,
                hcwclcrCapType = 0,
                windscreenNblAddendMultiplier = 0.75f,
                highObliquityWindscreenNblAddendMultiplier = 0.1f,
                highObliquityThresholdDeg = 0f,
                muzzleVelocityFeetPerSecond = 2230f,
                maxRangeYards = 14600f,
                maxElevationDeg = 20f,
                dragFunction = NaabLikeDragFunction.G1,
                ballisticCoefficient = 2.9727f,
                dragCoefficientAdjust = 0f,
                effectiveShellQuality = 0.575f
            };
        }

        public NaabLikeProjectile Clone()
        {
            return (NaabLikeProjectile)MemberwiseClone();
        }
    }

    public sealed class NaabLikeModelOptions
    {
        public bool enableNaabLikeAdaptation = true;

        public NaabLikeModelOptions Clone()
        {
            return (NaabLikeModelOptions)MemberwiseClone();
        }
    }

    public sealed class NaabLikeArmorInput
    {
        public float quality = 0.95f;
        public float elongationPercent = 22f;
        public float bhn = 235f;
        public float inclinedDeg;
    }

    public sealed class NaabLikeTrajectoryPoint
    {
        public float rangeYards;
        public float timeSeconds;
        public float speedFeetPerSecond;
        public float angleOfFallDeg;
        public float heightFeet;
    }

    public sealed class NaabLikeBallisticsResult
    {
        public bool success;
        public string failureReason;
        public float elevationDeg;
        public float rangeYards;
        public float timeOfFlightSeconds;
        public float impactVelocityFeetPerSecond;
        public float angleOfFallDeg;
        public float horizontalPenetrationInches;
        public float verticalPenetrationInches;
        public readonly List<NaabLikeTrajectoryPoint> trajectory = new();
    }

    public sealed class NaabLikeBallisticsData
    {
        public readonly Dictionary<NaabLikeDragFunction, NaabLikeDragTable> dragTables = new();
        public NaabLikeTerminalTables terminalTables;

        public static NaabLikeBallisticsData LoadEmbedded()
        {
            var data = new NaabLikeBallisticsData();
            AddDragTable(data, NaabLikeDragFunction.G1, NaabLikeEmbeddedData.DragG1);
            AddDragTable(data, NaabLikeDragFunction.G2, NaabLikeEmbeddedData.DragG2);
            AddDragTable(data, NaabLikeDragFunction.G5, NaabLikeEmbeddedData.DragG5);
            AddDragTable(data, NaabLikeDragFunction.G6, NaabLikeEmbeddedData.DragG6);
            AddDragTable(data, NaabLikeDragFunction.G7, NaabLikeEmbeddedData.DragG7);
            AddDragTable(data, NaabLikeDragFunction.G8, NaabLikeEmbeddedData.DragG8);
            AddDragTable(data, NaabLikeDragFunction.G9, NaabLikeEmbeddedData.DragG9);
            AddDragTable(data, NaabLikeDragFunction.GS, NaabLikeEmbeddedData.DragGS);
            AddDragTable(data, NaabLikeDragFunction.GL, NaabLikeEmbeddedData.DragGL);
            data.terminalTables = new NaabLikeTerminalTables
            {
                tdLowerBounds = NaabLikeEmbeddedData.TdLowerBounds,
                tdUpperBounds = NaabLikeEmbeddedData.TdUpperBounds,
                windscreenMatrix = NaabLikeEmbeddedData.WindscreenMatrix,
                windscreenMatrixTail = NaabLikeEmbeddedData.WindscreenMatrixTail,
                obliquityReferenceVector = NaabLikeEmbeddedData.ObliquityReferenceVector,
                highObliquityReferenceMatrix = NaabLikeEmbeddedData.HighObliquityReferenceMatrix,
                tdDecisionBreaks = NaabLikeEmbeddedData.TdDecisionBreaks,
                tdBaseVelocityCoefficients = NaabLikeEmbeddedData.TdDecisionParamA,
                tdThicknessPowerExponents = NaabLikeEmbeddedData.TdDecisionParamB,
                tdShapeSineAmplitudes = NaabLikeEmbeddedData.TdDecisionParamC,
                tdShapeSineFrequencies = NaabLikeEmbeddedData.TdDecisionParamD,
                tdShapeSinePhaseDegs = NaabLikeEmbeddedData.TdDecisionParamE,
                modeSharedMidOb = NaabLikeEmbeddedData.ModeSharedMidOb,
                mode1LowOb = NaabLikeEmbeddedData.Mode1LowOb,
                mode2LowOb = NaabLikeEmbeddedData.Mode2LowOb,
                armorHardnessProfile = NaabLikeEmbeddedData.ArmorHardnessProfile
            };
            return data;
        }

        static void AddDragTable(NaabLikeBallisticsData data, NaabLikeDragFunction dragFunction, float[] coefficients)
        {
            data.dragTables[dragFunction] = new NaabLikeDragTable(
                dragFunction.ToString(),
                NaabLikeEmbeddedData.DragSpeedFeetPerSecond,
                NaabLikeEmbeddedData.DragMach,
                coefficients);
        }
    }

    public sealed class NaabLikeDragTable
    {
        public readonly string name;
        readonly float[] speedFeetPerSecond;
        readonly float[] mach;
        readonly float[] cd;

        public NaabLikeDragTable(string name, float[] speedFeetPerSecond, float[] mach, float[] cd)
        {
            this.name = name;
            this.speedFeetPerSecond = speedFeetPerSecond;
            this.mach = mach;
            this.cd = cd;
        }

        public float CoefficientFromMach(float value, bool useCurvedInterpolation = true)
        {
            if (mach.Length == 0)
                return 0f;
            if (value <= mach[0])
                return cd[0];
            if (value >= mach[^1])
                return cd[^1];

            var idx = Array.BinarySearch(mach, value);
            if (idx >= 0)
                return cd[idx];
            var lower = ~idx - 1;
            return useCurvedInterpolation
                ? CurvedInterpolate(mach, cd, lower, value)
                : LinearInterpolate(mach, cd, lower, value);
        }

        static float CurvedInterpolate(float[] xs, float[] ys, int lower, float x)
        {
            if (lower < 0)
                return ys[0];
            if (lower >= xs.Length - 1)
                return ys[^1];

            var linear = LinearInterpolate(xs, ys, lower, x);
            if (lower >= xs.Length - 2)
                return linear;

            var x0 = xs[lower];
            var x1 = xs[lower + 1];
            var x2 = xs[lower + 2];
            var y0 = ys[lower];
            var y1 = ys[lower + 1];
            var y2 = ys[lower + 2];
            var h0 = x1 - x0;
            var h1 = x2 - x1;
            if (MathF.Abs(h0) < 1e-9f || MathF.Abs(h1) < 1e-9f)
                return linear;

            var dy0 = y1 - y0;
            var dy1 = y2 - y1;
            var slope0 = dy0 / h0;
            var slope1 = dy1 / h1;
            if (MathF.Abs(slope1) > MathF.Abs(10f * slope0) || MathF.Abs(slope0) > MathF.Abs(10f * slope1))
                return linear;

            var t = (x - x0) / h0;
            var correction = t * (t - 1f) * ((dy1 * h0 / h1) - dy0) * 0.5f;
            var curved = linear + correction;
            if (curved < linear)
                curved = (curved + linear) * 0.5f;
            return curved;
        }

        static float LinearInterpolate(float[] xs, float[] ys, int lower, float x)
        {
            var x0 = xs[lower];
            var x1 = xs[lower + 1];
            var y0 = ys[lower];
            var y1 = ys[lower + 1];
            if (MathF.Abs(x1 - x0) < 1e-9f)
                return y0;
            return ((x1 - x) * y0 + (x - x0) * y1) / (x1 - x0);
        }
    }

    public sealed class NaabLikeTerminalTables
    {
        public float[] tdLowerBounds;
        public float[] tdUpperBounds;
        public float[][] windscreenMatrix;
        public float[] windscreenMatrixTail;
        public float[] obliquityReferenceVector;
        public float[][] highObliquityReferenceMatrix;
        public float[] tdDecisionBreaks;
        public float[] tdBaseVelocityCoefficients;
        public float[] tdThicknessPowerExponents;
        public float[] tdShapeSineAmplitudes;
        public float[] tdShapeSineFrequencies;
        public float[] tdShapeSinePhaseDegs;
        public float[] modeSharedMidOb;
        public float[] mode1LowOb;
        public float[] mode2LowOb;
        public float[] armorHardnessProfile;
    }

    public sealed class NaabLikeExteriorBallisticsSolver
    {
        const float FeetToMeters = 0.3048f;
        const float MetersToFeet = 1f / FeetToMeters;
        const float Pre1962DragScaleFeet = 0.0002048757f;
        const float ExeDragAdjustRangeScaleFeet = 600000f;
        const float ExeMinBallisticCoefficient = 0.01f;
        const float Pre1962SoundScaleFeetPerSecond = 49.19f;
        const float Pre1962TempCoeff0 = -6.015e-06f;
        const float Pre1962TempCoeff1 = 0f;
        const float Pre1962DensityCoeff0 = -3.158e-05f;
        const float Pre1962DensityCoeff1 = 0f;
        const float Pre1962Epsilon = 1.0e-10f;
        const double EarthRadiusMeters = 6378135.0;
        const double EarthMu = 3.989411596224e14;

        readonly NaabLikeDragTable dragTable;
        readonly NaabLikeProjectile projectile;
        readonly NaabLikeModelOptions options;
        readonly float dxFeet;
        float dragAdjustMachLeOneRangeFeet;
        float dragAdjustMachGtOneRangeFeet;

        public NaabLikeExteriorBallisticsSolver(
            NaabLikeDragTable dragTable,
            NaabLikeProjectile projectile,
            float dxFeet = 3f,
            NaabLikeModelOptions options = null)
        {
            this.dragTable = dragTable;
            this.projectile = projectile;
            this.dxFeet = dxFeet;
            this.options = options?.Clone() ?? new NaabLikeModelOptions();
        }

        public NaabLikeBallisticsResult SolveToGround(float elevationDeg, float maxRangeYards, float sampleStepYards)
        {
            ResetDragAdjustState();
            var state = BuildInitialState(projectile.muzzleVelocityFeetPerSecond, elevationDeg);
            var result = new NaabLikeBallisticsResult
            {
                success = false,
                elevationDeg = elevationDeg
            };

            result.trajectory.Add(ToTrajectoryPoint(state));
            var nextSampleFeet = MathF.Max(sampleStepYards, 1f) * 3f;
            var maxRangeFeet = MathF.Max(maxRangeYards, 1f) * 3f;
            var previous = state;
            while (state.xFeet <= maxRangeFeet && state.timeSeconds <= 300f && state.vxFeetPerSecond > 1e-6f)
            {
                previous = state;
                state = Step(state);
                if (state.xFeet >= nextSampleFeet)
                {
                    result.trajectory.Add(ToTrajectoryPoint(state));
                    nextSampleFeet += MathF.Max(sampleStepYards, 1f) * 3f;
                }

                if (state.yFeet <= 0f && state.xFeet > dxFeet)
                {
                    var impact = InterpolateState(previous, state, previous.yFeet / MathF.Max(0.0001f, previous.yFeet - state.yFeet));
                    result.success = true;
                    result.rangeYards = impact.xFeet / 3f;
                    result.timeOfFlightSeconds = impact.timeSeconds;
                    result.impactVelocityFeetPerSecond = Speed(impact);
                    result.angleOfFallDeg = MathF.Max(RadiansToDegrees(MathF.Atan2(-impact.vyFeetPerSecond, impact.vxFeetPerSecond)), 0f);
                    if (result.trajectory.Count == 0 || MathF.Abs(result.trajectory[^1].rangeYards - result.rangeYards) > 0.1f)
                        result.trajectory.Add(ToTrajectoryPoint(impact));
                    return result;
                }
            }

            result.failureReason = "Projectile did not reach ground before the simulation limit.";
            return result;
        }

        public NaabLikeBallisticsResult SolveForTargetRange(float targetRangeYards, float maxElevationDeg, float? angleHintDeg = null)
        {
            var hit = FiringSolutionForRange(projectile.muzzleVelocityFeetPerSecond, targetRangeYards, maxElevationDeg, angleHintDeg);
            if (hit == null)
            {
                return new NaabLikeBallisticsResult
                {
                    success = false,
                    failureReason = "No low-angle firing solution was found.",
                    rangeYards = targetRangeYards
                };
            }

            var trajectory = SolveTrajectoryToRange(hit.elevationDeg, targetRangeYards, MathF.Max(targetRangeYards / 80f, 100f));
            return new NaabLikeBallisticsResult
            {
                success = true,
                elevationDeg = hit.elevationDeg,
                rangeYards = targetRangeYards,
                timeOfFlightSeconds = hit.timeSeconds,
                impactVelocityFeetPerSecond = hit.speedFeetPerSecond,
                angleOfFallDeg = hit.descentDeg,
                trajectory = { }
            }.WithTrajectory(trajectory);
        }

        public List<NaabLikeBallisticsResult> SolveForTargetRangesParallel(
            IReadOnlyList<float> targetRangeYards,
            float maxElevationDeg,
            int workerCount = 8,
            Action<int, int> progressCallback = null,
            CancellationToken cancellationToken = default)
        {
            if (targetRangeYards == null || targetRangeYards.Count == 0)
                return new List<NaabLikeBallisticsResult>();

            var maxTargetRange = targetRangeYards.Where(range => range > 0f).DefaultIfEmpty(projectile.maxRangeYards).Max();
            var simulationLimitYards = MathF.Max(projectile.maxRangeYards, maxTargetRange * 1.35f + 1000f);
            var sampleStepYards = MathF.Max(simulationLimitYards / 120f, 100f);
            var rangeToleranceYards = MathF.Max(0.5f, maxTargetRange * 0.0001f);
            var maxElevation = Math.Clamp(maxElevationDeg, 0.1f, 89.9f);
            var workers = Math.Clamp(workerCount, 1, 64);
            var cache = new SharedElevationRangeCache();

            var seedCount = Math.Clamp(workers, 2, 16);
            var seedElevations = new List<float>(seedCount);
            for (int i = 0; i < seedCount; i++)
            {
                var t = seedCount == 1 ? 1f : (float)i / (seedCount - 1);
                seedElevations.Add(Lerp(MathF.Min(0.5f, maxElevation), maxElevation, t));
            }

            Parallel.ForEach(seedElevations, new ParallelOptions
            {
                MaxDegreeOfParallelism = workers,
                CancellationToken = cancellationToken
            }, elevation =>
            {
                cache.GetOrAdd(elevation, angle => SolveGroundSample(angle, simulationLimitYards, sampleStepYards));
            });

            var results = new NaabLikeBallisticsResult[targetRangeYards.Count];
            var order = BuildRangeDistributedOrder(targetRangeYards, workers);
            var completed = 0;

            Parallel.ForEach(order, new ParallelOptions
            {
                MaxDegreeOfParallelism = workers,
                CancellationToken = cancellationToken
            }, index =>
            {
                var targetRange = targetRangeYards[index];
                results[index] = SolveTargetRangeFromSharedCache(
                    cache,
                    targetRange,
                    maxElevation,
                    simulationLimitYards,
                    sampleStepYards,
                    rangeToleranceYards,
                    cancellationToken);
                progressCallback?.Invoke(Interlocked.Increment(ref completed), targetRangeYards.Count);
            });

            return results.ToList();
        }

        NaabLikeBallisticsResult SolveTargetRangeFromSharedCache(
            SharedElevationRangeCache cache,
            float targetRangeYards,
            float maxElevationDeg,
            float simulationLimitYards,
            float sampleStepYards,
            float rangeToleranceYards,
            CancellationToken cancellationToken)
        {
            if (targetRangeYards <= 0f)
            {
                return new NaabLikeBallisticsResult
                {
                    success = false,
                    failureReason = "Target range must be greater than 0.",
                    rangeYards = targetRangeYards
                };
            }

            GroundSample bestSample = null;
            for (int iteration = 0; iteration < 48; iteration++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var lowBranch = cache.GetLowAngleBranch();
                if (lowBranch.Count == 0)
                {
                    cache.GetOrAdd(MathF.Min(0.5f, maxElevationDeg), angle => SolveGroundSample(angle, simulationLimitYards, sampleStepYards));
                    continue;
                }

                bestSample = FindNearestRangeSample(lowBranch, targetRangeYards, bestSample);
                if (bestSample?.success == true && MathF.Abs(bestSample.rangeYards - targetRangeYards) <= rangeToleranceYards)
                    return BuildTargetRangeResult(bestSample, targetRangeYards);

                if (TryFindRangeBracket(lowBranch, targetRangeYards, out var lo, out var hi))
                {
                    var span = hi.rangeYards - lo.rangeYards;
                    var t = MathF.Abs(span) < 1e-6f ? 0.5f : (targetRangeYards - lo.rangeYards) / span;
                    var nextElevation = Lerp(lo.elevationDeg, hi.elevationDeg, Math.Clamp(t, 0.05f, 0.95f));
                    if (MathF.Abs(hi.elevationDeg - lo.elevationDeg) <= 1e-5f)
                        break;
                    cache.GetOrAdd(nextElevation, angle => SolveGroundSample(angle, simulationLimitYards, sampleStepYards));
                    continue;
                }

                var first = lowBranch[0];
                var last = lowBranch[^1];
                if (targetRangeYards < first.rangeYards && first.elevationDeg > 1e-4f)
                {
                    cache.GetOrAdd(first.elevationDeg * 0.5f, angle => SolveGroundSample(angle, simulationLimitYards, sampleStepYards));
                    continue;
                }

                if (targetRangeYards > last.rangeYards && last.elevationDeg < maxElevationDeg - 1e-4f)
                {
                    var nextHigher = cache.GetNextHigherElevationSample(last.elevationDeg);
                    var upperElevation = nextHigher?.elevationDeg ?? maxElevationDeg;
                    if (upperElevation <= last.elevationDeg + 1e-4f)
                        break;
                    cache.GetOrAdd((last.elevationDeg + upperElevation) * 0.5f, angle => SolveGroundSample(angle, simulationLimitYards, sampleStepYards));
                    continue;
                }

                break;
            }

            var fallback = NewSolver().SolveForTargetRange(targetRangeYards, maxElevationDeg);
            if (fallback.success)
                return fallback;

            return bestSample?.success == true
                ? new NaabLikeBallisticsResult
                {
                    success = false,
                    failureReason = $"No low-angle firing solution was found. Nearest cached range: {bestSample.rangeYards:0} yd.",
                    rangeYards = targetRangeYards
                }
                : fallback;
        }

        GroundSample SolveGroundSample(float elevationDeg, float simulationLimitYards, float sampleStepYards)
        {
            var solver = NewSolver();
            var result = solver.SolveToGround(elevationDeg, simulationLimitYards, sampleStepYards);
            return new GroundSample
            {
                success = result.success,
                failureReason = result.failureReason,
                elevationDeg = elevationDeg,
                rangeYards = result.rangeYards,
                timeOfFlightSeconds = result.timeOfFlightSeconds,
                impactVelocityFeetPerSecond = result.impactVelocityFeetPerSecond,
                angleOfFallDeg = result.angleOfFallDeg
            };
        }

        NaabLikeBallisticsResult BuildTargetRangeResult(GroundSample sample, float targetRangeYards)
        {
            var trajectory = NewSolver().SolveTrajectoryToRange(sample.elevationDeg, targetRangeYards, MathF.Max(targetRangeYards / 80f, 100f));
            return new NaabLikeBallisticsResult
            {
                success = true,
                elevationDeg = sample.elevationDeg,
                rangeYards = targetRangeYards,
                timeOfFlightSeconds = sample.timeOfFlightSeconds,
                impactVelocityFeetPerSecond = sample.impactVelocityFeetPerSecond,
                angleOfFallDeg = sample.angleOfFallDeg
            }.WithTrajectory(trajectory);
        }

        NaabLikeExteriorBallisticsSolver NewSolver()
        {
            return new NaabLikeExteriorBallisticsSolver(dragTable, projectile.Clone(), dxFeet, options);
        }

        static GroundSample FindNearestRangeSample(List<GroundSample> samples, float targetRangeYards, GroundSample currentBest)
        {
            var best = currentBest;
            var bestError = best == null ? float.PositiveInfinity : MathF.Abs(best.rangeYards - targetRangeYards);
            foreach (var sample in samples)
            {
                var error = MathF.Abs(sample.rangeYards - targetRangeYards);
                if (error < bestError)
                {
                    best = sample;
                    bestError = error;
                }
            }
            return best;
        }

        static bool TryFindRangeBracket(List<GroundSample> lowBranch, float targetRangeYards, out GroundSample lo, out GroundSample hi)
        {
            lo = null;
            hi = null;
            for (int i = 1; i < lowBranch.Count; i++)
            {
                var a = lowBranch[i - 1];
                var b = lowBranch[i];
                if (a.rangeYards <= targetRangeYards && targetRangeYards <= b.rangeYards)
                {
                    lo = a;
                    hi = b;
                    return true;
                }
            }
            return false;
        }

        static List<int> BuildRangeDistributedOrder(IReadOnlyList<float> targetRanges, int workerCount)
        {
            var sorted = Enumerable.Range(0, targetRanges.Count)
                .OrderBy(index => targetRanges[index])
                .ToList();
            var output = new List<int>(targetRanges.Count);
            var used = new HashSet<int>();
            var initialCount = Math.Min(workerCount, sorted.Count);
            for (int i = 0; i < initialCount; i++)
            {
                var t = initialCount == 1 ? 0f : (float)i / (initialCount - 1);
                var sortedIndex = Math.Clamp((int)MathF.Round(t * (sorted.Count - 1)), 0, sorted.Count - 1);
                var index = sorted[sortedIndex];
                if (used.Add(index))
                    output.Add(index);
            }
            foreach (var index in sorted)
            {
                if (used.Add(index))
                    output.Add(index);
            }
            return output;
        }

        sealed class GroundSample
        {
            public bool success;
            public string failureReason;
            public float elevationDeg;
            public float rangeYards;
            public float timeOfFlightSeconds;
            public float impactVelocityFeetPerSecond;
            public float angleOfFallDeg;
        }

        sealed class SharedElevationRangeCache
        {
            readonly object gate = new();
            readonly List<GroundSample> samples = new();
            readonly HashSet<int> inFlightKeys = new();

            public GroundSample GetOrAdd(float elevationDeg, Func<float, GroundSample> factory)
            {
                var normalizedElevation = Math.Clamp(elevationDeg, 0f, 89.9f);
                var key = ElevationKey(normalizedElevation);
                lock (gate)
                {
                    while (inFlightKeys.Contains(key))
                        Monitor.Wait(gate, 10);

                    var existing = FindByKey(key);
                    if (existing != null)
                        return existing;

                    inFlightKeys.Add(key);
                }

                GroundSample created = null;
                try
                {
                    created = factory(normalizedElevation) ?? new GroundSample
                    {
                        success = false,
                        failureReason = "Sampling failed.",
                        elevationDeg = normalizedElevation
                    };
                }
                finally
                {
                    lock (gate)
                    {
                        inFlightKeys.Remove(key);
                        if (created != null && FindByKey(key) == null)
                        {
                            samples.Add(created);
                            samples.Sort((a, b) => a.elevationDeg.CompareTo(b.elevationDeg));
                        }
                        Monitor.PulseAll(gate);
                    }
                }

                return created;
            }

            public List<GroundSample> GetLowAngleBranch()
            {
                lock (gate)
                {
                    var output = new List<GroundSample>();
                    var previousRange = float.NegativeInfinity;
                    foreach (var sample in samples)
                    {
                        if (!sample.success)
                            continue;
                        if (sample.rangeYards <= previousRange + 0.01f)
                            break;
                        output.Add(sample);
                        previousRange = sample.rangeYards;
                    }
                    return output;
                }
            }

            public GroundSample GetNextHigherElevationSample(float elevationDeg)
            {
                lock (gate)
                {
                    foreach (var sample in samples)
                    {
                        if (sample.success && sample.elevationDeg > elevationDeg + 1e-4f)
                            return sample;
                    }
                    return null;
                }
            }

            GroundSample FindByKey(int key)
            {
                foreach (var sample in samples)
                {
                    if (ElevationKey(sample.elevationDeg) == key)
                        return sample;
                }
                return null;
            }

            static int ElevationKey(float elevationDeg) => (int)MathF.Round(elevationDeg * 1000000f);
        }

        public List<NaabLikeTrajectoryPoint> SolveTrajectoryToRange(float elevationDeg, float targetRangeYards, float sampleStepYards)
        {
            ResetDragAdjustState();
            var state = BuildInitialState(projectile.muzzleVelocityFeetPerSecond, elevationDeg);
            var output = new List<NaabLikeTrajectoryPoint> { ToTrajectoryPoint(state) };
            var targetRangeFeet = MathF.Max(targetRangeYards, 0f) * 3f;
            var nextSampleFeet = MathF.Max(sampleStepYards, 1f) * 3f;
            while (state.xFeet <= targetRangeFeet && state.timeSeconds <= 300f && state.vxFeetPerSecond > 1e-6f)
            {
                state = Step(state);
                if (state.xFeet >= nextSampleFeet)
                {
                    output.Add(ToTrajectoryPoint(state));
                    nextSampleFeet += MathF.Max(sampleStepYards, 1f) * 3f;
                }
            }
            if (output.Count == 0 || output[^1].rangeYards < targetRangeYards - 0.1f)
                output.Add(ToTrajectoryPoint(state));
            return output;
        }

        RangeImpactSolution FiringSolutionForRange(float muzzleVelocityFeetPerSecond, float targetRangeYards, float maxElevationDeg, float? angleHintDeg)
        {
            var cache = new Dictionary<float, RangeImpactSolution>();
            RangeImpactSolution Sample(float angle)
            {
                var key = MathF.Round(angle * 1000000f) / 1000000f;
                if (!cache.TryGetValue(key, out var cached))
                {
                    cached = SampleAtRange(muzzleVelocityFeetPerSecond, key, targetRangeYards);
                    cache[key] = cached;
                }
                return cached;
            }

            RangeImpactSolution Refine(float lo, float hi)
            {
                var loSample = Sample(lo);
                var hiSample = Sample(hi);
                if (loSample == null || hiSample == null)
                    return null;
                var loHeight = loSample.heightFeet;
                var hiHeight = hiSample.heightFeet;
                for (int i = 0; i < 36; i++)
                {
                    var mid = (lo + hi) * 0.5f;
                    var midSample = Sample(mid);
                    if (midSample == null)
                        return null;
                    var midHeight = midSample.heightFeet;
                    if (MathF.Abs(midHeight) <= 0.1f)
                        return midSample;
                    if ((loHeight < 0f && midHeight >= 0f) || (loHeight > 0f && midHeight <= 0f))
                    {
                        hi = mid;
                        hiHeight = midHeight;
                    }
                    else
                    {
                        lo = mid;
                        loHeight = midHeight;
                    }
                }
                return Sample((lo + hi) * 0.5f);
            }

            (float lo, float hi)? Scan(float start, float stop, float step)
            {
                float? previousAngle = null;
                float? previousHeight = null;
                for (var angle = start; angle <= stop + 1e-9f; angle += step)
                {
                    var hit = Sample(angle);
                    if (hit == null)
                        continue;
                    var height = hit.heightFeet;
                    if (MathF.Abs(height) <= 0.25f)
                        return (angle, angle);
                    if (previousAngle.HasValue && previousHeight.HasValue &&
                        ((previousHeight.Value < 0f && height >= 0f) || (previousHeight.Value > 0f && height <= 0f)))
                        return (previousAngle.Value, angle);
                    previousAngle = angle;
                    previousHeight = height;
                }
                return null;
            }

            var windows = new List<(float start, float stop, float step)>();
            if (angleHintDeg.HasValue)
            {
                windows.Add((MathF.Max(0f, angleHintDeg.Value - 2f), MathF.Min(maxElevationDeg, angleHintDeg.Value + 4f), 0.5f));
                windows.Add((MathF.Max(0f, angleHintDeg.Value - 6f), MathF.Min(maxElevationDeg, angleHintDeg.Value + 12f), 1f));
            }
            windows.Add((0f, maxElevationDeg, 1f));

            foreach (var window in windows)
            {
                var bracket = Scan(window.start, window.stop, window.step);
                if (!bracket.HasValue)
                    continue;
                if (MathF.Abs(bracket.Value.lo - bracket.Value.hi) < 1e-12f)
                    return Sample(bracket.Value.lo);

                var fineLo = MathF.Max(0f, bracket.Value.lo - 1f);
                var fineHi = MathF.Min(maxElevationDeg, bracket.Value.hi + 1f);
                var fine = Scan(fineLo, fineHi, 0.25f);
                if (fine.HasValue)
                    return MathF.Abs(fine.Value.lo - fine.Value.hi) < 1e-12f ? Sample(fine.Value.lo) : Refine(fine.Value.lo, fine.Value.hi);
                break;
            }

            return null;
        }

        RangeImpactSolution SampleAtRange(float muzzleVelocityFeetPerSecond, float elevationDeg, float targetRangeYards)
        {
            ResetDragAdjustState();
            var targetRangeFeet = targetRangeYards * 3f;
            var state = BuildInitialState(muzzleVelocityFeetPerSecond, elevationDeg);
            while (state.xFeet <= targetRangeFeet && state.timeSeconds <= 300f && state.vxFeetPerSecond > 1e-6f)
            {
                var previous = state;
                state = Step(state);
                if (state.xFeet >= targetRangeFeet)
                {
                    if (state.xFeet <= previous.xFeet)
                        return null;
                    var t = (targetRangeFeet - previous.xFeet) / (state.xFeet - previous.xFeet);
                    var hit = InterpolateState(previous, state, t);
                    return new RangeImpactSolution
                    {
                        targetRangeYards = targetRangeYards,
                        elevationDeg = elevationDeg,
                        timeSeconds = hit.timeSeconds,
                        speedFeetPerSecond = Speed(hit),
                        descentDeg = MathF.Max(RadiansToDegrees(MathF.Atan2(-hit.vyFeetPerSecond, hit.vxFeetPerSecond)), 0f),
                        heightFeet = hit.yFeet
                    };
                }
            }
            return null;
        }

        BallisticState Step(BallisticState point)
        {
            var slope1 = Slopes(point);
            var euler = new BallisticState
            {
                xFeet = point.xFeet + dxFeet,
                yFeet = point.yFeet + dxFeet * slope1.dyDx,
                vxFeetPerSecond = point.vxFeetPerSecond + dxFeet * slope1.dvxDx,
                vyFeetPerSecond = point.vyFeetPerSecond + dxFeet * slope1.dvyDx,
                timeSeconds = point.timeSeconds + dxFeet * slope1.dtDx
            };
            var slope2 = Slopes(euler);
            return new BallisticState
            {
                xFeet = point.xFeet + dxFeet,
                yFeet = point.yFeet + 0.5f * dxFeet * (slope1.dyDx + slope2.dyDx),
                vxFeetPerSecond = point.vxFeetPerSecond + 0.5f * dxFeet * (slope1.dvxDx + slope2.dvxDx),
                vyFeetPerSecond = point.vyFeetPerSecond + 0.5f * dxFeet * (slope1.dvyDx + slope2.dvyDx),
                timeSeconds = point.timeSeconds + 0.5f * dxFeet * (slope1.dtDx + slope2.dtDx)
            };
        }

        (float dyDx, float dvxDx, float dvyDx, float dtDx) Slopes(BallisticState point)
        {
            var speed = Speed(point);
            if (speed < 1e-9f || MathF.Abs(point.vxFeetPerSecond) < 1e-9f)
                return (0f, 0f, 0f, 0f);

            var atmosphere = AtmosphereAtHeight(point.yFeet);
            var mach = speed / atmosphere.soundFeetPerSecond;
            var cdRef = dragTable.CoefficientFromMach(mach, options.enableNaabLikeAdaptation);
            var bcEff = EffectiveBallisticCoefficient(point.xFeet, mach);
            var dragSlope = Pre1962DragScaleFeet * atmosphere.densityRatio * cdRef * speed / bcEff;
            var dvxDx = -dragSlope;
            var dvyDx = dvxDx * (point.vyFeetPerSecond / point.vxFeetPerSecond) - atmosphere.gravityFeetPerSecondSquared / point.vxFeetPerSecond;
            return (point.vyFeetPerSecond / point.vxFeetPerSecond, dvxDx, dvyDx, 1f / point.vxFeetPerSecond);
        }

        float EffectiveBallisticCoefficient(float rangeFeet, float mach)
        {
            var bc = projectile.ballisticCoefficient;
            if (!options.enableNaabLikeAdaptation)
                return bc;

            var adjust = projectile.dragCoefficientAdjust;
            if (MathF.Abs(adjust) < 1e-9f)
                return bc;

            var effectiveRange = rangeFeet > 0f ? rangeFeet : dxFeet;
            var rangeTerm = 0f;
            if (mach <= 1f)
            {
                if (dragAdjustMachLeOneRangeFeet == 0f)
                    dragAdjustMachLeOneRangeFeet = effectiveRange;
            }
            else
            {
                if (dragAdjustMachLeOneRangeFeet > 0f && dragAdjustMachGtOneRangeFeet == 0f)
                    dragAdjustMachGtOneRangeFeet = effectiveRange;
                rangeTerm = MathF.Abs(dragAdjustMachLeOneRangeFeet + dragAdjustMachGtOneRangeFeet - effectiveRange);
            }

            return MathF.Max(ExeMinBallisticCoefficient, bc + adjust * rangeTerm / ExeDragAdjustRangeScaleFeet);
        }

        static (float soundFeetPerSecond, float densityRatio, float gravityFeetPerSecondSquared) AtmosphereAtHeight(float heightFeet)
        {
            var tempRankine = 518.67f * MathF.Exp((Pre1962TempCoeff1 * heightFeet + Pre1962TempCoeff0) * heightFeet) + Pre1962Epsilon;
            var sound = MathF.Sqrt(tempRankine) * Pre1962SoundScaleFeetPerSecond + Pre1962Epsilon;
            var density = MathF.Exp((Pre1962DensityCoeff1 * heightFeet + Pre1962DensityCoeff0) * heightFeet);
            var radius = EarthRadiusMeters + heightFeet * FeetToMeters;
            var gravity = (float)(EarthMu / (radius * radius)) * MetersToFeet;
            return (sound, density, gravity);
        }

        static BallisticState BuildInitialState(float muzzleVelocityFeetPerSecond, float elevationDeg)
        {
            var angleRad = DegreesToRadians(elevationDeg);
            return new BallisticState
            {
                vxFeetPerSecond = muzzleVelocityFeetPerSecond * MathF.Cos(angleRad),
                vyFeetPerSecond = muzzleVelocityFeetPerSecond * MathF.Sin(angleRad)
            };
        }

        void ResetDragAdjustState()
        {
            dragAdjustMachLeOneRangeFeet = 0f;
            dragAdjustMachGtOneRangeFeet = 0f;
        }

        static NaabLikeTrajectoryPoint ToTrajectoryPoint(BallisticState state)
        {
            return new NaabLikeTrajectoryPoint
            {
                rangeYards = state.xFeet / 3f,
                timeSeconds = state.timeSeconds,
                speedFeetPerSecond = Speed(state),
                angleOfFallDeg = MathF.Max(RadiansToDegrees(MathF.Atan2(-state.vyFeetPerSecond, state.vxFeetPerSecond)), 0f),
                heightFeet = state.yFeet
            };
        }

        static BallisticState InterpolateState(BallisticState a, BallisticState b, float t)
        {
            t = Math.Clamp(t, 0f, 1f);
            return new BallisticState
            {
                xFeet = Lerp(a.xFeet, b.xFeet, t),
                yFeet = Lerp(a.yFeet, b.yFeet, t),
                vxFeetPerSecond = Lerp(a.vxFeetPerSecond, b.vxFeetPerSecond, t),
                vyFeetPerSecond = Lerp(a.vyFeetPerSecond, b.vyFeetPerSecond, t),
                timeSeconds = Lerp(a.timeSeconds, b.timeSeconds, t)
            };
        }

        static float Speed(BallisticState state) => MathF.Sqrt(state.vxFeetPerSecond * state.vxFeetPerSecond + state.vyFeetPerSecond * state.vyFeetPerSecond);
        static float Lerp(float a, float b, float t) => a + (b - a) * t;
        static float DegreesToRadians(float degrees) => degrees * MathF.PI / 180f;
        static float RadiansToDegrees(float radians) => radians * 180f / MathF.PI;

        sealed class BallisticState
        {
            public float xFeet;
            public float yFeet;
            public float vxFeetPerSecond;
            public float vyFeetPerSecond;
            public float timeSeconds;
        }

        sealed class RangeImpactSolution
        {
            public float targetRangeYards;
            public float elevationDeg;
            public float timeSeconds;
            public float speedFeetPerSecond;
            public float descentDeg;
            public float heightFeet;
        }
    }

    public sealed class NaabLikeTerminalBallisticsSolver
    {
        const float MinScannedThicknessInches = 0.1f;
        const float ThicknessScanStepInches = 0.02f;
        const float MaxThicknessByDiameterMultiplier = 6f;
        const float MaxThicknessDiameterClearanceInches = 0.1f;
        const float MaxScannedThicknessInches = 199f;
        const float PartialPenetrationVelocityMarginFeetPerSecond = 60f;
        const float NoHolingVelocityMarginFeetPerSecond = 120f;
        const float HighObliquityStartDeg = 45f;
        const float ExtremeObliquityStartDeg = 80f;
        const float RightAngleObliquityDeg = 90f;
        const float LowObliquityReferenceStepDeg = 2.5f;
        const float HighObliquityReferenceStepDeg = 2.5f;
        const float ComponentObliquityTableStepDeg = 5f;
        const float SharedCapObliquityStartDeg = 40f;
        const float SharedCapObliquityEndDeg = 75f;
        const float SharedCapObliquityStepDeg = 2.5f;
        const float ApCapThresholdReferenceObliquityDeg = 45f;
        const float TdTableStep = 0.05f;
        const float HighObliquityTdSaturation = 0.9f;
        const int MaxTdDecisionSegmentIndex = 11;
        const int ComponentTdTailBucket = 16;
        const int HighObliquityTdSaturatedBucket = 17;

        readonly NaabLikeTerminalTables tables;
        readonly NaabLikeProjectile projectile;
        readonly NaabLikeArmorInput armor;
        readonly NaabLikeModelOptions options;

        public NaabLikeTerminalBallisticsSolver(
            NaabLikeTerminalTables tables,
            NaabLikeProjectile projectile,
            NaabLikeArmorInput armor,
            NaabLikeModelOptions options = null)
        {
            this.tables = tables;
            this.projectile = projectile;
            this.armor = armor;
            this.options = options?.Clone() ?? new NaabLikeModelOptions();
        }

        public float CompletePenetrationInches(float strikingVelocityFeetPerSecond, float obliquityDeg)
        {
            return ScanBallisticLimitThicknesses(strikingVelocityFeetPerSecond, obliquityDeg).full;
        }

        public (float full, float partial, float noHoling) ScanBallisticLimitThicknesses(float strikingVelocityFeetPerSecond, float obliquityDeg)
        {
            var upper = MathF.Min(
                MaxThicknessByDiameterMultiplier * projectile.diameterInches - MaxThicknessDiameterClearanceInches,
                MaxScannedThicknessInches);
            var actual = MathF.Max(strikingVelocityFeetPerSecond, 0f);
            var full = 0f;
            var partial = 0f;
            var noHoling = 0f;

            for (var thickness = MinScannedThicknessInches; thickness <= upper + 1e-6f; thickness += ThicknessScanStepInches)
            {
                var trueNbl = TrueNblVelocityFeetPerSecond(thickness, obliquityDeg);
                var actualRounded = MathF.Round(actual);
                var trueRounded = MathF.Round(trueNbl);
                if (full == 0f && actual <= trueNbl)
                    full = thickness;
                if (partial == 0f && actualRounded + PartialPenetrationVelocityMarginFeetPerSecond <= trueRounded)
                    partial = thickness;
                if (actualRounded + NoHolingVelocityMarginFeetPerSecond < trueRounded)
                {
                    noHoling = thickness;
                    break;
                }
            }

            var scale = PenetrationTripletPostScale(obliquityDeg);
            return (full * scale, partial * scale, noHoling * scale);
        }

        float TrueNblVelocityFeetPerSecond(float thicknessInches, float obliquityDeg)
        {
            var diameter = MathF.Max(projectile.diameterInches, 0.1f);
            var td = ClampTd(thicknessInches / diameter);
            var (baseVelocityCoefficient, thicknessPowerExponent, _, _, _) = TdSegmentParameters(td);
            var weightOverD3 = MathF.Max(projectile.totalWeightPounds / (diameter * diameter * diameter), 1e-9f);
            var plateQuality = options.enableNaabLikeAdaptation
                ? EffectivePlateQualityFactor()
                : armor.quality;
            var powerBase = MathF.Max(plateQuality, 0.01f) * td;
            var baseNbl = baseVelocityCoefficient * MathF.Pow(powerBase, thicknessPowerExponent);
            baseNbl *= TdShapeMultiplier(td);
            baseNbl *= ScaleFactor();
            baseNbl /= MathF.Sqrt(weightOverD3);

            var ob = ClampObliquity(obliquityDeg);
            if (ob > 0.1f)
                baseNbl *= ObliquityMultiplier(ob, td);

            baseNbl *= ElongationFactor();
            if (options.enableNaabLikeAdaptation)
            {
                baseNbl *= 1f +
                    WindscreenSelectorAddend(td, ob) +
                    ApCapObliquityAddend(td, ob);
            }
            return baseNbl;
        }

        float EffectivePlateQualityFactor()
        {
            var baseline = ArmorHardnessProfile(235f);
            var current = ArmorHardnessProfile(armor.bhn);
            if (MathF.Abs(current) < 1e-12f)
                return armor.quality;
            return Math.Clamp(armor.quality * baseline / current, 0.5f, 1.1f);
        }

        float ScaleFactor()
        {
            var diameter = MathF.Max(projectile.diameterInches, 0.1f);
            return MathF.Sqrt(MathF.Max(1e-9f, 1f - 0.04f * MathF.Log(diameter / 3f)));
        }

        float ElongationFactor()
        {
            var diameter = MathF.Max(projectile.diameterInches, 8f);
            var pct = Math.Clamp(armor.elongationPercent, 0.1f, 25f);
            return 1f - (1f - MathF.Sqrt(pct / 25f)) * (diameter - 8f) / 8f;
        }

        float ObliquityMultiplier(float obliquityDeg, float td)
        {
            var ob = ClampObliquity(obliquityDeg);
            if (ob < HighObliquityStartDeg)
                return LowObliquityMultiplier(ob);
            var reference = HighObReference(td, ob);
            return reference / MathF.Max(MathF.Cos(DegreesToRadians(ob)), 1e-9f);
        }

        float LowObliquityMultiplier(float obliquityDeg)
        {
            var values = tables.obliquityReferenceVector;
            if (values == null || values.Length == 0)
                return 1f;
            var step = LowObliquityReferenceStepDeg;
            var ob = Math.Clamp(obliquityDeg, 0f, (values.Length - 1) * step);
            var scaled = ob / step;
            var idx = Math.Min(TruncatePositiveIndex(scaled), values.Length - 2);
            var frac = scaled - idx;
            var reference = Lerp(values[idx], values[idx + 1], frac);
            return reference / MathF.Max(MathF.Cos(DegreesToRadians(ob)), 1e-9f);
        }

        float HighObReference(float td, float obliquityDeg)
        {
            var matrix = tables.highObliquityReferenceMatrix;
            var obScaled = MathF.Max(0f, (obliquityDeg - HighObliquityStartDeg) / HighObliquityReferenceStepDeg);
            var obBucket = Math.Min(TruncatePositiveIndex(obScaled), matrix.Length - 2);
            var obFrac = (obliquityDeg - HighObliquityStartDeg - obBucket * HighObliquityReferenceStepDeg) / HighObliquityReferenceStepDeg;
            int tdBucket;
            float tdFrac;
            if (td >= HighObliquityTdSaturation)
            {
                tdBucket = HighObliquityTdSaturatedBucket;
                tdFrac = 1f;
            }
            else
            {
                var tdScaled = MathF.Max(0f, td / TdTableStep);
                tdBucket = Math.Min(TruncatePositiveIndex(tdScaled), matrix[0].Length - 2);
                tdFrac = (td - tdBucket * TdTableStep) / TdTableStep;
            }

            var row0 = matrix[obBucket];
            var row1 = matrix[obBucket + 1];
            var ref0 = Lerp(row0[tdBucket], row0[tdBucket + 1], tdFrac);
            var ref1 = Lerp(row1[tdBucket], row1[tdBucket + 1], tdFrac);
            return Lerp(ref0, ref1, obFrac);
        }

        (float baseVelocityCoefficient, float thicknessPowerExponent, float shapeSineAmplitude, float shapeSineFrequency, float shapeSinePhaseDeg) TdSegmentParameters(float td)
        {
            var idx = 1;
            var breaks = tables.tdDecisionBreaks;
            while (idx < breaks.Length - 1 && breaks[idx] < td && idx < MaxTdDecisionSegmentIndex)
                idx++;
            return (
                tables.tdBaseVelocityCoefficients[idx],
                tables.tdThicknessPowerExponents[idx],
                tables.tdShapeSineAmplitudes[idx],
                tables.tdShapeSineFrequencies[idx],
                tables.tdShapeSinePhaseDegs[idx]);
        }

        float TdShapeMultiplier(float td)
        {
            var (_, _, shapeSineAmplitude, shapeSineFrequency, shapeSinePhaseDeg) = TdSegmentParameters(td);
            if (MathF.Abs(shapeSineAmplitude) < 1e-12f)
                return 1f;
            var shaped = MathF.Sin(DegreesToRadians(td * shapeSineFrequency - shapeSinePhaseDeg));
            return 1f + shapeSineAmplitude * MathF.Max(shaped, 0f);
        }

        float ArmorHardnessProfile(float bhn)
        {
            var values = tables.armorHardnessProfile;
            var clamped = Math.Clamp(bhn, 200f, 200f + 5f * (values.Length - 1));
            var scaled = (clamped - 200f) / 5f;
            var idx = Math.Min(MathF.Floor(scaled), values.Length - 2);
            var i = Math.Max(0, (int)idx);
            return Lerp(values[i], values[i + 1], scaled - i);
        }

        float WindscreenSelectorAddend(float td, float obliquityDeg)
        {
            var selectorMultiplier = WindscreenNblAddendMultiplier(obliquityDeg);
            var windscreenPercent = 100f * MathF.Max(projectile.windscreenWeightPounds, 0f) / MathF.Max(projectile.totalWeightPounds, 1e-9f);
            if (windscreenPercent <= 0.1f)
                return 0f;

            var clampedTd = ClampTd(td);
            if (SuppressProjectileAddendForThinPlate(clampedTd, selectorMultiplier))
                return 0f;

            var interp = clampedTd < 0.03f
                ? InterpObMatrixRow(tables.windscreenMatrix[1], obliquityDeg)
                : InterpMatrixComponent(tables.windscreenMatrix, tables.windscreenMatrixTail, clampedTd, obliquityDeg);
            return (windscreenPercent / 5.1f) * selectorMultiplier * interp;
        }

        float ApCapObliquityAddend(float td, float obliquityDeg)
        {
            var mode = projectile.hcwclcrCapType;
            if (mode is not (1 or 2))
                return 0f;

            var apCapPercent = 100f * MathF.Max(projectile.apCapWeightPounds, 0f) / MathF.Max(projectile.totalWeightPounds, 1e-9f);
            if (apCapPercent <= 0.1f)
                return 0f;

            var ob = ClampObliquity(obliquityDeg);
            var clampedTd = ClampTd(td);
            if (SuppressProjectileAddendForThinPlate(clampedTd, WindscreenNblAddendMultiplier(ob)))
                return 0f;

            var divisor = mode == 2 ? 10f : 20f;
            var cutoff = mode == 2 ? 65f : 50f;
            var capRatio = apCapPercent / divisor;
            var threshold = ApCapObliquityThreshold(ob);
            if (clampedTd > threshold)
            {
                if (ob >= cutoff)
                    return 0f;
                var tableValue = mode == 1
                    ? InterpVectorByStep(tables.mode1LowOb, ob, 0f, 5f)
                    : InterpVectorByStep(tables.mode2LowOb, ob, 0f, 5f);
                return tableValue * (1f + 0.6f * (capRatio - 1f));
            }

            if (ob > SharedCapObliquityStartDeg && ob < SharedCapObliquityEndDeg)
                return InterpVectorByStep(tables.modeSharedMidOb, ob, SharedCapObliquityStartDeg, SharedCapObliquityStepDeg) * capRatio;
            return 0f;
        }

        float WindscreenNblAddendMultiplier(float obliquityDeg)
        {
            return projectile.highObliquityThresholdDeg > 0f && obliquityDeg > projectile.highObliquityThresholdDeg
                ? projectile.highObliquityWindscreenNblAddendMultiplier
                : projectile.windscreenNblAddendMultiplier;
        }

        static bool SuppressProjectileAddendForThinPlate(float td, float selectorMultiplier)
        {
            return td < 0.03f && (selectorMultiplier >= 0.75f || (selectorMultiplier >= 0.4f && td < 0.015f));
        }

        static float ApCapObliquityThreshold(float obliquityDeg)
        {
            return MathF.Round(PiecewiseObliquityThreshold(obliquityDeg) * 1000f) / 1000f;
        }

        static float PiecewiseObliquityThreshold(float obliquityDeg)
        {
            var ob = MathF.Max(obliquityDeg, 0f);
            if (ob > 65f)
                return 0.42f;
            if (ob > 55f)
                return 0.44f - 0.002f * (ob - 55f);
            return 0.66f - 0.18f * (ob / ApCapThresholdReferenceObliquityDeg);
        }

        float InterpMatrixComponent(float[][] matrix, float[] tail, float td, float obliquityDeg)
        {
            var obBucket = ObBucket(obliquityDeg, out var obLerp);
            var tdBucket = TdBucket(td);
            var tdLerp = TdLerp(td, tdBucket);
            if (tdBucket == ComponentTdTailBucket)
                return Lerp(tail[0], tail[1], obLerp);

            var row0 = matrix[tdBucket];
            var row1 = matrix[Math.Min(tdBucket + 1, matrix.Length - 1)];
            var col0 = obBucket;
            var col1 = Math.Min(obBucket + 1, row0.Length - 1);
            var base0 = Lerp(row0[col0], row0[col1], obLerp);
            var base1 = Lerp(row1[col0], row1[col1], obLerp);
            return Lerp(base0, base1, tdLerp);
        }

        float InterpObMatrixRow(float[] row, float obliquityDeg)
        {
            var obBucket = ObBucket(obliquityDeg, out var obLerp);
            var col1 = Math.Min(obBucket + 1, row.Length - 1);
            return Lerp(row[obBucket], row[col1], obLerp);
        }

        int TdBucket(float td)
        {
            var bucket = 0;
            var upper = tables.tdUpperBounds;
            while (bucket < ComponentTdTailBucket && upper[bucket] < td)
                bucket++;
            return bucket;
        }

        float TdLerp(float td, int bucket)
        {
            if (bucket >= ComponentTdTailBucket)
                return 0f;
            var lo = tables.tdLowerBounds[bucket];
            var hi = tables.tdUpperBounds[bucket];
            return hi <= lo ? 0f : (td - lo) / (hi - lo);
        }

        static int ObBucket(float obliquityDeg, out float fraction)
        {
            var ob = ClampObliquity(obliquityDeg);
            var bucket = Math.Clamp((int)(ob / ComponentObliquityTableStepDeg), 0, ComponentTdTailBucket);
            fraction = bucket < ComponentTdTailBucket ? (ob - bucket * ComponentObliquityTableStepDeg) / ComponentObliquityTableStepDeg : 0f;
            return bucket;
        }

        float InterpVectorByStep(float[] values, float x, float start, float step)
        {
            if (values.Length == 0)
                return 0f;
            if (values.Length == 1)
                return values[0];
            var scaled = (x - start) / step;
            var idx = Math.Clamp((int)MathF.Floor(scaled), 0, values.Length - 2);
            var frac = Math.Clamp(scaled - idx, 0f, 1f);
            return Lerp(values[idx], values[idx + 1], frac);
        }

        float PenetrationTripletPostScale(float obliquityDeg)
        {
            var extremeObliquityScale = 1f;
            if (options.enableNaabLikeAdaptation &&
                obliquityDeg >= ExtremeObliquityStartDeg &&
                obliquityDeg < RightAngleObliquityDeg)
            {
                var cosOb = MathF.Max(MathF.Cos(DegreesToRadians(obliquityDeg)), 0f);
                var cosRef = MathF.Max(MathF.Cos(DegreesToRadians(ExtremeObliquityStartDeg)), 1e-9f);
                extremeObliquityScale = MathF.Pow(cosOb / cosRef, 1.1f);
            }
            return extremeObliquityScale * Math.Clamp(projectile.effectiveShellQuality, 0.2f, 1.2f);
        }

        static float ClampTd(float td) => Math.Clamp(td, 0.001f, 5.99999f);
        static float ClampObliquity(float ob) => Math.Clamp(ob, 0f, 79.9999f);
        static int TruncatePositiveIndex(float value) => Math.Max(0, (int)value);
        static float Lerp(float a, float b, float t) => a + (b - a) * t;
        static float DegreesToRadians(float degrees) => degrees * MathF.PI / 180f;
    }

    static class NaabLikeBallisticsResultExtensions
    {
        public static NaabLikeBallisticsResult WithTrajectory(this NaabLikeBallisticsResult result, IEnumerable<NaabLikeTrajectoryPoint> trajectory)
        {
            result.trajectory.Clear();
            if (trajectory != null)
                result.trajectory.AddRange(trajectory);
            return result;
        }
    }
}
