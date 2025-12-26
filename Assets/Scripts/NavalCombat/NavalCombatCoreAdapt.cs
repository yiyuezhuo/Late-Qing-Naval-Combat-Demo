using UnityEngine;
using NavalCombatCore;
using GeographicLib;
using TMPro;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Xml.Serialization;
using System.Xml;
using System.IO;
using System.Linq;
using Unity.Properties;
using System;

using CoreUtils;

namespace NavalCombatCore
{
    public partial class ShipClass
    {
        [CreateProperty]
        public float armorScoreProp => EvaluateArmorScore();

        [CreateProperty]
        public float survivabilityProp => EvaluateSurvivability();

        [CreateProperty]
        public float batteryFirepowerProp => EvaluateBatteryFirepowerScore();

        [CreateProperty]
        public float torpedoThreatScoreProp => EvaluateTorpedoThreatScore();

        [CreateProperty]
        public float rapidFiringFirepowerProp => EvaluateRapidFiringFirepowerScore();

        [CreateProperty]
        public float firepoweScoreProp => EvaluateFirepowerScore();

        [CreateProperty]
        public float generalScoreProp => EvaluateGeneralScore();

        [CreateProperty]
        public float firepowerBowProp => EvaluateBatteryFirepowerScore(0, TargetAspect.Broad, 0, 0);

        [CreateProperty]
        public float firepowerStarboardProp => EvaluateBatteryFirepowerScore(0, TargetAspect.Broad, 0, 90);

        [CreateProperty]
        public float firepowerSternProp => EvaluateBatteryFirepowerScore(0, TargetAspect.Broad, 0, 180);

        [CreateProperty]
        public float firepowerPortProp => EvaluateBatteryFirepowerScore(0, TargetAspect.Broad, 0, 270);

        [CreateProperty]
        public StyleBackground portraitTopStyleBackground => UnityWebRequestImageReader.Instance.FetchStyleBackground(portraitTopReference.ResolvePath());

        [CreateProperty]
        public StyleBackground portraitIconStyleBackground => UnityWebRequestImageReader.Instance.FetchStyleBackground(portraitIconReference.ResolvePath());

        [CreateProperty]
        public StyleBackground portraitStyleBackground => UnityWebRequestImageReader.Instance.FetchStyleBackground(portraitReference.ResolvePath());
    
        [CreateProperty]
        public string forceBuilderText => $"< {name.GetShortName()} ({GetPoint()})";

        [CreateProperty]
        public Length portraitWidth => new Length(
            lengthFoot / 1000 * 100, // 300 foot => 30 (30%)
            LengthUnit.Percent
        );

        [CreateProperty]
        public Texture2D countryFlagTexture => UnityWebRequestImageReader.Instance.FetchTexture2D(Utils.GetCountryPath(country));
    
        [CreateProperty]
        public bool isInEditMode => GamePreference.Instance.isInEditMode;
    }

    public partial class LaunchedTorpedo : IPortraitViewerObservable
    {
        static PictureReference sharedPortraitTopReference = new() { path = "Pictures/Ships/Schwartzkopff Torpedo_Top", isBuiltin = true };
        static PictureReference sharedPortraitIconReference = new() { path = "Pictures/Ships/Schwartzkopff Torpedo_icon", isBuiltin = true };


        Country IPortraitViewerObservable.GetCountry() => GetShooter()?.shipClass?.country ?? Country.General;
        PictureReference IPortraitViewerObservable.GetPortraitTopReference() => sharedPortraitTopReference;
        PictureReference IPortraitViewerObservable.GetPortraitIconReference() => sharedPortraitIconReference;
        bool IPortraitViewerObservable.IsShowArrow() => false;
        GlobalString IPortraitViewerObservable.GetName() => sourceName;
        float IPortraitViewerObservable.GetDesiredHeadingDeg() => headingDeg;
        string IPortraitViewerObservable.GetAcronym() => "T";

        [CreateProperty]
        public string shooterDesc
        {
            get => GetShooter()?.namedShip.name.GetMergedName() ?? "[Not Specified or Invalid]";
        }

