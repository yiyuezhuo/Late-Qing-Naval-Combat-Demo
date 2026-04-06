using System.Collections.Generic;
using CoreUtils;
using Unity.Properties;
using UnityEngine.UIElements;
using UnityEngine;
using NavalCombatCore;
using UnityEngine.InputSystem.Utilities;
using System.Xml.Serialization;
using StrategicCombatCore;
using System;
using System.Linq;
using YYZ;

namespace StrategicCombatCore
{
    public partial class DepartmentPosition
    {
        [CreateProperty]
        public string leaderName => EntityManager.Instance.Get<Leader>(objectId)?.name?.GetMergedName() ?? "[Not Defined or Invalid]";

        [CreateProperty]
        public StyleBackground leaderPortrait => EntityManager.Instance.Get<Leader>(objectId)?.portraitReference?.pictureStyleBackground ?? null;
    }


    public partial class Cell
    {
        [CreateProperty]
        public string brief
        {
            get
            {
                if(IsAreaCell())
                    return $"{terrain}, {Label?.GetShortName()}";
                return $"({x}, {y}), {terrain}, {Label?.GetShortName()}";
            }
        }

        // public string brief => $"({x}, {y}), {terrain} {Label?.GetShortName()}";

        // [CreateProperty]
        // public int cellInfoGroupCount => StrategicGameState.Instance.hexInfoMap.GetValueOrDefault((x, y))?.parentGroupReferences?.Count ?? 0;

        [CreateProperty]
        public int cellInfoGroupCount => StrategicGroupReferences?.Count ?? 0;

        [CreateProperty]
        public string sideNameHex => EntityManager.Instance.Get<SideState>(sideObjectIdHex)?.name?.mergedName ?? "";

        [CreateProperty]
        public string sideNameTop => EntityManager.Instance.Get<SideState>(sideObjectIdTop)?.name?.mergedName ?? "";

        [CreateProperty]
        public string sideNameTopRight => EntityManager.Instance.Get<SideState>(sideObjectIdTopRight)?.name?.mergedName ?? "";

        [CreateProperty]
        public string sideNameBottomRight => EntityManager.Instance.Get<SideState>(sideObjectIdBottomRight)?.name?.mergedName ?? "";

        [CreateProperty]
        public string sideNameBottom => EntityManager.Instance.Get<SideState>(sideObjectIdBottom)?.name?.mergedName ?? "";

        [CreateProperty]
        public string sideNameBottomLeft => EntityManager.Instance.Get<SideState>(sideObjectIdBottomLeft)?.name?.mergedName ?? "";

        [CreateProperty]
        public string sideNameTopLeft => EntityManager.Instance.Get<SideState>(sideObjectIdTopLeft)?.name?.mergedName ?? "";

        [XmlIgnore]
        [CreateProperty]
        public bool labelCreated
        {
            get => Label != null;
            set
            {
                if (value)
                {
                    Label = new();
                }
                else
                {
                    Label = null;
                }
            }
        }

        [CreateProperty]
        public bool hasActiveLandBattle => landBattleId != null;
    }

    public static class StyleConstants
    {
        public static Dictionary<Country, Color> countryColorMap = new()
        {
            {Country.China, Color.yellow},
            {Country.Japan, Color.white},
            {Country.Britain, Color.red},
            {Country.France, Color.purple},
            {Country.Russia, Color.darkGreen},
            {Country.UnitedState, Color.blue},
            {Country.Spain, Color.darkOrange},
            {Country.Germany, Color.black},
            {Country.Italy, Color.greenYellow},
            {Country.AustriaHugary, Color.silver},
        };
    }

    public partial class NavalContactReport : ILayableWorldSpaceGroupIconDataSource
    {
        public float stackPriority{get; set;}

        [CreateProperty]
        public string sizeStr => "";

        [CreateProperty]
        public string bottomLabelText => $"{estimation.GetPowerPoint()}?"; // Show 1/1/1/1/1/1 Style report?

        [CreateProperty]
        public Color countryColor => StyleConstants.countryColorMap.GetValueOrDefault(GetObservedSide().countries.FirstOrDefault(), Color.gray);

        [CreateProperty]
        public Sprite typeIconSprite => UnityWebRequestImageReader.Instance.FetchSprite(typeIconPath);

        [CreateProperty]
        public string typeIconPath => $"{Application.streamingAssetsPath}/Pictures/GroupTypeIcons/Fleet.png";

        [CreateProperty]
        public StyleBackground typeIcon => UnityWebRequestImageReader.Instance.FetchStyleBackground(typeIconPath);

        // public bool IsOnGridCell() => GetCell().IsGridCell();
        // public bool IsOnAreaCell() => GetCell().IsAreaCell();

        // public int x{get => GetCell().x;}
        // public int y{get => GetCell().y;}
        // public string areaCellObjectId{get => GetCell().objectId;}

        public SideState side{get => GetObservedSide();}
        public Cell cell{get => GetCell();}

        [CreateProperty]
        public string dateTimeStr => $"{CoreParameter.Instance.GetReferenceTimeZoneDateTimeOffsetString(dateTime)} (before {GetHoursToCurrent()} hours)";

        [CreateProperty]
        public string sideName => GetObservedSide().name.GetMergedName();

        [CreateProperty]
        public string estimateStr => estimation.GetEstimatateSummary();

