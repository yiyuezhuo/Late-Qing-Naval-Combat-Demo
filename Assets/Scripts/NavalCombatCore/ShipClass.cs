using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using System.Xml;
using System.IO;
using MathNet.Numerics.Distributions;
using System.Linq;
using System.Collections;

namespace NavalCombatCore
{
    public enum Country
    {
        General,
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
        NotSpecified,
        Battleship, // BB, ironclad, pre-dreadnought, dreadnought, post-dreadnought
        LightCruiser, // CL
        Cruiser, // CR, CC
        ArmoredCruiser, // CA
        Destroyer, // DD
        PatrolGunboat, // PG
        TorpedoBoat, // TB
        ArmedMerchantCruiser // AMC
    }

    public enum MountLocation // SEEKRIEG like 3x3 location
    {
        NotSpecified, // indicate binding error
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

        public float GetValue(RangeBand rangeBand, TargetAspect targetAspect)
        {
            return (rangeBand, targetAspect) switch
            {
                (RangeBand.Short, TargetAspect.Broad) => shortBroad,
                (RangeBand.Short, TargetAspect.Narrow) => shortNarrow,
                (RangeBand.Medium, TargetAspect.Broad) => mediumBroad,
                (RangeBand.Medium, TargetAspect.Narrow) => mediumNarrow,
                (RangeBand.Long, TargetAspect.Broad) => longBroad,
                (RangeBand.Long, TargetAspect.Narrow) => longNarrow,
                (RangeBand.Extreme, TargetAspect.Broad) => extremeBroad,
                (RangeBand.Extreme, TargetAspect.Narrow) => extremeNarrow,
                _ => shortBroad
            };
        }
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

        public float GetValue(ArmorLocationAngleType angleType)
        {
            return angleType switch
            {
                ArmorLocationAngleType.Horizontal => horizontalPenetrationInchs,
                ArmorLocationAngleType.Vertical => verticalPenetrationInchs,
                _ => verticalPenetrationInchs
            };
        }
    }

    public class MountArcRecord
    {
        [XmlAttribute]
        public float startDeg;

        [XmlAttribute]
        public float CoverageDeg;

        [XmlAttribute]
        public bool isCrossDeckFire;

        public string Summary()
        {
            var s = isCrossDeckFire ? "C" : "";
            return $"{startDeg}-{(startDeg + CoverageDeg) % 360}{s}";
        }

        public bool IsInArc(float bearingRelativeToBowDeg)
        {
            return MeasureUtils.IsAngleInArc(bearingRelativeToBowDeg, startDeg, CoverageDeg);
        }

        public bool IsInArc(float bearingRelativeToBowDeg, float relaxedAngle)
        {
            return MeasureUtils.IsAngleInArcRelaxed(bearingRelativeToBowDeg, startDeg, CoverageDeg, relaxedAngle);
        }

        public float AngleDifferenceFromArc(float bearingRelativeToBowDeg)
        {
            return MeasureUtils.AngleDifferenceFromArc(bearingRelativeToBowDeg, startDeg, CoverageDeg);
        }
    }

    public class MountLocationRecord : IObjectIdLabeled
    {
        public string objectId { get; set; }
        public MountLocation mountLocation;
        public int barrels; // Single, Double, Triple, Quadruple
        public int mounts;
        // public List<MountArcRecord> mountArcs = new() { new() };
        public List<MountArcRecord> mountArcs = new();
        public bool useRestAngle; // If rest angle is not overriden, it's derived from arc.
        public float restAngleDeg; // Graphic purpose only
        public bool trainable; // for torpedo
        public int reloadLimit; // Mainly for torpedo, 0 denotes no limit, > 0 will restrict max ammunition reloaded to the mount generated from this record. It represents separated ammunition room or single-shot torpedo tube.
        public string SummaryArcs() => string.Join(",", mountArcs.Select(arc => arc.Summary()));

        public IEnumerable<IObjectIdLabeled> GetSubObjects()
        {
            yield break;
        }

        public bool IsInArc(float bearingRelativeToBowDeg)
        {
            return mountArcs.Any(arc => arc.IsInArc(bearingRelativeToBowDeg));
        }

        public bool IsInArcRelaxed(float bearingRelativeToBowDeg, float relaxedAngle)
        {
            return mountArcs.Any(arc => arc.IsInArc(bearingRelativeToBowDeg, relaxedAngle));
        }

        public float AngleDifferenceFromArc(float bearingRelativeToBowDeg)
        {
            return mountArcs.Min(arc => arc.AngleDifferenceFromArc(bearingRelativeToBowDeg));
        }

