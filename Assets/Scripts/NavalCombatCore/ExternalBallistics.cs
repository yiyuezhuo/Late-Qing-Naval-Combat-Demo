using System;
using System.Collections.Generic;

namespace NavalCombatCore
{
    public enum ExternalBallisticsDragModel
    {
        G1,
        G7
    }

    public sealed class ExternalBallisticsInput
    {
        public float muzzleVelocityMetersPerSecond = 730f;
        public float elevationAngleDeg = 15f;
        public float projectileDiameterMeters = 0.3048f;
        public float projectileMassKg = 386f;
        public float ballisticCoefficient = 0.5f;
        public ExternalBallisticsDragModel dragModel = ExternalBallisticsDragModel.G1;
        public float airDensityKgPerCubicMeter = 1.225f;
        public float speedOfSoundMetersPerSecond = 340.29f;
        public float timeStepSeconds = 0.02f;
        public float gravityMetersPerSecondSquared = 9.80665f;
        public float maxSimulationSeconds = 240f;
    }

    public sealed class ExternalBallisticsTrajectoryPoint
    {
        public float timeSeconds;
        public float xMeters;
        public float yMeters;
        public float velocityXMetersPerSecond;
        public float velocityYMetersPerSecond;

        public float speedMetersPerSecond => MathF.Sqrt(
            velocityXMetersPerSecond * velocityXMetersPerSecond +
            velocityYMetersPerSecond * velocityYMetersPerSecond);
    }

    public sealed class ExternalBallisticsResult
    {
        public bool success;
        public string failureReason;
        public float elevationAngleDeg;
        public float rangeMeters;
        public float timeOfFlightSeconds;
        public float impactVelocityMetersPerSecond;
        public float angleOfFallDeg;
        public List<ExternalBallisticsTrajectoryPoint> trajectory = new();
    }

    public static class ExternalBallisticsSolver
    {
        const float InchesPerMeter = 39.3700787f;
        const float PoundsPerKilogram = 2.20462262f;

        public static ExternalBallisticsResult Solve(ExternalBallisticsInput input)
        {
            var validationError = Validate(input);
            if (validationError != null)
            {
                return new ExternalBallisticsResult
                {
                    success = false,
                    failureReason = validationError,
                    elevationAngleDeg = input?.elevationAngleDeg ?? 0f
                };
            }

            var result = new ExternalBallisticsResult
            {
                success = true,
                elevationAngleDeg = input.elevationAngleDeg
            };

            var angleRad = input.elevationAngleDeg * MathF.PI / 180f;
            var current = new ExternalBallisticsTrajectoryPoint
            {
                timeSeconds = 0f,
                xMeters = 0f,
                yMeters = 0f,
                velocityXMetersPerSecond = input.muzzleVelocityMetersPerSecond * MathF.Cos(angleRad),
                velocityYMetersPerSecond = input.muzzleVelocityMetersPerSecond * MathF.Sin(angleRad)
            };

            result.trajectory.Add(ClonePoint(current));

            var stepCount = Math.Max(1, (int)MathF.Ceiling(input.maxSimulationSeconds / input.timeStepSeconds));
            for (int stepIndex = 0; stepIndex < stepCount; stepIndex++)
            {
                var previous = current;
                current = Step(previous, input);

                if (current.yMeters <= 0f && current.timeSeconds > 0f)
                {
                    var impact = InterpolateImpact(previous, current);
                    result.trajectory.Add(impact);
                    result.rangeMeters = impact.xMeters;
                    result.timeOfFlightSeconds = impact.timeSeconds;
                    result.impactVelocityMetersPerSecond = impact.speedMetersPerSecond;
                    result.angleOfFallDeg = MathF.Atan2(
                        MathF.Max(0f, -impact.velocityYMetersPerSecond),
                        MathF.Max(0.0001f, impact.velocityXMetersPerSecond)) * 180f / MathF.PI;
                    return result;
                }

                result.trajectory.Add(current);
            }

            result.success = false;
            result.failureReason = "Projectile did not reach ground before the simulation time limit.";
            return result;
        }

        static string Validate(ExternalBallisticsInput input)
        {
            if (input == null)
                return "Input is missing.";
            if (!IsFinitePositive(input.muzzleVelocityMetersPerSecond))
                return "Muzzle velocity must be greater than 0.";
            if (!IsFinitePositive(input.projectileDiameterMeters))
                return "Projectile diameter must be greater than 0.";
            if (!IsFinitePositive(input.projectileMassKg))
                return "Projectile mass must be greater than 0.";
            if (!IsFinitePositive(input.ballisticCoefficient))
                return "Ballistic coefficient must be greater than 0.";
            if (!IsFinitePositive(input.airDensityKgPerCubicMeter))
                return "Air density must be greater than 0.";
            if (!IsFinitePositive(input.speedOfSoundMetersPerSecond))
                return "Speed of sound must be greater than 0.";
            if (!IsFinitePositive(input.timeStepSeconds))
                return "Time step must be greater than 0.";
            if (!IsFinitePositive(input.gravityMetersPerSecondSquared))
                return "Gravity must be greater than 0.";
            if (!float.IsFinite(input.elevationAngleDeg) || input.elevationAngleDeg <= 0f || input.elevationAngleDeg >= 90f)
                return "Elevation angle must be greater than 0 and less than 90 degrees.";
            return null;
        }