        [CreateProperty]
        public float timelinessOpacity => GetTimelinessCoef();
    }

    public partial class StrategicGroup : ILayableWorldSpaceGroupIconDataSource
    {
        public float stackPriority{get; set;} // 0 ~ 1, reassigned when toggle

        [CreateProperty]
        public string sizeStr => GetSizeStr();

        [CreateProperty]
        public int combinedSubUnitSize => GetCombinedSubUnitSize();

        // [CreateProperty]
        // public int combinedPowerPointRounded => Mathf.RoundToInt(GetCombinedPowerPoint(true));
        [CreateProperty]
        public string bottomLabelText => Mathf.RoundToInt(GetCombinedPowerPoint(true)).ToString();


        [CreateProperty]
        public Color countryColor => StyleConstants.countryColorMap.GetValueOrDefault(country, Color.gray);

        [CreateProperty]
        public Sprite typeIconSprite => UnityWebRequestImageReader.Instance.FetchSprite(typeIconPath);

        [CreateProperty]
        public string typeIconPath => $"{Application.streamingAssetsPath}/Pictures/GroupTypeIcons/{type}.png";

        [CreateProperty]
        public StyleBackground typeIcon => UnityWebRequestImageReader.Instance.FetchStyleBackground(typeIconPath);

        [XmlIgnore]
        [CreateProperty]
        public int xProp
        {
            get => x;
            set => TryRelocateIndependentGroupToGrid(value, y);
        }

        [XmlIgnore]
        [CreateProperty]
        public int yProp
        {
            get => y;
            set => TryRelocateIndependentGroupToGrid(x, value);
        }

        [CreateProperty]
        public string areaCellObjectIdProp
        {
            get => areaCellObjectId;
            // Setter
        }

        [CreateProperty]
        public bool isXYEditable => deployState == DeployState.Independent;

        [CreateProperty]
        public StrategicGroupReference parentGroupReferenceProp => parentGroupReference;

        [CreateProperty]
        public string nameLink
        {
            get
            {
                var rawName = name.GetMergedName();
                if (rawName == null)
                    rawName = "_";
                return $"<link=\"nameLink\"><color=#40a0ff>{rawName}</color></link>";
            }
        }

        [CreateProperty]
        public string oobNameLink
        {
            get
            {
                var rawName = name.GetShortName();
                if (rawName == null)
                    rawName = "_";
                return $"<link=\"nameLink\"><color=#40a0ff>{rawName}</color></link>";
            }
        }

        [CreateProperty]
        public Leader oobLeader => leaderReference.Get();

        [CreateProperty]
        public string oobLeaderNameLink
        {
            get
            {
                var leaderName = oobLeader?.name?.GetShortName();
                if (leaderName == null)
                    return string.Empty;
                return $"<link=\"leaderLink\">{leaderName}</link>";
            }
        }

        [CreateProperty]
        public string leaderNameLink
        {
            get
            {
                var leaderName = leaderReference.Get()?.name?.GetMergedName();
                if(leaderName == null)
                {
                    return "[Not Specified]";
                }
                return $"<link=\"nameLink\"><color=#40a0ff>{leaderName}</color></link>";
            }
        }

        [XmlIgnore]
        [CreateProperty]
        public DeployState deployStateProp
        {
            get => deployState;
            set => SetDeployState(value);
        }

        // IStrategicGroupMemberReferenceable Shared
        # region
        [CreateProperty]
        public string parentName => ((IStrategicGroupMemberReferenceable)this).GetParentName();

        [CreateProperty]
        public string homeBaseName => GetHomeBaseGroup()?.name?.mergedName ?? "[Not Defined]";

        [CreateProperty]
        public string currentSourceDepotName => ((IStrategicGroupMemberReferenceable)this).GetCurrentSourceDepotName();

        [CreateProperty]
        public string detachedFromGroupName => ((IStrategicGroupMemberReferenceable)this).GetDetachedFromGroupName();

        [CreateProperty]
        public bool canReattachToDetachedGroup => ((IStrategicGroupMemberReferenceable)this).GetDetachedFromGroup() != null;

        [CreateProperty]
        public bool enableAutoReattachProp
        {
            get => enableAutoReattach;
            set => enableAutoReattach = value;
        }
        #endregion

        [CreateProperty]
        public string assignedMissionName => EntityManager.Instance.Get<StrategicMission>(assignedMissionObjectId)?.name?.mergedName ?? "[Undefined or Invalid]";

        [CreateProperty]
        public string subordinateSummary => $"{combinedSubUnitSize} sub units, {GetStrengthMen()} men, {GetShipTons()} tons ships, {GetSupplyCostTonsPerDay()} tons supply cost/day, {supplyStatsProp}";

        [CreateProperty]
        public string strategicSpeedSummary
        {
            get
            {
                var speedKmPerHour = GetSpeedKmPerHour();
                var speedNmPerHour = speedKmPerHour * MeasureUtils.kilometerToNavalMile;
                return $"Strategic speed: {speedKmPerHour:0.0} km/h ({speedNmPerHour:0.0} nm/h)";
            }
        }

        [CreateProperty]
        public bool hasStrategicSpeedSummary => GetSpeedKmPerHour() > 0;