        public static Dictionary<MountLocation, string> mountLocationAcronymMap = new()
        {
            // NotSpecified, // indicate binding error
            // PortForward,
            // Forward, // A, B
            // StarboardForward,
            // PortMidship,
            // Midship,
            // StarboardMidship,
            // PortAfter,
            // After, // X, Y
            // StarboardAfter,
            {MountLocation.NotSpecified, "NA"},
            {MountLocation.PortForward, "P/F"},
            {MountLocation.Forward, "F"},
            {MountLocation.StarboardForward, "S/F"},
            {MountLocation.PortMidship, "P/M"},
            {MountLocation.Midship, "M"},
            {MountLocation.StarboardMidship, "S/M"},
            {MountLocation.PortAfter, "P/A"},
            {MountLocation.After, "A"},
            {MountLocation.StarboardAfter, "S/A"},
        };

        public string mountLocationAcronym => mountLocationAcronymMap[mountLocation];
    }

    public class BatteryRecord : IObjectIdLabeled
    {
        public string objectId { get; set; }
        public GlobalString name = new();
        public float damageRating;
        public float maxRateOfFireShootPerMin; // shoot/min
        public int fireControlPositions;
        public FireControlSystem fireControlType = new();
        public float rangeYards;
        public float fireControlRadarModifier;
        public GlobalString fireControlRadarName = new();
        public float shellSizeInch;
        public float shellWeightPounds; // lb
        public int ammunitionCapacity;

        public List<FireControlTableRecord> fireControlTableRecords = new();
        public AmmunitionType penetrationTableBaseType;
        public List<PenetrationTableRecord> penetrationTableRecords = new();
        public List<MountLocationRecord> mountLocationRecords = new();

        

        public IEnumerable<IObjectIdLabeled> GetSubObjects()
        {
            foreach (var m in mountLocationRecords)
            {
                yield return m;
            }
        }

        static XmlSerializer serializer = new XmlSerializer(typeof(BatteryRecord));

        public string ToXML()
        {
            using (var textWriter = new StringWriter())
            {
                using (XmlWriter xmlWriter = XmlWriter.Create(textWriter))
                {
                    serializer.Serialize(xmlWriter, this);
                    string serializedXml = textWriter.ToString();

                    return serializedXml;
                }
            }
        }

        public static BatteryRecord FromXml(string xml)
        {
            using (var reader = new StringReader(xml))
            {
                return (BatteryRecord)serializer.Deserialize(reader);
            }
        }

        public float EvaluateFirepowerPerBarrel() // all-directional
        {
            var damageScrore = damageRating;
            var RateOfFireScore = penetrationTableRecords.FirstOrDefault()?.rateOfFire ?? 0;
            var fireControlScore = fireControlTableRecords.FirstOrDefault()?.shortBroad ?? 0;
            return damageScrore * RateOfFireScore * fireControlScore;
        }

        public float EvaluateFirepowerScore()
        {
            var barrels = mountLocationRecords.Sum(m => m.mounts * m.barrels);
            return barrels * EvaluateFirepowerPerBarrel();
        }

        // for a specified direction only
        public float EvaluateFirepowerPerBarrel(float distanceYards, TargetAspect targetAspect, float targetSpeedKnots)
        {
            var penetrationItem = penetrationTableRecords.FirstOrDefault(r => distanceYards <= r.distanceYards);
            if (penetrationItem == null)
                return 0;
            var rateOfFire = penetrationItem.rateOfFire;
            var rangeBand = penetrationItem.rangeBand;
            var fireControlRow = fireControlTableRecords.FirstOrDefault(r => targetSpeedKnots <= r.speedThresholdKnot);
            if (fireControlRow == null)
                return 0;
            var fireControlValue = fireControlRow.GetValue(rangeBand, targetAspect);

            return damageRating * rateOfFire * fireControlValue;
        }

        public float EvaluateFirepowerScore(float distanceYards, TargetAspect targetAspect, float targetSpeedKnots, float bearingRelativeToBowDeg)
        {
            var firepowerPerBarrel = EvaluateFirepowerPerBarrel(distanceYards, targetAspect, targetSpeedKnots);
            var avaialbleBarrels = mountLocationRecords.Where(r => r.IsInArc(bearingRelativeToBowDeg)).Sum(r => r.barrels * r.mounts);
            return firepowerPerBarrel * avaialbleBarrels;
        }
    }

    public class TorpedoSetting
    {
        [XmlAttribute]
        public float rangeYards;

        [XmlAttribute]
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

