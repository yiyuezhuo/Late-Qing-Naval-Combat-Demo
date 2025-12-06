using System.Collections.Generic;
using System.Data;
using System.Linq;
using System;
// using SunCalcNet;
using SunCalcSharp;
using CoreUtils;

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
        LightFog, // Code 3, 1-3%
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
        public int seaStateBeaufort;
        public bool hasMoonlight = true;

        // public string description;
        public GlobalString globalDescription = new();

        public SimulationClock weaponSimulationAssignmentClock = new() { intervalSeconds = 120 }; // In default setting, ship replan its course per 2 min.
        // public SimulationClock obstacleAvoidCheckClock = new() { intervalSeconds = 20 };
        public SimulationClock obstacleAvoidCheckClock = new() { intervalSeconds = 1 };
        
        public bool doingStep;
        public bool firstLoaded; // if false, loading will trigger First Load Scenario Trigger and set to true. Save Edit will set it back to false in the file.
        // public int referenceTimeZoneOffset = +8; // +8 timezone
        public bool effectiveCompleted; // TODO: Add it to UI?
        // public DateTimeOffset GetReferenceTimeZoneDateTimeOffset()
        // {
        //     var dateTimeOffset = new DateTimeOffset(dateTime);
        //     return dateTimeOffset.ToOffset(TimeSpan.FromHours(referenceTimeZoneOffset));
        // }
        public bool firstDisengaged;
        public bool firstReachEndDateTime;

        public bool hasEndDateTime;
        public DateTime endDateTime = new DateTime(1895, 4, 17, 0, 0, 0, DateTimeKind.Utc);

        public static float GetTimeZoneOffset(float longitude)
        {
            var intervals = 24f;
            var degreesPerInterval = 360f / intervals;
            return (float)Math.Round(longitude / degreesPerInterval);
        }

        public DateTime GetLocalDateTime(float longitude)
        {
            return dateTime.AddHours(GetTimeZoneOffset(longitude));
        }

        public static DateTimeOffset GetLocalDateTimeOffset(float longitude, DateTime time)
        {
            var dateTimeOffset = new DateTimeOffset(time);
            return dateTimeOffset.ToOffset(TimeSpan.FromHours(GetTimeZoneOffset(longitude)));
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