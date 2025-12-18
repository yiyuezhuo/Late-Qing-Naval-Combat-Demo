using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using System.Xml;
using System.IO;
using MathNet.Numerics.Distributions;
using System.Linq;
using System.Collections;

using CoreUtils;
using System.Diagnostics;

namespace NavalCombatCore
{

    public enum ShipType // SK5 type
    {
        NotSpecified,
        Battleship, // BB, ironclad, pre-dreadnought, dreadnought, post-dreadnought
        LightCruiser, // CL
        Cruiser, // CR, CC
        ArmoredCruiser, // CA
        Destroyer, // DD
        PatrolGunboat, // PG
        TorpedoBoat, // TB
        ArmedMerchantCruiser, // AMC
        Transport, // TR, Transport or merchant ship
        Repair, // AR, Auxiliary Repair
    }

    public enum ExtraShipType
    {
        NotSpecified,
        Ironclad,
        Predreatought,
        Dreadnought,
        UnprotectedCruiser, // Ram Cruiser
        ProtectedCruiser,
        ArmoredCruiser,
        TorpedoCruiser
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

    public enum FCSCode // SK5 Table A11 FIRE CONTROL SYSTEM DATA
    {
        Custom,
        Z, // Basic gun-sight, no fire control instruments, no director control, manual stabilization
        Y, // Basic gun-sight, basic fire control instruments, no optical range-finders, no director control, manual stabilization
        X, // Basic gun-sight, basic fire control instruments, optical range-finders, no director control, manual stabilization
        W, // Basic gun-sight, basic fire control instruments, no optical range-finders, early FTP control system, manual stabilization
        V, // Basic gun-sight, basic fire control instruments, optical range-finders, early FTP control system, manual stabilization
        U, // Telescopic gun-sights, no fire control instruments, no optical range-finders, no director control, manual stabilization,
        T, // Telescopic gun-sights, basic fire control instruments, no optical range-finders, no director control, manual stabilization
        S, // Telescopic gun-sights, basic fire control instruments, optical range-finders, no director control, manual stabilization
        R, // Telescopic gun-sights, basic fire control instruments, no optical range-finders, early FTP control system, manual stabilization
        Q, // Telescopic gun-sights, basic fire control instruments, optical range-finders, early FTP control system, manual stabilization
        P, // Telescopic gun-sights, mechanical computer, optical range-finders, no director control, manual stabilization
        N, // Telescopic gun-sights, mechanical computer, optical range-finders, early FTP control system, manual stabilization.
        M, // Telescopic gun-sights, basic fire control instruments, optical range-finders, early FTP control system, gyro-assisted stabilization.
        L, // Telescopic gun-sights, mechanical computer, optical range-finders, no director control, gyro-assisted stabilization.
        K, // Telescopic gun-sights, mechanical computer, optical range-finders, early FTP control system, gyro-assisted stabilization
        J, // Telescopic gun-sights, mechanical computer, optical range-finders, director control system, manual stabilization.
        H, // Telescopic gun-sights, mechanical computer, optical range-finders, director control system, gyro-assisted stabilization, partial RPC
        G, // Telescopic gun-sights, mechanical computer, optical range-finders, director control system, gyro-assisted stabilization.
        F, // Telescopic gun-sights, advanced mechanical computer, optical range-finders, director control system, gyro-assisted stabilization.
        E, // Telescopic gun-sights, advanced mechanical computer, optical range-finders, director control system, gyro-assisted stabilization, partial RPC
        D, // Telescopic gun-sights, advanced mechanical computer, optical range-finders, director control system, gyro-assisted stabilization, full RPC
        C, // Telescipic gun-sights, advanced mechanical computer, optical range-finders, director control system, stable element.
        B, // Telescopic gun-sights, advanced mechanical computer, optical range-finders, director control ssytem, stable element, partial RPC.
        A, // Telescipic gun-sights, advanced mechanical computer, optical range-finders, director control system, stable element, full RPC.
    }

    public partial class FireControlSystem
    {
        public FCSCode code;
        public GunSightType gunSight; // Basic, Telescope
        public FireControlInstrumentType fireControlInstrument; // None, Basic, MechanicalComputer, AdvancedMechanicalComputer
        public RangeFinderType rangeFinder; // None, Optical
        public DirectorControlType directorControl; // None, FollowThePointer (FTP), Director
        public StabilizationType stabilization; // Manual, GyroAssisted, StableElement
        public PowerRemoteControlType powerRemoteControl; // None, Partial, Full