    public enum TorpedoPistolType
    {
        Contact,
        Magnetic
    }

    public class TorpedoSector
    {
        public GlobalString name = new();
        public List<MountLocationRecord> mountLocationRecords = new();
        public List<TorpedoSetting> torpedoSettings = new();
        // public List<MountLocationRecord> mountLocationRecords = new() { new() };
        // public List<TorpedoSetting> torpedoSettings = new() { new() };
        public int ammunitionCapacity;
        public TorpedoDamageClass damageClass;
        public float dudProbability = 0.5f; // General value of 15% for 1880-1945 is too "optimistic" for 1894
        public TorpedoPistolType pistolType;

        public float EvaluateTorpedoThreatPerBarrel()
        {
            return 1; // TODO: Add handling for Damage CLass, Speed, and Damage.
        }

        public float EvaluateTorpedoThreatScore()
        {
            var barrels = mountLocationRecords.Sum(r => r.mounts * r.barrels);
            return barrels * EvaluateTorpedoThreatPerBarrel();
        }

        public float EvaluateTorpedoThreatScore(float distanceYards, float bearingRelativeToBowDeg)
        {
            var setting = torpedoSettings.FirstOrDefault(setting => setting.rangeYards * CoreParameter.Instance.automaticTorpedoFiringRangeRelaxedCoef >= distanceYards);
            if (setting == null)
                return 0;
            var barrels = mountLocationRecords.Where(m => m.IsInArc(bearingRelativeToBowDeg)).Sum(m => m.mounts * m.barrels);
            return barrels * EvaluateTorpedoThreatPerBarrel();
        }
    }

    public class RapidFireBatteryFireControlLevelRecord
    {
        [XmlAttribute]
        public float fireControlMaxRange; // FC value for [eff, Max]

        [XmlAttribute]
        public float fireControlEffectiveRange; // FC value for [0, eff]
    }

    public class RapidFireBatteryRecord
    {
        public GlobalString name = new();
        public float maxRangeYards;
        public float effectiveRangeYards;
        // public List<RapidFireBatteryFireControlLevelRecord> fireControlRecords = new() { new() };
        public List<RapidFireBatteryFireControlLevelRecord> fireControlRecords = new();
        public float damageFactor; // RF
        // public List<int> barrelsLevelPort = new() { 0 };
        // public List<int> barrelsLevelStarboard = new() { 0 };
        public List<int> barrelsLevelPort = new();
        public List<int> barrelsLevelStarboard = new();

        public float EvaluateFirepowerPerBarrel()
        {
            var damageScore = damageFactor;
            var rateOfFireScore = 1;
            var fireControlScore = fireControlRecords.FirstOrDefault()?.fireControlEffectiveRange ?? 0;
            return damageScore * rateOfFireScore * fireControlScore;
        }

        public float EvaluateFirepowerScore()
        {
            var barrels = barrelsLevelPort.FirstOrDefault() + barrelsLevelStarboard.FirstOrDefault();
            return barrels * EvaluateFirepowerPerBarrel();
        }
    }

    public class SpeedIncreaseRecord
    {
        [XmlAttribute]
        public float thresholdSpeedKnots;

        [XmlAttribute]
        public float increaseSpeedKnots;
    }

    public class ArmorRatingReocrd
    {
        [XmlAttribute]
        public float effectInch;

        [XmlAttribute]
        public float actualInch;
    }

    public enum ArmorLocation
    {
        Deck,
        TurretHorizontal,
        SuperStructureHorizontal,
        ConningTower,
        MainBelt,
        BeltEnd,
        Barbette,
        TurretVertical,
        SuperStructureVertical,
        Ineffective,
    }

    public enum ArmorLocationAngleType
    {
        Horizontal,
        Vertical
    }

    public enum TargetAspect
    {
        Broad,
        Narrow
    }

    public class ArmorRating // Carrier is ignored at this point
    {
        public float armorTypeFactor;
        public ArmorRatingReocrd deck = new(); // 1H
        public ArmorRatingReocrd turretHorizontal = new(); // 2H
        public ArmorRatingReocrd superStructureHorizontal = new(); // 3H
        public ArmorRatingReocrd conningTower = new(); // 4V
        public ArmorRatingReocrd mainBelt = new(); // 5V
        public ArmorRatingReocrd beltEnd = new(); // 6V
        public ArmorRatingReocrd barbette = new(); // 7V
        public ArmorRatingReocrd turretVertical = new(); // 8V
        public ArmorRatingReocrd superStructureVertical = new(); // 9V