        [CreateProperty]
        public string desiredTargetDesc
        {
            get => GetDesiredTarget()?.namedShip.name.GetMergedName() ?? "[Not Specified or Invalid]";
        }

        [CreateProperty]
        public string sourceNameDesc
        {
            get => sourceName.GetMergedName();
        }

        [CreateProperty]
        public string hitObjectDesc
        {
            get => GetHitObject()?.namedShip.name.GetMergedName() ?? "[Not Specified or Invalid]";
        }
    }

    public partial class ShipLog : IPortraitViewerObservable
    {
        [CreateProperty]
        public ShipClass shipClassProperty
        {
            get => shipClass;
        }

        [CreateProperty]
        public Leader leaderProp
        {
            get => leader;
        }

        [CreateProperty]
        public NamedShip namedShipProp
        {
            get => namedShip;
        }

        [CreateProperty]
        public string namedShipDesc
        {
            get => namedShip?.name.mergedName ?? "[Not Specified]";
        }

        [CreateProperty]
        public string namedShipDescLink
        {
            get
            {
                var name = namedShip?.name.mergedName;
                if (name == null)
                    return "[Not Specified]";
                return $"<link=\"namedShip\"><color=#40a0ff><u>{name}</u></color></link>";
            }
        }

        [CreateProperty]
        public string shipClassDescLink
        {
            get
            {
                var name = shipClass?.name.mergedName;
                if (name == null)
                    return "[Not Specified]";
                return $"Class: <link=\"shipClass\"><color=#40a0ff><u>{name}</u></color></link>";
            }
        }

        [CreateProperty]
        public string captainDesc
        {
            get => leader?.name.mergedName ?? "[Not Specified]";
        }

        [CreateProperty]
        public string captainDescLink
        {
            get
            {
                var name = leader?.name.mergedName;
                if (name == null)
                    return "[Not Specified]";
                // return $"Captain: <link=\"captain\"><color=#40a0ff><u>{name}</u></color></link>";
                return $"<link=\"captain\"><color=#40a0ff><u>{name}</u></color></link>";
            }
        }

        [CreateProperty]
        public Texture2D captainPortraitTexture => leader?.portraitReference?.texture2d ?? null; 

        [CreateProperty]
        public string oobParentDescLink
        {
            get
            {
                var member = (IShipGroupMember)this;
                var parentGroup = member.GetParentGroup();
                // return parentGroup?.name.mergedName ?? "[Not Specified]";
                var name = parentGroup?.name.mergedName ?? "[Not Specified]";
                return $"<link=\"group\"><color=#40a0ff><u>{name}</u></color></link>";
            }
        }

        [CreateProperty]
        public string oobParentDesc
        {
            get
            {
                var member = (IShipGroupMember)this;
                var parentGroup = member.GetParentGroup();
                return parentGroup?.name.mergedName ?? "[Not Specified]";
            }
        }

        [CreateProperty]
        public string summary
        {
            get => Summary();
        }

        [CreateProperty]
        public string followedTargetDesc
        {
            get => followedTarget?.namedShip?.name.mergedName ?? "[Not Specified or Invalid]";
        }

        // [CreateProperty]
        // public DisplayStyle
        [CreateProperty]
        public ShipLog followedTargetProp
        {
            get => followedTarget;
        }

        [CreateProperty]
        public StyleEnum<DisplayStyle> displayStyleOfControlModeIsFollowTarget
        {
            get => controlMode == ControlMode.FollowTarget ? DisplayStyle.Flex : DisplayStyle.None;
        }

        [CreateProperty]
        public string relativeToTargetDesc
        {
            get => relativeToTarget?.namedShip?.name.mergedName ?? "[Not Specified or Invalid]";
        }