        [CreateProperty]
        public string containerName => EntityManager.Instance.Get<ShipLog>(containerObjectId)?.namedShip?.name?.mergedName ?? "[Undefined or Invalid]";

        [CreateProperty]
        public bool isIndependent => deployState == DeployState.Independent;

        // [CreateProperty]
        // public bool isActivePosture => posture == GroupPostureType.Active;

        [CreateProperty]
        public bool isInRestorableState => posture == GroupPostureType.Disengaged || posture == GroupPostureType.Reorganized;

        [CreateProperty]
        public string commandDesc => GetCommandDesc().Resolve();

        [CreateProperty]
        public float timelinessOpacity => 1f;

        // [CreateProperty]
        // public double supplyTonsProp => GetSupplyTons();

        // [CreateProperty]
        // public double supplyCapTonsProp => GetSupplyCapTons();

        [CreateProperty]
        public string supplyStatsProp
        {
            get
            {
                var supplyTons = GetSupplyTons();
                var supplyCapTons = GetSupplyCapTons();
                var supplyPercent = supplyTons / supplyCapTons;
                var percentStr = supplyPercent.ToString("P");
                return $"{supplyTons:0.0}/{supplyCapTons:0.0} tons ({percentStr})";
            }
        }

        [CreateProperty]
        public bool hasAssignedMission => GetAssignedMission() != null;

        [CreateProperty]
        public string assignedMissionNameLink => GetAssignedMission()?.nameLink;

        public partial class ArriveState
        {
            ScenarioStateDateTimeViewModel _arriveTimeViewModel;

            [CreateProperty]
            public ScenarioStateDateTimeViewModel arriveTimeViewModel
            {
                get => _arriveTimeViewModel ??= ScenarioStateDateTimeViewModel.GetDateTimeHolder
                (
                    () => arriveTime,
                    dt =>arriveTime = dt
                );
            }
        }

        [CreateProperty]
        public bool enableArriveState
        {
            get => arriveState != null;
            set
            {
                if(value != enableArriveState)
                {
                    if(value)
                    {
                        arriveState = new ArriveState();
                    }
                    else
                    {
                        arriveState = null;
                    }
                }
            }
        }
    }

    public partial class StrategicGroupReference
    {
        [CreateProperty]
        public string name => Get()?.name?.mergedName ?? "[Not Set or Invalid]";
    }

    public partial class StrategicGroupMemberReference
    {
        [CreateProperty]
        public string name
        {
            get
            {
                var obj = Get();
                if (obj == null)
                    return "[Undefined or Invalid]";
                // TODO: Move casting to interface method
                if (obj is ShipLog shipLog)
                {
                    return shipLog?.namedShip?.name?.mergedName ?? "[Undefined or Invalid ShipLog]";
                }
                if (obj is StrategicGroup group)
                {
                    return group?.name?.mergedName ?? "[Undefined or Invalid StrategicGroup]";
                }
                if (obj is LandUnit landUnit)
                {
                    return landUnit?.name?.mergedName ?? "[Undefined or Invalid LandUnit]";
                }
                return "[Undefined or Invalid Unknown Type]";
            }
        }

        [CreateProperty]
        public StyleBackground icon
        {
            get
            {
                var obj = Get();
                if (obj == null)
                    return null;
                if (obj is ShipLog shipLog)
                {
                    return shipLog?.shipClass?.portraitStyleBackground ?? null;
                }
                // if (obj is StrategicGroup group)
                // {
                //     return group.typeIcon;
                // }
                return null;
            }
        }

        [CreateProperty]
        public bool isShip
        {
            get
            {
                var obj = Get();
                return obj is ShipLog;
            }
        }

        [CreateProperty]
        public StyleBackground icon2
        {
            get
            {
                var obj = Get();
                if (obj == null)
                    return null;
                if (obj is StrategicGroup group)
                {
                    return UnityWebRequestImageReader.Instance.FetchStyleBackground(group.typeIconPath);
                }
                else if (obj is LandUnit landUnit)
                {
                    return landUnit.GetLandUnitTemplate()?.typeIcon ?? null;
                }
                return null;
            }
        }

        [CreateProperty]
        public string sizeStr
        {
            get
            {
                var obj = Get();
                if (obj is StrategicGroup group)
                {
                    return group.sizeStr;
                }
                else if (obj is LandUnit landUnit)
                {
                    var sizeStr = StrategicGroup.sizeStrMap.GetValueOrDefault(landUnit.GetLandUnitTemplate()?.size ?? StrategicUnitSize.Squad); // Move Dictionary to more common location
                    return sizeStr;
                }
                return "";
            }
        }

        // Visitor
        [CreateProperty]
        public string desc1
        {
            get
            {
                var obj = Get();
                if (obj == null)
                    return "";
                // TODO: Move casting to interface method
                if (obj is ShipLog shipLog)
                {
                    var tons = shipLog?.shipClass?.displacementTons;
                    var type = shipLog?.shipClass?.type;
                    var crews = shipLog?.shipClass?.complementMen;
                    var className = shipLog?.shipClass?.name.mergedName;

                    return $"{type}, {tons} tons, {crews} men, (class: {className})";
                }
                if (obj is StrategicGroup group)
                {
                    var shipTons = group.GetShipTons();
                    var shipTonsStr = shipTons == 0 ? "" : $"{shipTons} tons ships";
                    return $"{group.type}, {group.combinedSubUnitSize} sub units, {group.GetStrengthMen()} men, {shipTonsStr}";
                }
                if (obj is LandUnit landUnit)
                {
                    var unitType = landUnit.GetLandUnitTemplate()?.unitType;
                    if (unitType == LandUnitType.Supply)
                    {
                        return $"Supply: {landUnit.supplyTons} tons";
                    }
                    else if(unitType == LandUnitType.Port)
                    {
                        return $"Port: {landUnit.portLevel}, Repair Shipyard: {landUnit.repairShipyardLevel}";
                    }
                    return $"{landUnit.strength} men";
                }
                return "";
            }
        }

