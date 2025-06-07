using System.Collections.Generic;
using System.Data;
using System.Linq;
using System;
// using SunCalcNet;
using SunCalcSharp;

namespace NavalCombatCore
{
    public enum DayNightLevel
    {
        Day,
        Night,
        Twilight,
    }

    public class SunState
    {
        public float azimuthDeg;
        public float altitudeDeg;
        public DayNightLevel GetDayNightLevel()
        {
            if (altitudeDeg > 5)
                return DayNightLevel.Day;
            else if (altitudeDeg > 0)
                return DayNightLevel.Twilight;
            return DayNightLevel.Night;
        }
    }

    public enum VisibilityDescription
    {
        DenseFog, // Code: 0-2, 0%
        LightFog, // COde 3, 1-3%
        ThinFog, // Code 4, 3-5%
        Haze, // Code 5, 5-8%
        LightHaze, // Code 6, 8-18%
        Clear, // Code 7, 18-37%
        VeryClear1, // Code 8, 37-60%
        VeryClear2, // Code 8, 60-85%
        ExceptionallyClear // Code 9, 85%-95%
    }

    public partial class ScenarioState
    {
        public DateTime dateTime = new DateTime(1894, 9, 17, 4, 30, 0, DateTimeKind.Utc); // 4:30 +8 (TZ) => 12:30, thus begin time of the Battle of Yalu river
        // public DateTime dateTime = DateTime.Now;
        // public DateTime dateTime = DateTime.UtcNow;
        // public DateTime dateTime = new DateTime(2013, 3, 5, 0, 0, 0, DateTimeKind.Utc);
        // public DateTime dateTime = new DateTime(2013, 9, 7, 0, 0, 0, DateTimeKind.Utc);
        // public DateTime dateTime = new DateTime(2013, 9, 7, 4, 30, 0, DateTimeKind.Utc);
        // public DateTime dateTime = new DateTime(2013, 9, 17, 4, 30, 0, DateTimeKind.Utc);
        public VisibilityDescription visibility = VisibilityDescription.ExceptionallyClear;

        public float GetTimeZoneOffset(float longtitude)
        {
            var intervals = 24f;
            var degreesPerInterval = 360f / intervals;
            return (float)Math.Round(longtitude / degreesPerInterval);
        }

        public DateTime GetLocalDateTime(float longitude)
        {
            return dateTime.AddHours(GetTimeZoneOffset(longitude));
        }

        public SunState GetSunPosition(LatLon latLon)
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

        public void Step(float deltaSeconds)
        {
            dateTime = dateTime.AddSeconds(deltaSeconds);
        }

        // void Test()
        // {
        //     SunCalc.GetSunPosition()
        // }
    }
}