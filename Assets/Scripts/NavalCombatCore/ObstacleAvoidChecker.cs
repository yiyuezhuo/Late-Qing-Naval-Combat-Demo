using System.Collections.Generic;
using CoreUtils;
using GeographicLib;

namespace NavalCombatCore
{
    public class ObstacleAvoidChecker
    {
        // Argments
        // public ShipLog shipLog;
        public LatLon latLon;
        public float initialDesiredHeadingDeg;
        public float speedMeterPerSecond;
        // public IElevationProvider elevationProvider;

        // States
        Dictionary<float, bool> headingPassedMap = new();

        public static float stepDeg = 1; //
        public static float boundDeg = 10; //
        public static float extrapolateSecondsLow = 60; // 1min
        public static float extrapolateMinHigh = 300; // 5min
        public static float extrapolateMinStep = 60; // 1min/step

        public static ObstacleAvoidChecker Extract(ShipLog shipLog)
        {
            var speedKnots = shipLog.GetSpeedKnots();
            var speedMeterPerSecond = speedKnots / 3600 * MeasureUtils.navalMileToMeter;

            var latLon = new LatLon(shipLog.GetLatitudeDeg(), shipLog.GetLongitudeDeg());

            return new()
            {
                latLon=latLon,
                initialDesiredHeadingDeg=shipLog.desiredHeadingDeg,
                speedMeterPerSecond=speedMeterPerSecond,
                // elevationProvider=ServiceLocator.Get<IElevationProvider>()
            };
        }

        public float Check()
        {
            if(speedMeterPerSecond <= 0)
                return initialDesiredHeadingDeg;

            for(var deltaHeadingDeg = 0f; deltaHeadingDeg < 180; deltaHeadingDeg += stepDeg)
            {
                var currentHeading = initialDesiredHeadingDeg + deltaHeadingDeg;
                if(Detect3(currentHeading))
                {
                    return currentHeading;
                }
                if(deltaHeadingDeg > 0)
                {
                    currentHeading = initialDesiredHeadingDeg - deltaHeadingDeg;
                    if(Detect3(currentHeading))
                    {
                        return currentHeading;
                    }
                }
            }

            return initialDesiredHeadingDeg;
        }

        public bool Detect3(float headingDeg)
        {
            // headingDeg = MeasureUtils.NormalizeAngle(headingDeg);
            return Detect(headingDeg - boundDeg)
                && Detect(headingDeg)
                && Detect(headingDeg + boundDeg);
        }

        public bool Detect(float headingDeg)
        {
            if(headingPassedMap.TryGetValue(headingDeg, out var passed))
                return passed;

            var ret = true;
            for(var extrapolateSeconds = extrapolateSecondsLow; extrapolateSeconds <= extrapolateMinHigh; extrapolateSeconds += extrapolateMinStep)
            {
                var distM = speedMeterPerSecond * extrapolateSeconds;
                double arcLength = Geodesic.WGS84.Direct(latLon.LatDeg, latLon.LonDeg, headingDeg, distM, out double lat2, out double lon2);
                var latLon2 = new LatLon((float)lat2, (float)lon2);
                // if(elevationProvider.GetElevation(latLon2) >= 0)
                if(ElevationService.Instance.GetElevation(latLon2) > 0)
                {
                    ret = false;
                    break;
                }
            }

            headingPassedMap[headingDeg] = ret;
            return ret;
        }

    }
}