        [CreateProperty]
        public StyleEnum<DisplayStyle> displayStyleOfControlModeIsRelativeToTarget
        {
            get => controlMode == ControlMode.RelativeToTarget ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // Score: presentation & AI debug
        [CreateProperty]
        public float armorScoreProp => EvaluateArmorScore();

        [CreateProperty]
        public float survivabilityProp => EvaluateSurvivability();

        [CreateProperty]
        public float batteryFirepowerProp => EvaluateBatteryFirepowerScore();

        [CreateProperty]
        public float torpedoThreatScoreProp => EvaluateTorpedoThreatScore();

        [CreateProperty]
        public float rapidFiringFirepowerProp => EvaluateRapidFiringFirepowerScore();

        [CreateProperty]
        public float firepoweScoreProp => EvaluateFirepowerScore();

        [CreateProperty]
        public float generalScoreProp => EvaluateGeneralScore();

        [CreateProperty]
        public float firepowerBowProp => EvaluateBowFirepowerScore();

        [CreateProperty]
        public float firepowerStarboardProp => EvaluateStarboardFirepowerScore();

        [CreateProperty]
        public float firepowerSternProp => EvaluateSternFirepowerScore();

        [CreateProperty]
        public float firepowerPortProp => EvaluatePortFirepowerScore();

        [CreateProperty]
        public Doctrine doctrineProp => doctrine;

        [CreateProperty]
        public string damagePointProgrssDesc => Localize("DP Progress {0} Damage Tier: {1}", (damagePoint / Math.Max(1, shipClass?.damagePoint ?? 0)).ToString("P1"), GetDamageTier());
        // public string damagePointProgrssDesc => $"DP Progress {(damagePoint / Math.Max(1, shipClass?.damagePoint ?? 0)).ToString("P1")} Damage Tier: {GetDamageTier()}";

        [CreateProperty]
        public string damagePointProgrssDescShort => $"DP {damagePoint:F0}/{shipClass?.damagePoint} ({(damagePoint / Math.Max(1, shipClass?.damagePoint ?? 0)).ToString("P1")})";


        [CreateProperty]
        public float maxSpeedKnotsProp => GetMaxSpeedKnots();

        [CreateProperty]
        public float minSpeedKnotsProp => GetMinSpeedKnots();

        [CreateProperty]
        public int damageControlRatingProp => GetDamageControlRating();

        public string GetMapStatePrefix()
        {
            return mapState switch
            {
                MapState.NotDeployed => "_",
                MapState.Deployed => "",
                MapState.Destroyed => "+",
                _ => "Unknown"
            };
        }

        [CreateProperty]
        public string labelName => GetMapStatePrefix() + (namedShip?.name?.GetMergedName() ?? "[Named Ship not invalid or not specified]");

        [CreateProperty]
        public string damageEffectDesc
        {
            get
            {
                var totalSubStates = GetSubStatesDownward().ToList();
                var severityStatesCount = totalSubStates.Where(de => de.lifeCycle == StateLifeCycle.SeverityBased || de.lifeCycle == StateLifeCycle.ShipboardFire).Count();
                var dcr = shipClass?.damageControlRatingUnmodified - damageControlRatingHits;
                return $"Dmg Ctrl: {severityStatesCount}/{dcr} T:{totalSubStates.Count}";
            }
        }

        [CreateProperty]
        public bool isInEditMode => GamePreference.Instance.isInEditMode;

        // IPortraitViewerObservable
        PictureReference IPortraitViewerObservable.GetPortraitTopReference() => shipClass?.portraitTopReference;
        PictureReference IPortraitViewerObservable.GetPortraitIconReference() => shipClass?.portraitIconReference;
        // string IPortraitViewerObservable.GetPortraitTopCode() => shipClass.portraitTopCode;
        Country IPortraitViewerObservable.GetCountry() => shipClass.country;
        GlobalString IPortraitViewerObservable.GetName() => namedShip?.name;
        bool IPortraitViewerObservable.IsShowArrow() => GetEffectiveControlMode() == ControlMode.Independent;
        string IPortraitViewerObservable.GetAcronym() => shipClass.GetAcronym();
        float IPortraitViewerObservable.GetDesiredHeadingDeg() => desiredHeadingDeg;
    }

    public partial class FireControlSystemStatusRecord
    {
        [CreateProperty]
        public string info => $"FCS #{GetSubIndex() + 1}";

        [CreateProperty]
        public string targetDesc => GetTarget()?.namedShip?.name.mergedName ?? "[Not Specified]";
    
        [CreateProperty]
        public bool isInEditMode => GamePreference.Instance.isInEditMode;
    }

