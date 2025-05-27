using System.Collections.Generic;
using System.Data;
using System.Linq;
using System;
using Unity.VisualScripting;
using Unity.Profiling;

namespace NavalCombatCore
{
    public class BatteryAmmunitionRecord
    {
        public int ArmorPiercing;
        public int semiArmorPiercing;
        public int common;
        public int highExplosive;
    }

    public enum MountStatus
    {
        Operational,
        Disabled, // may restore after a period of time or by die roll
        Destroyed
    }

    public partial class MountStatusRecord : IObjectIdLabeled
    {
        public string objectId { get; set; }
        public MountStatus status; // Is "SectorStatus" a better name?
        public int mountsDestroyed;

        public MountLocationRecord GetMountLocationRecord()
        {
            var battery = EntityManager.Instance.GetParent<BatteryStatus>(this);

            if (battery == null)
                return null;

            var mountIdx = battery.mountStatus.IndexOf(this);

            var batteryRecord = battery.GetBatteryRecord();
            if (batteryRecord == null)
                return null;
            // var shipLog = EntityManager.Instance.GetParent<ShipLog>(battery);
            // if (shipLog == null)
            //     return null;

            // var batteryIndex = shipLog.batteryStatus.IndexOf(battery);

            // var shipClass = shipLog.shipClass;
            // if (shipClass == null)
            //     return null;

            // if (batteryIndex < 0 || batteryIndex >= shipClass.batteryRecords.Count)
            //     return null;

            // var batteryRecord = shipClass.batteryRecords[batteryIndex];

            if (mountIdx < 0 || mountIdx >= batteryRecord.mountLocationRecords.Count)
                return null;

            var mountLocationRecord = batteryRecord.mountLocationRecords[mountIdx];

            return mountLocationRecord;
        }

        public MountLocationRecord GetTorpedoMountLocationRecord()
        {
            var shipLog = EntityManager.Instance.GetParent<ShipLog>(this);
            if (shipLog == null)
                return null;

            var mountIdx = shipLog.torpedoSectorStatus.mountStatus.IndexOf(this);

            var shipClass = shipLog.shipClass;
            if (shipClass == null)
                return null;

            if (mountIdx < 0 || mountIdx >= shipClass.torpedoSector.mountLocationRecords.Count)
                return null;

            var mountLocationRecord = shipClass.torpedoSector.mountLocationRecords[mountIdx];
            return mountLocationRecord;
        }
    }

    public partial class BatteryStatus : IObjectIdLabeled
    {
        public string objectId { get; set; }
        public BatteryAmmunitionRecord ammunition = new(); // TODO: based on mount instead of battery?
        public List<MountStatusRecord> mountStatus = new();
        public int fireControlHits;

        public IEnumerable<IObjectIdLabeled> GetSubObjects()
        {
            foreach (var mount in mountStatus)
            {
                yield return mount;
            }
        }

        public BatteryRecord GetBatteryRecord()
        {
            var shipLog = EntityManager.Instance.GetParent<ShipLog>(this);
            if (shipLog == null)
                return null;
            var idx = shipLog.batteryStatus.IndexOf(this);
            var shipClass = shipLog.shipClass;
            if (shipClass == null || idx < 0 || idx >= shipClass.batteryRecords.Count)
                return null;
            return shipClass.batteryRecords[idx];
        }

        // public ShipLog GetShipLog()
        // {
        //     return EntityManager.Instance.GetParent<ShipLog>(this);
        // }

        // public BatteryRecord GetBatteryRecord() // TODO: Performance issue?
        // {
        //     var shipLog = GetShipLog();
        //     var idx = shipLog.batteryStatus.IndexOf(this);
        //     var shipClass = shipLog.shipClass;
        //     if (idx < 0 || idx >= shipClass.batteryRecords.Count)
        //         return null;
        //     return shipClass.batteryRecords[idx];
        // }
    }

    public class TorpedoSectorStatus
    {
        public int ammunition;
        public List<MountStatusRecord> mountStatus = new();
    }

    public partial class RapidFiringStatus : IObjectIdLabeled
    {
        public string objectId { get; set; }
        public int portMountHits;
        public int starboardMountHits;
        public int fireControlHits;