        [CreateProperty]
        public string desc2
        {
            get
            {
                var obj = Get();
                if (obj == null)
                    return "";

                if (obj is ShipLog shipLog)
                {
                    var maxSpeed = shipLog.GetMaxSpeedKnots();
                    // var mapStateStr = shipLog.mapState == MapState.Deployed ? $"{shipLog.mapState}" : $"<b>{shipLog.mapState}</b>";
                    // var operationalStateStr = shipLog.operationalState == ShipOperationalState.Operational ? $"{shipLog.operationalState}" : $"<b>{shipLog.operationalState}</b>";
                    var mapStateStr = shipLog.mapState == MapState.Deployed ? $"{shipLog.mapState}" : $"<b><color=\"red\">{shipLog.mapState}</color></b>";
                    var operationalStateStr = shipLog.operationalState == ShipOperationalState.Operational ? $"{shipLog.operationalState}" : $"<b><color=\"red\">{shipLog.operationalState}</color></b>";
                    var extraParts = new List<string>();
                    AddDetachedDisplayPart(extraParts, shipLog);
                    if (StrategicGroupSubGroupUtility.NeedsDetachForRepair(shipLog))
                    {
                        extraParts.Add("<b><color=\"red\">Detached for Repair</color></b>");
                    }
                    var extraSuffix = extraParts.Count > 0 ? $", {string.Join(", ", extraParts)}" : "";
                    return $"{mapStateStr}, {operationalStateStr}, {maxSpeed} kts, DP: {shipLog.damagePoint} / {shipLog.shipClass.damagePoint}{extraSuffix}";
                }
                else if (obj is StrategicGroup group)
                {
                    var deployStateStr = group.deployState == StrategicGroup.DeployState.Combined ? $"{group.deployState}" : $"<b>{group.deployState}</b>";
                    var autoCombinedableStr = group.autoCombinable ? " <b>Auto-Combinedable</b>" : "";
                    var dissolvableStr = group.dissolvable ? " <b><color=\"red\">Dissolvable</color></b>" : "";
                    var extraParts = new List<string>();
                    AddDetachedDisplayPart(extraParts, group);
                    var extraSuffix = extraParts.Count > 0 ? $", {string.Join(", ", extraParts)}" : "";
                    return $"{deployStateStr}{autoCombinedableStr}{dissolvableStr}{extraSuffix}";
                }
                else if (obj is LandUnit landUnit)
                {
                    var extraParts = new List<string>();
                    AddDetachedDisplayPart(extraParts, landUnit);
                    return string.Join(", ", extraParts);
                }

                return "";
            }
        }

        static void AddDetachedDisplayPart(List<string> parts, IStrategicGroupMemberReferenceable member)
        {
            var detachedFromGroup = member?.GetDetachedFromGroup();
            var detachedShortName = detachedFromGroup?.name?.GetShortName();
            if (!string.IsNullOrWhiteSpace(detachedShortName))
            {
                parts.Add($"Detached from {detachedShortName}");
            }
        }

    }

    public partial class WeaponRecord
    {
        [CreateProperty]
        public string name => Get()?.name?.mergedName ?? "[Undefined or Invalid]";

        [CreateProperty]
        public bool isInEditMode => GamePreference.Instance.isInEditMode;
    }

    public partial class LandUnitTemplate
    {
        [CreateProperty]
        public int weaponStrength => GetWeaponStrength();

        [CreateProperty]
        public int weaponGuns => GetWeaponGuns();

        [CreateProperty]
        public StyleBackground typeIcon => UnityWebRequestImageReader.Instance.FetchStyleBackground($"{Application.streamingAssetsPath}/Pictures/LandUnitType/{unitType}.png");

        [CreateProperty]
        public float lethality => GetLethality();

        [CreateProperty]
        public bool isInEditMode => GamePreference.Instance.isInEditMode;
    }

    public partial class LandUnit
    {
        [CreateProperty]
        public string landUnitTemplateName => GetLandUnitTemplate()?.name?.mergedName ?? "[Undefined or Invalid]";

        // IStrategicGroupMemberReferenceable Shared
        # region
        [CreateProperty]
        public string parentName => ((IStrategicGroupMemberReferenceable)this).GetParentName();

        [CreateProperty]
        public string currentSourceDepotName => ((IStrategicGroupMemberReferenceable)this).GetCurrentSourceDepotName();

        [CreateProperty]
        public string detachedFromGroupName => ((IStrategicGroupMemberReferenceable)this).GetDetachedFromGroupName();

        [CreateProperty]
        public bool canReattachToDetachedGroup => ((IStrategicGroupMemberReferenceable)this).GetDetachedFromGroup() != null;

