using System;
using GeographicLib;


namespace NavalCombatCore
{
    public class MeasureStats
    {
        public float distanceYards;
        public float observerToTargetTrueBearingRelativeToNorthDeg;
        public float targetToObserverTrueBearingRelativeToNorthDeg;
        public float observerToTargetBearingRelativeToBowDeg;
        public float targetToObserverBearingRelativeToBowDeg;
        public TargetAspect observerPresentAspectFromTarget;
        public TargetAspect targetPresentAspectFromObserver;

        public static MeasureStats Measure(IDF3Model observer, IDF3Model target)
        {
            var inverseLine = Geodesic.WGS84.InverseLine(
                observer.GetLatitudeDeg(), observer.GetLongitudeDeg(),
                target.GetLatitudeDeg(), target.GetLongitudeDeg()
            );
            var distM = (float)inverseLine.Distance;
            var distYards = distM * MeasureUtils.meterToYard;
            var observerToTargetTrueBearingRelativeToNorthDeg = MeasureUtils.NormalizeAngle((float)inverseLine.Azimuth);
            var targetToObserverTrueBearingRelativeToNorthDeg = MeasureUtils.NormalizeAngle(observerToTargetTrueBearingRelativeToNorthDeg + 180); // Though not very accurete if distance is extremely long.
            var observerToTargetViewBearingRelativeToBowDeg = MeasureUtils.NormalizeAngle(observerToTargetTrueBearingRelativeToNorthDeg - observer.GetHeadingDeg());
            var targetToObserverViewBearingRelativeToBowDeg = MeasureUtils.NormalizeAngle(targetToObserverTrueBearingRelativeToNorthDeg - target.GetHeadingDeg());
            var observerPresentAspectFromTarget = GetXPresentAspectFromY(observerToTargetViewBearingRelativeToBowDeg);
            var targetPresentAspectFromObserver = GetXPresentAspectFromY(targetToObserverViewBearingRelativeToBowDeg);

            return new()
            {
                distanceYards = distYards,
                observerToTargetTrueBearingRelativeToNorthDeg = observerToTargetTrueBearingRelativeToNorthDeg,
                targetToObserverTrueBearingRelativeToNorthDeg = targetToObserverTrueBearingRelativeToNorthDeg,
                observerToTargetBearingRelativeToBowDeg = observerToTargetViewBearingRelativeToBowDeg,
                targetToObserverBearingRelativeToBowDeg = targetToObserverViewBearingRelativeToBowDeg,
                observerPresentAspectFromTarget = observerPresentAspectFromTarget,
                targetPresentAspectFromObserver = targetPresentAspectFromObserver
            };
        }

        static TargetAspect GetXPresentAspectFromY(float XToYrelativeBearingToBowDeg)
        {
            return Math.Min(
                MeasureUtils.GetPositiveAngleDifference(XToYrelativeBearingToBowDeg, 90),
                MeasureUtils.GetPositiveAngleDifference(XToYrelativeBearingToBowDeg, 270)
            ) <= 60 ? TargetAspect.Broad : TargetAspect.Narrow;
        }
    }

    public static class MeasureUtils
    {
        public static float yardToMeter = 0.9144f;
        public static float meterToYard = 1.09361f;
        public static float navalMileToMeter = 1852f;


        public static float MoveAngleTowards(float current, float target, float step)
        {
            // Normalize angles to 0-360 range
            current = NormalizeAngle(current);
            target = NormalizeAngle(target);

            // Calculate both possible directional differences (clockwise and counter-clockwise)
            float diff = target - current;
            float absDiff = Math.Abs(diff);

            // If direct difference is greater than 180°, choose the other direction
            if (absDiff > 180f)
            {
                if (diff > 0)
                {
                    diff -= 360f; // Take the shorter path clockwise
                }
                else
                {
                    diff += 360f; // Take the shorter path counter-clockwise
                }
            }

            // Calculate actual movement (not exceeding step size)
            float move = Math.Sign(diff) * Math.Min(Math.Abs(diff), step);

            // Apply movement and normalize result
            float result = current + move;
            return NormalizeAngle(result);
        }

        // Helper function: Normalize angle to 0-360 range
        public static float NormalizeAngle(float angle)
        {
            angle %= 360f; // Reduce to base value
            if (angle < 0)
            {
                angle += 360f; // Convert negative angles to positive equivalents
            }
            return angle;
        }

        public static float GetPositiveAngleDifference(float angle1, float angle2)
        {
            // Normalize angles to 0-360 range
            angle1 = NormalizeAngle(angle1);
            angle2 = NormalizeAngle(angle2);

            // Calculate both possible directional differences
            float diff = Math.Abs(angle1 - angle2);

            // If difference is greater than 180°, take the shorter path around the circle
            if (diff > 180f)
            {
                diff = 360f - diff;
            }

            // Return the smallest positive difference
            return diff;
        }

        /// <summary>
        /// Checks if an angle is within a given arc range
        /// </summary>
        /// <param name="angle">The angle to check (in degrees)</param>
        /// <param name="startAngle">The starting angle of the arc (in degrees)</param>
        /// <param name="sweepAngle">The sweep angle of the arc (in degrees, positive for clockwise direction)</param>
        /// <returns>True if the angle is within the arc range, false otherwise</returns>
        public static bool IsAngleInArc(float angle, float startAngle, float sweepAngle)
        {
            // Normalize all angles to 0-360 range
            angle = NormalizeAngle(angle);
            startAngle = NormalizeAngle(startAngle);

            // Calculate the end angle of the arc
            float endAngle = NormalizeAngle(startAngle + sweepAngle);

            // Handle arcs that don't cross 0°
            if (sweepAngle > 0 && startAngle < endAngle)
            {
                return angle >= startAngle && angle <= endAngle;
            }
            else if (sweepAngle < 0 && startAngle > endAngle)
            {
                return angle <= startAngle && angle >= endAngle;
            }
            // Handle arcs that cross 0°
            else
            {
                if (sweepAngle > 0) // Clockwise arc crossing 0°
                {
                    return angle >= startAngle || angle <= endAngle;
                }
                else // Counter-clockwise arc crossing 0°
                {
                    return angle <= startAngle || angle >= endAngle;
                }
            }
        }


    }
}