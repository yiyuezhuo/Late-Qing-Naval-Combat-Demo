using System;
using System.Collections.Generic;
using CoreUtils;
using GeographicLib;
using UnityEngine;

namespace NavalCombatCore
{
    public enum ObstacleAvoidanceMode
    {
        None,
        Weak,
        Strong
    }

    public sealed class ObstacleAvoidanceParameters
    {
        public float roiPreviewSeconds;
        public float roiHardClearancePixels;
        public float roiInfluenceDistancePixels;
        public float roiEarlyExitDistancePixels;

        public static readonly ObstacleAvoidanceParameters Strong = new()
        {
            roiPreviewSeconds = 75f,
            roiHardClearancePixels = 1.5f,
            roiInfluenceDistancePixels = 4f,
            roiEarlyExitDistancePixels = 15f
        };

        public static readonly ObstacleAvoidanceParameters Weak = new()
        {
            roiPreviewSeconds = 37.5f,
            roiHardClearancePixels = 0.75f,
            roiInfluenceDistancePixels = 2f,
            roiEarlyExitDistancePixels = 7.5f
        };
    }

    public class ObstacleAvoidChecker
    {
        const float ROIShoreGradientEpsilon = 1e-4f;
        const float LegacyStepDeg = 10f;
        const float LegacySimpleStepDeg = 20f;
        const float LegacyBoundDeg = 10f;
        static readonly float[] LegacyExtrapolateRange = { 30f, 60f, 120f, 240f };
        static readonly float[] LegacySimpleExtrapolateRange = { 60f };

        // Argments
        // public ShipLog shipLog;
        public LatLon latLon;
        public float initialDesiredHeadingDeg;
        public float speedMeterPerSecond;
        public bool simple;
        public ObstacleAvoidanceParameters parameters;
        // public IElevationProvider elevationProvider;

        // States
        Dictionary<float, bool> headingPassedMap = new();

        public static bool enableROIShoreFieldAvoidance = true;

        public static ObstacleAvoidChecker Extract(ShipLog shipLog, ObstacleAvoidanceParameters parameters, bool simple=false)
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
                simple=simple,
                parameters=parameters ?? ObstacleAvoidanceParameters.Strong
                // elevationProvider=ServiceLocator.Get<IElevationProvider>()
            };
        }

        float GetLegacyStepDeg() => simple ? LegacySimpleStepDeg : LegacyStepDeg;

        public float Check()
        {
            if (TryCheckROIShoreField(out var shoreFieldHeadingDeg))
                return shoreFieldHeadingDeg;

            return CheckLegacy();
        }

        float CheckLegacy()
        {
            if(speedMeterPerSecond <= 0)
                return initialDesiredHeadingDeg;

            for(var deltaHeadingDeg = 0f; deltaHeadingDeg < 180; deltaHeadingDeg += GetLegacyStepDeg())
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

        bool TryCheckROIShoreField(out float newHeadingDeg)
        {
            newHeadingDeg = initialDesiredHeadingDeg;

            if (speedMeterPerSecond <= 0 || !enableROIShoreFieldAvoidance)
                return false;

            if (ElevationService.Instance.elevationProvider is not ElevationProvider elevationProvider)
                return false;

            if (!elevationProvider.TrySampleROIShoreField(latLon, out var currentSample))
                return false;

            if (currentSample.distancePixels >= parameters.roiEarlyExitDistancePixels)
            {
                newHeadingDeg = initialDesiredHeadingDeg;
                return true;
            }

            var previewDistanceM = speedMeterPerSecond * parameters.roiPreviewSeconds;
            Geodesic.WGS84.Direct(latLon.LatDeg, latLon.LonDeg, initialDesiredHeadingDeg, previewDistanceM, out double lat2, out double lon2);
            var previewLatLon = new LatLon((float)lat2, (float)lon2);
            if (!elevationProvider.TrySampleROIShoreField(previewLatLon, out var previewSample))
                return false;

            var goal = HeadingDegToVector(initialDesiredHeadingDeg);
            var away = ResolveAwayVector(currentSample, previewSample);
            var previewDistancePixels = previewSample.distancePixels;
            var avoidWeight = previewDistancePixels <= parameters.roiHardClearancePixels
                ? 1f
                : Mathf.Clamp01((parameters.roiInfluenceDistancePixels - previewDistancePixels) / (parameters.roiInfluenceDistancePixels - parameters.roiHardClearancePixels));

            if (avoidWeight <= 0f)
            {
                newHeadingDeg = initialDesiredHeadingDeg;
                return true;
            }

            if (away.sqrMagnitude <= ROIShoreGradientEpsilon)
                return false;

            away.Normalize();
            var tangentLeft = new Vector2(-away.y, away.x);
            var tangentRight = -tangentLeft;
            var tangent = Vector2.Dot(goal, tangentLeft) >= Vector2.Dot(goal, tangentRight) ? tangentLeft : tangentRight;

            Vector2 steer = previewDistancePixels <= parameters.roiHardClearancePixels
                ? tangent * 0.55f + away * 1.0f
                : goal * (1f - avoidWeight) + tangent * avoidWeight * 0.7f + away * avoidWeight * 0.9f;

            if (steer.sqrMagnitude <= ROIShoreGradientEpsilon)
                return false;

            steer.Normalize();
            newHeadingDeg = VectorToHeadingDeg(steer);
            return true;
        }

        bool IsUseLegacyBound() => true;

        public bool Detect3(float headingDeg)
        {
            if(IsUseLegacyBound())
            {
                return Detect(headingDeg - LegacyBoundDeg)
                    && Detect(headingDeg)
                    && Detect(headingDeg + LegacyBoundDeg);
            }
            return Detect(headingDeg);
        }

        IReadOnlyList<float> GetLegacyExtrapolateRange() => simple ? LegacySimpleExtrapolateRange : LegacyExtrapolateRange;

        public bool Detect(float headingDeg)
        {
            if(headingPassedMap.TryGetValue(headingDeg, out var passed))
                return passed;

            var ret = true;
            // for(var extrapolateSeconds = extrapolateSecondsLow; extrapolateSeconds <= extrapolateMinHigh; extrapolateSeconds += extrapolateMinStep)
            foreach(var extrapolateSeconds in GetLegacyExtrapolateRange())
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

        static Vector2 HeadingDegToVector(float headingDeg)
        {
            var rad = headingDeg * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(rad), Mathf.Cos(rad));
        }

        static float VectorToHeadingDeg(Vector2 direction)
        {
            return MeasureUtils.NormalizeAngle(Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg);
        }

        static Vector2 ResolveAwayVector(ShoreFieldSample currentSample, ShoreFieldSample previewSample)
        {
            var previewGradient = previewSample.gradient;
            var currentGradient = currentSample.gradient;

            if (previewGradient.sqrMagnitude > ROIShoreGradientEpsilon && currentGradient.sqrMagnitude > ROIShoreGradientEpsilon)
                return previewGradient.normalized * 0.8f + currentGradient.normalized * 0.2f;

            if (previewGradient.sqrMagnitude > ROIShoreGradientEpsilon)
                return previewGradient;

            return currentGradient;
        }

    }
}