        [CreateProperty]
        public bool enableAutoReattachProp
        {
            get => enableAutoReattach;
            set => enableAutoReattach = value;
        }
        #endregion

        [CreateProperty]
        public float supplyCostTonsPerMenDay => GetSupplyCostTonsPerMenDay();

        [CreateProperty]
        public float supplyCostTonsPerDay => GetSupplyCostTonsPerDay();

        [CreateProperty]
        public float supplyCapTons => GetSupplyCapTons();

        [CreateProperty]
        public double transferWeightTons => GetTransferWeightTons();

        [CreateProperty]
        public bool isPort => GetLandUnitTemplate()?.unitType == LandUnitType.Port;

        [CreateProperty]
        public bool isInEditMode => GamePreference.Instance.isInEditMode;

        [CreateProperty]
        public string nameLink => $"<link=\"nameLink\"><color=#40a0ff>{GetName().GetMergedName()}</color></link>";

        [CreateProperty]
        public string oobNameLink
        {
            get
            {
                var rawName = GetName()?.GetShortName();
                if (rawName == null)
                    return "[Not Specified]";
                return $"<link=\"nameLink\"><color=#40a0ff>{rawName}</color></link>";
            }
        }

        [CreateProperty]
        public Leader oobLeader => null;

        [CreateProperty]
        public string oobLeaderNameLink => string.Empty;
    }

    public partial class SubStrategicCombat
    {
        public partial class CombatSideState
        {
            [CreateProperty]
            public float moraleDynamic => GetMoraleDynamic();

            [CreateProperty]
            public float firepower => GetFirepower();
        }

        [CreateProperty]
        public float distanceMeterProp
        {
            get => distanceMeter;
            set => distanceMeter = value;
        }
    }

    public partial class Weapon
    {
        [CreateProperty]
        public bool isInEditMode => GamePreference.Instance.isInEditMode;
    }

    public partial class DiplomacyRelation
    {
        [CreateProperty]
        public string sideName => GetSideState()?.name?.mergedName ?? "[Undefined or Invalid]";
    }


    public partial class LocalNavalCombatBuilder : ITree<IShipGroupMember, string>
    {

        public class LocalNavalCombatBuilderOneSide
        {
            // required parameters
            public LocalNavalCombatBuilder builder;
            public int innerIndex;

            public string topGroupObjectId => builder.rootShipGroups[innerIndex].objectId;
            public PendingNavalCombat.PendingNavalCombatSideState pendingNavalCombatSideState => innerIndex == 0 ? builder.pendingNavalCombat.sideState0 : builder.pendingNavalCombat.sideState1;

            public IEnumerable<T> Walk<T>(ShipGroup shipGroup) where T : IObjectIdLabeled
            {
                foreach (var childrenObjectId in shipGroup.childrenObjectIds)
                {
                    var child = builder.localEntityManager.Get<IObjectIdLabeled>(childrenObjectId);
                    if (child is T t)
                    {
                        yield return t;
                    }
                    if (child is ShipGroup subShipGroup)
                    {
                        foreach (var ret in Walk<T>(subShipGroup))
                        {
                            yield return ret;
                        }
                    }
                }
            }

            public IEnumerable<T> WalkRootGroup<T>() where T : IObjectIdLabeled => Walk<T>(GetRootGroup());

            public ShipGroup GetRootGroup() => builder.localEntityManager.Get<ShipGroup>(topGroupObjectId);

            public Country GetCountry()
            {
                foreach (var shipLog in WalkRootGroup<ShipLog>())
                {
                    var country = shipLog?.shipClass?.country ?? Country.General;
                    if (country != Country.General)
                        return country;
                }
                return Country.General;
            }
            
            public Leader GetLeader()
            {
                foreach (var obj in WalkRootGroup<IObjectIdLabeled>())
                {
                    if (obj is ShipGroup shipGroup && shipGroup.leader != null)
                    {
                        return shipGroup.leader;
                    }
                    else if (obj is ShipLog shipLog && shipLog.leader != null)
                    {
                        return shipLog.leader;
                    }
                }
                return null;
            }
        }


        public LocalNavalCombatBuilderOneSide GetSide0() => new() { builder = this, innerIndex=0}; // generally "left"
        public LocalNavalCombatBuilderOneSide GetSide1() => new() { builder = this, innerIndex=1}; // generally "right"

        public IShipGroupMember GetParent(IShipGroupMember node) => node.GetParentGroup();

        public IEnumerable<IShipGroupMember> GetChildren(IShipGroupMember node)
        {
            if (node is ShipGroup shipGroup)
            {
                foreach (var childId in shipGroup.childrenObjectIds)
                {
                    yield return localEntityManager.Get<IShipGroupMember>(childId);
                }
            }
        }

        public string GetData(IShipGroupMember node) => node.GetMemberName();
    }

    public partial class StrategicMission
    {
        // public bool activeOnlyForAI;

        [CreateProperty]
        public string sideName => EntityManager.Instance.Get<SideState>(sideObjectId)?.name?.GetMergedName() ?? "[Unspecified or Invalid]";

        [CreateProperty]
        public PatrolMission asPatrol => this as PatrolMission;

        [CreateProperty]
        public bool isPatrol => asPatrol != null;

        [CreateProperty]
        public SupplyMission asSupply => this as SupplyMission;

