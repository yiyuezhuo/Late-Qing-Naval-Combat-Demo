using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        public bool simple;
        // public IElevationProvider elevationProvider;

        // States
        Dictionary<float, bool> headingPassedMap = new();

        public static float stepDeg = 10; //
        public static float simpleStepDeg = 20;

        public static float boundDeg = 10; //

        public static float extrapolateSecondsLow = 60; // 1min
        public static float extrapolateMinHigh = 300; // 5min
        // public static float extrapolateSecondsLow = 60; // 2min
        // public static float extrapolateMinHigh = 120; // 2min
        public static float extrapolateMinStep = 60; // 1min/step

        public static bool useBound = true;
        public static bool simpleUseBound = true;

        public static List<float> extrapolateRange = new(){30, 60, 120, 240};
        public static List<float> simpleExtrapolateRange = new(){60};
        
        

        public static ObstacleAvoidChecker Extract(ShipLog shipLog, bool simple=false)
        {
            // var speedKnots = shipLog.GetSpeedKnots();
            // if(speedKnots < 0) // Assume it's move astern to resolve collision
            //     speedKnots = 4;
            var speedKnots = Math.Max(shipLog.GetSpeedKnots(), shipLog.GetMaxSpeedKnots() * 0.75f);
            
            var speedMeterPerSecond = speedKnots / 3600 * MeasureUtils.navalMileToMeter;

            var latLon = new LatLon(shipLog.GetLatitudeDeg(), shipLog.GetLongitudeDeg());

            return new()
            {
                latLon=latLon,
                initialDesiredHeadingDeg=shipLog.desiredHeadingDeg,
                speedMeterPerSecond=speedMeterPerSecond,
                simple=simple
                // elevationProvider=ServiceLocator.Get<IElevationProvider>()
            };
        }

        public float GetStepDeg() => simple ? simpleStepDeg : stepDeg;

        public float Check()
        {
            if(speedMeterPerSecond <= 0)
                return initialDesiredHeadingDeg;

            for(var deltaHeadingDeg = 0f; deltaHeadingDeg < 180; deltaHeadingDeg += GetStepDeg())
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

        public bool IsUseBound() => simple ? simpleUseBound : useBound;

        public bool Detect3(float headingDeg)
        {
            if(IsUseBound())
            {
                return Detect(headingDeg - boundDeg)
                    && Detect(headingDeg)
                    && Detect(headingDeg + boundDeg);
            }
            return Detect(headingDeg);
        }

        public List<float> GetExtrapolateRange() => simple ? simpleExtrapolateRange : extrapolateRange;

        public bool Detect(float headingDeg)
        {
            if(headingPassedMap.TryGetValue(headingDeg, out var passed))
                return passed;

            var ret = true;
            // for(var extrapolateSeconds = extrapolateSecondsLow; extrapolateSeconds <= extrapolateMinHigh; extrapolateSeconds += extrapolateMinStep)
            foreach(var extrapolateSeconds in GetExtrapolateRange())
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