    public partial class BatteryStatus
    {
        [CreateProperty]
        public BatteryRecord batteryRecord
        {
            get => GetBatteryRecord();
        }

        [CreateProperty]
        public bool isInEditMode => GamePreference.Instance.isInEditMode;
    }

    public partial class BatteryRecord
    {
        [CreateProperty]
        public string labelName
        {
            get
            {
                var shipClass = EntityManager.Instance.GetParent<ShipClass>(this);
                var shipClassName = shipClass != null ? shipClass.name.GetShortName() : "_";
                return $"{shipClassName} | {name.GetShortName()} ({shellSizeInch}″, {maxRateOfFireShootPerMin}r/min, {damageRating}, {fireControlType.code}, {roundsPerGun} rpg)";
            }
        }

        [CreateProperty]
        public float roundsPerGun => GetRoundsPerGun();
    }

    public partial class AbstractMountStatusRecord
    {
        [CreateProperty]
        public string firingTargetDesc
        {
            get => GetFiringTarget()?.namedShip?.name?.GetMergedName() ?? "[Not Specified]";
        }

        [CreateProperty]
        public bool isInEditMode => GamePreference.Instance.isInEditMode;
    }

    public partial class MountStatusRecord
    {
        [CreateProperty]
        public MountLocationRecordInfo mountLocationRecordInfo
        {
            get => GetMountLocationRecordInfo();
        }

        [CreateProperty]
        public MountLocation mountLocation
        {
            get => mountLocationRecordInfo?.record?.mountLocation ?? MountLocation.NotSpecified;
        }

        [CreateProperty]
        public string mountLocationRecordSummary
        {
            get
            {
                // var r = mountLocationRecordInfo?.record;
                // if (r == null)
                //     return "Invalid";
                // return $"{r.mounts}x{r.barrels} {r.mountLocation}";
                return GetMountLocationRecordInfo()?.Summary() ?? "Invalid";
            }
        }

        // [CreateProperty]
        // public bool isInEditMode => GamePreference.Instance.isInEditMode;

    }

    public partial class TorpedoMountStatusRecord
    {
        [CreateProperty]
        public MountLocationRecordInfo torpedoMountLocationRecordInfo
        {
            get => GetTorpedoMountLocationRecordInfo();
            // get => GetTorpedoMountLocationRecordInfo()?.Summary() ?? "Invalid";
        }

        [CreateProperty]
        public string torpedoMountLocationRecordSummary
        {
            get
            {
                // var r = torpedoMountLocationRecordInfo?.record;
                // if (r == null)
                //     return "Invalid";
                // return $"{r.mounts}x{r.barrels} {r.mountLocation}";
                return GetTorpedoMountLocationRecordInfo()?.Summary() ?? "Invalid";
            }
        }

        [CreateProperty]
        public MountLocation torpedoMountLocation
        {
            get => torpedoMountLocationRecordInfo?.record?.mountLocation ?? MountLocation.NotSpecified;
        }
    }

    public partial class RapidFiringStatus
    {
        [CreateProperty]
        public RapidFireBatteryRecord rapidFireBatteryRecord
        {
            get => GetRapidFireBatteryRecord();
        }

        [CreateProperty]
        public string info
        {
            get
            {
                return GetInfo();
            }
        }

        [CreateProperty]
        public bool isInEditMode => GamePreference.Instance.isInEditMode;

    }

    public partial class ShipGroup
    {
        [CreateProperty]
        public Leader leaderProp => leader;

        [CreateProperty]
        public Doctrine doctrineProp => doctrine;

        [CreateProperty]
        public bool isInEditMode => GamePreference.Instance.isInEditMode;
    }

    public partial class LeaderReference
    {
        [CreateProperty]
        public StyleBackground portrait
        {
            get => Get()?.portraitReference?.pictureStyleBackground ?? null;
        }

        [CreateProperty]
        public Texture2D portraitTexture2D
        {
            get => Get()?.portraitReference?.texture2d ?? null;
        }

        [CreateProperty]
        public string name => Get()?.name?.mergedName ?? "[Not Specified or Invalid]";
    }