        [CreateProperty]
        public bool isSupply => asSupply != null;

        [CreateProperty]
        public NavalTransferMission asNavalTransfer => this as NavalTransferMission;

        [CreateProperty]
        public bool isNavalTransfer => asNavalTransfer != null;

        // [CreateProperty]
        // public OneShotRaidingMission asOneShotRaiding => this as OneShotRaidingMission;

        // [CreateProperty]
        // public bool isOneShotRaiding => asOneShotRaiding != null;

        [CreateProperty]
        public OneShotSortieMission asOneShotSortie => this as OneShotSortieMission;

        [CreateProperty]
        public bool isOneShotSortie => asOneShotSortie != null;

        [CreateProperty]
        public RectAreaPatrolMission asRectAreaPatrol => this as RectAreaPatrolMission;

        [CreateProperty]
        public bool isRectAreaPatrol => asRectAreaPatrol != null;


        [CreateProperty]
        public string missionTypeName => GetType().Name;

        // [CreateProperty]
        // public string sourceDepotName => EntityManager.Instance.Get<LandUnit>(sourceDepotObjectId)?.name?.mergedName ?? "[Not defined or Invalid]";

        // [CreateProperty]
        // public string targetDepotName => EntityManager.Instance.Get<LandUnit>(targetDepotObjectId)?.name?.mergedName ?? "[Not defined or Invalid]";

        [CreateProperty]
        public string nameLink
        {
            get
            {
                var rawName = name.GetMergedName();
                return $"<link=\"nameLink\"><color=#40a0ff>{rawName}</color></link>";
            }
        }
    }

    public partial class LandUnitReference
    {
        [CreateProperty]
        public string name => Get()?.name?.mergedName ?? "[Not defined or Invalid]";
    }

    public partial class SupplyFlowRecord
    {
        [CreateProperty]
        public string otherObjectName =>  GetOther()?.GetName()?.mergedName ?? "[Not defined or Invalid]";
    }

    public partial class PendingNavalCombat
    {
        protected static string Localize(string key, params object[] args) => ServiceLocator.Get<ILocalizeService>().Get(key, args);

        [CreateProperty]
        public string name => Localize(
            "Combat in Hex ({0})",
            xy.GetCell()?.GetLocationSummary()
        );
        // public string name => $"Combat in Hex ({xy.x} {xy.y})";
    }

    public partial class LandBattleSideStateDynamic
    {
        public partial class LandUnitBundle
        {
            [CreateProperty]
            public StyleBackground icon => landUnit?.GetLandUnitTemplate()?.typeIcon ?? null;

            [CreateProperty]
            public string desc => $"{landUnit.name.GetShortName()}";
            // TODO: Disable hyperlink since it seem to related to strange UITK bugs
            // [CreateProperty]
            // public string desc => $"<link=\"nameLink\"><color=#40a0ff><u>{landUnit.name.GetShortName()}</u></color></link>";

            // TODO: Replace S, L, K, ... with icon
            [CreateProperty]
            public string desc2 => $"S: {landUnit.strength}, L: {battleUnitState.accumulatedStrengthLoss} (+{battleUnitState.currentStrengthLoss}) K:{battleUnitState.accumulatedStrengthKill} (+{battleUnitState.currentStrengthKill}) CM:{chanceCostModifier:+0.00%;-0.00%;0.00%}, TM: {tacticalModifier:+0.00%;-0.00%;0.00%}";

            [CreateProperty]
            public Length strengthPercent => new Length(
                ((float)landUnit.strength) / Math.Max(1, landUnit.strength + battleUnitState.accumulatedStrengthLoss) * 100,
                LengthUnit.Percent
            );

            [CreateProperty]
            public Length suppressionSuppPercent => new Length((1 - landUnit.suppression) * 100, LengthUnit.Percent);

            [CreateProperty]
            public Length moralePercent => new Length(landUnit.morale * 100, LengthUnit.Percent);

            [CreateProperty]
            public Length fatigueSuppPercent => new Length((1 - landUnit.fatigue) * 100, LengthUnit.Percent);
        }

        [CreateProperty]
        public StyleBackground leaderPortrait => battleLeader.portraitReference?.pictureStyleBackground ?? null;

        [CreateProperty]
        public string leaderName => battleLeader?.name?.GetShortName() ?? "";

        static string Localize(string key, params object[] args) => ServiceLocator.Get<ILocalizeService>().Get(key, args);

        [CreateProperty]
        public string summary
        {
            get
            {
                var strengh = topGroupBundles.Sum(b => b.group.GetStrengthMen());
                var currentLoss = landUnitBundles.Sum(b => b.battleUnitState.currentStrengthLoss);
                var accLos = landUnitBundles.Sum(b => b.battleUnitState.accumulatedStrengthLoss);

                var costModifierStr = strengh == 0 ? "" : $"{leadingGroupBundle.accumulatedChanceCostModifier:+0.00%;-0.00%;0.00%}";
                var tacticalModifierStr = strengh == 0 ? "" : $"{leadingGroupBundle.averageTacticalModifier:+0.00%;-0.00%;0.00%}";

                // return $"Land Units: {landUnitBundles.Count}, Strength: {strengh}, Loss: {accLos} (+{currentLoss}), avg CM: {costModifierStr}, avg TM: {tacticalModifierStr}";
                return Localize(
                    "Land Units: {0}, Strength: {1}, Loss: {2} (+{3}), avg CM: {4}, avg TM: {5}",
                    landUnitBundles.Count, strengh, accLos, currentLoss, costModifierStr, tacticalModifierStr
                );
            }
        }

