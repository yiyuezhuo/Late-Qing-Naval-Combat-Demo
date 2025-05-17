using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngineInternal;

namespace NavalCombatCore
{
    public enum Country
    {
        Japan,
        China, // Qing
        Britain,
        France,
        Russia,
        UnitedState,
        Spain,
        Germany,
        Italy,
        Austria, // Austria-Hugary
        Turkey, // Ottoman
        Holland
    }

    public enum ShipType
    {
        Battleship, // BB, ironclad, pre-dreadnought, dreadnought, post-dreadnought
        LightCruiser, // CL
        Cruiser, // CR, CC
        ArmoredCruiser, // CA
        Destroyer, // DD
        PatrolGunboat, // PG
        TorpedoBoat // TB
    }

    public enum MountLocation // SEEKRIEG like 3x3 location
    {
        PortForward,
        Forward, // A, B
        StarboardForward,
        PortMidship,
        Midship,
        StarboardMidship,
        PortAfter,
        After, // X, Y
        StarboardAfter,
    }

    public enum GunSightType
    {
        Basic,
        Telescope
    }

    public enum FireControlInstrumentType
    {
        None,
        Basic,
        MechanicalComputer,
        AdvancedMechanicalComputer,
    }

    public enum RangeFinderType
    {
        None,
        Optical
    }

    public enum DirectorControlType
    {
        None,
        FollowThePointer, // FTP
        Director,
    }

    public enum StabilizationType
    {
        Manual,
        GyroAssisted,
        StableElement
    }

    public enum PowerRemoteControlType // RPC
    {
        None,
        Partial,
        Full
    }

    public class FireControlSystem
    {
        public GunSightType gunSight;
        public FireControlInstrumentType fireControlInstrument;
        public RangeFinderType rangeFinder;
        public DirectorControlType directorControl;
        public StabilizationType stabilization;
        public PowerRemoteControlType powerRemoteControl;
    }

    public enum RangeBand
    {
        Short,
        Medium,
        Long,
        Extreme
    }

    public class FireControlTableRecord
    {
        public float speedThresholdKnot;
        public float shortBroad;
        public float shortNarrow;
        public float mediumBroad;
        public float mediumNarrow;
        public float longBroad;
        public float longNarrow;
        public float extremeBroad;
        public float extremeNarrow;
    }

    public enum AmmunitionType
    {
        ArmorPiercing, // AP
        SemiArmorPiercing, // SAP
        Common, // COM
        HighExplosive, // HE
    }

    public class PenetrationTableRecord
    {
        public float distanceYards;
        public float rateOfFire; // Rounds per 2 minutes (1 SK game turn = 2 min)
        public RangeBand rangeBand;
        public float horizontalPenetrationInchs;
        public float verticalPenetrationInchs;
    }

    public class MountArcRecord
    {
        public float startDeg;
        public float endDeg;
    }

    public class MountLocationRecord
    {
        public MountLocation mountLocation;
        public int barrels; // Single, Double, Triple, Quadruple
        public int mounts;
        public List<MountArcRecord> mountArcs = new() { new() };
        public float restAngleDeg; // Graphic purpose only
        public bool trainable; // for torpedo
    }

    public class BatteryRecord
    {
        public GlobalString name;
        public float damageRating;
        public float maxRateOfFireShootPerMin; // shoot/min
        public int fireControlPositions;
        public FireControlSystem fireControlType;
        public float rangeYards;
        public float fireControlRadarModifier;
        public GlobalString fireControlRadarName;
        public float shellSizeInch;
        public float shellWeightPounds; // lb
        public int ammunitionCapacity;
        
        public List<FireControlTableRecord> fireControlTableRecords = new() { new() };
        public List<PenetrationTableRecord> penetrationTableRecords = new(){new()};
        public List<MountLocationRecord> mountLocationRecords = new(){new()};
    }

    public class TorpedoSetting
    {
        public float rangeYards;
        public float speedKnots;
    }

    public enum TorpedoDamageClass
    {
        A,
        B,
        C,
        D,
        E,
        F,
        G,
        H,
        I
    }

    public class TorpedoSector
    {
        public GlobalString name;
        public List<MountLocationRecord> mountLocationRecords = new() { new() };
        public List<TorpedoSetting> torpedoSettings = new() { new() };
        public int ammunitionCapacity;
        public TorpedoDamageClass damageClass;
    }

    public class RapidFireBatteryFireControlLevelRecord
    {
        public float fireControlMaxRange;
        public float fireControlEffectiveRange;
    }

    public class RapidFireBatteryRecord
    {
        public GlobalString name;
        public float maxRangeYards;
        public float effectiveRangeYards;
        public List<RapidFireBatteryFireControlLevelRecord> fireControlRecords = new() { new() };
        public float damageFactor; // RF
        public List<int> barrelsLevelPort = new() { 0};
        public List<int> barrelsLevelStarboard= new() {0 };
    }

    public class SpeedIncreaseRecord
    {
        public float thresholdSpeedKnots;
        public float increaseSpeedKnots;
    }

    public class ArmorRatingReocrd
    {
        public float effectInch;
        public float actualInch;
    }

    public class ArmorRating // Carrier is ignored at this point
    {
        public ArmorRatingReocrd deck = new(); // 1H
        public ArmorRatingReocrd turretHorizontal = new(); // 2H
        public ArmorRatingReocrd superStructureHorizontal = new(); // 3H
        public ArmorRatingReocrd conningTower = new(); // 4V
        public ArmorRatingReocrd beltMiddle = new(); // 5V
        public ArmorRatingReocrd beltEnd = new(); // 6V
        public ArmorRatingReocrd barbette = new(); // 7V
        public ArmorRatingReocrd turretVertical = new(); // 8V
        public ArmorRatingReocrd superStructureVertical = new(); // 9V
    }

    public class ShipClass
    {
        public GlobalString name;
        public ShipType type;
        public Country country;
        public float displacementTons;
        public float lengthFoot;
        public float beamFoot;
        public float draftFoot;
        public string launchedDate;
        public string completedDate;
        public List<BatteryRecord> batteryRecords = new() { new() };
        public TorpedoSector torpedoSector = new();
        public List<RapidFireBatteryRecord> rapidFireBatteryRecords = new() { new() };
        public int targetSizeModifier;
        public float damagePoint;
        public float speedKnots;
        public int damageControlRatingUnmodified;
        public List<float> speedKnotsEngineRoomsLevels = new() { 0 };
        public List<float> speedKnotsPropulsionShaftLevels = new() { 0 };
        public List<float> speedKnotsBoilerRooms = new() { 0 };
        public List<SpeedIncreaseRecord> speedIncreaseRecord = new() { new() };
        public float standardTurnDegPer2Min; // per 2 min
        public float emergencyTurnDegPer2Min; // per 2 min
        public ArmorRating armorRating = new();
    }

}