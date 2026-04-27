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
using System.Globalization;
using YYZ;

using CoreUtils;

namespace NavalCombatCore
{
    internal static class InformationPanelSummaryUtils
    {
        public static string GetCurrentTargetSuffix(IEnumerable<ShipLog> targets)
        {
            var targetNames = new List<string>();
            var seenTargetIds = new HashSet<string>();

            foreach (var target in targets)
            {
                if (target == null)
                    continue;

                var dedupeKey = target.objectId ?? target.namedShipObjectId ?? target.namedShip?.name?.GetShortName();
                if (!string.IsNullOrWhiteSpace(dedupeKey) && !seenTargetIds.Add(dedupeKey))
                    continue;

                var shortName = target.namedShip?.name?.GetShortName();
                targetNames.Add(string.IsNullOrWhiteSpace(shortName) ? target.objectId ?? "[Unknown]" : shortName);
            }

            return targetNames.Count == 0 ? "" : $" -> {string.Join(", ", targetNames)}";
        }
    }

    public class NameLinkPlaceholder // ShipLog, LandUnit, StrategicGroup "implement" this now
    {
        [CreateProperty]
        public string nameLink;
    }

    public class StrategicOOBTreeRowPlaceholder
    {
        [CreateProperty]
        public string oobNameLink;

        [CreateProperty]
        public string oobLeaderNameLink;

        [CreateProperty]
        public Leader oobLeader;
    }

    public partial class ShipClass : INamed
    {
        [XmlIgnore]
        [CreateProperty]
        public float displacementTonsProp
        {
            get => displacementTons;
            set
            {
                displacementTons = value;
                damagePoint = CalculateDamagePointFromDisplacement(value);
                targetSizeModifier = CalculateTargetSizeModifierFromDisplacement(value);
                damageControlRatingUnmodified = CalculateDamageControlRatingFromDisplacement(value);
                ApplyDisplacementTypeDefaults();
            }
        }

        [XmlIgnore]
        [CreateProperty]
        public ShipType typeProp
        {
            get => type;
            set
            {
                type = value;
                ApplyDisplacementTypeDefaults();
            }
        }

        void ApplyDisplacementTypeDefaults()
        {
            lengthFoot = CalculateLengthFootFromDisplacementAndType(displacementTons, type);
            beamFoot = CalculateBeamFootFromDisplacementAndType(displacementTons, type);
            draftFoot = CalculateDraftFootFromDisplacementAndType(displacementTons, type);
            complementMen = CalculateComplementMenFromDisplacementAndType(displacementTons, type);
        }

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

        [CreateProperty]
        public bool isTransport => type == ShipType.Transport;

        [CreateProperty]
        public bool isDebug => GamePreference.Instance.isDebug;

        public GlobalString GetName() => name;
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
        public string GetBatterySummary()
        {
            if (shipClass == null)
                return "[Class Invalid or not binded]";
            return string.Join("\n", batteryStatus.Select(bs => bs.Summary()));
        }

        public string GetTorpedoSummary()
        {
            var _shipClass = shipClass;
            if (_shipClass == null)
                return "[Class Invalid or not binded]";

            var torpedoBarrels = _shipClass.torpedoSector.mountLocationRecords.Sum(r => r.barrels * r.mounts);
            var torpedoBarrelsAvailable = torpedoSectorStatus.mountStatus.Where(m => m.IsOperational()).Sum(m => m.barrels);
            var torpedoMagazine = torpedoSectorStatus.ammunition;
            var torpedoLoaded = torpedoSectorStatus.mountStatus.Sum(m => m.currentLoad);
            var torpedoTotal = torpedoSectorStatus.GetAmmunitionMagazinePlusLoaded();
            var targetSuffix = InformationPanelSummaryUtils.GetCurrentTargetSuffix(
                torpedoSectorStatus.mountStatus.Select(m => m.GetFiringTarget())
            );
            return $"x{torpedoBarrelsAvailable}/{torpedoBarrels} {_shipClass.torpedoSector.name.GetShortName()} (mag {torpedoMagazine}, tube {torpedoLoaded}, total {torpedoTotal}){targetSuffix}";
        }