        [CreateProperty]
        public StyleBackground countryFlag => UnityWebRequestImageReader.Instance.FetchTexture2D(Utils.GetCountryPath(country));

    }

    public partial class LandBattle
    {
        static string Localize(string key, params object[] args) => ServiceLocator.Get<ILocalizeService>().Get(key, args);

        [CreateProperty]
        public string labelName
        {
            get
            {
                
                var beginDateTimeStr = CoreParameter.Instance.GetReferenceTimeZoneDateTimeOffsetString(beginDateTime);
                // var endDateTimeStr = end ? CoreParameter.Instance.GetReferenceTimeZoneDateTimeOffsetString(endDateTime) : "now";
                var endDateTimeStr = end ? CoreParameter.Instance.GetReferenceTimeZoneDateTimeOffsetString(endDateTime) : Localize("now");
                var cellName = StrategicGameState.Instance.GetCellName(cellXY);
                return $"{cellName} {beginDateTimeStr} ~ {endDateTimeStr}";
            }
        }

        [CreateProperty]
        public string title
        {
            get
            {
                // return $"The battle of {landBattle.cellXY}";
                // return $"The battle of {StrategicGameState.Instance.GetCellName(cellXY)}";
                return Localize(
                    "The battle of {0}",
                    StrategicGameState.Instance.GetCellName(cellXY)
                );
            }
        }

        [CreateProperty]
        public string summary => Localize(attackerVictory ? "Attacker Victory" : "Defender Victory");

        [CreateProperty]
        public string dateTimeRange
        {
            get
            {
                var beginDateTimeStr = CoreParameter.Instance.GetReferenceTimeZoneDateTimeOffsetString(beginDateTime);
                var endDateTimeStr = end ? CoreParameter.Instance.GetReferenceTimeZoneDateTimeOffsetString(endDateTime) : "now";
                return $"{beginDateTimeStr} - {endDateTimeStr}";
            }
        }
    }

    public partial class LandBattleSideState
    {
        [CreateProperty]
        public StyleBackground leaderPortrait => GetLeader()?.portraitReference?.pictureStyleBackground ?? null;

        [CreateProperty]
        public string leaderName => GetLeader()?.name?.GetShortName() ?? "";

        [CreateProperty]
        public StyleBackground countryFlag => UnityWebRequestImageReader.Instance.FetchTexture2D(Utils.GetCountryPath(currentCountry));

        [CreateProperty]
        public string summary => GetSummary().Resolve();
    }

    public partial class LandBattleUnitState
    {
        static string Localize(string key, params object[] args) => ServiceLocator.Get<ILocalizeService>().Get(key, args);

        [CreateProperty]
        public StyleBackground icon => GetLandUnit()?.GetLandUnitTemplate()?.typeIcon ?? null;

        [CreateProperty]
        public string name => $"{GetLandUnit()?.name?.GetShortName()}";

        // [CreateProperty]
        // public string desc => $"Strength: {endStrength}, Lost: {accumulatedStrengthLoss}, Kill:{accumulatedStrengthKill})";
        [CreateProperty]
        public string desc => Localize(
            "Strength: {0}, Lost: {1}, Kill:{2}",
            endStrength, accumulatedStrengthLoss, accumulatedStrengthKill
        );
    }

    public partial class StrategicScenarioState : IDateTimeHolder
    {
        public DateTime GetDateTime() => dateTime;
        public void SetDateTime(DateTime dt) => dateTime = dt;

        ScenarioStateDateTimeViewModel _dateTimeViewModel; // Note it's possible to initialize the view model attribute from empty constructor but this may break core's capabbility to leverage empty constructor

        [CreateProperty]
        public ScenarioStateDateTimeViewModel dateTimeViewModel
        {
            get
            {
                if (_dateTimeViewModel == null)
                {
                    _dateTimeViewModel = new ScenarioStateDateTimeViewModel() { dateTimeHolder = this };
                }
                return _dateTimeViewModel;
            }
        }

        [CreateProperty]
        public string areaSystemSummary => $"({areaSystem.areaStates.Count}) {areaSystem.backgroundReference.isBuiltin}, {areaSystem.backgroundReference.path}";

        [CreateProperty]
        public int falloffAlgorithmValue
        {
            get => (int)powerInfluenceFalloffAlgorithm;
            set => powerInfluenceFalloffAlgorithm = (InfluenceMapFalloffAlgorithm)value;
        }

        [CreateProperty]
        public float linearRangeCost
        {
            get => powerInfluenceLinearRangeCost;
            set => powerInfluenceLinearRangeCost = value;
        }

        [CreateProperty]
        public float exponentialDecayLengthCost
        {
            get => powerInfluenceExponentialDecayLengthCost;
            set => powerInfluenceExponentialDecayLengthCost = value;
        }

        [CreateProperty]
        public float inverseHalfEffectDistanceCost
        {
            get => powerInfluenceInverseHalfEffectDistanceCost;
            set => powerInfluenceInverseHalfEffectDistanceCost = value;
        }