    public partial class NamedShip
    {
        [CreateProperty]
        public Leader defaultLeaderProp => defaultLeader;

        [CreateProperty]
        public StyleBackground shipClassPortraitStyleBackground
        {
            get
            {
                return UnityWebRequestImageReader.Instance.FetchStyleBackground(shipClass?.portraitReference?.ResolvePath());
            }
        }

        [CreateProperty]
        public StyleBackground shipClassTopPortraitStyleBackground
        {
            get
            {
                return UnityWebRequestImageReader.Instance.FetchStyleBackground(shipClass?.portraitTopReference?.ResolvePath());
            }
        }

        [CreateProperty]
        public StyleBackground shipClassIconPortraitStyleBackground => UnityWebRequestImageReader.Instance.FetchStyleBackground(shipClass.portraitIconReference.ResolvePath());


        [CreateProperty]
        public Country shipClassCountry
        {
            get => EntityManager.Instance.Get<ShipClass>(shipClassObjectId)?.country ?? Country.General;
        }

        [CreateProperty]
        public string shipClassDesc
        {
            get => EntityManager.Instance.Get<ShipClass>(shipClassObjectId)?.name.mergedName ?? "[Not Specified]";
        }

        [CreateProperty]
        public string forceBuilderText => $"< {name.GetShortName()} ({shipClass.GetPoint()})";
    
        [CreateProperty]
        public Length portraitWidth => new Length(
            (shipClass?.lengthFoot ?? 300) / 1000 * 100, // 300 foot => 30 (30%)
            LengthUnit.Percent
        );

        [CreateProperty]
        public string shipClassRemark => shipClass?.remark ?? "";

        [CreateProperty]
        public bool isInEditMode => GamePreference.Instance.isInEditMode;
    }

    public partial class RapidFiringTargettingStatus
    {
        [CreateProperty]
        public string targetDesc
        {
            get => GetTarget()?.namedShip?.name?.GetMergedName() ?? "[Not Specified or Invalid]";
        }

        [CreateProperty]
        public bool isInEditMode => GamePreference.Instance.isInEditMode;

    }

    public partial class SubState
    {
        [CreateProperty]
        public string description => Describe();

        [CreateProperty]
        public bool damageControllableProp => damageControllable;
    }

    public partial class UnitModule
    {
        [XmlIgnore] // For some reason, XmlSeralizer will serialize this readonly property defaultly, so impose this resctrition.
        [CreateProperty]
        public List<SubState> subStatesDownward => GetSubStatesDownward().ToList();
    }

    public partial class ShipTypeLossItem
    {
        [CreateProperty]
        public string shipTypeDesc => ShipClass.GetAcronymFor(shipType);
    }

    public partial class SubjectLog
    {
        [CreateProperty]
        public string summaryContent
        {
            get
            {
                var subjectName = EntityManager.Instance.Get<ShipLog>(subjectId)?.namedShip?.name.GetShortName() ?? "[Invalid]";
                return $"{subjectName} {log.SummaryContent()}";
            }
        }
    }

    public partial class SideVictoryStatus
    {
        static string Localize(string key, params object[] args) => ServiceLocator.Get<ILocalizeService>().Get(key, args);

        [CreateProperty]
        public string victoryLevelStr => Localize($"{nameof(VictoryLevel)}.{victoryLevel}");
    }

    public partial class FireControlSystem
    {
        [XmlIgnore]
        [CreateProperty]
        public FCSCode codeProp
        {
            get => code;
            set
            {
                code = value;
                SyncStatesByCode();
            }
        }

        [XmlIgnore]
        [CreateProperty]
        public GunSightType gunSightProp
        {
            get => gunSight;
            set
            {
                gunSight = value;
                SyncCodeByStates();
            }
        }

        [XmlIgnore]
        [CreateProperty]
        FireControlInstrumentType fireControlInstrumentProp
        {
            get => fireControlInstrument;
            set
            {
                fireControlInstrument = value;
                SyncCodeByStates();
            }
        }

        [XmlIgnore]
        [CreateProperty]
        RangeFinderType rangeFinderProp
        {
            get => rangeFinder;
            set
            {
                rangeFinder = value;
                SyncCodeByStates();
            }
        }