        public string GetRapidFiringSummary()
        {
            if (shipClass == null)
                return "[Class Invalid or not binded]";
            return string.Join("\n", rapidFiringStatus.Select(s => s.GetInfo()));
        }

        public string Summary()
        {
            if (shipClass == null)
                return "[Class Invalid or not binded]";

            var lines = new List<string>
            {
                Localize("Battery:"),
                GetBatterySummary(),
                Localize("Torpedo:"),
                GetTorpedoSummary(),
                Localize("Rapid Firing Battery:"),
                GetRapidFiringSummary()
            };

            return string.Join("\n", lines);
        }

        [XmlIgnore]
        public int NonPhysicalPoseRevision { get; private set; }

        public void MarkNonPhysicalPoseChanged()
        {
            NonPhysicalPoseRevision++;
        }

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
                return $"<link=\"namedShip\"><color=#40a0ff>{name}</color></link>";
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
                return $"Class: <link=\"shipClass\"><color=#40a0ff>{name}</color></link>";
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
                return $"<link=\"captain\"><color=#40a0ff>{name}</color></link>";
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
                return $"<link=\"group\"><color=#40a0ff>{name}</color></link>";
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
        public string executionDelayDesc
        {
            get
            {
                var delayValue = Math.Clamp(1f - GetIndependentResponseCoef(), 0f, 1f);
                var delayText = delayValue.ToString("0.###", CultureInfo.InvariantCulture);
                return IsDetachedForCommandStructure() ? $"{delayText} (Detached)" : delayText;
            }
        }

        [CreateProperty]
        public StyleEnum<DisplayStyle> displayStyleOfExecutionDelay
        {
            get => GetEffectiveControlMode() == ControlMode.Independent ? DisplayStyle.Flex : DisplayStyle.None;
        }

        [CreateProperty]
        public string summary
        {
            get => Summary();
        }

        [CreateProperty]
        public string batterySummary
        {
            get => GetBatterySummary();
        }

        [CreateProperty]
        public string torpedoSummary
        {
            get => GetTorpedoSummary();
        }