        public float GetArmorEffectiveInch(ArmorLocation loc)
        {
            return loc switch
            {
                ArmorLocation.Deck => deck.actualInch,
                ArmorLocation.TurretHorizontal => turretHorizontal.actualInch,
                ArmorLocation.SuperStructureHorizontal => superStructureHorizontal.actualInch,
                ArmorLocation.ConningTower => conningTower.actualInch,
                ArmorLocation.MainBelt => mainBelt.actualInch,
                ArmorLocation.BeltEnd => beltEnd.actualInch,
                ArmorLocation.Barbette => barbette.actualInch,
                ArmorLocation.TurretVertical => turretVertical.actualInch,
                ArmorLocation.SuperStructureVertical => superStructureVertical.actualInch,
                _ => mainBelt.actualInch
            };
        }

        // public static float[,] broadAspectLocationWeightTable = new float[,]
        // {// Short   Medium  Long/Extreme
        //     {2,     12,     25}, // 1H DECK
        //     {1,     3,      6},  // 2H TURRET
        //     {2,     4,      8},  // 3H SUPERSTR
        //     {4,     3,      3},  // 4V CON
        //     {26,    18,     16}, // 5V MAIN BELT
        //     {9,     8,      7},  // 6V BELT ENDS
        //     {19,    17,     11}, // 7V BARBETTE
        //     {17,    16,     11}, // 8V TURRET
        //     {19,    17,     12}, // 9V SUPERSTR
        //     {1,     1,      1}   // INEFFECTIVE
        // };

        // public static float[,] narrowAspectLocationWeightTable = new float[,]
        // {// Short   Medium  Long/Extreme
        //     {4,     20,     34}, // 1H DECK
        //     {2,     3,      7},  // 2H TURRET
        //     {3,     9,      14}, // 3H SUPERSTR
        //     {6,     4,      3},  // 4V CON
        //     {7,     5,      5},  // 5V MAIN BELT
        //     {4,     3,      3},  // 6V BELT ENDS
        //     {28,    16,     6},  // 7V BARBETTE
        //     {23,    19,     13}, // 8V TURRET
        //     {22,    20,     14}, // 9V SUPERSTR
        //     {1,     1,      1}   // INEFFECTIVE
        // };

        // public static double[] GetLocationWeights(TargetAspect targetAspect, RangeBand rangeBand)
        // {
        //     var table = targetAspect switch
        //     {
        //         TargetAspect.Broad => broadAspectLocationWeightTable,
        //         TargetAspect.Narrow => narrowAspectLocationWeightTable,
        //         _ => broadAspectLocationWeightTable
        //     };
        //     var colIdx = Math.Min(table.GetLength(1), (int)rangeBand);
        //     var rows = table.GetLength(0);
        //     var weights = new double[rows];
        //     for (int rowIdx = 0; rowIdx < table.GetLength(0); rowIdx++)
        //         weights[rowIdx] = table[rowIdx, colIdx];
        //     return weights;
        // }

        public float GetWeightedArmor(TargetAspect targetAspect, RangeBand rangeBand)
        {
            var weights = RuleChart.GetLocationWeights(targetAspect, rangeBand);
            var sumWeights = 0.0;
            var sumArmor = 0.0;
            for (var i = 0; i < weights.Length; i++)
            {
                var loc = (ArmorLocation)i;
                if (loc != ArmorLocation.Ineffective)
                {
                    sumWeights += weights[i];
                    sumArmor += weights[i] * GetArmorEffectiveInch(loc);
                }
            }
            return (float)(sumArmor / sumWeights);
        }

        // public static ArmorLocation RollArmorLocation(TargetAspect targetAspect, RangeBand rangeBand)
        // {
        //     var idx = Categorical.Sample(GetLocationWeights(targetAspect, rangeBand));
        //     return (ArmorLocation)idx;
        // }
    }

    public partial class ShipClass : IObjectIdLabeled
    {
        public string objectId { set; get; }
        public GlobalString name = new();
        public ShipType type;
        public Country country;
        // public int applicableYearBegin = 1900;
        // public int applicableYearEnd = 1900;
        public float displacementTons;
        public int complementMen;

