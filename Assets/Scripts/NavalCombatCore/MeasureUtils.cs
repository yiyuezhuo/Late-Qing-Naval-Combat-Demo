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
        public TargetAspect observerPresentAspectFromTarget => GetXPresentAspectFromY(observerToTargetTrueBearingRelativeToNorthDeg);
        public TargetAspect targetPresentAspectFromObserver => GetXPresentAspectFromY(targetToObserverBearingRelativeToBowDeg);

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
            var observerToTargetBearingRelativeToBowDeg = MeasureUtils.NormalizeAngle(observerToTargetTrueBearingRelativeToNorthDeg - observer.GetHeadingDeg());
            var targetToObserverBearingRelativeToBowDeg = MeasureUtils.NormalizeAngle(targetToObserverTrueBearingRelativeToNorthDeg - target.GetHeadingDeg());

            return new()
            {
                distanceYards = distYards,
                observerToTargetTrueBearingRelativeToNorthDeg = observerToTargetTrueBearingRelativeToNorthDeg,
                targetToObserverTrueBearingRelativeToNorthDeg = targetToObserverTrueBearingRelativeToNorthDeg,
                observerToTargetBearingRelativeToBowDeg = observerToTargetBearingRelativeToBowDeg,
                targetToObserverBearingRelativeToBowDeg = targetToObserverBearingRelativeToBowDeg,
            };
        }

        public static MeasureStats MeasureApproximation(IDF3Model observer, IDF3Model target)
        {
            var (DistanceKm, InitialBearingDeg) = Approximation.CalculateDistanceKmAndBearingDeg(observer, target);
            var distYards = (float)DistanceKm * 1000 * MeasureUtils.meterToYard;
            var observerToTargetTrueBearingRelativeToNorthDeg = MeasureUtils.NormalizeAngle((float)InitialBearingDeg);
            var targetToObserverTrueBearingRelativeToNorthDeg = MeasureUtils.NormalizeAngle(observerToTargetTrueBearingRelativeToNorthDeg + 180); // Though not very accurete if distance is extremely long.
            var observerToTargetBearingRelativeToBowDeg = MeasureUtils.NormalizeAngle(observerToTargetTrueBearingRelativeToNorthDeg - observer.GetHeadingDeg());
            var targetToObserverBearingRelativeToBowDeg = MeasureUtils.NormalizeAngle(targetToObserverTrueBearingRelativeToNorthDeg - target.GetHeadingDeg());
            return new()
            {
                distanceYards = distYards,
                observerToTargetTrueBearingRelativeToNorthDeg = observerToTargetTrueBearingRelativeToNorthDeg,
                targetToObserverTrueBearingRelativeToNorthDeg = targetToObserverTrueBearingRelativeToNorthDeg,
                observerToTargetBearingRelativeToBowDeg = observerToTargetBearingRelativeToBowDeg,
                targetToObserverBearingRelativeToBowDeg = targetToObserverBearingRelativeToBowDeg,
            };
        }

        static TargetAspect GetXPresentAspectFromY(float XToYrelativeBearingToBowDeg)
        {
            return Math.Min(
                MeasureUtils.GetPositiveAngleDifference(XToYrelativeBearingToBowDeg, 90),
                MeasureUtils.GetPositiveAngleDifference(XToYrelativeBearingToBowDeg, 270)
            ) <= 60 ? TargetAspect.Broad : TargetAspect.Narrow;
        }

        public static class Approximation
        {
            private const double EarthRadius = 6371000; // m

            public static (double newLat, double newLon) CalculateNewPosition(double lat, double lon, double bearing, double distanceM)
            {
                double latRad = DegreesToRadians(lat);
                double lonRad = DegreesToRadians(lon);
                double bearingRad = DegreesToRadians(bearing);

                double newLatRad = Math.Asin(Math.Sin(latRad) * Math.Cos(distanceM / EarthRadius) +
                                    Math.Cos(latRad) * Math.Sin(distanceM / EarthRadius) * Math.Cos(bearingRad));

                double newLonRad = lonRad + Math.Atan2(Math.Sin(bearingRad) * Math.Sin(distanceM / EarthRadius) * Math.Cos(latRad),
                                                    Math.Cos(distanceM / EarthRadius) - Math.Sin(latRad) * Math.Sin(newLatRad));

                double newLat = RadiansToDegrees(newLatRad);
                double newLon = RadiansToDegrees(newLonRad);

                return (newLat, newLon);
            }

            private static double DegreesToRadians(double degrees)
            {
                return degrees * Math.PI / 180.0;
            }

            private static double RadiansToDegrees(double radians)
            {
                return radians * 180.0 / Math.PI;
            }

            public static string Test()
            {
                // Lat 40.7128，Lon-74.0060，45 deg（east-west），move 1000m
                var result = CalculateNewPosition(40.7128, -74.0060, 45, 1000);
                return $"New Latitude: {result.newLat}, New Longitude: {result.newLon}";
            }

            public static double CalculateInitialBearing(double lat1, double lon1, double lat2, double lon2)
            {
                // Convert latitude and longitude from degrees to radians
                double lat1Rad = DegreesToRadians(lat1);
                double lon1Rad = DegreesToRadians(lon1);
                double lat2Rad = DegreesToRadians(lat2);
                double lon2Rad = DegreesToRadians(lon2);

                // Calculate the difference in longitude
                double deltaLon = lon2Rad - lon1Rad;

                // Use the spherical trigonometry formula to calculate the initial bearing
                double y = Math.Sin(deltaLon) * Math.Cos(lat2Rad);
                double x = Math.Cos(lat1Rad) * Math.Sin(lat2Rad) - Math.Sin(lat1Rad) * Math.Cos(lat2Rad) * Math.Cos(deltaLon);
                double initialBearingRad = Math.Atan2(y, x);

                // Convert the bearing from radians to degrees
                double initialBearingDeg = RadiansToDegrees(initialBearingRad);

                // Normalize the bearing to the range [0, 360)
                initialBearingDeg = (initialBearingDeg + 360) % 360;

                return initialBearingDeg;
            }

            public static double CalculateInitialBearing(IDF3Model observer, IDF3Model target)
            {
                return CalculateInitialBearing(observer.GetLatitudeDeg(), observer.GetLongitudeDeg(), target.GetLatitudeDeg(), target.GetLongitudeDeg());
            }

            public static string Test2()
            {
                // Latitude and longitude of the starting point (in degrees)
                double lat1 = 34.0522; // Example: Latitude of Los Angeles
                double lon1 = -118.2437; // Example: Longitude of Los Angeles

                // Latitude and longitude of the destination (in degrees)
                double lat2 = 40.7128; // Example: Latitude of New York
                double lon2 = -74.0060; // Example: Longitude of New York

                // Calculate the initial bearing
                double initialBearing = CalculateInitialBearing(lat1, lon1, lat2, lon2);

                return $"Initial Bearing: {initialBearing} degrees";
            }

            public static double HaversineDistanceKm(double lat1, double lon1, double lat2, double lon2)
            {
                const double R = 6371;

                double dLat = DegreesToRadians(lat2 - lat1);
                double dLon = DegreesToRadians(lon2 - lon1);

                double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                        Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2)) *
                        Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

                double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

                double distance = R * c;

                return distance;
            }

            public static double HaversineDistanceYards(IDF3Model observer, IDF3Model target)
            {
                var distanceKm = HaversineDistanceKm(observer.GetLatitudeDeg(), observer.GetLongitudeDeg(), target.GetLatitudeDeg(), target.GetLongitudeDeg());
                return distanceKm * 1000 * MeasureUtils.meterToYard;
            }

            public static (double DistanceKm, double InitialBearingDeg) CalculateDistanceKmAndBearingDeg(double lat1, double lon1, double lat2, double lon2)
            {
                const double R = 6371; // Earth's radius in km

                // Convert latitude and longitude from degrees to radians
                double lat1Rad = DegreesToRadians(lat1);
                double lon1Rad = DegreesToRadians(lon1);
                double lat2Rad = DegreesToRadians(lat2);
                double lon2Rad = DegreesToRadians(lon2);

                // Differences in coordinates
                double dLat = lat2Rad - lat1Rad;
                double dLon = lon2Rad - lon1Rad;

                // Haversine distance calculation
                double sinDLat2 = Math.Sin(dLat / 2);
                double sinDLon2 = Math.Sin(dLon / 2);
                double a = sinDLat2 * sinDLat2 +
                        Math.Cos(lat1Rad) * Math.Cos(lat2Rad) *
                        sinDLon2 * sinDLon2;
                double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
                double distance = R * c;

                // Initial bearing calculation
                double y = Math.Sin(dLon) * Math.Cos(lat2Rad);
                double x = Math.Cos(lat1Rad) * Math.Sin(lat2Rad) -
                        Math.Sin(lat1Rad) * Math.Cos(lat2Rad) * Math.Cos(dLon);
                double initialBearingRad = Math.Atan2(y, x);
                double initialBearingDeg = RadiansToDegrees(initialBearingRad);
                initialBearingDeg = (initialBearingDeg + 360) % 360; // Normalize to [0, 360)

                return (distance, initialBearingDeg);
            }

            public static (double DistanceKm, double InitialBearingDeg) CalculateDistanceKmAndBearingDeg(IDF3Model observer, IDF3Model target)
            {
                return CalculateDistanceKmAndBearingDeg(observer.GetLatitudeDeg(), observer.GetLongitudeDeg(), target.GetLatitudeDeg(), target.GetLongitudeDeg());
            }
        }
    }

    public static class MeasureUtils
    {
        public static float yardToMeter = 0.9144f;
        public static float meterToYard = 1.09361f;
        public static float navalMileToMeter = 1852f;
        public static float yardToFoot = 3;
        public static float footToYard = 1f / 3;
        public static float meterToFoot = meterToYard * yardToFoot;
        public static float navalMileToKilometer = 1.852f;
        public static float kilometerToNavalMile = 1f / navalMileToKilometer;
        public static float kilometerToYard = meterToYard * 1000;

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

        public static bool IsAngleInArcRelaxed(float angle, float startAngle, float sweepAngle, float relaxAngle)
        {
            return IsAngleInArc(angle, startAngle - relaxAngle, sweepAngle + 2 * relaxAngle);
        }

        public static float AngleDifferenceFromArc(float angle, float startAngle, float sweepAngle)
        {
            // Normalize all angles to 0-360 range
            angle = NormalizeAngle(angle);
            startAngle = NormalizeAngle(startAngle);
            
            // If the angle is inside the arc, return 0 difference
            if (IsAngleInArc(angle, startAngle, sweepAngle))
            {
                return 0f;
            }
            
            // Calculate the end angle of the arc
            float endAngle = NormalizeAngle(startAngle + sweepAngle);
            
            // Calculate the difference to both start and end angles
            float diffToStart = Math.Min(Math.Abs(angle - startAngle), 360 - Math.Abs(angle - startAngle));
            float diffToEnd = Math.Min(Math.Abs(angle - endAngle), 360 - Math.Abs(angle - endAngle));
            
            // Return the smaller difference
            return Math.Min(diffToStart, diffToEnd);
        }
    }

    public class InterceptionPointSolver
    {
        public class Result
        {
            public bool success;
            public float azimuth;
            public float arrivalSeconds;
            public float distanceYards;
        }

        public static Result Calcualte(IDF4Model shooter, IDF4Model target, float speedKnots)
        {

            var shooterLat = shooter.GetLatitudeDeg();
            var shooterLon = shooter.GetLongitudeDeg();
            var targetLat = target.GetLatitudeDeg();
            var targetLon = target.GetLongitudeDeg();

            var prevArrivalTimeSeconds = 0f;

            for (int i = 0; i < 20; i++) // solved using iteration method, success if approximated fixed point is found
            {

                var (distanceKm, azi1) = MeasureStats.Approximation.CalculateDistanceKmAndBearingDeg(shooterLat, shooterLon, targetLat, targetLon);
                var distanceNm = distanceKm * MeasureUtils.kilometerToNavalMile;
                var arrivalTimeSeconds = distanceNm / speedKnots * 3600;

                var diffSeconds = arrivalTimeSeconds - prevArrivalTimeSeconds;
                if (Math.Abs(diffSeconds) < 0.1f) // convergence condition, diff < 0.1s
                {
                    return new()
                    {
                        success = true,
                        azimuth = (float)azi1,
                        arrivalSeconds = (float)arrivalTimeSeconds,
                        distanceYards = (float)distanceKm * 1000 * MeasureUtils.meterToYard
                    };
                }
                prevArrivalTimeSeconds = (float)arrivalTimeSeconds;
                var movedM = diffSeconds / 3600 * target.GetSpeedKnots() * MeasureUtils.navalMileToMeter;
                var (_targetLat, _targetLon) = MeasureStats.Approximation.CalculateNewPosition(targetLat, targetLon, target.GetHeadingDeg(), movedM);
                targetLat = (float)_targetLat;
                targetLon = (float)_targetLon;
            }

            return new();
        }
    }

    public interface ICollider
    {
        public float GetLengthFoot();
        public float GetBeamFoot();
        public float GetHeadingDeg();
        public LatLon GetPosition();
    }

    public static class CollideUtils
    {
        public static bool IsCollided(LatLon newPosition, ICollider self, ICollider other)
        {
            var otherPos = other.GetPosition();

            // Exact Method
            // Geodesic.WGS84.Inverse(newPosition.LatDeg, newPosition.LonDeg, otherPos.LatDeg, otherPos.LonDeg, out var distanceM, out var azi1, out var azi2);
            // Approximation Method
            var (distanceKm, azi1) = MeasureStats.Approximation.CalculateDistanceKmAndBearingDeg(newPosition.LatDeg, newPosition.LonDeg, otherPos.LatDeg, otherPos.LonDeg);
            var distanceM = distanceKm * 1000;
            var azi2 = azi1;

            if (MeasureUtils.GetPositiveAngleDifference(self.GetHeadingDeg(), (float)azi1) > 90)
                return false;

            var distanceFoot = distanceM * MeasureUtils.meterToFoot;
            var lengthFoot = self.GetLengthFoot();
            var otherLengthFoot = other.GetLengthFoot();
            if (distanceFoot < lengthFoot / 2 + otherLengthFoot / 2)
            {
                var diff = MeasureUtils.GetPositiveAngleDifference(other.GetHeadingDeg(), (float)azi2);
                var coef = Math.Abs(diff - 90) / 90;
                var otherMix = otherLengthFoot * coef + other.GetBeamFoot() * (1 - coef);
                if (distanceFoot < lengthFoot / 2 + otherMix / 2)
                {
                    return true;
                }
            }
            return false;
        }


    }
}