        [CreateProperty]
        public string rapidFiringSummary
        {
            get => GetRapidFiringSummary();
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

        [CreateProperty]
        public string manualRouteStatusDesc
        {
            get => Localize(GetManualRouteStatusKey());
        }

        [CreateProperty]
        public string manualRouteSummaryDesc
        {
            get => Localize(
                "Waypoints: {0}, Remaining distance: {1:0.00} nm",
                GetManualRouteWaypointCount(),
                GetManualRouteRemainingDistanceMeters() / MeasureUtils.navalMileToMeter);
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
        public string damagePointProgrssDesc
        {
            get
            {
                var totalDamagePoint = shipClass?.damagePoint ?? 0;
                var progressRatio = damagePoint / Math.Max(1, totalDamagePoint);
                return Localize(
                    "DP Progress {0} ({1}/{2}) Damage Tier: {3}",
                    progressRatio.ToString("P1"),
                    damagePoint.ToString("F0"),
                    totalDamagePoint.ToString("F0"),
                    GetDamageTier());
            }
        }
        // public string damagePointProgrssDesc => $"DP Progress {(damagePoint / Math.Max(1, shipClass?.damagePoint ?? 0)).ToString("P1")} Damage Tier: {GetDamageTier()}";

        [CreateProperty]
        public string damagePointProgrssDescShort => $"DP {damagePoint:F0}/{shipClass?.damagePoint} ({(damagePoint / Math.Max(1, shipClass?.damagePoint ?? 0)).ToString("P1")})";


        [CreateProperty]
        public float maxSpeedKnotsProp => GetMaxSpeedKnots();

        [CreateProperty]
        public float minSpeedKnotsProp => GetMinSpeedKnots();

        [CreateProperty]
        public bool surpriseAttackStatusVisible => IsSurpriseRestricted();

        [CreateProperty]
        public string surpriseAttackStatusText
        {
            get
            {
                var awakeCountdownMinutes = GetResolvedAwakeCountdownMinutes();
                if (awakeCountdownMinutes <= 0)
                    return "";
                return IsSleepUnderSurpriseAttack()
                    ? Localize("Sleep ({0} min)", awakeCountdownMinutes)
                    : Localize("Awaking ({0} min)", awakeCountdownMinutes);
            }
        }

        [CreateProperty]
        public bool surpriseAttackCommandEditable => !IsSurpriseCommandChangeBlocked();

        [CreateProperty]
        public int damageControlRatingProp => GetDamageControlRating();

        [CreateProperty]
        public int portSearchlightHits
        {
            get => searchLightHits?.portHit ?? 0;
            set
            {
                if (searchLightHits != null)
                    searchLightHits.portHit = value;
            }
        }

        [CreateProperty]
        public int starboardSearchlightHits
        {
            get => searchLightHits?.starboardHit ?? 0;
            set
            {
                if (searchLightHits != null)
                    searchLightHits.starboardHit = value;
            }
        }

        [CreateProperty]
        public bool portSearchlightEnabled
        {
            get => searchLightHits?.portEnabled ?? false;
            set
            {
                if (searchLightHits != null)
                    searchLightHits.portEnabled = value;
            }
        }

        [CreateProperty]
        public float portSearchlightDirectionDeg
        {
            get => searchLightHits?.portDirectionDeg ?? 0f;
            set
            {
                if (searchLightHits != null)
                    searchLightHits.portDirectionDeg = value;
            }
        }

        [CreateProperty]
        public bool starboardSearchlightEnabled
        {
            get => searchLightHits?.starboardEnabled ?? false;
            set
            {
                if (searchLightHits != null)
                    searchLightHits.starboardEnabled = value;
            }
        }

        [CreateProperty]
        public float starboardSearchlightDirectionDeg
        {
            get => searchLightHits?.starboardDirectionDeg ?? 0f;
            set
            {
                if (searchLightHits != null)
                    searchLightHits.starboardDirectionDeg = value;
            }
        }

        [CreateProperty]
        public bool portSearchlightToggleEditable => isInEditMode && (searchLightHits?.CanUsePortSearchlight() ?? false);

        [CreateProperty]
        public bool portSearchlightDirectionEditable => isInEditMode && (searchLightHits?.portEnabled ?? false);

        [CreateProperty]
        public bool starboardSearchlightToggleEditable => isInEditMode && (searchLightHits?.CanUseStarboardSearchlight() ?? false);

        [CreateProperty]
        public bool starboardSearchlightDirectionEditable => isInEditMode && (searchLightHits?.starboardEnabled ?? false);

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
                var severityStatesCount = totalSubStates.Count(de => de.lifeCycle == StateLifeCycle.SeverityBased || de.lifeCycle == StateLifeCycle.ShipboardFire);
                var dcr = shipClass?.damageControlRatingUnmodified - damageControlRatingHits;
                // var maxShipboardFireSeverity = totalSubStates
                //     .Where(ss => ss.lifeCycle == StateLifeCycle.ShipboardFire)
                //     .Select(ss => ss.severity)
                //     .DefaultIfEmpty(0)
                //     .Max();
                var maxShipboardFireSeverity = totalSubStates
                    .Where(ss => ss.lifeCycle == StateLifeCycle.ShipboardFire)
                    .Sum(ss => ss.severity);

                var machinerySpaces = (shipClass?.speedKnotsEngineRoomsLevels?.Count ?? 0) + (shipClass?.speedKnotsBoilerRooms?.Count ?? 0);
                var floodedMachineryHits = (dynamicStatus?.engineRoomFloodingHits ?? 0) + (dynamicStatus?.boilerRoomFloodingHits ?? 0);
                var floodedThresholdHits = (int)Math.Ceiling(machinerySpaces * 0.8f);

                var baseDesc = $"Dmg Ctrl: {severityStatesCount}/{dcr} T:{totalSubStates.Count} " +
                    $"<color=#ff4040>Fire:{maxShipboardFireSeverity:F0}</color>";
                if (IsLandBattery())
                    return baseDesc;

                return baseDesc + $" <color=#4090ff>Flood:{floodedMachineryHits}/{floodedThresholdHits}</color>";
            }
        }

        [CreateProperty]
        public string nameLink
        {
            get
            {
                var name = GetName()?.GetMergedName();
                if (name == null)
                    return "[Not Specified]";
                return $"<link=\"nameLink\"><color=#40a0ff>{name}</color></link>";
            }
        }

        [CreateProperty]
        public bool isInEditMode => GamePreference.Instance.isInEditMode;