        // public FireControlSystem Clone()
        // {
        //     return new FireControlSystem()
        //     {
        //         gunSight = gunSight,
        //         fireControlInstrument = fireControlInstrument,
        //         rangeFinder = rangeFinder,
        //         directorControl = directorControl,
        //         stabilization = stabilization,
        //         powerRemoteControl = powerRemoteControl
        //     };
        // }

        public override string ToString()
        {
            return $"FireControlSystem({code}, {gunSight} {fireControlInstrument} {rangeFinder} {directorControl} {stabilization} {powerRemoteControl})";
        }

        static List<FireControlSystem> referenceFireControlSystems = new()
        {
            new(){code=FCSCode.Z, gunSight=GunSightType.Basic,     fireControlInstrument=FireControlInstrumentType.None,                       rangeFinder=RangeFinderType.None,    directorControl=DirectorControlType.None,             stabilization=StabilizationType.Manual,        powerRemoteControl=PowerRemoteControlType.None    },
            new(){code=FCSCode.Y, gunSight=GunSightType.Basic,     fireControlInstrument=FireControlInstrumentType.Basic,                      rangeFinder=RangeFinderType.None,    directorControl=DirectorControlType.None,             stabilization=StabilizationType.Manual,        powerRemoteControl=PowerRemoteControlType.None    },
            new(){code=FCSCode.X, gunSight=GunSightType.Basic,     fireControlInstrument=FireControlInstrumentType.Basic,                      rangeFinder=RangeFinderType.Optical, directorControl=DirectorControlType.None,             stabilization=StabilizationType.Manual,        powerRemoteControl=PowerRemoteControlType.None    },
            new(){code=FCSCode.W, gunSight=GunSightType.Basic,     fireControlInstrument=FireControlInstrumentType.Basic,                      rangeFinder=RangeFinderType.None,    directorControl=DirectorControlType.FollowThePointer, stabilization=StabilizationType.Manual,        powerRemoteControl=PowerRemoteControlType.None    },
            new(){code=FCSCode.V, gunSight=GunSightType.Basic,     fireControlInstrument=FireControlInstrumentType.Basic,                      rangeFinder=RangeFinderType.Optical, directorControl=DirectorControlType.FollowThePointer, stabilization=StabilizationType.Manual,        powerRemoteControl=PowerRemoteControlType.None    },
            new(){code=FCSCode.U, gunSight=GunSightType.Telescope, fireControlInstrument=FireControlInstrumentType.None,                       rangeFinder=RangeFinderType.None,    directorControl=DirectorControlType.None,             stabilization=StabilizationType.Manual,        powerRemoteControl=PowerRemoteControlType.None    },
            new(){code=FCSCode.T, gunSight=GunSightType.Telescope, fireControlInstrument=FireControlInstrumentType.Basic,                      rangeFinder=RangeFinderType.None,    directorControl=DirectorControlType.None,             stabilization=StabilizationType.Manual,        powerRemoteControl=PowerRemoteControlType.None    },
            new(){code=FCSCode.S, gunSight=GunSightType.Telescope, fireControlInstrument=FireControlInstrumentType.Basic,                      rangeFinder=RangeFinderType.Optical, directorControl=DirectorControlType.None,             stabilization=StabilizationType.Manual,        powerRemoteControl=PowerRemoteControlType.None    },
            new(){code=FCSCode.R, gunSight=GunSightType.Telescope, fireControlInstrument=FireControlInstrumentType.Basic,                      rangeFinder=RangeFinderType.None,    directorControl=DirectorControlType.FollowThePointer, stabilization=StabilizationType.Manual,        powerRemoteControl=PowerRemoteControlType.None    },
            new(){code=FCSCode.Q, gunSight=GunSightType.Telescope, fireControlInstrument=FireControlInstrumentType.Basic,                      rangeFinder=RangeFinderType.Optical, directorControl=DirectorControlType.FollowThePointer, stabilization=StabilizationType.Manual,        powerRemoteControl=PowerRemoteControlType.None    },
            new(){code=FCSCode.P, gunSight=GunSightType.Telescope, fireControlInstrument=FireControlInstrumentType.MechanicalComputer,         rangeFinder=RangeFinderType.Optical, directorControl=DirectorControlType.None,             stabilization=StabilizationType.Manual,        powerRemoteControl=PowerRemoteControlType.None    },
            new(){code=FCSCode.N, gunSight=GunSightType.Telescope, fireControlInstrument=FireControlInstrumentType.MechanicalComputer,         rangeFinder=RangeFinderType.Optical, directorControl=DirectorControlType.FollowThePointer, stabilization=StabilizationType.Manual,        powerRemoteControl=PowerRemoteControlType.None    },
            new(){code=FCSCode.M, gunSight=GunSightType.Telescope, fireControlInstrument=FireControlInstrumentType.Basic,                      rangeFinder=RangeFinderType.Optical, directorControl=DirectorControlType.FollowThePointer, stabilization=StabilizationType.GyroAssisted,  powerRemoteControl=PowerRemoteControlType.None    },
            new(){code=FCSCode.L, gunSight=GunSightType.Telescope, fireControlInstrument=FireControlInstrumentType.MechanicalComputer,         rangeFinder=RangeFinderType.Optical, directorControl=DirectorControlType.None,             stabilization=StabilizationType.GyroAssisted,  powerRemoteControl=PowerRemoteControlType.None    },
            new(){code=FCSCode.K, gunSight=GunSightType.Telescope, fireControlInstrument=FireControlInstrumentType.MechanicalComputer,         rangeFinder=RangeFinderType.Optical, directorControl=DirectorControlType.FollowThePointer, stabilization=StabilizationType.GyroAssisted,  powerRemoteControl=PowerRemoteControlType.None    },
            new(){code=FCSCode.J, gunSight=GunSightType.Telescope, fireControlInstrument=FireControlInstrumentType.MechanicalComputer,         rangeFinder=RangeFinderType.Optical, directorControl=DirectorControlType.Director,         stabilization=StabilizationType.Manual,        powerRemoteControl=PowerRemoteControlType.None    },
            new(){code=FCSCode.H, gunSight=GunSightType.Telescope, fireControlInstrument=FireControlInstrumentType.MechanicalComputer,         rangeFinder=RangeFinderType.Optical, directorControl=DirectorControlType.Director,         stabilization=StabilizationType.GyroAssisted,  powerRemoteControl=PowerRemoteControlType.Partial },
            new(){code=FCSCode.G, gunSight=GunSightType.Telescope, fireControlInstrument=FireControlInstrumentType.MechanicalComputer,         rangeFinder=RangeFinderType.Optical, directorControl=DirectorControlType.Director,         stabilization=StabilizationType.GyroAssisted,  powerRemoteControl=PowerRemoteControlType.None    },
            new(){code=FCSCode.F, gunSight=GunSightType.Telescope, fireControlInstrument=FireControlInstrumentType.AdvancedMechanicalComputer, rangeFinder=RangeFinderType.Optical, directorControl=DirectorControlType.Director,         stabilization=StabilizationType.GyroAssisted,  powerRemoteControl=PowerRemoteControlType.None    },
            new(){code=FCSCode.E, gunSight=GunSightType.Telescope, fireControlInstrument=FireControlInstrumentType.AdvancedMechanicalComputer, rangeFinder=RangeFinderType.Optical, directorControl=DirectorControlType.Director,         stabilization=StabilizationType.GyroAssisted,  powerRemoteControl=PowerRemoteControlType.Partial },
            new(){code=FCSCode.D, gunSight=GunSightType.Telescope, fireControlInstrument=FireControlInstrumentType.AdvancedMechanicalComputer, rangeFinder=RangeFinderType.Optical, directorControl=DirectorControlType.Director,         stabilization=StabilizationType.GyroAssisted,  powerRemoteControl=PowerRemoteControlType.Full    },
            new(){code=FCSCode.C, gunSight=GunSightType.Telescope, fireControlInstrument=FireControlInstrumentType.AdvancedMechanicalComputer, rangeFinder=RangeFinderType.Optical, directorControl=DirectorControlType.Director,         stabilization=StabilizationType.StableElement, powerRemoteControl=PowerRemoteControlType.None    },
            new(){code=FCSCode.B, gunSight=GunSightType.Telescope, fireControlInstrument=FireControlInstrumentType.AdvancedMechanicalComputer, rangeFinder=RangeFinderType.Optical, directorControl=DirectorControlType.Director,         stabilization=StabilizationType.StableElement, powerRemoteControl=PowerRemoteControlType.Partial },
            new(){code=FCSCode.A, gunSight=GunSightType.Telescope, fireControlInstrument=FireControlInstrumentType.AdvancedMechanicalComputer, rangeFinder=RangeFinderType.Optical, directorControl=DirectorControlType.Director,         stabilization=StabilizationType.StableElement, powerRemoteControl=PowerRemoteControlType.Full    },
        };

