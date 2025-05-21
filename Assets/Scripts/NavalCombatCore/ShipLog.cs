using System.Collections.Generic;
using System.Data;
using System.Linq;

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

    public class MountStatusRecord : IObjectIdLabeled
    {
        public string objectId { get; set; }
        public MountStatus status;
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

    public class RapidFiringStatus
    {
        public int portMountHits;
        public int starboardMountHits;
        public int fireControlHits;
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

    public partial class ShipLog : IObjectIdLabeled
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
        public float damagePoints;
        // DCR Modifier or DCR modifier type?

        public List<BatteryStatus> batteryStatus = new();
        public TorpedoSectorStatus torpedoSectorStatus = new();
        public List<RapidFiringStatus> rapidFiringStatus = new();
        public DynamicStatus dynamicStatus = new();
        public SearchLightStatus searchLightHits = new();
        public int damageControlRatingHits;
        public List<DamageEffectRecord> damageEffectRecords = new();
        public List<ShipboardFireStatus> shipboardFireStatus = new();

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
        }
    }
}