        static bool IsFinitePositive(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;

        static ExternalBallisticsTrajectoryPoint Step(ExternalBallisticsTrajectoryPoint point, ExternalBallisticsInput input)
        {
            var speed = MathF.Max(0.0001f, point.speedMetersPerSecond);
            var dragAcceleration = GetDragAccelerationMetersPerSecondSquared(speed, input);
            var dragX = dragAcceleration * point.velocityXMetersPerSecond / speed;
            var dragY = dragAcceleration * point.velocityYMetersPerSecond / speed;

            var nextVelocityX = point.velocityXMetersPerSecond - dragX * input.timeStepSeconds;
            var nextVelocityY = point.velocityYMetersPerSecond - (input.gravityMetersPerSecondSquared + dragY) * input.timeStepSeconds;

            return new ExternalBallisticsTrajectoryPoint
            {
                timeSeconds = point.timeSeconds + input.timeStepSeconds,
                xMeters = point.xMeters + (point.velocityXMetersPerSecond + nextVelocityX) * 0.5f * input.timeStepSeconds,
                yMeters = point.yMeters + (point.velocityYMetersPerSecond + nextVelocityY) * 0.5f * input.timeStepSeconds,
                velocityXMetersPerSecond = nextVelocityX,
                velocityYMetersPerSecond = nextVelocityY
            };
        }

        static float GetDragAccelerationMetersPerSecondSquared(float speedMetersPerSecond, ExternalBallisticsInput input)
        {
            var mach = speedMetersPerSecond / input.speedOfSoundMetersPerSecond;
            var dragCoefficient = GetReferenceDragCoefficient(input.dragModel, mach);
            var referenceArea = MathF.PI * input.projectileDiameterMeters * input.projectileDiameterMeters * 0.25f;
            var effectiveCoefficient = dragCoefficient / MathF.Max(0.0001f, input.ballisticCoefficient);
            return 0.5f * input.airDensityKgPerCubicMeter * speedMetersPerSecond * speedMetersPerSecond *
                effectiveCoefficient * referenceArea / input.projectileMassKg;
        }

        static float GetReferenceDragCoefficient(ExternalBallisticsDragModel dragModel, float mach)
        {
            return dragModel switch
            {
                ExternalBallisticsDragModel.G7 => InterpolateDragCoefficient(mach, G7DragTable),
                _ => InterpolateDragCoefficient(mach, G1DragTable)
            };
        }

        static float InterpolateDragCoefficient(float mach, (float mach, float cd)[] table)
        {
            if (mach <= table[0].mach)
                return table[0].cd;

            for (int i = 1; i < table.Length; i++)
            {
                if (mach <= table[i].mach)
                {
                    var prev = table[i - 1];
                    var next = table[i];
                    var ratio = (mach - prev.mach) / (next.mach - prev.mach);
                    return prev.cd + (next.cd - prev.cd) * ratio;
                }
            }

            return table[^1].cd;
        }

        static ExternalBallisticsTrajectoryPoint InterpolateImpact(
            ExternalBallisticsTrajectoryPoint previous,
            ExternalBallisticsTrajectoryPoint current)
        {
            var ratio = previous.yMeters / MathF.Max(0.0001f, previous.yMeters - current.yMeters);
            ratio = Math.Clamp(ratio, 0f, 1f);

            return new ExternalBallisticsTrajectoryPoint
            {
                timeSeconds = Lerp(previous.timeSeconds, current.timeSeconds, ratio),
                xMeters = Lerp(previous.xMeters, current.xMeters, ratio),
                yMeters = 0f,
                velocityXMetersPerSecond = Lerp(previous.velocityXMetersPerSecond, current.velocityXMetersPerSecond, ratio),
                velocityYMetersPerSecond = Lerp(previous.velocityYMetersPerSecond, current.velocityYMetersPerSecond, ratio)
            };
        }

        static ExternalBallisticsTrajectoryPoint ClonePoint(ExternalBallisticsTrajectoryPoint point)
        {
            return new ExternalBallisticsTrajectoryPoint
            {
                timeSeconds = point.timeSeconds,
                xMeters = point.xMeters,
                yMeters = point.yMeters,
                velocityXMetersPerSecond = point.velocityXMetersPerSecond,
                velocityYMetersPerSecond = point.velocityYMetersPerSecond
            };
        }

        static float Lerp(float a, float b, float ratio) => a + (b - a) * ratio;

        public static float MetersToYards(float meters) => meters * 1.0936133f;
        public static float YardsToMeters(float yards) => yards / 1.0936133f;
        public static float InchesToMeters(float inches) => inches / InchesPerMeter;
        public static float MetersToInches(float meters) => meters * InchesPerMeter;
        public static float KilogramsToPounds(float kilograms) => kilograms * PoundsPerKilogram;
        public static float PoundsToKilograms(float pounds) => pounds / PoundsPerKilogram;

        static readonly (float mach, float cd)[] G1DragTable =
        {
            (0.00f, 0.262f),
            (0.50f, 0.255f),
            (0.70f, 0.269f),
            (0.85f, 0.319f),
            (0.95f, 0.480f),
            (1.05f, 0.620f),
            (1.20f, 0.590f),
            (1.50f, 0.500f),
            (2.00f, 0.410f),
            (2.50f, 0.360f),
            (3.00f, 0.330f),
            (4.00f, 0.300f)
        };

        static readonly (float mach, float cd)[] G7DragTable =
        {
            (0.00f, 0.120f),
            (0.50f, 0.119f),
            (0.70f, 0.125f),
            (0.85f, 0.150f),
            (0.95f, 0.290f),
            (1.05f, 0.380f),
            (1.20f, 0.360f),
            (1.50f, 0.300f),
            (2.00f, 0.245f),
            (2.50f, 0.215f),
            (3.00f, 0.195f),
            (4.00f, 0.180f)
        };
    }
}