        (GunSightType, FireControlInstrumentType, RangeFinderType, DirectorControlType, StabilizationType, PowerRemoteControlType) GetStates() => (
            gunSight,
            fireControlInstrument,
            rangeFinder,
            directorControl,
            stabilization,
            powerRemoteControl
        );

        public void CopyStates(FireControlSystem x)
        {
            gunSight = x.gunSight;
            fireControlInstrument = x.fireControlInstrument;
            powerRemoteControl = x.powerRemoteControl;
            rangeFinder = x.rangeFinder;
            stabilization = x.stabilization;
            directorControl = x.directorControl;
        }

        static Dictionary<FCSCode, FireControlSystem> code2fcs = referenceFireControlSystems.ToDictionary(x => x.code, x => x);
        static Dictionary<(GunSightType, FireControlInstrumentType, RangeFinderType, DirectorControlType, StabilizationType, PowerRemoteControlType), FireControlSystem> states2fcs = referenceFireControlSystems.ToDictionary(x => x.GetStates(), x => x);

        public void SyncCodeByStates()
        {
            if(states2fcs.TryGetValue(GetStates(), out var refFcs))
            {
                // CopyKey(refFcs);
                code = refFcs.code;
            }
            else
            {
                code = FCSCode.Custom;
            }
        }