        [CreateProperty]
        public float gaussianSigmaCost
        {
            get => powerInfluenceGaussianSigmaCost;
            set => powerInfluenceGaussianSigmaCost = value;
        }

        [XmlIgnore]
        [CreateProperty]
        public bool enableGridSystemProp
        {
            get => enableGridSystem;
            set
            {
                enableGridSystem = value;
                StrategicGameManager.Instance.RefreshGridSystemAreaSystemVisibility();
            }
        }

        [XmlIgnore]
        [CreateProperty]
        public bool enableAreaSystemProp
        {
            get => enableAreaSystem;
            set
            {
                enableAreaSystem = value;
                StrategicGameManager.Instance.RefreshGridSystemAreaSystemVisibility();
            }
        }
    }

    public partial class XY
    {
        [CreateProperty]
        public string areaCellName => GetAreaCellName();

        [CreateProperty]
        public string cellName => StrategicGameState.Instance.GetCellName(this);
    }

    public partial class Theater
    {
        [CreateProperty]
        public int postureValue
        {
            get => (int)posture;
            set => posture = (TheaterPosture)value;
        }

        [CreateProperty]
        public string sideName => GetSide()?.name?.GetMergedName() ?? string.Empty;

        [CreateProperty]
        public int cellCount => cells?.Count ?? 0;

        [CreateProperty]
        public string cellCountText => cellCount.ToString();

        [CreateProperty]
        public string cellSummaryText
        {
            get
            {
                return StrategicGameState.Instance.BuildCellSummaryText(cells);
            }
        }

        [CreateProperty]
        public int frontlineCellCount => frontlineCellInfos?.Count ?? 0;

        [CreateProperty]
        public string frontlineCellSummaryText => StrategicGameState.Instance.BuildCellSummaryText(
            (frontlineCellInfos ?? Enumerable.Empty<FrontlineCellInfo>())
                .Where(info => info != null)
                .Select(info => info.xy)
        );
    }

    public partial class CellConnection
    {
        [XmlIgnore]
        [CreateProperty]
        public float costProp
        {
            get => cost;
            set
            {
                cost = value;
                var otherConn = GetOtherConnectionToSelf();
                if(otherConn != null)
                {
                    otherConn.cost = value;
                }
            }
        }

        [XmlIgnore]
        [CreateProperty]
        public float costCoefProp
        {
            get => costCoef;
            set
            {
                costCoef = value;
                var otherConn = GetOtherConnectionToSelf();
                if(otherConn != null)
                {
                    otherConn.costCoef = value;
                }
            }
        }
    }

    public partial class CellSideInfo
    {
        [CreateProperty]
        public string sideName => EntityManager.Instance.Get<SideState>(sideObjectId)?.name?.GetMergedName() ?? "[Not Specified or Invalid]";
    }

    public partial class StrategicMissionReference
    {
        [CreateProperty]
        public string missionDesc
        {
            get
            {
                var mission = EntityManager.Instance.Get<StrategicMission>(objectId);
                if(mission == null)
                    return "[Not Specified or Invalid]";
                return $"{mission?.name?.GetMergedName()}"; // TODO: Add some state indicator
            }
        }
    }

    public partial class SidedLazyLocalizedString
    {
        [CreateProperty]
        public string resolvedString => GamePreference.Instance.isInEditMode ? GetSidedLog().Resolve() : log.Resolve();
    }
}

namespace NavalCombatCore
{
    public partial class ShipLog
    {
        [CreateProperty]
        public string parentName => ((IStrategicGroupMemberReferenceable)this).GetParentName();

        [CreateProperty]
        public string currentSourceDepotName => ((IStrategicGroupMemberReferenceable)this).GetCurrentSourceDepotName();

        [CreateProperty]
        public string detachedFromGroupName => ((IStrategicGroupMemberReferenceable)this).GetDetachedFromGroupName();

        [CreateProperty]
        public bool canReattachToDetachedGroup => ((IStrategicGroupMemberReferenceable)this).GetDetachedFromGroup() != null;

        [CreateProperty]
        public bool enableAutoReattachProp
        {
            get => enableAutoReattach;
            set => enableAutoReattach = value;
        }

        [CreateProperty]
        public string needsDetachForRepairLabel
        {
            get => StrategicGroupSubGroupUtility.NeedsDetachForRepair(this) ? "Detached for Repair" : string.Empty;
        }

        [CreateProperty]
        public float supplyCostTonsPerDay => GetSupplyCostTonsPerDay();

        [CreateProperty]
        public float supplyCapTons => GetSupplyCapTons();

        [CreateProperty]
        public string oobNameLink
        {
            get
            {
                var rawName = namedShip?.name?.GetShortName();
                if (rawName == null)
                    return "[Not Specified]";
                return $"<link=\"nameLink\"><color=#40a0ff>{rawName}</color></link>";
            }
        }

        [CreateProperty]
        public Leader oobLeader => leader;

        [CreateProperty]
        public string oobLeaderNameLink
        {
            get
            {
                var leaderName = leader?.name?.GetShortName();
                if (leaderName == null)
                    return string.Empty;
                return $"<link=\"leaderLink\">{leaderName}</link>";
            }
        }
    }
}

// Move to a dedicated file?
namespace CoreUtils
{
    public partial class LazyLocalizedString
    {
        [CreateProperty]
        public string resolvedString => Resolve();
    }

}