        [CreateProperty]
        public bool isLandBattery => IsLandBattery();

        [CreateProperty]
        public bool isTransport => shipClass?.isTransport ?? false;

        [CreateProperty]
        public string shipLevelFiringTargetDesc
        {
            get
            {
                var shipLevelFiringTarget = GetShipLevelFiringTarget();
                if(shipLevelFiringTarget == null)
                    return "";
                return $"Attack: {shipLevelFiringTarget?.namedShip.name.GetMergedName()}";
            }
        }

        // IPortraitViewerObservable
        PictureReference IPortraitViewerObservable.GetPortraitTopReference() => shipClass?.portraitTopReference;
        PictureReference IPortraitViewerObservable.GetPortraitIconReference() => shipClass?.portraitIconReference;
        // string IPortraitViewerObservable.GetPortraitTopCode() => shipClass.portraitTopCode;
        Country IPortraitViewerObservable.GetCountry() => shipClass.country;
        GlobalString IPortraitViewerObservable.GetName() => namedShip?.name;
        bool IPortraitViewerObservable.IsShowArrow() => !IsLandBattery() && mapState == MapState.Deployed && GetEffectiveControlMode() == ControlMode.Independent;
        string IPortraitViewerObservable.GetAcronym() => shipClass.GetAcronym();
        float IPortraitViewerObservable.GetDesiredHeadingDeg() => IsLandBattery() ? 0f : desiredHeadingDeg;
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
        const float DamageRatingShellSizeCoef = 1.30f;
        const float DamageRatingShellWeightSqrtCoef = 0.82f;
        const float DamageRatingIntercept = 0.4f;

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

        [XmlIgnore]
        [CreateProperty]
        public float shellSizeInchProp
        {
            get => shellSizeInch;
            set
            {
                shellSizeInch = value;
                UpdateDamageRatingDefault();
            }
        }

        [XmlIgnore]
        [CreateProperty]
        public float shellWeightPoundsProp
        {
            get => shellWeightPounds;
            set
            {
                shellWeightPounds = value;
                UpdateDamageRatingDefault();
            }
        }

        void UpdateDamageRatingDefault()
        {
            var shellSize = Math.Max(0f, shellSizeInch);
            var shellWeight = Math.Max(0f, shellWeightPounds);
            damageRating = RoundHalfUp(DamageRatingIntercept
                + DamageRatingShellSizeCoef * shellSize
                + DamageRatingShellWeightSqrtCoef * Mathf.Sqrt(shellWeight));
        }

        static float RoundHalfUp(float value)
        {
            return Mathf.Floor(value + 0.5f);
        }
    }

    public partial class RapidFireBatteryRecord
    {
        const float EffectiveRangeToMaxRangeCoef = 0.45f;

        [XmlIgnore]
        [CreateProperty]
        public string metaInfoLabel
        {
            get
            {
                if (metaInfo == null)
                    return "Meta: None";

                return $"Meta Info: {metaInfo.shellSizeInch.ToString("0.###", CultureInfo.InvariantCulture)}'' ";
            }
        }

        [XmlIgnore]
        [CreateProperty]
        public float maxRangeYardsProp
        {
            get => maxRangeYards;
            set
            {
                maxRangeYards = value;
                effectiveRangeYards = RoundHalfUp(Math.Max(0f, maxRangeYards) * EffectiveRangeToMaxRangeCoef);
            }
        }

        static float RoundHalfUp(float value)
        {
            return Mathf.Floor(value + 0.5f);
        }
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

    public partial class BatteryStatus
    {
        public string Summary() // Used in information panel
        {
            var batteryRecord = GetBatteryRecord();
            if (batteryRecord == null)
                return "[Not Specified]";

            var barrels = batteryRecord.mountLocationRecords.Sum(r => r.barrels * r.mounts);
            var availableBarrels = mountStatus.Where(m => m.IsOperational()).Sum(m => m.barrels);
            var targetSuffix = InformationPanelSummaryUtils.GetCurrentTargetSuffix(
                mountStatus.Select(m => m.GetFiringTarget())
            );
            return $"{availableBarrels}/{barrels} {batteryRecord.name.GetShortName()} ({ammunition.Summary()}){targetSuffix}";
        }
    }