        public void SyncStatesByCode()
        {
            if(code != FCSCode.Custom && code2fcs.TryGetValue(code, out var refFcs))
            {
                CopyStates(refFcs);
            }
        }
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

    public enum AmmunitionType // SK5 Table A12
    {
        ArmorPiercing, // AP, APC (Armor-piercing Capped), APCBC
        // Physical Description:
        // Thick casing walls and a hardened nose. Nose of shell capped (in APC) with slightly softer metal in order to improve penetration. Relatively small bursting charge (c. 2% of projectile weight).
        // Base fuse. Sometimes provided with a ballistic cap (or windscreen) to improve aerodynamic capabilities by reducing air resistance as in the APCBC type.
        // Effect:
        // Intended for use against heavily armored targets and expected to successfully penetrate armor of up to it's caliber in thickness (i.e. a 12'' shell would be expected to penetrate to up 12'' of armor and remain intact for bursting).
        // Instantaneous detonation behind the armor, causing great fragmentation in all directions at very high velocities. All of the explosive is consumed and all parts of the projectile are broken into small pieces (500 to 1800 for an 8'' shell).
        SemiArmorPiercing, // SAP, SAPC, SAPBC
        // Physical Description:
        // Similar to AP but with a non-hardened cap and thinner walls. Bursting charge of c. 4%. SAP is sometimes used to refer to COM, HE or HC shell with base or delayed fuses. USN Special Common shells were a version of SAP.
        // Effect:
        // Intended for use against lightly armored targets. Better penetration than COM but less fragmentation and explosive effect.
        Common, // COM, CPC, CPCBC
        // Physical Description:
        // Thinner walls than AP shells and a higher bursting charge (c. 6-8% of projectile weight). The designation CPC and CPCBC indicate common shells with added caps to increase either penetration or ballistic performance.
        // Effect:
        // Indended for use against lightly armored targets, exposed personnel and earthworks. Expected to successfully penetrate armor of up to one-half it's caliber in thickness
        // (i.e. an 8'' shell would be expected to penetrate up to 4'' of armor and remain intact for bursting). Produced greater fragmentation than API and spread destruction over a larger area.
        HighExplosive, // HE, HC (High-Capacity)
        // Physical Description:
        // Thinner walls than AP and COM shells to accommodate a higher bursting charge (c. 10-12% or more of projectile weight).
        // Projectile is made only suffciently strong enough to withstand the shock of firing (allowing shells for lower MV guns to contain more explosive). Many different types of explosives and fragmentation material used in HC shells.
        // Effect:
        // Indended for use against unarmored targets, unprotected shore installations and aircraft. Expected to successfully penetrate armor of approximately one-tenth the caliber in thickness
        // (i.e. a 15'' shell would be expected to penetrate roughly 1.5'' of armor). Capable of producing devastating fragmentation effects over a large area and starting numerous fires.
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

