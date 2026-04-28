using System;
using System.Collections.Generic;

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

    public enum NaabLikeCapType
    {
        None,
        HardCap
    }

    public sealed class NaabLikeProjectile
    {
        public string name = "";
        public int nation = 1;
        public int shellClass = 1;
        public float diameterInches = 5f;
        public float totalWeightPounds = 50f;
        public float bodyWeightPounds = 50f;
        public float windscreenWeightPounds;
        public float apCapWeightPounds;
        public NaabLikeCapType capType = NaabLikeCapType.None;
        public int hcwclcrCapType;
        public float muzzleVelocityFeetPerSecond = 3000f;
        public float maxRangeYards = 22600f;
        public float maxElevationDeg = 20f;
        public NaabLikeDragFunction dragFunction = NaabLikeDragFunction.G5;
        public float ballisticCoefficient = 1.9307f;
        public float dragCoefficientAdjust = 14f;
        public float shellQuality = 0.575f;
        public float defaultShellQuality;
        public float shellPlim;
        public float shellPdam;

        public NaabLikeProjectile Clone()
        {
            return (NaabLikeProjectile)MemberwiseClone();
        }
    }

    public sealed class NaabLikeArmorInput
    {
        public float quality = 0.95f;
        public float elongationPercent = 22f;
        public float bnh = 235f;
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
                tdDecisionParamA = NaabLikeEmbeddedData.TdDecisionParamA,
                tdDecisionParamB = NaabLikeEmbeddedData.TdDecisionParamB,
                tdDecisionParamC = NaabLikeEmbeddedData.TdDecisionParamC,
                tdDecisionParamD = NaabLikeEmbeddedData.TdDecisionParamD,
                tdDecisionParamE = NaabLikeEmbeddedData.TdDecisionParamE,
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

        public float CoefficientFromMach(float value)
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
            return CurvedInterpolate(mach, cd, lower, value);
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
        public float[] tdDecisionParamA;
        public float[] tdDecisionParamB;
        public float[] tdDecisionParamC;
        public float[] tdDecisionParamD;
        public float[] tdDecisionParamE;
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
        readonly float dxFeet;
        float dragAdjustMachLeOneRangeFeet;
        float dragAdjustMachGtOneRangeFeet;

        public NaabLikeExteriorBallisticsSolver(NaabLikeDragTable dragTable, NaabLikeProjectile projectile, float dxFeet = 3f)
        {
            this.dragTable = dragTable;
            this.projectile = projectile;
            this.dxFeet = dxFeet;
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
            var cdRef = dragTable.CoefficientFromMach(mach);
            var bcEff = EffectiveBallisticCoefficient(point.xFeet, mach);
            var dragSlope = Pre1962DragScaleFeet * atmosphere.densityRatio * cdRef * speed / bcEff;
            var dvxDx = -dragSlope;
            var dvyDx = dvxDx * (point.vyFeetPerSecond / point.vxFeetPerSecond) - atmosphere.gravityFeetPerSecondSquared / point.vxFeetPerSecond;
            return (point.vyFeetPerSecond / point.vxFeetPerSecond, dvxDx, dvyDx, 1f / point.vxFeetPerSecond);
        }

        float EffectiveBallisticCoefficient(float rangeFeet, float mach)
        {
            var bc = projectile.ballisticCoefficient;
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
        readonly NaabLikeTerminalTables tables;
        readonly NaabLikeProjectile projectile;
        readonly NaabLikeArmorInput armor;

        public NaabLikeTerminalBallisticsSolver(NaabLikeTerminalTables tables, NaabLikeProjectile projectile, NaabLikeArmorInput armor)
        {
            this.tables = tables;
            this.projectile = projectile;
            this.armor = armor;
        }

        public float CompletePenetrationInches(float strikingVelocityFeetPerSecond, float obliquityDeg)
        {
            return ScanBallisticLimitThicknesses(strikingVelocityFeetPerSecond, obliquityDeg).full;
        }

        public (float full, float partial, float noHoling) ScanBallisticLimitThicknesses(float strikingVelocityFeetPerSecond, float obliquityDeg)
        {
            var upper = MathF.Min(6f * projectile.diameterInches - 0.1f, 199f);
            var actual = MathF.Max(strikingVelocityFeetPerSecond, 0f);
            var full = 0f;
            var partial = 0f;
            var noHoling = 0f;

            for (var thickness = 0.1f; thickness <= upper + 1e-6f; thickness += 0.02f)
            {
                var trueNbl = TrueNblVelocityFeetPerSecond(thickness, obliquityDeg);
                var actualRounded = MathF.Round(actual);
                var trueRounded = MathF.Round(trueNbl);
                if (full == 0f && actual <= trueNbl)
                    full = thickness;
                if (partial == 0f && actualRounded + 60f <= trueRounded)
                    partial = thickness;
                if (actualRounded + 120f < trueRounded)
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
            var (paramA, paramB, _, _, _) = TdSegmentParameters(td);
            var weightOverD3 = MathF.Max(projectile.totalWeightPounds / (diameter * diameter * diameter), 1e-9f);
            var powerBase = MathF.Max(EffectivePlateQualityFactor(), 0.01f) * td;
            var baseNbl = paramA * MathF.Pow(powerBase, paramB);
            baseNbl *= TdShapeMultiplier(td);
            baseNbl *= ScaleFactor();
            baseNbl /= MathF.Sqrt(weightOverD3);

            var ob = ClampObliquity(obliquityDeg);
            if (ob > 0.1f)
                baseNbl *= ObliquityMultiplier(ob, td);

            baseNbl *= ElongationFactor();
            baseNbl *= 1f +
                WindscreenSelectorAddend(td, ob) +
                ApCapObliquityAddend(td, ob);
            return baseNbl;
        }

        float EffectivePlateQualityFactor()
        {
            var baseline = ArmorHardnessProfile(235f);
            var current = ArmorHardnessProfile(armor.bnh);
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
            if (ob < 45f)
                return LowObliquityMultiplier(ob);
            var reference = HighObReference(td, ob);
            return reference / MathF.Max(MathF.Cos(DegreesToRadians(ob)), 1e-9f);
        }

        float LowObliquityMultiplier(float obliquityDeg)
        {
            var values = tables.obliquityReferenceVector;
            if (values == null || values.Length == 0)
                return 1f;
            var step = 2.5f;
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
            var obScaled = MathF.Max(0f, (obliquityDeg - 45f) / 2.5f);
            var obBucket = Math.Min(TruncatePositiveIndex(obScaled), matrix.Length - 2);
            var obFrac = (obliquityDeg - 45f - obBucket * 2.5f) / 2.5f;
            int tdBucket;
            float tdFrac;
            if (td >= 0.9f)
            {
                tdBucket = 17;
                tdFrac = 1f;
            }
            else
            {
                var tdScaled = MathF.Max(0f, td / 0.05f);
                tdBucket = Math.Min(TruncatePositiveIndex(tdScaled), matrix[0].Length - 2);
                tdFrac = (td - tdBucket * 0.05f) / 0.05f;
            }

            var row0 = matrix[obBucket];
            var row1 = matrix[obBucket + 1];
            var ref0 = Lerp(row0[tdBucket], row0[tdBucket + 1], tdFrac);
            var ref1 = Lerp(row1[tdBucket], row1[tdBucket + 1], tdFrac);
            return Lerp(ref0, ref1, obFrac);
        }

        (float a, float b, float c, float d, float e) TdSegmentParameters(float td)
        {
            var idx = 1;
            var breaks = tables.tdDecisionBreaks;
            while (idx < breaks.Length - 1 && breaks[idx] < td && idx < 0xB)
                idx++;
            return (
                tables.tdDecisionParamA[idx],
                tables.tdDecisionParamB[idx],
                tables.tdDecisionParamC[idx],
                tables.tdDecisionParamD[idx],
                tables.tdDecisionParamE[idx]);
        }

        float TdShapeMultiplier(float td)
        {
            var (_, _, c, d, e) = TdSegmentParameters(td);
            if (MathF.Abs(c) < 1e-12f)
                return 1f;
            var shaped = MathF.Sin(DegreesToRadians(td * d - e));
            return 1f + c * MathF.Max(shaped, 0f);
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
            var selectorMultiplier = ProjectileSelectorMultiplier(projectile.nation, projectile.shellClass, obliquityDeg);
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
            if (SuppressProjectileAddendForThinPlate(clampedTd, ProjectileSelectorMultiplier(projectile.nation, projectile.shellClass, ob)))
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

            if (ob > 40f && ob < 75f)
                return InterpVectorByStep(tables.modeSharedMidOb, ob, 40f, 2.5f) * capRatio;
            return 0f;
        }

        float ProjectileSelectorMultiplier(int nation, int shellClass, float obliquityDeg)
        {
            if (projectile.diameterInches <= 3f)
                return 1f;
            return nation switch
            {
                1 => 0.75f,
                2 => shellClass == 16 ? 0.05f : 1f,
                3 => shellClass >= 13 && shellClass <= 16 ? 0.33f : 1f,
                4 => shellClass == 8 ? 0.33f : 1f,
                5 => shellClass >= 7 && shellClass <= 12 ? 0.33f : 1f,
                6 => shellClass is 8 or 9 && obliquityDeg > 45f ? 0.1f : 1f,
                _ => 1f
            };
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
            return 0.66f - 0.18f * (ob / 45f);
        }

        float InterpMatrixComponent(float[][] matrix, float[] tail, float td, float obliquityDeg)
        {
            var obBucket = ObBucket(obliquityDeg, out var obLerp);
            var tdBucket = TdBucket(td);
            var tdLerp = TdLerp(td, tdBucket);
            if (tdBucket == 16)
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
            while (bucket < 16 && upper[bucket] < td)
                bucket++;
            return bucket;
        }

        float TdLerp(float td, int bucket)
        {
            if (bucket >= 16)
                return 0f;
            var lo = tables.tdLowerBounds[bucket];
            var hi = tables.tdUpperBounds[bucket];
            return hi <= lo ? 0f : (td - lo) / (hi - lo);
        }

        static int ObBucket(float obliquityDeg, out float fraction)
        {
            var ob = ClampObliquity(obliquityDeg);
            var bucket = Math.Clamp((int)(ob / 5f), 0, 16);
            fraction = bucket < 16 ? (ob - bucket * 5f) / 5f : 0f;
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
            var local44 = 1f;
            if (obliquityDeg >= 80f && obliquityDeg < 90f)
            {
                var cosOb = MathF.Max(MathF.Cos(DegreesToRadians(obliquityDeg)), 0f);
                var cosRef = MathF.Max(MathF.Cos(DegreesToRadians(80f)), 1e-9f);
                local44 = MathF.Pow(cosOb / cosRef, 1.1f);
            }
            return local44 * Math.Clamp(ShellQualityFactor(), 0.2f, 1.2f);
        }

        float ShellQualityFactor()
        {
            var defaults = DefaultShellQualityLimits(projectile.nation, projectile.shellClass);
            var plim = projectile.shellPlim > 0f ? projectile.shellPlim : defaults.plim;
            var pdam = projectile.shellPdam > 0f ? projectile.shellPdam : defaults.pdam;
            if (pdam <= 0f && projectile.shellPlim <= 0f)
                plim = projectile.shellQuality;
            if (projectile.defaultShellQuality > 0f && MathF.Abs(projectile.shellQuality - projectile.defaultShellQuality) > 1e-6f)
                plim = projectile.shellQuality;
            return pdam <= 0f ? plim : 0.75f * plim + 0.25f * pdam;
        }

        static (float plim, float pdam) DefaultShellQualityLimits(int nation, int shellClass)
        {
            return (nation, shellClass) switch
            {
                (1, 1) => (0.6f, 0.5f),
                (3, 15) => (0.988f, 0.977f),
                _ => (1f, -1f)
            };
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