    public partial class RapidFiringStatus
    {
        public string GetInfo()
        {
            var r = rapidFireBatteryRecord;
            if (r == null)
                return "Not Valid";

            var (portClass, portCurrent) = GetClassCurrentBarrels(r.barrelsLevelPort, portMountHits);
            var (starboardClass, starboardCurrent) = GetClassCurrentBarrels(r.barrelsLevelStarboard, starboardMountHits);
            var targetSuffix = InformationPanelSummaryUtils.GetCurrentTargetSuffix(
                targettingRecords.Select(rf => rf.GetTarget())
            );
            return $"{portClass}({portCurrent})/{starboardClass}({starboardCurrent}) {r.name.GetShortName()} ({ammunition}){targetSuffix}";
        }

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

        [CreateProperty]
        public string nameLink
        {
            get
            {
                var name = Get()?.name?.mergedName;
                if (name == null)
                    return "[Not Specified or Invalid]";
                return $"<link=\"nameLink\"><color=#40a0ff>{name}</color></link>";
            }
        }
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
        static string ToFlowText(int initial, int current) => initial == 0 ? $"{current}" : $"{initial} -> {current}";

        [CreateProperty]
        public string shipTypeDesc => ShipClass.GetAcronymFor(shipType);

        [CreateProperty]
        public string undamagedFlowStr => ToFlowText(initialUndamaged, undamaged);

        [CreateProperty]
        public string lightFlowStr => ToFlowText(initialLight, light);

        [CreateProperty]
        public string mediumFlowStr => ToFlowText(initialMedium, medium);

        [CreateProperty]
        public string heavyFlowStr => ToFlowText(initialHeavy, heavy);

        [CreateProperty]
        public string sunkFlowStr => ToFlowText(initialSunk, sunk);
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

    public partial class ShipVictoryDetailItem
    {
        [CreateProperty]
        public string displayName => EntityManager.Instance.Get<ShipLog>(shipObjectId)?.namedShip?.name?.GetShortName() ?? name;

        [CreateProperty]
        public Length portraitWidth => new Length(
            (EntityManager.Instance.Get<ShipLog>(shipObjectId)?.shipClass?.lengthFoot ?? 300f) / 1000f * 100f,
            LengthUnit.Percent
        );

        [CreateProperty]
        public bool isSunkProp => isSunk;

        [CreateProperty]
        public StyleBackground shipIconStyleBackground
        {
            get => EntityManager.Instance.Get<ShipLog>(shipObjectId)?.shipClass?.portraitIconStyleBackground ?? default;
        }

        [CreateProperty]
        public string statOverlayText => $"S{shotsFiredCount} H{hitsLandedCount} IH{hitsTakenCount} L{damagePointLost:0.#} O{damagePointInflicted:0.#}";
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

        // [XmlIgnore]
        // [CreateProperty]
        // public bool defaultNarrowProp
        // {
        //     get => defaultNarrow;
        //     set
        //     {
        //         defaultNarrow = value;
        //         SyncDefaultMountArcs();
        //     }
        // }

        [XmlIgnore]
        [CreateProperty]
        public MountArcsPattern mountArcsPatternProp
        {
            get => mountArcsPattern;
            set
            {
                mountArcsPattern = value;
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
        public string resolvedSearchlightAutomaticTypeStr => LocalizeEnum(GetSearchlightAutomaticType());

        [CreateProperty]
        public string resolvedAmmunitionFallbackableStr => Localize(GetAmmunitionFallbackable().ToString());

        [CreateProperty]
        public string resolvedAmmunitionSwitchAutomationTypeStr => LocalizeEnum(GetAmmunitionSwitchAutomaticType());

        [CreateProperty]
        public bool showSurpriseAttackSettings => NavalGameState.Instance?.scenarioState?.surpriseAttack ?? false;

        [CreateProperty]
        public string resolvedAwakeCountdownMinutesStr => GetAwakeCountdownMinutes().ToString();

        [CreateProperty]
        public string resolvedAwakingStr => Localize(GetAwaking().ToString());

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

        [CreateProperty]
        public string shortName => GetShortName();
    }

    public partial class AbstractGameState
    {
        [CreateProperty]
        public bool isInEditMode => GamePreference.Instance.isInEditMode;
    }
}