        public float lengthFoot;
        public float beamFoot;
        public float draftFoot;
        // public GlobalString builderDesc = new();
        public GlobalString engineDesc = new();
        public GlobalString boilersDesc = new();
        // public List<BatteryRecord> batteryRecords = new() { new() };
        public List<BatteryRecord> batteryRecords = new();
        public TorpedoSector torpedoSector = new();
        // public List<RapidFireBatteryRecord> rapidFireBatteryRecords = new() { new() };
        public List<RapidFireBatteryRecord> rapidFireBatteryRecords = new();
        public int targetSizeModifier;
        public float damagePoint;
        public float speedKnots;
        public int damageControlRatingUnmodified;
        // public List<float> speedKnotsEngineRoomsLevels = new() { 0 };
        // public List<float> speedKnotsPropulsionShaftLevels = new() { 0 };
        // public List<float> speedKnotsBoilerRooms = new() { 0 };
        // public List<SpeedIncreaseRecord> speedIncreaseRecord = new() { new() };
        public List<float> speedKnotsEngineRoomsLevels = new();
        public List<float> speedKnotsPropulsionShaftLevels = new();
        public List<float> speedKnotsBoilerRooms = new();
        public List<SpeedIncreaseRecord> speedIncreaseRecord = new();
        public float standardTurnDegPer2Min; // per 2 min
        public float emergencyTurnDegPer2Min; // per 2 min
        public ArmorRating armorRating = new();

        public string portraitUrl;
        public string portraitCode;
        public string portraitTopCode;

        public IEnumerable<IObjectIdLabeled> GetSubObjects()
        {
            foreach (var batteryRecord in batteryRecords)
            {
                yield return batteryRecord;
            }
            foreach (var mountLocationRecord in torpedoSector.mountLocationRecords)
            {
                yield return mountLocationRecord;
            }
        }

        public static Dictionary<ShipType, string> acronymForShipType = new()
        {
            {ShipType.Battleship, "BB"},
            {ShipType.LightCruiser, "CL"},
            {ShipType.Cruiser, "CC"},
            {ShipType.ArmoredCruiser, "CA"},
            {ShipType.Destroyer, "DD"},
            {ShipType.PatrolGunboat, "PG"},
            {ShipType.TorpedoBoat, "TB"},
            {ShipType.ArmedMerchantCruiser, "AMC"}
        };

        public static string GetAcronymFor(ShipType shipType)
        {
            if (acronymForShipType.TryGetValue(shipType, out string acronym))
            {
                return acronym;
            }
            return shipType.ToString();
        }

        public string GetAcronym()
        {
            return GetAcronymFor(type);
        }

        public float EvaluateArmorScore()
        {
            return EvaluateArmorScore(TargetAspect.Broad, RangeBand.Short);
        }

        public float EvaluateArmorScore(TargetAspect targetAspect, RangeBand rangeBand)
        {
            return armorRating.GetWeightedArmor(targetAspect, rangeBand);
        }

        public float EvaluateSurvivability()
        {
            // var armorScoreSmoothed = (float)(1 + Math.Sqrt(EvaluateArmorScore()));
            var armorScoreSmoothed = 1 + EvaluateArmorScore();
            return damagePoint * armorScoreSmoothed;
        }

        public float EvaluateBatteryFirepowerScore()
        {
            return batteryRecords.Sum(bs => bs.EvaluateFirepowerScore());
        }

        public float EvaluateBatteryFirepowerScore(float distanceYards, TargetAspect targetAspect, float targetSpeedKnots, float bearingRelativeToBowDeg)
        {
            return batteryRecords.Sum(bs => bs.EvaluateFirepowerScore(distanceYards, targetAspect, targetSpeedKnots, bearingRelativeToBowDeg));
        }

        public float EvaluateTorpedoThreatScore()
        {
            return torpedoSector.EvaluateTorpedoThreatScore();
        }

        public float EvaluateTorpedoThreatScore(float distanceYards, float bearingRelativeToBowDeg)
        {
            return torpedoSector.EvaluateTorpedoThreatScore(distanceYards, bearingRelativeToBowDeg);
        }

        public float EvaluateRapidFiringFirepowerScore()
        {
            return rapidFireBatteryRecords.Sum(rf => rf.EvaluateFirepowerScore());
        }

        public float EvaluateFirepowerScore()
        {
            var batteryFirepower = EvaluateBatteryFirepowerScore();
            // Torpedo is not handled here
            var torpedoThreat = EvaluateTorpedoThreatScore();
            var rapidFiringFirepower = EvaluateRapidFiringFirepowerScore();

            return 1f * batteryFirepower + 20f * torpedoThreat + 1f * rapidFiringFirepower;
        }

        public float EvaluateGeneralScore()
        {
            var survivability = EvaluateSurvivability();
            var firepowerScore = EvaluateFirepowerScore();
            // var armorScoreSmoothed = 1 + (float)Math.Sqrt(armorScore);
            return 1f * survivability + 1f * firepowerScore; // TODO: Consider DP?
        }
        
    }
}