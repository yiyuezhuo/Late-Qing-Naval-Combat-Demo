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
        public string brief => $"({x}, {y}), {terrain}";

        // [CreateProperty]
        // public int cellInfoGroupCount => StrategicGameState.Instance.hexInfoMap.GetValueOrDefault((x, y))?.strategicGroupReferences?.Count ?? 0;

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
            {Country.Russia, Color.green},
            {Country.UnitedState, Color.blue},
            {Country.Spain, Color.darkOrange},
            {Country.Germany, Color.black},
            {Country.Italy, Color.greenYellow},
            {Country.AustriaHugary, Color.silver},
        };
    }

    public partial class StrategicGroup
    {
        [CreateProperty]
        public string sizeStr => GetSizeStr();

        [CreateProperty]
        public int combinedSubUnitSize => GetCombinedSubUnitSize();

        [CreateProperty]
        public int combinedPowerPointRounded => Mathf.RoundToInt(GetCombinedPowerPoint(true));

        [CreateProperty]
        public Color countryColor => StyleConstants.countryColorMap.GetValueOrDefault(country, Color.gray);

        [CreateProperty]
        public StyleBackground typeIcon => UnityWebRequestImageReader.Instance.FetchStyleBackground($"{Application.streamingAssetsPath}/Pictures/GroupTypeIcons/{type}.png");

        [CreateProperty]
        public int xProp
        {
            get => x;
            set => x = value;
        }

        [CreateProperty]
        public int yProp
        {
            get => y;
            set => y = value;
        }

        [CreateProperty]
        public bool isXYEditable => deployState == DeployState.Independent;

        [CreateProperty]
        public StrategicGroupReference strategicGroupReferenceProp => strategicGroupReference;

        [CreateProperty]
        public string nameLink
        {
            get
            {
                var rawName = name.GetMergedName();
                if (rawName == null)
                    rawName = "_";
                return $"<link=\"nameLink\"><color=#40a0ff><u>{rawName}</u></color></link>";
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
        public string currentSourceDepotName => ((IStrategicGroupMemberReferenceable)this).GetCurrentSourceDepotName();
        #endregion

        [CreateProperty]
        public string assignedMissionName => EntityManager.Instance.Get<StrategicMission>(assignedMissionObjectId)?.name?.mergedName ?? "[Undefined or Invalid]";

        [CreateProperty]
        public string subordinateSummary => $"{combinedSubUnitSize} sub units, {GetStrengthMen()} men, {GetShipTons()} tons ships, {GetSupplyCostTonsPerDay()} tons supply cost/day";

        [CreateProperty]
        public string containerName => EntityManager.Instance.Get<ShipLog>(containerObjectId)?.namedShip?.name?.mergedName ?? "[Undefined or Invalid]";

        [CreateProperty]
        public bool isIndependent => deployState == DeployState.Independent;

        // [CreateProperty]
        // public bool isActivePosture => posture == GroupPostureType.Active;

        [CreateProperty]
        public bool isInRestorableState => posture == GroupPostureType.Disengaged || posture == GroupPostureType.Reorganized;

        // [CreateProperty]
        // public float commandCapacity => GetCommandCapacity();

        // [CreateProperty]
        // public float combinedCommandUsage => GetCombinedCommandUsage();

        // [CreateProperty]
        // public string commandDesc => $"Command: {GetCombinedCommandUsage()}/{GetCommandCapacity()}, Chance Cost: {GetChanceCostModifier():+0.00%;-0.00%;0.00%}, Tactical Modifier: {GetTacticalModifier():+0.00%;-0.00%;0.00%}";
    
        [CreateProperty]
        public string commandDesc
        {
            get
            {
                var (usageDirect, usage, accCostMod, currentLayerCostMod) = GetAverageAccumulatedChanceCostModifier();
                // var costMod = GetChanceCostModifier();
                var commandCap = GetCommandCapacity();
                var tacMod = GetTacticalModifier();
                return $"Command: {usage}/{commandCap}, Chance Cost: {currentLayerCostMod:+0.00%;-0.00%;0.00%} (Acc Avg: {accCostMod:+0.00%;-0.00%;0.00%}), Tactical Modifier: {tacMod:+0.00%;-0.00%;0.00%}";
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
                    return group.typeIcon;
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
                    return $"{mapStateStr}, {operationalStateStr}, {maxSpeed} kts, DP: {shipLog.damagePoint} / {shipLog.shipClass.damagePoint}";
                }
                else if (obj is StrategicGroup group)
                {
                    var deployStateStr = group.deployState == StrategicGroup.DeployState.Combined ? $"{group.deployState}" : $"<b>{group.deployState}</b>";
                    var autoCombinedableStr = group.autoCombinable ? " <b>Auto-Combinedable</b>" : "";
                    var dissolvableStr = group.dissolvable ? " <b><color=\"red\">Dissolvable</color></b>" : "";
                    return $"{deployStateStr}{autoCombinedableStr}{dissolvableStr}";
                }

                return "";
            }
        }

    }

    public partial class WeaponRecord
    {
        [CreateProperty]
        public string name => Get()?.name?.mergedName ?? "[Undefined or Invalid]";

    }

    public partial class LandUnitTemplate
    {
        [CreateProperty]
        public int weaponStrength => GetWeaponStrength();

        [CreateProperty]
        public int weaponGuns => GetWeaponGuns();

        [CreateProperty]
        public StyleBackground typeIcon => UnityWebRequestImageReader.Instance.FetchStyleBackground($"{Application.streamingAssetsPath}/Pictures/LandUnitType/{unitType}.png");
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
        [CreateProperty]
        public bool isPatrol => type == MissionType.Patrol;

        [CreateProperty]
        public bool isSupply => type == MissionType.Supply;

        [CreateProperty]
        public bool isNavalTransfer => type == MissionType.NavalTransfer;

        // [CreateProperty]
        // public string sourceDepotName => EntityManager.Instance.Get<LandUnit>(sourceDepotObjectId)?.name?.mergedName ?? "[Not defined or Invalid]";

        // [CreateProperty]
        // public string targetDepotName => EntityManager.Instance.Get<LandUnit>(targetDepotObjectId)?.name?.mergedName ?? "[Not defined or Invalid]";
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
            "Combat in Hex ({0} {1})",
            xy.x,
            xy.y
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
            public string desc => $"{landUnit.name.GetShortName()}, S: {landUnit.strength}, L: {battleUnitState.accumulatedStrengthLoss} (+{battleUnitState.currentStrengthLoss}) CM:{chanceCostModifier:+0.00%;-0.00%;0.00%}, TM: {tacticalModifier:+0.00%;-0.00%;0.00%}";
        }

        [CreateProperty]
        public StyleBackground leaderPortrait => battleLeader.portraitReference?.pictureStyleBackground ?? null;

        [CreateProperty]
        public string summary
        {
            get
            {
                var strengh = topGroupBundles.Sum(b => b.group.GetStrengthMen());
                var currentLoss = landUnitBundles.Sum(b => b.battleUnitState.currentStrengthLoss);
                var accLos = landUnitBundles.Sum(b => b.battleUnitState.accumulatedStrengthLoss); 
                return $"Land Units: {landUnitBundles.Count}, Strength: {strengh}, Loss: {accLos} (+{currentLoss}), avg CM: {leadingGroupBundle.accumulatedChanceCostModifier:+0.00%;-0.00%;0.00%}, avg TM: {leadingGroupBundle.averageTacticalModifier:+0.00%;-0.00%;0.00%}";
            }
        }

        [CreateProperty]
        public StyleBackground countryFlag => UnityWebRequestImageReader.Instance.FetchTexture2D(Utils.GetCountryPath(country));

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
        public float supplyCostTonsPerDay => GetSupplyCostTonsPerDay();

        [CreateProperty]
        public float supplyCapTons => GetSupplyCapTons();
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