        public MountArcRecord Clone() => new()
        {
            startDeg = startDeg,
            CoverageDeg = CoverageDeg,
            isCrossDeckFire = isCrossDeckFire
        };

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

    public partial class MountLocationRecord : IObjectIdLabeled
    {
        public string objectId { get; set; }
        public MountLocation mountLocation;
        public int barrels; // Single, Double, Triple, Quadruple
        public int mounts;
        // public List<MountArcRecord> mountArcs = new() { new() };
        public List<MountArcRecord> mountArcs = new();
        // public bool useRestAngle; // If rest angle is not overriden, it's derived from arc.
        // public float restAngleDeg; // Graphic purpose only
        public bool trainable; // for torpedo
        public int reloadLimit; // Mainly for torpedo, 0 denotes no limit, > 0 will restrict max ammunition reloaded to the mount generated from this record. It represents separated ammunition room or single-shot torpedo tube.
        public bool defaultNarrow;
        public string SummaryArcs() => string.Join(",", mountArcs.Select(arc => arc.Summary()));

        public IEnumerable<IObjectIdLabeled> GetSubObjects()
        {
            yield break;
        }

        public override string ToString()
        {
            return $"MountLocationRecord({mountLocation}, {barrels}x{mounts})";
        }

        static Dictionary<MountLocation, List<MountArcRecord>> mountLocation2defaultMountArcs = new()
        {
            {MountLocation.NotSpecified, new()},
            {MountLocation.Forward, new(){new(){startDeg=240, CoverageDeg=240}}},
            {MountLocation.StarboardForward, new(){new(){startDeg=0, CoverageDeg=120}}},
            {MountLocation.StarboardMidship, new(){new(){startDeg=30, CoverageDeg=120}}},
            {MountLocation.StarboardAfter, new(){new(){startDeg=60, CoverageDeg=120}}},
            {MountLocation.After, new(){new(){startDeg=60, CoverageDeg=240}}},
            {MountLocation.PortAfter, new(){new(){startDeg=180, CoverageDeg=120}}},
            {MountLocation.PortMidship, new(){new(){startDeg=210, CoverageDeg=120}}},
            {MountLocation.PortForward, new(){new(){startDeg=240, CoverageDeg=120}}},
            {MountLocation.Midship, new(){new(){startDeg=30, CoverageDeg=120}, new(){startDeg=210, CoverageDeg=120}}},
        };

        public class DefaultMountArcsConfig
        {
            public List<MountArcRecord> normal = new();
            public List<MountArcRecord> narrow = new();
        }

        static Dictionary<MountLocation, DefaultMountArcsConfig> mountLocation2defaultMountArcsConfig = new()
        {
            {MountLocation.NotSpecified, new()},
            {MountLocation.Forward, new(){
                normal=new(){new(){startDeg=240, CoverageDeg=240}},
                narrow=new(){new(){startDeg=345, CoverageDeg=30}}
            }},
            {MountLocation.StarboardForward, new(){
                normal=new(){new(){startDeg=0, CoverageDeg=120}},
                narrow=new(){new(){startDeg=75, CoverageDeg=30}}
            }},
            {MountLocation.StarboardMidship, new(){
                normal=new(){new(){startDeg=30, CoverageDeg=120}},
                narrow=new(){new(){startDeg=75, CoverageDeg=30}}
            }},
            {MountLocation.StarboardAfter, new(){
                normal=new(){new(){startDeg=60, CoverageDeg=120}},
                narrow=new(){new(){startDeg=75, CoverageDeg=30}}
            }},
            {MountLocation.After, new(){
                normal=new(){new(){startDeg=60, CoverageDeg=240}},
                narrow=new(){new(){startDeg=165, CoverageDeg=30}}
            }},
            {MountLocation.PortAfter, new(){
                normal=new(){new(){startDeg=180, CoverageDeg=120}},
                narrow=new(){new(){startDeg=255, CoverageDeg=30}}
            }},
            {MountLocation.PortMidship, new(){
                normal=new(){new(){startDeg=210, CoverageDeg=120}},
                narrow=new(){new(){startDeg=255, CoverageDeg=30}}
            }},
            {MountLocation.PortForward, new(){
                normal=new(){new(){startDeg=240, CoverageDeg=120}},
                narrow=new(){new(){startDeg=255, CoverageDeg=30}}
            }},
            {MountLocation.Midship, new(){
                normal=new(){
                    new(){startDeg=30, CoverageDeg=120},
                    new(){startDeg=210, CoverageDeg=120}
                },
                narrow=new(){
                    new(){startDeg=75, CoverageDeg=30},
                    new(){startDeg=255, CoverageDeg=30}
                }
            }},
        };