        public RapidFireBatteryRecord GetRapidFireBatteryRecord()
        {
            var shipLog = EntityManager.Instance.GetParent<ShipLog>(this);
            if (shipLog == null)
                return null;
            var idx = shipLog.rapidFiringStatus.IndexOf(this);
            var shipClass = shipLog.shipClass;
            if (shipClass == null || idx < 0 || idx >= shipClass.rapidFireBatteryRecords.Count)
                return null;
            return shipClass.rapidFireBatteryRecords[idx];
        }

        public string GetInfo()
        {
            var r = rapidFireBatteryRecord;
            if (r == null)
                return "Not Valid";

            var ( portClass, portCurrent) = GetClassCurrentBarrels(r.barrelsLevelPort, portMountHits);
            var ( starboardClass, starboardCurrent) = GetClassCurrentBarrels(r.barrelsLevelStarboard, starboardMountHits);

            return $"{portClass}({portCurrent})/{starboardClass}({starboardCurrent}) {r.name.mergedName}";
        }

        (int, int) GetClassCurrentBarrels(List<int> barrelsLevel, int hit)
        {
            hit = Math.Max(0, hit);
            if (barrelsLevel.Count == 0)
                return (0, 0);
            var barrelsClass = barrelsLevel[0];
            var barrelsCurrent = hit >= barrelsLevel.Count ? 0 : barrelsLevel[hit];
            return (barrelsClass, barrelsCurrent);
        }
    }

    public class DynamicStatus
    {
        public float speedKnotsDirectModifier;
        public int engineRoomHits;
        public int propulsionShaftHits;
        public int boilerRoomHits;
    }

    public class SearchLightStatus
    {
        public int portHit;
        public int starboardHit;
    }

    public enum DamageEffectCode
    {
        Invalid,
        CannotToRight
    }

    public class DamageEffectRecord
    {
        public DamageEffectCode code;
        public float severity;
        public float countdownSeconds;
    }

    public class ShipboardFireStatus
    {
        public string description; // location & start time description
        public float severity;
        public float countdownSeconds;
    }

    public interface IDF3Model
    {
        float GetLatitudeDeg();
        float GetLongitudeDeg();
        float GetHeadingDeg();
    }

    public enum MapState
    {
        NotDeployed,
        Deployed,
        Destroyed
    }

    public partial class ShipLog : IObjectIdLabeled, IDF3Model, IShipGroupMember
    {
        public string objectId { get; set; }
        // public ShipClass shipClass;
        public string shipClassObjectId;
        // public string shipClassStr;
        public ShipClass shipClass
        {
            // get => NavalGameState.Instance.shipClasses.FirstOrDefault(x => x.name.english == shipClassStr);
            get => EntityManager.Instance.Get<ShipClass>(shipClassObjectId);
        }
        public GlobalString name = new();
        public GlobalString captain = new();
        public int crewRating;
        public float damagePoint; // current damage point vs "max" damage point defined in the class
        public LatLon position = new();
        public float speedKnots; // current speed vs "max" speed defined in the class
        public float headingDeg;
        public MapState mapState;
        // DCR Modifier or DCR modifier type?

        public float GetLatitudeDeg() => position.LatDeg;
        public float GetLongitudeDeg() => position.LonDeg;
        public float GetHeadingDeg() => headingDeg;

        public List<BatteryStatus> batteryStatus = new();
        public TorpedoSectorStatus torpedoSectorStatus = new();
        public List<RapidFiringStatus> rapidFiringStatus = new();
        public DynamicStatus dynamicStatus = new();
        public SearchLightStatus searchLightHits = new();
        public int damageControlRatingHits;
        public List<DamageEffectRecord> damageEffectRecords = new();
        public List<ShipboardFireStatus> shipboardFireStatus = new();

        // remarks
        public string launchedDate;
        public string completedDate;
        public GlobalString fateDesc = new();

        public string parentObjectId { get; set; }

        public string GetMemberName() => name.mergedName;

        

        public IEnumerable<IObjectIdLabeled> GetSubObjects()
        {
            foreach (var obj in batteryStatus)
            {
                yield return obj;
            }

            foreach (var obj in torpedoSectorStatus.mountStatus)
            {
                yield return obj;
            }

            foreach (var obj in rapidFiringStatus)
            {
                yield return obj;
            }
        }

        public bool IsOnMap() => mapState == MapState.Deployed;
    }
}