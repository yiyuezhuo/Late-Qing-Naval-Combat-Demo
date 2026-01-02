
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
    }
}