        [XmlIgnore]
        [CreateProperty]
        DirectorControlType directorControlProp
        {
            get => directorControl;
            set
            {
                directorControl = value;
                SyncCodeByStates();
            }
        }

        [XmlIgnore]
        [CreateProperty]
        StabilizationType stabilizationProp
        {
            get => stabilization;
            set
            {
                stabilization = value;
                SyncCodeByStates();
            }
        }

        [XmlIgnore]
        [CreateProperty]
        PowerRemoteControlType powerRemoteControlProp
        {
            get => powerRemoteControl;
            set
            {
                powerRemoteControl = value;
                SyncCodeByStates();
            }
        }
    }

    public partial class MountLocationRecord
    {
        [XmlIgnore]
        [CreateProperty]
        public MountLocation mountLocationProp
        {
            get => mountLocation;
            set
            {
                mountLocation = value;
                SyncDefaultMountArcs();
            }
        }

        [XmlIgnore]
        [CreateProperty]
        public bool defaultNarrowProp
        {
            get => defaultNarrow;
            set
            {
                defaultNarrow = value;
                SyncDefaultMountArcs();
            }
        }
    }

    public partial class ArmorRating
    {
        [XmlIgnore]
        [CreateProperty]
        public ArmorType armorTypeProp
        {
            get => armorType;
            set
            {
                armorType = value;
                if(armorType != ArmorType.NotSpecified)
                {
                    TrySetFactorAndEffectInch();
                }
            }
        }

        [XmlIgnore]
        [CreateProperty]
        public float armorTypeFactorProp
        {
            get => armorTypeFactor;
            set
            {
                armorTypeFactor = value;
                TryInferArmorType();
                SetEffectInchByArmorTypeFactor();
            }
        }
    }

    public partial class BatteryAmmunitionRecord
    {
        [CreateProperty]
        public bool isInEditMode => GamePreference.Instance.isInEditMode;
    }

    public partial class TorpedoSector
    {
        [CreateProperty]
        public bool isInEditMode => GamePreference.Instance.isInEditMode;
    }

    public partial class Doctrine
    {
        protected static string Localize(string key, params object[] args) => ServiceLocator.Get<ILocalizeService>().Get(key, args);
        protected static string LocalizeEnum<T>(T obj) => ServiceLocator.Get<ILocalizeService>().GetEnum(obj);

        [CreateProperty]
        public string resolvedManeuverAutomaticTypeStr => LocalizeEnum(GetManeuverAutomaticType());

        [CreateProperty]
        public string resolvedFireAutomaticTypeStr => LocalizeEnum(GetFireAutomaticType());

        [CreateProperty]
        public string resolvedAmmunitionFallbackableStr => Localize(GetAmmunitionFallbackable().ToString());

        [CreateProperty]
        public string resolvedAmmunitionSwitchAutomationTypeStr => LocalizeEnum(GetAmmunitionSwitchAutomaticType());

        public string Describe(UnspecifiableFloat num)
        {
            if(!num.isSpecified)
            {
                return Localize("Unspecified");
            }
            return num.value.ToString();
        }

        [CreateProperty]
        public string resolvedMaximumFiringDistanceYardsFor200mmPlusDesc => Describe(GetMaximumFiringDistanceYardsFor200mmPlus());

        [CreateProperty]
        public string resolvedMaximumFiringDistanceYardsFor100mmTo200mm => Describe(GetMaximumFiringDistanceYardsFor100mmTo200mm());

        [CreateProperty]
        public string resolvedMaximumFiringDistanceYardsFor100mmLess => Describe(GetMaximumFiringDistanceYardsFor100mmLess());

        [CreateProperty]
        public string resolvedMaximumFiringDistanceYardsForTorpedo => Describe(GetMaximumFiringDistanceYardsForTorpedo());
    }
}

namespace CoreUtils
{
    public partial class GlobalString
    {
        [CreateProperty]
        public string mergedName
        {
            get => GetMergedName();
        }
    }

    public partial class AbstractGameState
    {
        [CreateProperty]
        public bool isInEditMode => GamePreference.Instance.isInEditMode;
    }
}