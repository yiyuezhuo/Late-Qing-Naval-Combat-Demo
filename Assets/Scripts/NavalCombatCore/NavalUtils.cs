
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System;
// using SunCalcNet;
using SunCalcSharp;

namespace NavalCombatCore
{
    public static class NavalUtils
    {
        public const float TargetSilhouettedByHorizonAzimuthToleranceDeg = 30f;
        public const float SearchlightSectorSweepDeg = 30f;
        public const float SearchlightSectorRangeYards = 3500f;

        public static SunState GetSunPosition(DateTime dateTime, LatLon latLon)
        {
            // var dt = dateTime;
            // var validDateTime = new DateTime(2025, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second); // SunCalc's support year range is limited, so we reproject the year. However this will introduce little error.
            // var dt = GetLocalDateTime(latLon.LonDeg);
            var dt = dateTime;
            var sunPos = SunCalc.GetPosition(dt, latLon.LatDeg, latLon.LonDeg);
            var azimuthDeg = (sunPos.Azimuth / 2 / Math.PI * 360 + 180) % 360;
            var altitudeDeg = sunPos.Altitude / 2 / Math.PI * 360;
            return new SunState()
            {
                azimuthDeg = (float)azimuthDeg,
                altitudeDeg = (float)altitudeDeg
            };
        }

        public static int GetDawnDuskFireControlOffset(SunState targetSunState, float observerToTargetTrueBearingRelativeToNorthDeg)
        {
            if (targetSunState == null || targetSunState.GetDayNightLevel() != DayNightLevel.Twilight)
                return 0;

            // CaS5 gives a 30-degree azimuth tolerance for dawn/dusk target-lighting sectors; SK5 lists the modifier but does not define the test.
            var silhouettedDiff = MeasureUtils.GetPositiveAngleDifference(
                targetSunState.azimuthDeg,
                observerToTargetTrueBearingRelativeToNorthDeg
            );
            if (silhouettedDiff <= TargetSilhouettedByHorizonAzimuthToleranceDeg)
                return 1;

            var darknessDiff = 180f - silhouettedDiff;
            if (darknessDiff <= TargetSilhouettedByHorizonAzimuthToleranceDeg)
                return -2;

            return 0;
        }

        public static bool IsUsingSearchlight(ShipLog ship)
        {
            if (ship?.searchLightHits == null)
                return false;

            if (IsSearchlightDisabled(ship))
                return false;

            return ship.searchLightHits.portEnabled || ship.searchLightHits.starboardEnabled;
        }

        public static bool IsSearchlightDisabled(ShipLog ship)
        {
            return ship == null || ship.GetSubStates<IElectronicSystemModifier>().Any(mod => mod.IsSearchLightDisabled());
        }

        public static RapidFiringBatteryLocation GetBatterySide(float bearingRelativeToBowDeg)
        {
            return MeasureUtils.NormalizeAngle(bearingRelativeToBowDeg) > 180f
                ? RapidFiringBatteryLocation.Port
                : RapidFiringBatteryLocation.Starboard;
        }

        public static bool CanOperateSearchlight(ShipLog ship, RapidFiringBatteryLocation side)
        {
            if (ship?.searchLightHits == null || IsSearchlightDisabled(ship))
                return false;

            return side == RapidFiringBatteryLocation.Port
                ? ship.searchLightHits.CanUsePortSearchlight()
                : ship.searchLightHits.CanUseStarboardSearchlight();
        }

        public static bool TryResolveSearchlightTargetAssignment(
            ShipLog illuminator,
            ShipLog target,
            out RapidFiringBatteryLocation side,
            out float directionDeg)
        {
            side = RapidFiringBatteryLocation.Starboard;
            directionDeg = 0f;

            if (illuminator == null || target == null || illuminator == target || !target.IsOnMap())
                return false;

            var stats = MeasureStats.MeasureApproximation(illuminator, target);
            if (stats.distanceYards > SearchlightSectorRangeYards)
                return false;

            side = GetBatterySide(stats.observerToTargetBearingRelativeToBowDeg);
            if (!CanOperateSearchlight(illuminator, side))
                return false;

            directionDeg = stats.observerToTargetBearingRelativeToBowDeg;
            return true;
        }

        public static bool IsAfire(ShipLog ship)
        {
            return ship != null && ship.GetSubStates<ShipboardFireState>().Any(state => state.severity >= 50f);
        }

        static bool IsTargetInsideSearchlightSector(ShipLog illuminator, ShipLog target, bool enabled, float directionDeg)
        {
            if (!enabled)
                return false;

            var stats = MeasureStats.MeasureApproximation(illuminator, target);
            if (stats.distanceYards > SearchlightSectorRangeYards)
                return false;

            return MeasureUtils.GetPositiveAngleDifference(stats.observerToTargetBearingRelativeToBowDeg, directionDeg) <= SearchlightSectorSweepDeg * 0.5f;
        }

        public static bool IsIlluminatedBySearchlight(ShipLog target)
        {
            if (target == null || NavalGameState.Instance == null)
                return false;

            foreach (var illuminator in NavalGameState.Instance.shipLogsOnMap)
            {
                if (illuminator == null || illuminator == target || !IsUsingSearchlight(illuminator))
                    continue;

                var searchLightHits = illuminator.searchLightHits;
                if (searchLightHits == null)
                    continue;

                if (IsTargetInsideSearchlightSector(illuminator, target, searchLightHits.portEnabled, searchLightHits.portDirectionDeg) ||
                    IsTargetInsideSearchlightSector(illuminator, target, searchLightHits.starboardEnabled, searchLightHits.starboardDirectionDeg))
                {
                    return true;
                }
            }

            return false;
        }

        public static (int fireControlOffset, bool targetUsingSearchlight, bool targetIlluminatedBySearchlight, bool targetAfire) GetNightIlluminationFireControlModifier(
            ShipLog target,
            SunState targetSunState)
        {
            if (target == null || targetSunState == null || targetSunState.GetDayNightLevel() == DayNightLevel.Day)
                return (0, false, false, false);

            var targetUsingSearchlight = IsUsingSearchlight(target);
            var targetIlluminatedBySearchlight = IsIlluminatedBySearchlight(target);
            var targetAfire = IsAfire(target);

            var fireControlOffset = (targetAfire || targetIlluminatedBySearchlight) ? 2 : (targetUsingSearchlight ? 1 : 0);
            return (fireControlOffset, targetUsingSearchlight, targetIlluminatedBySearchlight, targetAfire);
        }
    }
}