        public void SyncDefaultMountArcs()
        {
            // if(mountLocation2defaultMountArcs.TryGetValue(mountLocation, out var defaultMountArcs))
            // {
            //     mountArcs.Clear();
            //     mountArcs.AddRange(defaultMountArcs.Select(arc => arc.Clone()));
            // }
            if(mountLocation2defaultMountArcsConfig.TryGetValue(mountLocation, out var defaultMountArcsConfig))
            {
                mountArcs.Clear();
                var defaultMountArcs = defaultNarrow ? defaultMountArcsConfig.narrow : defaultMountArcsConfig.normal;
                mountArcs.AddRange(defaultMountArcs.Select(arc => arc.Clone()));
            }
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

    public partial class BatteryRecord : IObjectIdLabeled
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

        public override string ToString()
        {
            return $"BatteryRecord({name.GetMergedNamePure()})";
        }

        public float GetRoundsPerGun()
        {
            return (float)ammunitionCapacity / mountLocationRecords.Sum(mnt => mnt.mounts * mnt.barrels);
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

    public enum ArmorType // SK5 Table A14 (ID is not encoded in the enum to simplify LocalizedEnum's binding)
    {
        NotSpecified, // Not Specified
        NoArmor, // No Armor
        WroughtIron, // 1, Wrought Iron, 1855-1890 (All) All naval armor, 0.6
        MildSteel, // 2, Mild Steel, 1876-1945 (All) Some armor. 0.75
        CompoundHardSteelFacedWroughtIron, // 3, Compound Hard Steel Faced Wriought Iron, 1878-1890 (All except France) Heavy vertical armor, 0.68
        NickelSteel, // 9, Nickel-Steel, 1890-1925 All armor, 0.9
        HarveyMildSteel, // 5, Harvey Mild Steel, 1891-1899 (All) Vertical armor 6'' and up, 0.74
        HarveyNickelSteel, // 21, Harvey Nickel-Steel, 1891-1899 (All) Vertical armor 6'' and up, 0.78
        KruppChromeNickelSteel, // 10, Krupp Chrome Nickel Steel (Krupp Soft), 1894-1918 (Germany) turret and CT roots and vertical light armor (to 3.2''), 0.95
        KruppCemented1894, // 6, Krupp Cemented (KCa), 1894-1918 (Germany) Vertical armor 3.2'' and up, 0.83
        HighTensileSteel, // 7, High Tensile Steel, 1895-1945 light armor and protective decks, 0.82
        // KruppCemented, // 22, Krupp Cemented (KCa), 1898-1918 (All), 0.83
        ClassAArmor1900, // 30, Class A Armor, 1900-1910 (USA) Vertical armor 4'' and up, 0.83
        KruppNickelSteel, // 4, Krupp Nickel Steel, 1900-1918 (Germany) protective decks, 0.83
        KruppNonCemented, // 12, Krupp Non-Cemented (KNC), 1900-1925 (Great Britain) turret and CT roof armor. Also Vertical armor (to 4''), 0.95
        KruppCementedWW1Era1905, // 25, Krupp Cemented (WW1 Era), 1905-1910 (Britain/Italy/Japan) Vertical armor over 4'', 0.83
        WitkowitzerKC, // 24, Witkowitzer KC, 1905-1918 (AustriaHungary) Vertical armor 3.2'' and up, 0.95
        ClassAArmorMidvaleNonCemented, // 32, Class A Armor Midvale Non-Cemented, 1907-1912 (USA) Vertical armor 4'' and up, 0.88
        ClassBArmor1910, // 16, Class B Armor, 1910-1932 (USA) Turret and CT root, gun mount, director and CT armor less than 4'', 0.95
        SpecialTreatmentSteel, // 15, Special Treatment Steel (STS), 1910-1960 (USA) vertical hull armor under 5''. amounred decks, lower belts 2'' to 12'', 1.0
        ClassAArmor1911, // 31, Class A Armor, 1911-1923 (USA) Vertical armor 4'' and up, 0.89
        KruppCementedWW1Era1911, // 26, Krupp Cemented WW1 Era, 1911-1936 (Britain/Italy/Japan) Vertical armor over 4'', 0.85
        KruppWolanHardNickelSteel, // 11, Krupp Wolan Hard Nickel Steel, 1925-1945 (Germany) horizontal and vertical armor (to 4.72''), 1.0
        DSiliconManganeseHTSteel, // 8, D Silicon-Manganese HT Steel, 1925-1945 light armor (to 2'') and bulkheads, 0.90
        NewVickersNonCemented, // 18, New Vickers Non-Cemented (NVNC), 1926-1945 (Japan), 0.95
        NonCementedArmor, // 13, Non Cemented Armor (NCA), 1926-1945 (Great Britain) turret and CT roofs, armored decks and vertical armor less than 4'', 1.0
        KruppCemented1928, // 23, Krupp Cemented (KCa), 1928-1945 (Germany) Vertical armor 3.94'' and up, 1.0
        POHomogenousPlate, // 14, PO Homogenous Plate, 1929-1943 (Italy) turret and CT roofs, armored decks and vertical armor less than 4'', 1.0
        ItalianWW2EraKruppCemented, // 28, Italian WW2 Era Krupp Cemented, 1929-1943 (Italy) Vertical armor over 4'', 1.0
        BritishCementedArmor, // 27, British Cemented Armor (CA), 1933-1946 (Britain) Vertical battleship armor over 4'', 1.0
        ClassBArmor1933, // 17, Class B Armor, 1933-1955 (USA) Turret roof, gun mount, director armor under 4''. Turret face 16'' and up. CT. 1.0
        ClassAArmor1933, // 33, Class A Armor, 1933-1955 (USA) Vertical armor 5'' and up to 16'', 1.0
        VickersNonCemented, // 29, Vickers Non-Cemented (VH), 1937-1945 (Japan) Vertical armor over 11'' and Yamato Class only. 0.84
        MolybdenumNonCemented // 19, Molybdenum Non-Cemented (MNC), 1941-1945 (Japan) Deck armor for Yamato Class only. 0.97
    }

    public partial class ArmorRating // Carrier is ignored at this point
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

        public ArmorType armorType;

        static Dictionary<ArmorType, float> armorTypeToFactor = new()
        {
            // { ArmorType.NotSpecified, 1 },
            { ArmorType.NoArmor, 0},
            { ArmorType.WroughtIron, 0.6f },
            { ArmorType.MildSteel, 0.75f },
            { ArmorType.CompoundHardSteelFacedWroughtIron, 0.68f },
            { ArmorType.NickelSteel, 0.9f },
            { ArmorType.HarveyMildSteel, 0.74f},
            { ArmorType.HarveyNickelSteel, 0.78f },
            { ArmorType.KruppChromeNickelSteel, 0.95f },
            { ArmorType.KruppCemented1894, 0.83f },
            { ArmorType.HighTensileSteel, 0.82f },
            { ArmorType.ClassAArmor1900, 0.83f },
            { ArmorType.KruppNickelSteel, 0.83f },
            { ArmorType.KruppNonCemented, 0.95f },
            { ArmorType.KruppCementedWW1Era1905, 0.83f },
            { ArmorType.WitkowitzerKC, 0.95f },
            { ArmorType.ClassAArmorMidvaleNonCemented, 0.88f },
            { ArmorType.ClassBArmor1910, 0.95f },
            { ArmorType.SpecialTreatmentSteel, 1f },
            { ArmorType.ClassAArmor1911, 0.89f },
            { ArmorType.KruppCementedWW1Era1911, 0.85f },
            { ArmorType.KruppWolanHardNickelSteel, 1f },
            { ArmorType.DSiliconManganeseHTSteel, 0.9f },
            { ArmorType.NewVickersNonCemented, 0.95f },
            { ArmorType.NonCementedArmor, 1f },
            { ArmorType.KruppCemented1928, 1f },
            { ArmorType.POHomogenousPlate, 1f },
            { ArmorType.ItalianWW2EraKruppCemented, 1f },
            { ArmorType.BritishCementedArmor, 1f },
            { ArmorType.ClassBArmor1933, 1f },
            { ArmorType.ClassAArmor1933, 1f },
            { ArmorType.VickersNonCemented, 0.84f },
            { ArmorType.MolybdenumNonCemented, 0.97f }
        };

        public void TryInferArmorType()
        {
            var pairs = armorTypeToFactor.Where(kv => kv.Value == armorTypeFactor).ToList();
            if(pairs.Count == 1)
            {
                armorType = armorType = pairs[0].Key;
                
                return;
            }
            armorType = ArmorType.NotSpecified;
        }

        IEnumerable<ArmorRatingReocrd> IterateArmorRatingRecords()
        {
            yield return deck;
            yield return turretHorizontal;
            yield return superStructureHorizontal;
            yield return conningTower;
            yield return mainBelt;
            yield return beltEnd;
            yield return barbette;
            yield return turretVertical;
            yield return superStructureVertical;
        }

        void SetEffectInchByArmorTypeFactor()
        {
            foreach(var armorRatingRecord in IterateArmorRatingRecords())
            {
                armorRatingRecord.effectInch = MathF.Round(armorRatingRecord.actualInch * armorTypeFactor, 1);
            }
        }

        public void TrySetFactorAndEffectInch()
        {
            if(armorTypeToFactor.TryGetValue(armorType, out var factor))
            {
                armorTypeFactor = factor;
                SetEffectInchByArmorTypeFactor();
            }
        }

        public ArmorRatingReocrd GetRecord(ArmorLocation loc)
        {
            return loc switch
            {
                ArmorLocation.Deck => deck,
                ArmorLocation.TurretHorizontal => turretHorizontal,
                ArmorLocation.SuperStructureHorizontal => superStructureHorizontal,
                ArmorLocation.ConningTower => conningTower,
                ArmorLocation.MainBelt => mainBelt,
                ArmorLocation.BeltEnd => beltEnd,
                ArmorLocation.Barbette => barbette,
                ArmorLocation.TurretVertical => turretVertical,
                ArmorLocation.SuperStructureVertical => superStructureVertical,
                _ => mainBelt
            };
        }

        public float GetArmorEffectiveInch(ArmorLocation loc)
        {
            return GetRecord(loc).effectInch;
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
        public ExtraShipType extraShipType;
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
        public string remark;
        // public string portraitCode;
        // public string portraitTopCode;
        public PictureReference portraitReference = new();
        public PictureReference portraitTopReference = new();
        public PictureReference portraitIconReference = new();

        public RamType ram;

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
            {ShipType.ArmedMerchantCruiser, "AMC"},
            {ShipType.Transport, "TR"},
            {ShipType.Repair, "AR"}
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

        public static float torpedoWeightPounds = 845; // Whitehead Mk1 845 lbs
        public static float rapidFiringGunAmmoCapacityTacticalTurns = 15; // SK5 default rule: RF Gun can fire for 15 tactical turns (30 min)
        public static float rapidFiringGunAmmoWeightPoundsPerRound = 50; // 50 pounds, (47mm Hotchkiss)
        public static float rapidFiringGunAverageRoundPerMin = 10; // 10 round/min (47mm Hotchkiss)

        // Move to ShipClass
        public float GetMaxAmmoWeightPounds()
        {
            var btyWeightPounds = batteryRecords.Sum(r => r.ammunitionCapacity * r.shellWeightPounds);
            var torpedoWeightPounds = torpedoSector.ammunitionCapacity * ShipClass.torpedoWeightPounds;
            // Rapid fire battery assume 30 min "rapid firing" capacity, 10 round/min ROF, 5 pounds/round (47mm Hotchkiss)
            var rapidFiringBarrels = rapidFireBatteryRecords.Sum(r => r.barrelsLevelPort.FirstOrDefault() + r.barrelsLevelStarboard.FirstOrDefault());
            var rapidFiringAmmo = rapidFiringBarrels * rapidFiringGunAmmoCapacityTacticalTurns;
            var rapidFiringEqRound = rapidFiringAmmo * 2 * rapidFiringGunAverageRoundPerMin;
            var rfBtyWeightPounds = rapidFiringEqRound * rapidFiringGunAmmoWeightPoundsPerRound;
            return btyWeightPounds + torpedoWeightPounds + rfBtyWeightPounds;
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

        public override string ToString()
        {
            return $"ShipClass({name.GetMergedName()})";
        }

        public float GetPoint() => EvaluateGeneralScore(); // TODO: Modify it with type?

    }
}