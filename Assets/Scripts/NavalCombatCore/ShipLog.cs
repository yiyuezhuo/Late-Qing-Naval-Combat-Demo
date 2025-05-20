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

    public class BatteryStatus
    {
        public BatteryAmmunitionRecord ammunition = new(); // TODO: based on mount instead of battery?
        public List<MountStatus> mountStatus = new();
        public int fireControlHits;
    }

    public class TorpedoSectorStatus
    {
        public int ammunition;
        public List<MountStatus> mountStatus = new();
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

    public partial class ShipLog
    {
        // public ShipClass shipClass;
        public string shipClassStr;
        public ShipClass shipClass
        {
            get => NavalGameState.Instance.shipClasses.FirstOrDefault(x => x.name.english == shipClassStr);
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
    }
}