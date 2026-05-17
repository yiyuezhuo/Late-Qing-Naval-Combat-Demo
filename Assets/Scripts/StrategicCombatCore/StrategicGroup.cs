using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using CoreUtils;
using NavalCombatCore;
using YYZ;
using YYZ.PathFinding;

namespace StrategicCombatCore
{
    public enum NavySubMission
    {
        General,
        Supply,
        Transfer
    }

    public partial class StrategicGroupMemberReference
    {
        public string referenceId;

        public IStrategicGroupMemberReferenceable Get()
        {
            return EntityManager.Instance.Get<IStrategicGroupMemberReferenceable>(referenceId);
        }

        public int GetCombinedSubUnitSize()
        {
            var obj = Get();
            if (obj == null)
                return 0;
            if (obj is StrategicGroup group)
                return group.GetCombinedSubUnitSize();
            if (obj is ShipLog shipLog && shipLog.mapState == MapState.Destroyed)
                return 0;
            return 1; // Otherwise (Subunit), translate to 1. 
        }

        public int GetSubUnitSize() => Get()?.GetSubUnitSize() ?? 0;
        public int GetStrengthMen() => Get()?.GetStrengthMen() ?? 0;
        public float GetShipTons() => Get()?.GetShipTons() ?? 0f;
        // public float GetCombinedCombatShipTons() => Get()?.GetCombatShipTons() ?? 0f;
        public float GetCombinedPowerPoint(bool isTop) => Get()?.GetCombinedPowerPoint(isTop) ?? 0f;
    }

    public partial class StrategicGroupReference
    {
        public string referenceId;

        public StrategicGroup Get()
        {
            return EntityManager.Instance.Get<StrategicGroup>(referenceId);
        }
        public Cell GetCell()
        {
            var parentGroup = Get();
            if (parentGroup == null || !parentGroup.IsOnMap())
                return null;
            return parentGroup.cell;
        }
        public SideState GetSide()
        {
            var parentGroup = Get();
            if (parentGroup == null)
                return null;
            return parentGroup.side;
        }

        public bool isReferenceAny() => referenceId != null && referenceId != "";
    }
    
    public partial class XY // General Cell (Grid or Area) reference, may be better to change the name.
    {
        [XmlAttribute]
        public int x;

        [XmlAttribute]
        public int y;

        [XmlAttribute]
        public string areaCellObjectId;

        public Cell GetCell()
        {
            if(areaCellObjectId != null)
                return EntityManager.Instance.Get<Cell>(areaCellObjectId);
            if(x >= 0 && y >= 0)
                return StrategicGameState.Instance.cellMatrix[x, y];
            return null;
        }

        public override string ToString()
        {
            return $"XY({x}, {y}, {areaCellObjectId})";
        }

        public string GetAreaCellName() => areaCellObjectId != null ? EntityManager.Instance.Get<Cell>(areaCellObjectId)?.Label?.GetShortName() : areaCellObjectId;
        public GlobalString GetAreaCellNameGlobalString() => areaCellObjectId != null ? EntityManager.Instance.Get<Cell>(areaCellObjectId)?.Label : new(){english=areaCellObjectId};

        public XY Clone()
        {
            return new XY()
            {
                x = x,
                y = y,
                areaCellObjectId = areaCellObjectId,
            };
        }
    }

    public class EmbarkingLandingPair
    {
        public XY embarking;
        public XY landing;

        public EmbarkingLandingPair Clone()
        {
            return new EmbarkingLandingPair()
            {
                embarking = embarking?.Clone(),
                landing = landing?.Clone(),
            };
        }
    }

    public partial class StrategicGroup : IObjectIdLabeled, IStrategicGroupMemberReferenceable, INamed
    {
        public string objectId { get; set; }
        public GlobalString name = new();
        public enum Type
        {
            General,
            HeadQuarter,
            Infantry,
            Cavalry, // Cavalry Regiment
            Artillery,
            Engineer,
            Fleet,
            CoastArtillery,
            Base
        }
        public Type type;
        public StrategicUnitSize size;
        public Country country;
        public enum DeployState
        {
            NotDeployed,
            Combined,
            Independent,
            // Loaded, // Similar to NotDeployed, but it's actually attached loaded in a ship. Used to naval transfer
            // VolatileIndependent // Similar to Independent, but it would "dissolve" automatically if it's possible to combine to its parent. Use to naval transfer
        }
        public DeployState deployState; // generally, deployState should be set with SetDeployState()
        public bool destroyed;
        
        // Independent sub states:
        public bool autoCombinable; // if true, it will convert from independent to combining when applicable
        public bool dissolvable; // if true, it would "dissolve" automatically if combine is applicable.
        public bool nonHistorical;
        
        public string containerObjectId; // Generally shipLog's objectId.
        
        public int independentX = -1;
        public int independentY = -1;

        [XmlIgnore]
        public int x
        {
            get
            {
                if (deployState == DeployState.NotDeployed)
                {
                    return -1;
                }
                else if (deployState == DeployState.Combined)
                {
                    return parentGroupReference.Get()?.x ?? -1;
                }
                return independentX;
            }
            set
            {
                if (deployState == DeployState.Independent)
                {
                    independentX = value;
                }
            }
        }

        [XmlIgnore]
        public int y
        {
            get
            {
                if (deployState == DeployState.NotDeployed)
                {
                    return -1;
                }
                else if (deployState == DeployState.Combined)
                {
                    return parentGroupReference.Get()?.y ?? -1;
                }
                return independentY;
            }
            set
            {
                if (deployState == DeployState.Independent)
                {
                    independentY = value;
                }
            }
        }

        public string independentAreaCellObjectId;
        public string areaCellObjectId // If it's not null, then the group is in an area and ignore x, y (they should be -1, -1 if areaObjectId is not null)
        {
            get
            {
                if(deployState == DeployState.NotDeployed)
                {
                    return null;
                }
                else if(deployState == DeployState.Combined)
                {
                    return parentGroupReference.Get()?.areaCellObjectId;
                }
                return independentAreaCellObjectId;
            }
        }

        public LeaderReference leaderReference = new();

        // Membership is organizational, not necessarily physical containment:
        // Independent subgroups may still be direct members while moving separately from the parent counter.
        public List<StrategicGroupMemberReference> directMemberReferences = new();
        // public List<StrategicGroupMemberReference> subordinatesInCommandOfChain = new();

        // public string strategicGroupId;
        public StrategicGroupReference parentGroupReference { get; set; } = new(); // parent strategic group
        public StrategicGroupReference detachedFromGroupReference { get; set; } = new();
        public bool enableAutoReattach { get; set; }
        public string assignedMissionObjectId;
        public string homeBaseObjectId;
        public NavySubMission navySubMission;
        public XY navalTransportTarget;
        public List<EmbarkingLandingPair> embarkingLandingPairs = new();

        public bool ShouldSerializeNavalTransportTarget() => false;
        public bool ShouldSerializeEmbarkingLandingPairs() => embarkingLandingPairs != null && embarkingLandingPairs.Count > 0;

        static readonly GlobalString advanceBaseSuffix = new()
        {
            english = " Advance Base",
            japanese = "前進基地",
            chineseSimplified = " 前进基地",
            chineseTraditional = " 前進基地",
        };

        static readonly GlobalString depotSuffix = new()
        {
            english = " Depot",
            japanese = "倉庫",
            chineseSimplified = " 仓库",
            chineseTraditional = " 倉庫",
        };

        public void SetAssignedMission(StrategicMission mission)
        {
            var oldMission = GetAssignedMission();
            if (oldMission != null)
            {
                oldMission.groups.RemoveAll(r => r.referenceId == objectId);
            }

            if (mission == null)
            {
                assignedMissionObjectId = null;
            }
            else
            {
                assignedMissionObjectId = mission.objectId;
                mission.groups.Add(new() { referenceId = objectId });
            }
        }

        public StrategicMission GetAssignedMission() => EntityManager.Instance.Get<StrategicMission>(assignedMissionObjectId);
        public StrategicGroup GetHomeBaseGroup()
        {
            if (deployState != DeployState.Independent)
            {
                var parentGroup = parentGroupReference.Get();
                if (parentGroup == null || parentGroup == this)
                    return null;
                return parentGroup.GetHomeBaseGroup();
            }

            if (homeBaseObjectId == objectId)
                return null;

            var homeBase = EntityManager.Instance.Get<StrategicGroup>(homeBaseObjectId);
            if (homeBase?.type != Type.Base)
                return null;
            return homeBase;
        }

        public LandUnit GetFirstDepot()
        {
            foreach (var subordinateRef in directMemberReferences)
            {
                if (subordinateRef.Get() is LandUnit landUnit &&
                    landUnit.GetLandUnitTemplate()?.unitType == LandUnitType.Supply)
                {
                    return landUnit;
                }
            }
            return null;
        }

        public LandUnit GetHomeBaseDepot() => GetHomeBaseGroup()?.GetFirstDepot();

        LandUnit GetNearestFriendlyBaseDepot(SupplyNetworkCache cache = null)
        {
            var srcCell = cell;
            var sideState = side;
            if (srcCell == null || sideState == null)
                return null;

            if (cache != null)
                return cache.GetNearestFriendlyBaseDepot(this);

            var graph = new DynamicLandSupplyNetworkingGraph() { side = sideState };
            LandUnit bestDepot = null;
            var bestCost = float.PositiveInfinity;

            foreach (var baseGroup in StrategicGameState.Instance.strategicGroups.Where(
                group => group != null && group.type == Type.Base && group.side == sideState))
            {
                var depot = baseGroup.GetFirstDepot();
                var dstCell = baseGroup.cell;
                if (depot == null || dstCell == null)
                    continue;

                var result = PathFinding<Cell>.AStar3(graph, srcCell, dstCell);
                if (result.Cost < bestCost)
                {
                    bestCost = result.Cost;
                    bestDepot = depot;
                }
            }

            return bestDepot;
        }

        public LandUnit GetCurrentSourceDepot(SupplyNetworkCache cache = null)
        {
            if (type == Type.Fleet || type == Type.Base || !string.IsNullOrEmpty(homeBaseObjectId))
            {
                return GetHomeBaseDepot();
            }

            return GetNearestFriendlyBaseDepot(cache);
        }

        public EmbarkingLandingPair GetCurrentEmbarkingLandingPair()
        {
            return embarkingLandingPairs?
                .FirstOrDefault(pair => pair?.embarking?.GetCell() != null && pair.landing?.GetCell() != null);
        }

        public Cell GetCurrentEmbarkingCell() => GetCurrentEmbarkingLandingPair()?.embarking?.GetCell();
        public Cell GetCurrentLandingCell() => GetCurrentEmbarkingLandingPair()?.landing?.GetCell();
        public Cell GetNavalTransportTargetCell() => GetCurrentLandingCell() ?? navalTransportTarget?.GetCell();

        public bool HasNavalTransportTarget() => GetNavalTransportTargetCell() != null;

        public void ClearNavalTransportTarget()
        {
            navalTransportTarget = null;
            ClearEmbarkingLandingPairs();
        }

        public void ClearEmbarkingLandingPairs()
        {
            embarkingLandingPairs?.Clear();
        }

        public void SetEmbarkingLandingPairs(IEnumerable<EmbarkingLandingPair> pairs)
        {
            embarkingLandingPairs = pairs?
                .Where(pair => pair?.embarking != null && pair.landing != null)
                .Select(pair => pair.Clone())
                .ToList() ?? new List<EmbarkingLandingPair>();
            navalTransportTarget = null;
        }

        public void AppendEmbarkingLandingPairs(IEnumerable<EmbarkingLandingPair> pairs)
        {
            embarkingLandingPairs ??= new();
            if (pairs == null)
                return;

            embarkingLandingPairs.AddRange(pairs
                .Where(pair => pair?.embarking != null && pair.landing != null)
                .Select(pair => pair.Clone()));
            navalTransportTarget = null;
        }

        public List<Cell> GetCurrentNavalTransportPathCells()
        {
            var pair = GetCurrentEmbarkingLandingPair();
            var embarkingCell = pair?.embarking?.GetCell();
            var landingCell = pair?.landing?.GetCell();
            if (embarkingCell == null || landingCell == null)
                return new();

            var embarkingIndex = FindPlannedPathCellIndex(embarkingCell);
            var landingIndex = embarkingIndex >= 0 ? FindPlannedPathCellIndex(landingCell, embarkingIndex + 1) : -1;
            if (embarkingIndex >= 0 && landingIndex > embarkingIndex)
            {
                return plannedPath
                    .Skip(embarkingIndex)
                    .Take(landingIndex - embarkingIndex + 1)
                    .Select(xy => xy?.GetCell())
                    .Where(cell => cell != null)
                    .ToList();
            }

            return new() { embarkingCell, landingCell };
        }

        public void CompleteCurrentNavalTransportTransfer(Cell landingCell)
        {
            var targetLandingCell = GetCurrentLandingCell() ?? landingCell;
            if (embarkingLandingPairs != null && embarkingLandingPairs.Count > 0)
            {
                embarkingLandingPairs.RemoveAt(0);
            }

            navalTransportTarget = null;
            TrimPlannedPathThroughLanding(targetLandingCell);
        }

        int FindPlannedPathCellIndex(Cell targetCell, int startIndex = 0)
        {
            if (targetCell == null || plannedPath == null)
                return -1;

            for (var idx = Math.Max(0, startIndex); idx < plannedPath.Count; idx++)
            {
                if (plannedPath[idx]?.GetCell() == targetCell)
                    return idx;
            }

            return -1;
        }

        void TrimPlannedPathThroughLanding(Cell landingCell)
        {
            if (landingCell == null || plannedPath == null || plannedPath.Count == 0)
            {
                ClearPlannedPath();
                return;
            }

            var landingIndex = FindPlannedPathCellIndex(landingCell);
            if (landingIndex > 0)
            {
                plannedPath.RemoveRange(0, landingIndex);
            }
            else if (landingIndex < 0)
            {
                plannedPath.Clear();
                plannedPath.Add(landingCell.ToXY());
            }

            moveProgressionKm = 0;
            if (plannedPath.Count < 2)
            {
                plannedPath.Clear();
            }
        }

        public bool IsValidNavalTransportTargetCell(Cell targetCell)
        {
            return targetCell != null &&
                targetCell.IsCoast &&
                targetCell.IsArmyPassable();
        }

        public bool TrySetNavalTransportTargetCell(Cell targetCell)
        {
            if (!IsValidNavalTransportTargetCell(targetCell))
                return false;

            navalTransportTarget = targetCell.ToXY();
            return true;
        }

        public StrategicGroup GetFriendlyBaseOnCell(Cell targetCell)
        {
            var currentSide = side;
            if (targetCell == null || currentSide == null)
                return null;

            return targetCell.StrategicGroupReferences
                .Select(reference => reference.Get())
                .FirstOrDefault(group => group != null && group.IsBase() && group.side == currentSide);
        }

        public StrategicGroup GetFriendlyBaseOnCurrentCell() => GetFriendlyBaseOnCell(cell);

        public bool CanConfigureNavalTransport()
        {
            return !IsNavy() &&
                !IsBase() &&
                deployState == DeployState.Independent &&
                GetFriendlyBaseOnCurrentCell() != null;
        }

        public bool IsReadyForNavalTransportTransfer()
        {
            if (IsNavy() ||
                IsBase() ||
                deployState != DeployState.Independent ||
                cell == null ||
                containerObjectId != null)
            {
                return false;
            }

            var currentPair = GetCurrentEmbarkingLandingPair();
            var hasCurrentEmbarkingLandingPair = currentPair != null &&
                currentPair.embarking?.GetCell() == cell &&
                currentPair.landing?.GetCell() != null;
            var hasLegacyNavalTransportTarget = currentPair == null &&
                (plannedPath?.Count ?? 0) == 0 &&
                navalTransportTarget?.GetCell() != null;

            return (hasCurrentEmbarkingLandingPair || hasLegacyNavalTransportTarget) &&
                GetFriendlyBaseOnCurrentCell() != null;
        }

        public SideState side => StrategicGameState.Instance.countryToSideStateMap.GetValueOrDefault(country);
        // public HexInfo hexInfo => StrategicGameState.Instance.hexInfoMap.GetValueOrDefault((x, y));

        [XmlIgnore]
        public List<StrategicGroup> currentStack
        {
            get
            {
                var currentSide = side;
                // return hexInfo.parentGroupReferences.Select(r => r.Get()).Where(g => g.side == currentSide).ToList();
                return cell.StrategicGroupReferences.Select(r => r.Get()).Where(g => g.side == currentSide).ToList();
            }
        }

        [XmlIgnore]
        public Cell cell
        {
            get
            {
                if(areaCellObjectId != null)
                {
                    return EntityManager.Instance.Get<Cell>(areaCellObjectId);
                }
                else if(x != -1 && y != -1)
                {
                    return StrategicGameState.Instance.cellMatrix[x, y];
                }
                return null;
            }
        }
        // public Cell cell => x != -1 && y != -1 ? StrategicGameState.Instance.cellMatrix[x, y] : null;

        public void SetStrategicGroupReference(StrategicGroup group) => IStrategicGroupMemberReferenceable.SetStrategicGroupReference(this, group);

        public string remark; // Referenced by UITK

        public float moveProgressionKm = 0;

        public List<XY> plannedPath = new();

        public enum GroupPostureType
        {
            Active,
            Passive, // Land Only
            Disengaged, // Disengaged/Retreat will not block hostile movement and would not engaged in combat generation and resolution
            Reorganized // Victory side of combat will be in reorganized state for 12 hours, so defeated side would retreat without risk
        }

        public GroupPostureType posture;
        public int restoredHours;

        public bool forcedReturningToBase; // RTB due to Out of Fuel

        // Arrive related attributes
        // public bool enableArriveTime;
        // public DateTime arriveTime = new DateTime(1894, 9, 17, 4, 30, 0, DateTimeKind.Utc);
        // public bool arrived;
        // public XY arriveTo = new();

        public partial class ArriveState
        {
            public static DateTime defaultArriveTime = new DateTime(1894, 9, 17, 4, 30, 0, DateTimeKind.Utc);

            public DateTime arriveTime = defaultArriveTime;
            public bool arrived;
            public XY arriveTo = new();
        }

        public ArriveState arriveState;

        // public bool ShouldSerializearriveTime() => enableArriveTime;
        // public bool ShouldSerializearrived() => enableArriveTime;

        public partial class FixedState
        {
            public static DateTime defaultReleaseTime = ArriveState.defaultArriveTime;

            public bool released;
            public bool enableReleaseTime;
            public DateTime releaseTime = defaultReleaseTime;

            public bool ShouldSerializeenableReleaseTime() => enableReleaseTime;
            public bool ShouldSerializereleaseTime() => enableReleaseTime;
        }

        public FixedState fixedState;

        public static Dictionary<StrategicUnitSize, string> sizeStrMap = new()
        {
            { StrategicUnitSize.Unspecified, "O" },
            { StrategicUnitSize.ArmyGroup, "XXXXX" },
            { StrategicUnitSize.Army, "XXXX" },
            { StrategicUnitSize.Corp, "XXX" },
            { StrategicUnitSize.Division, "XX" },
            { StrategicUnitSize.Bridge, "X" },
            { StrategicUnitSize.Regiment, "III" },
            { StrategicUnitSize.Battalion, "II" },
            { StrategicUnitSize.Company, "I" },
            { StrategicUnitSize.Platoon, "···" },
            { StrategicUnitSize.Squad, "··" },
        };

        public override string ToString()
        {
            return $"StrategicGroup({name.GetMergedName()})";
        }

        public bool HasLeader() => leaderReference.Get() != null;
        public bool IsNavy() => type == Type.Fleet;
        public bool IsArmy() => type != Type.Fleet;
        public bool IsBase() => type == Type.Base;

        public bool IsIndependent() => deployState == DeployState.Independent;
        public bool IsFixed => fixedState != null && !fixedState.released;
        public bool CanActStrategically => !IsFixed;

        public bool ReleaseFixed()
        {
            if (fixedState == null || fixedState.released)
                return false;

            fixedState.released = true;
            return true;
        }

        /// <summary>
        /// Is On Map (Independent or as combined sub unit in the another independent group)
        /// </summary>
        /// <returns></returns>
        public bool IsOnMap()
        {
            var pt = this;
            while (pt !=null && pt.deployState != DeployState.NotDeployed) // Combined or Independent
            {
                if (pt.deployState == DeployState.Independent)
                    return true;
                pt = pt.parentGroupReference.Get(); 
            }
            return false;
        }

        public int GetSubUnitSize() => directMemberReferences.Sum(r => r.GetSubUnitSize());
        public int GetStrengthMen() => directMemberReferences.Sum(r => r.GetStrengthMen());
        public float GetShipTons() => directMemberReferences.Sum(r => r.GetShipTons());
        public float GetCombatShipTons() => WalkGroupMembersDeployedShips().Select(shipLog => shipLog.shipClass).Where(shipClass => shipClass.IsCombatShip()).Sum(shipClass => shipClass.displacementTons);
        // WalkGroupMembersDeployedShips
        public IEnumerable<IStrategicGroupMemberReferenceable> WalkDirectMembers()
        {
            foreach (var subordinateRef in directMemberReferences)
            {
                var subordinate = subordinateRef.Get();
                if (subordinate != null)
                    yield return subordinate;
            }
        }

        public IEnumerable<T> WalkDirectMembers<T>() where T : class, IStrategicGroupMemberReferenceable
        {
            foreach (var subordinate in WalkDirectMembers())
            {
                if (subordinate is T obj)
                    yield return obj;
            }
        }

        public IEnumerable<StrategicGroup> WalkSelfAndDescendantStrategicGroups()
        {
            yield return this;

            foreach (var subGroup in WalkDirectMembers<StrategicGroup>())
            {
                foreach (var nestedGroup in subGroup.WalkSelfAndDescendantStrategicGroups())
                    yield return nestedGroup;
            }
        }

        public IEnumerable<StrategicGroup> WalkDescendantStrategicGroups(bool includeNotCombined = false)
        {
            foreach (var subGroup in WalkDirectMembers<StrategicGroup>())
            {
                yield return subGroup;

                if (includeNotCombined || subGroup.deployState == DeployState.Combined)
                {
                    foreach (var nestedGroup in subGroup.WalkDescendantStrategicGroups(includeNotCombined))
                        yield return nestedGroup;
                }
            }
        }

        public bool HasDescendantStrategicGroupType(Type targetType)
        {
            return WalkSelfAndDescendantStrategicGroups().Any(group => group != this && group.type == targetType);
        }

        public bool IsDescendantOf(StrategicGroup ancestorCandidate)
        {
            if (ancestorCandidate == null)
                return false;

            var current = parentGroupReference.Get();
            var visitedGroupIds = new HashSet<string>();
            while (current != null)
            {
                if (current == ancestorCandidate)
                    return true;
                if (!visitedGroupIds.Add(current.objectId))
                    throw new InvalidOperationException(
                        $"Strategic group parent cycle while checking ancestry: group={objectId}, ancestorCandidate={ancestorCandidate.objectId}, repeated={current.objectId}");
                current = current.parentGroupReference.Get();
            }

            return false;
        }

        public bool IsAncestorOf(StrategicGroup descendantCandidate)
        {
            return descendantCandidate != null && descendantCandidate.IsDescendantOf(this);
        }

        public bool IsHostileFortifiedBaseFor(SideState otherSide)
        {
            return IsBase() &&
                deployState == DeployState.Independent &&
                side != null &&
                otherSide != null &&
                side != otherSide &&
                HasDescendantStrategicGroupType(Type.CoastArtillery);
        }

        public static bool CellHasHostileFortifiedBaseFor(Cell targetCell, SideState movingSide)
        {
            if (targetCell == null || movingSide == null)
                return false;

            return targetCell.StrategicGroupReferences
                .Select(reference => reference.Get())
                .Any(group => group != null && group.IsHostileFortifiedBaseFor(movingSide));
        }

        public bool CanEnterCell(Cell toCell)
        {
            if (toCell == null)
                return false;

            if (cell == toCell)
                return true;

            if (IsArmy() && !toCell.IsArmyPassable())
                return false;

            return !IsNavy() || !CellHasHostileFortifiedBaseFor(toCell, side);
        }

        public bool ConvertCapturedBaseTo(SideState occupyingSide)
        {
            if (!IsBase() || occupyingSide == null || side == occupyingSide)
                return false;

            var newCountry = occupyingSide.countries.FirstOrDefault();
            foreach (var group in WalkSelfAndDescendantStrategicGroups())
            {
                group.leaderReference.referenceObjectId = null;
                group.country = newCountry;
            }

            homeBaseObjectId = null;
            return true;
        }

        public float GetCombinedPowerPoint(bool isTop)
        {
            if (!isTop && deployState != DeployState.Combined)
                return 0;
            return directMemberReferences.Sum(r => r.GetCombinedPowerPoint(false));
        }

        public float GetSupplyCostTonsPerDay() // Combined
        {
            var supplySum = 0f;
            foreach (var subordinateRef in directMemberReferences)
            {
                var subordinate = subordinateRef.Get();
                if (subordinate == null)
                    continue;
                if (subordinate is LandUnit landUnit && landUnit?.GetLandUnitTemplate()?.unitType != LandUnitType.Supply)
                {
                    supplySum += landUnit.GetSupplyCostTonsPerDay();
                }
                else if (subordinate is ShipLog shipLog)
                {
                    supplySum += shipLog.GetSupplyCostTonsPerDay();
                }
                else if (subordinate is StrategicGroup group)
                {
                    supplySum += group.GetSupplyCostTonsPerDay();
                }
            }
            return supplySum;
        }

        public double GetSupplyTons()
        {
            var landUnitSupplyTons = WalkGroupMembers<LandUnit>().Sum(landUnit => landUnit.supplyTons);
            var shipSupplyTons = WalkGroupMembers<ShipLog>().Sum(ship => ship.supplyTons);
            return landUnitSupplyTons + shipSupplyTons;
        }

        public double GetSupplyCapTons()
        {
            var landUnitSupplyTonsCap = WalkGroupMembers<LandUnit>().Sum(landUnit => landUnit.GetSupplyCapTons());
            var shipSupplyTonsCap = WalkGroupMembers<ShipLog>().Sum(ship => ship.GetSupplyCapTons());
            return landUnitSupplyTonsCap + shipSupplyTonsCap;
        }

        // public double GetSupplyPercent() => GetSupplyTons() / GetSupplyCapTons();

        internal void EnsureDirectMemberReference(string memberObjectId)
        {
            if (string.IsNullOrWhiteSpace(memberObjectId) || memberObjectId == objectId)
                return;

            if (directMemberReferences.All(reference => reference.referenceId != memberObjectId))
            {
                directMemberReferences.Add(new StrategicGroupMemberReference() { referenceId = memberObjectId });
            }
        }

        static int GetDirectMemberSortCategory(IStrategicGroupMemberReferenceable member)
        {
            return member switch
            {
                LandUnit => 0,
                ShipLog => 1,
                StrategicGroup => 2,
                _ => 3,
            };
        }

        static string GetDirectMemberSortName(IStrategicGroupMemberReferenceable member)
        {
            return member switch
            {
                LandUnit landUnit => landUnit.name?.GetMergedName() ?? string.Empty,
                ShipLog shipLog => shipLog.namedShip?.name?.GetMergedName() ?? string.Empty,
                StrategicGroup group => group.name?.GetMergedName() ?? string.Empty,
                _ => string.Empty,
            };
        }

        public void SortDirectMemberReferencesByPower()
        {
            if (type != Type.Fleet)
                return;

            directMemberReferences.Sort((left, right) =>
            {
                var leftMember = left?.Get();
                var rightMember = right?.Get();

                if (leftMember == null || rightMember == null)
                {
                    if (leftMember == null && rightMember == null)
                        return string.Compare(left?.referenceId, right?.referenceId, StringComparison.Ordinal);
                    return leftMember == null ? 1 : -1;
                }

                var categoryResult = GetDirectMemberSortCategory(leftMember).CompareTo(GetDirectMemberSortCategory(rightMember));
                if (categoryResult != 0)
                    return categoryResult;

                var powerResult = rightMember.GetCombinedPowerPoint(true).CompareTo(leftMember.GetCombinedPowerPoint(true));
                if (powerResult != 0)
                    return powerResult;

                var nameResult = string.Compare(
                    GetDirectMemberSortName(leftMember),
                    GetDirectMemberSortName(rightMember),
                    StringComparison.CurrentCultureIgnoreCase
                );
                if (nameResult != 0)
                    return nameResult;

                return string.Compare(leftMember.objectId, rightMember.objectId, StringComparison.Ordinal);
            });
        }

        internal void RemoveDirectMemberReference(string memberObjectId)
        {
            if (string.IsNullOrWhiteSpace(memberObjectId))
                return;

            directMemberReferences.RemoveAll(reference => reference.referenceId == memberObjectId);
        }

        public static void ReassignMember(IStrategicGroupMemberReferenceable member, StrategicGroup newParentGroup)
        {
            if (member == null)
                return;

            ThrowIfInvalidParentAssignment(member, newParentGroup, "reassign member");

            var oldParentGroup = member.parentGroupReference.Get();
            if (oldParentGroup == newParentGroup)
            {
                newParentGroup?.EnsureDirectMemberReference(member.objectId);
                newParentGroup?.SortDirectMemberReferencesByPower();
                member.parentGroupReference.referenceId = newParentGroup?.objectId;
                return;
            }

            oldParentGroup?.RemoveDirectMemberReference(member.objectId);
            newParentGroup?.EnsureDirectMemberReference(member.objectId);
            member.parentGroupReference.referenceId = newParentGroup?.objectId;
            oldParentGroup?.SortDirectMemberReferencesByPower();
            newParentGroup?.SortDirectMemberReferencesByPower();
        }

        internal static void ThrowIfInvalidParentAssignment(
            IStrategicGroupMemberReferenceable member,
            StrategicGroup newParentGroup,
            string operation)
        {
            if (member == null || newParentGroup == null)
                return;

            if (member is not StrategicGroup memberGroup)
                return;

            if (newParentGroup == memberGroup)
            {
                throw new InvalidOperationException(
                    $"Invalid strategic group membership during {operation}: group {memberGroup.objectId} cannot be its own parent.");
            }

            if (newParentGroup.IsDescendantOf(memberGroup))
            {
                throw new InvalidOperationException(
                    $"Invalid strategic group membership during {operation}: cannot assign group {memberGroup.objectId} under descendant {newParentGroup.objectId}.");
            }
        }

        public void AttachMember(IStrategicGroupMemberReferenceable member) => ReassignMember(member, this);
        public void DetachMember(IStrategicGroupMemberReferenceable member) => ReassignMember(member, null);
        public void MoveMemberToGroup(IStrategicGroupMemberReferenceable member, StrategicGroup otherGroup) => ReassignMember(member, otherGroup);

        public bool ReplaceDirectMemberReference(StrategicGroupMemberReference slot, IStrategicGroupMemberReferenceable newMember)
        {
            if (slot == null || !directMemberReferences.Contains(slot))
                return false;

            if (newMember is StrategicGroup newGroup && newGroup.objectId == objectId)
                return false;

            var oldMember = slot.Get();
            if (oldMember != null)
            {
                oldMember.parentGroupReference.referenceId = null;
                IStrategicGroupMemberReferenceable.ClearDetachedFromGroupState(oldMember);
            }

            slot.referenceId = null;
            if (newMember == null)
                return true;

            var oldParentGroup = newMember.parentGroupReference.Get();
            oldParentGroup?.RemoveDirectMemberReference(newMember.objectId);

            slot.referenceId = newMember.objectId;
            newMember.parentGroupReference.referenceId = objectId;
            IStrategicGroupMemberReferenceable.ClearDetachedFromGroupState(newMember);
            directMemberReferences.RemoveAll(reference => !ReferenceEquals(reference, slot) && reference.referenceId == newMember.objectId);
            SortDirectMemberReferencesByPower();
            return true;
        }

        public bool TryRelocateIndependentGroup(Cell toCell, bool moveThroughEdge = false)
        {
            if (deployState != DeployState.Independent || toCell == null || !CanActStrategically)
                return false;

            return MoveToCell(toCell, moveThroughEdge);
        }

        public bool TryRelocateIndependentGroupToGrid(int targetX, int targetY)
        {
            if (deployState != DeployState.Independent)
                return false;

            var gameState = StrategicGameState.Instance;
            if (gameState == null || !gameState.scenarioState.enableGridSystem || gameState.cellMatrix == null)
                return false;

            if (targetX < 0 || targetY < 0 ||
                targetX >= gameState.cellMatrix.GetLength(0) ||
                targetY >= gameState.cellMatrix.GetLength(1))
                return false;

            var targetCell = gameState.cellMatrix[targetX, targetY];
            if (targetCell == null)
                return false;

            return TryRelocateIndependentGroup(targetCell, false);
        }

        // From Vacuum or to vacuum, or move to other cell through vacuum.
        // public void MoveToXY(int toX, int toY, bool moveThroughEdge)
        public bool MoveToCell(Cell toCell, bool moveThroughEdge)
        {
            if (toCell == null)
                return false;

            // var toCell = StrategicGameState.Instance.cellMatrix[toX, toY];
            var prevCell = cell;
            if (IsFixed && deployState == DeployState.Independent && prevCell != null && prevCell != toCell)
                return false;

            if (!CanEnterCell(toCell))
                return false;

            if (deployState == DeployState.Independent && prevCell != null)
            {
                prevCell.StrategicGroupReferences.RemoveAll(gp => gp.referenceId == objectId);
            }

            deployState = DeployState.Independent;
            // x = toX;
            // y = toY;
            if(toCell.IsAreaCell())
            {
                x = -1;
                y = -1;
                independentAreaCellObjectId = toCell.objectId;
            }
            else // Grid Cell
            {
                x = toCell.x;
                y = toCell.y;
                independentAreaCellObjectId = null;
            }


            toCell.StrategicGroupReferences.Add(new() { referenceId = objectId });

            if (moveThroughEdge && IsArmy() && prevCell != null)
            {
                if (toCell.TryGetDirection(prevCell, out var edge))
                {
                    toCell.SetEdgeSide(edge, side);
                }

                prevCell.RefreshControlState();
                // StrategicGameState.Instance.InvokeMapCellUpdated(prevCell.x, prevCell.y);
                StrategicGameState.Instance.InvokeMapCellUpdated(prevCell);
            }


            toCell.RefreshControlState();
            // StrategicGameState.Instance.InvokeMapCellUpdated(toCell.x, toCell.y);
            StrategicGameState.Instance.InvokeMapCellUpdated(toCell);
            return true;
        }
        
        public void UnloadFromContainer(bool clearPlannedPath = true)
        {
            if(containerObjectId != null)
            {
                var container = EntityManager.Instance.Get<ShipLog>(containerObjectId);
                if(container != null)
                {
                    var containerGroup = container.parentGroupReference.Get();
                    if(containerGroup != null)
                    {
                        // MoveToCell(containerGroup.x, containerGroup.y, false);
                        MoveToCell(containerGroup.cell, false);
                        container.loadedGroups.RemoveAll(r => r.referenceId == objectId);
                        containerObjectId = null;
                        if (clearPlannedPath)
                        {
                            ClearPlannedPath();
                        }
                    }
                }
            }
        }

        public void RemoveFromMap() // When Independent is transitioned to Combined or NotDeployed
        {
            var prevCell = cell;

            if (prevCell == null)
            {
                return;
            }
            
            // var hexInfoMap = StrategicGameState.Instance.hexInfoMap;

            if (deployState == DeployState.Independent)
            {
                // cellInfo.parentGroupReferences.RemoveAll(gp => gp.referenceId == objectId);
                cell.StrategicGroupReferences.RemoveAll(gp => gp.referenceId == objectId);
            }

            independentX = -1;
            independentY = -1;
            independentAreaCellObjectId = null;

            // deployState = DeployState.NotDeployed;

            prevCell.RefreshControlState();
        }

        public void SetDeployState(DeployState newState)
        {
            if (newState == deployState)
                return;

            if (newState == DeployState.Independent)
            {
                var parentGroup = parentGroupReference.Get();
                var parentCell = parentGroup?.cell;
                if (parentCell != null)
                {
                    // MoveToXY(parentGroup.x, parentGroup.y, false);
                    MoveToCell(parentCell, false);
                }
                else
                {
                    UnityEngine.Debug.LogWarning($"Refusing to set {this} to Independent without a valid parent location.");
                }
            }
            else if (newState == DeployState.NotDeployed || newState == DeployState.Combined)
            {
                RemoveFromMap();
                deployState = newState;
            }
        }

        public int GetCombinedSubUnitSize()
        {
            return directMemberReferences.Sum(r => r.GetCombinedSubUnitSize());
        }

        public string GetSizeStr()
        {
            return sizeStrMap.GetValueOrDefault(size, "?");
        }

        public IEnumerable<IObjectIdLabeled> GetSubObjects()
        {
            yield break;
        }

        public float GetSpeedKmPerHour()
        {
            if (!CanActStrategically || IsBase())
            {
                return 0;
            }
            if (posture == GroupPostureType.Reorganized)
            {
                return 0;
            }
            // var disengagedMod = posture == GroupPostureType.Disengaged ? 1.1f : 1;
            var disengagedMod = 1f;
            if(posture == GroupPostureType.Disengaged)
            {
                disengagedMod = IsNavy() ? 1.1f : 2f;
            }
            
            if (IsArmy())
            {
                if (cell == null || side == null)
                    return 0;

                if (IsReadyForNavalTransportTransfer())
                    return 0;

                var nextCell = GetPathNextCell();
                if (nextCell != null && cell.TryGetDirection(nextCell, out var edge))
                {
                    var edgeSide = cell.GetEdgeSide(edge);
                    if (edgeSide == null || edgeSide.objectId != side.objectId)
                        return 0; // edge control block

                    var normalSpeedKmPerHour = GetSpeedKmPerHour(cell, nextCell);
                    if (HasAnyOutOfSupplyLandUnit())
                    {
                        normalSpeedKmPerHour *= 0.5f;
                    }
                    return normalSpeedKmPerHour * disengagedMod;
                }
                return 0;
            }
            return GetFleetStrategicSpeedKmPerHour() * disengagedMod;
        }

        public float GetFleetStrategicSpeedKmPerHour()
        {
            var deployedShips = WalkGroupMembersDeployedShips().ToList();
            if (deployedShips.Count == 0)
            {
                return 0;
            }

            var speedKnots = deployedShips.Min(GetShipStrategicSpeedKnots);
            return speedKnots * MeasureUtils.navalMileToKilometer;
        }

        public static float cruiseSpeedCoef = 0.5f;

        public float GetShipStrategicSpeedKnots(ShipLog shipLog)
        {
            if (shipLog == null)
            {
                return 0;
            }

            if (shipLog.supplyTons <= 0)
            {
                return 4f;
            }

            return Math.Max(4f, shipLog.GetMaxSpeedKnots() * cruiseSpeedCoef);
        }

        public bool HasAnyOutOfSupplyLandUnit()
        {
            return WalkGroupMembers<LandUnit>().Any(landUnit =>
                landUnit.GetLandUnitTemplate()?.unitType != LandUnitType.Supply &&
                landUnit.supplyTons <= 0
            );
        }

        public bool HasCombatEffectiveLandUnit()
        {
            return WalkGroupMembers<LandUnit>().Any(landUnit =>
                landUnit != null &&
                landUnit.strength > 0 &&
                landUnit.GetLandUnitTemplate()?.unitType != LandUnitType.Supply
            );
        }

        public Cell GetPathNextCell()
        {
            if (plannedPath.Count >= 2)
            {
                return plannedPath[1].GetCell();
            }
            return null;
        }

        public bool HasSamePlannedPathAndProgressAs(StrategicGroup other, float epsilon = 0.001f)
        {
            if (other == null)
                return false;

            if (plannedPath.Count != other.plannedPath.Count ||
                Math.Abs(moveProgressionKm - other.moveProgressionKm) > epsilon)
            {
                return false;
            }

            for (var idx = 0; idx < plannedPath.Count; idx++)
            {
                var left = plannedPath[idx];
                var right = other.plannedPath[idx];
                if (left == null || right == null)
                {
                    if (!ReferenceEquals(left, right))
                        return false;
                    continue;
                }

                if (left.x != right.x ||
                    left.y != right.y ||
                    left.areaCellObjectId != right.areaCellObjectId)
                {
                    return false;
                }
            }

            return true;
        }

        public static float GetSpeedKmPerHour(Cell src, Cell dst)
        {
            if (src.TryGetDirection(dst, out var edge))
            {
                var terrainSpeed = terrainToSpeedKmPerHour.GetValueOrDefault(dst.terrain, 1);
                if (src.roads.Contains(edge) || src.railroads.Contains(edge))
                {
                    terrainSpeed = 2f;
                }
                return terrainSpeed;
            }
            return 1;
        }

        static float speedBase = 0.9f; // 0.9km/h

        // Road/Railroad: 2
        public static Dictionary<TerrainType, float> terrainToSpeedKmPerHour = new()
        {
            // {TerrainType.Clear, 1},  // 1km/h for general infantry
            {TerrainType.Clear, speedBase},
            {TerrainType.Rough, speedBase * 0.5f},
            {TerrainType.Mountain, speedBase * 0.3f},
            {TerrainType.Forest, speedBase * 0.5f},
            {TerrainType.Jungle, speedBase * 0.5f},
            {TerrainType.Desert, speedBase},
            {TerrainType.Swamp, speedBase * 0.3f},
            {TerrainType.ForestRough, speedBase * 0.4f},
            {TerrainType.JungleRough, speedBase * 0.4f},
            {TerrainType.DesertRough, speedBase * 0.5f},
            {TerrainType.TropicalMountain, speedBase * 0.3f},
            {TerrainType.SandDesert, speedBase * 0.3f},
            {TerrainType.Field, speedBase},
        };

        public IEnumerable<T> WalkGroupMembers<T>(bool includeNotCombined=false) where T : IStrategicGroupMemberReferenceable
        {
            // By default this walks members physically inside this counter (Combined groups).
            // Pass includeNotCombined for the full organizational tree, including Independent subgroups.
            foreach (var subordinate in WalkDirectMembers())
            {
                if (subordinate is T obj && obj != null)
                    yield return obj;

                if (subordinate is StrategicGroup group && group != null &&
                    (includeNotCombined || group.deployState == DeployState.Combined))
                {
                    foreach (var subObj in group.WalkGroupMembers<T>(includeNotCombined))
                    {
                        yield return subObj;
                    }
                }
            }
        }

        public double GetTransferWeightTons()
        {
            return WalkGroupMembers<LandUnit>().Sum(landUnit => landUnit.GetTransferWeightTons());
        }

        public void LoadToShip(ShipLog shipLog)
        {
            if (shipLog == null || string.IsNullOrWhiteSpace(objectId))
                return;

            if (deployState == DeployState.Combined)
            {
                autoCombinable = true;
            }
            else if (deployState == DeployState.Independent)
            {
            }
            // deployState = DeployState.NotDeployed;
            RemoveFromMap();
            deployState = DeployState.NotDeployed;

            containerObjectId = shipLog.objectId;
            shipLog.loadedGroups.RemoveAll(reference => reference.referenceId == objectId);
            shipLog.loadedGroups.Add(new() { referenceId = objectId });
        }

        public void AttachTo(StrategicGroup newParentGroup)
        {
            ReassignMember(this, newParentGroup);
        }

        public void TransferLandUnit(LandUnit subLandUnit, StrategicGroup toGroup)
        {
            MoveMemberToGroup(subLandUnit, toGroup);
        }

        public void Split()
        {
            if (directMemberReferences.Count < 2)
                return;

            var newGroup = new StrategicGroup()
            {
                name = name.Add("/2"),
                type = type,
                size = size,
                country = country,
                deployState = DeployState.NotDeployed,
            };
            var gameState = StrategicGameState.Instance;
            var idx = gameState.strategicGroups.IndexOf(this);
            gameState.strategicGroups.Insert(idx + 1, newGroup);
            EntityManager.Instance.Register(newGroup, null);

            newGroup.AttachTo(parentGroupReference.Get());

            if (deployState == DeployState.Independent)
            {
                // newGroup.MoveToXY(independentX, independentY, false);
                newGroup.MoveToCell(cell, false);
            }

            var transferElements = Enumerable.Range(0, directMemberReferences.Count)
                .Where(idx => idx % 2 == 1)
                .Select(idx => directMemberReferences[idx])
                .ToList();

            foreach (var transferElementRef in transferElements)
            {
                var element = transferElementRef.Get();
                MoveElementTo(element, newGroup);
            }
        }

        public void MoveElementTo(IStrategicGroupMemberReferenceable element, StrategicGroup otherGroup)
        {
            MoveMemberToGroup(element, otherGroup);
        }

        public bool Combatable() => CanActStrategically && deployState == DeployState.Independent && posture != GroupPostureType.Disengaged;
        public bool NavalCombatable() => CanActStrategically && deployState == DeployState.Independent && posture != GroupPostureType.Disengaged && type == Type.Fleet;
        public bool LandCombatable() => CanActStrategically && deployState == DeployState.Independent && posture != GroupPostureType.Disengaged && type != Type.Fleet;

        /// <summary>
        /// Drive the group to its base, if disengagedHours == 0, it's a normal return, otherwise it's a retreat return.
        /// </summary>
        /// <param name="disengagedHours"></param>
        public void StartReturnToBase(int disengagedHours)
        {
            if (deployState != DeployState.Independent || !CanActStrategically)
                return;

            if (disengagedHours > 0)
            {
                posture = GroupPostureType.Disengaged;
                restoredHours = disengagedHours;
            }

            var depotGroup = GetDepotGroup();

            var depotCell = depotGroup?.cell;
            if (depotGroup != null && depotCell != null)
            {
                TryPlanPathTo(depotCell);
            }
            else
            {
                // TODO: Dismiss or retreat to a relative "safe" location determined dynamically?
                StartMoveToARandomMovableNeighbor();
            }
        }

        public void StartStopLandAttack()
        {
            StartReorgnize(24);
        }

        public bool StartMoveToARandomMovableNeighbor()
        {
            if (!CanActStrategically)
                return false;

            plannedPath.Clear();
            
            IGraphEnumerable<Cell> graph = IsNavy() ? new DynamicCellGraphNavy(){movingSide=side} : new DynamicLandRetreatGraph(){side=side};
            var possibleNeighbors = graph.Neighbors(cell).ToList();
            if(possibleNeighbors.Count > 0)
            {
                var dstCell = RandomUtils.Sample(possibleNeighbors);
                var pathCells = PathFinding<Cell>.AStar(graph, cell, dstCell);
                // plannedPath.AddRange(pathCells.Select(c => new XY() { x = c.x, y = c.y }));
                plannedPath.AddRange(pathCells.Select(c => c.ToXY()));
                moveProgressionKm = 0; // TODO: This may override movement progression which should be maintained.
                return true;
            }

            return false; // if unit can't retreat, it should be eliminated generally
        }

        public void StartRetreatFromLandDefend()
        {
            if (IsBase())
                return;

            posture = GroupPostureType.Disengaged;
            restoredHours = 24; // TODO: It's questionable to "return" to Active state sometimes, looks like we should separated those types of states.

            DoLandDisengage();
        }

        public void SetPlannedPath(List<XY> newPlannedPath)
        {
            if (!CanActStrategically)
            {
                ClearPlannedPath();
                return;
            }

            if(newPlannedPath.Count < 2)
            {
                plannedPath.Clear();
                ClearEmbarkingLandingPairs();
                moveProgressionKm = 0;
                return;
            }

            var moveProgressionKmMaintained = plannedPath.Count >= 2 && plannedPath[1].GetCell() == newPlannedPath[1].GetCell();
            if(!moveProgressionKmMaintained)
            {
                moveProgressionKm = 0;
            }
            ClearEmbarkingLandingPairs();
            plannedPath.Clear();
            plannedPath.AddRange(newPlannedPath);
        }

        public void TryPlanPathTo(Cell dstCell)
        {
            if (!CanActStrategically)
                return;

            plannedPath.Clear();
            
            IGraphEnumerable<Cell> graph = IsNavy() ? new DynamicCellGraphNavy(){movingSide=side} : new DynamicCellGraphArmy();
            var pathCells = PathFinding<Cell>.AStar(graph, cell, dstCell);
            var pathXY = pathCells.Select(c => c.ToXY()).ToList();

            SetPlannedPath(pathXY);
        }

        public bool TryPlanArmyMixedPathTo(Cell dstCell, out bool hasNavalTransportSegment)
        {
            if (!TryBuildArmyMixedPathTo(dstCell, out var pathCells, out var embarkingLandingPairs, out hasNavalTransportSegment))
                return false;

            SetPlannedPath(pathCells.Select(c => c.ToXY()).ToList());
            SetEmbarkingLandingPairs(embarkingLandingPairs);
            return true;
        }

        public bool TryBuildArmyMixedPathTo(
            Cell dstCell,
            out List<Cell> pathCells,
            out List<EmbarkingLandingPair> embarkingLandingPairs,
            out bool hasNavalTransportSegment)
        {
            hasNavalTransportSegment = false;
            pathCells = new();
            embarkingLandingPairs = new();
            if (!CanActStrategically ||
                !IsArmy() ||
                cell == null ||
                dstCell == null ||
                !dstCell.IsArmyPassable())
            {
                return false;
            }

            var graph = new DynamicCellGraphArmyWithNavalTransport()
            {
                movingSide = side,
            };
            var srcNode = new DynamicCellGraphArmyWithNavalTransport.Node(cell, false);
            var dstNode = new DynamicCellGraphArmyWithNavalTransport.Node(dstCell, false);
            var pathCost = PathFinding<DynamicCellGraphArmyWithNavalTransport.Node>.AStar2(graph, srcNode, dstNode, out var pathNodes);
            if (pathNodes.Count < 2 || float.IsInfinity(pathCost))
                return false;

            Cell currentEmbarkingCell = null;
            foreach (var node in pathNodes)
            {
                if (pathCells.Count == 0 || pathCells[^1] != node.cell)
                {
                    pathCells.Add(node.cell);
                }
            }

            for (var idx = 1; idx < pathNodes.Count; idx++)
            {
                var prev = pathNodes[idx - 1];
                var current = pathNodes[idx];
                if (prev.cell != current.cell)
                    continue;

                if (!prev.navalTransportState && current.navalTransportState)
                {
                    currentEmbarkingCell = current.cell;
                }
                else if (prev.navalTransportState && !current.navalTransportState && currentEmbarkingCell != null)
                {
                    embarkingLandingPairs.Add(new()
                    {
                        embarking = currentEmbarkingCell.ToXY(),
                        landing = current.cell.ToXY(),
                    });
                    currentEmbarkingCell = null;
                }
            }

            hasNavalTransportSegment = embarkingLandingPairs.Count > 0;
            return true;
        }

        public bool IsFleetReadyForMissionDeparture()
        {
            var groupCell = cell;
            if(type == Type.Fleet)
            {
                var depotCell = GetDepotGroup()?.cell;
                if(depotCell != null && groupCell == depotCell)
                {
                    var ships = WalkGroupMembersDeployedShips().ToList();
                    if (ships.Any(StrategicGroupSubGroupUtility.NeedsDetachForRepair))
                    {
                        return false;
                    }
                    if(ships.Any(ship => ship.GetSupplyPercent() < 0.95))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public bool IsFleetHasSufficientFuelToReturnHome()
        {
            if(type == Type.Fleet)
            {
                var groupCell = cell;
                var depotCell = GetDepotGroup()?.cell;
                if(depotCell != null && groupCell != depotCell)
                {
                    var graph = new DynamicCellGraphNavy(){movingSide=side};
                    var pathCells = PathFinding<Cell>.AStar(graph, groupCell, depotCell);
                    if (pathCells.Count >= 2)
                    {
                        var distKm = 0f;
                        for(int i=0; i<pathCells.Count-1; i++)
                        {
                            distKm += pathCells[i].GetDistanceUnsafe(pathCells[i+1]);
                        }
                        var rtbHours = distKm / GetSpeedKmPerHour();
                        // var rtbDays = rtbHours / 24f;
                        // var supplyThresholdPercent = rtbDays / ShipLog.shipEnduranceDays;
                        var hasOutOfFuelRisk = WalkGroupMembersDeployedShips().Any(ship => ship.GetEnduranceHours() <= rtbHours);
                        if(hasOutOfFuelRisk)
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        public void DoLandDisengage()
        {
            if (!CanActStrategically)
                return;

            var dstCell = GetDepotGroup()?.cell;
            if(dstCell != null)
            {
                var graph = new DynamicLandRetreatGraph(){side=side};
                var pathCells = PathFinding<Cell>.AStar(graph, cell, dstCell);
                if(pathCells.Count >= 2)
                {
                    // plannedPath.Clear();
                    // plannedPath.AddRange(pathCells.Take(2).Select(c => new XY() { x = c.x, y = c.y }));
                    // moveProgressionKm = 0; // TODO: This may override movement progression which should be maintained.
                    // var newPlannedPath = pathCells.Take(2).Select(c => new XY() { x = c.x, y = c.y }).ToList();
                    var newPlannedPath = pathCells.Take(2).Select(c => c.ToXY()).ToList();
                    SetPlannedPath(newPlannedPath);
                    return;
                }
            }

            if(!StartMoveToARandomMovableNeighbor())
            {
                EliminateByNoRetreatPath();
            }
        }

        void EliminateByNoRetreatPath()
        {
            if (IsBase())
                return;

            MarkAsDestroyed();
        }

        public void MarkAsDestroyed()
        {
            ClearPlannedPath();
            RemoveFromMap();
            deployState = DeployState.NotDeployed;
            destroyed = true;
            // TODO: Add log?
        }
        
        public void StartReorgnize(int reorgnizedHours)
        {
            if (deployState != DeployState.Independent)
                return;

            if(reorgnizedHours > 0)
            {
                posture = GroupPostureType.Reorganized;
                restoredHours = reorgnizedHours;
            }
        }

        public void ClearPlannedPath()
        {
            plannedPath.Clear();
            ClearEmbarkingLandingPairs();
            moveProgressionKm = 0;
        }

        LandUnit GetFriendlyDepotAtCurrentCell()
        {
            var currentCell = cell;
            var currentSide = side;
            if (currentCell == null || currentSide == null)
                return null;

            return currentCell.StrategicGroupReferences
                .Select(reference => reference.Get())
                .Where(group => group != null && group.IsBase() && group.side == currentSide)
                .Select(group => group.GetFirstDepot())
                .FirstOrDefault(depot => depot != null);
        }

        public void HandleCompletedStrategicPath()
        {
            if (type != Type.Fleet)
                return;

            if (navySubMission == NavySubMission.Supply)
            {
                UnloadOneShotSupplySubMission();
                navySubMission = NavySubMission.General;
            }
            else if (navySubMission == NavySubMission.Transfer)
            {
                UnloadOneShotTransferSubMission();
                navySubMission = NavySubMission.General;
            }
        }

        void UnloadOneShotSupplySubMission()
        {
            var targetDepot = GetFriendlyDepotAtCurrentCell();
            if (targetDepot == null)
            {
                ServiceLocator.Get<ILoggerService>().Log($"Supply Sub Mission: {name.GetMergedName()} found no friendly depot at destination.");
                return;
            }

            foreach (var ship in WalkGroupMembersDeployedShips())
            {
                if (ship?.shipClass?.type != ShipType.Transport)
                    continue;

                var returnToBaseThresholdTons = ship.GetSupplyCapTons() * 0.1;
                var transferableTons = Math.Max(0, ship.supplyTons - returnToBaseThresholdTons);
                if (transferableTons <= 0)
                    continue;

                ship.supplyTons -= transferableTons;
                targetDepot.supplyTons += transferableTons;

                ServiceLocator.Get<ILoggerService>().Log($"Supply Sub Mission Transfer: {ship.namedShip.name.GetMergedName()} -> {targetDepot.name.GetMergedName()} ({transferableTons})");
            }
        }

        void UnloadOneShotTransferSubMission()
        {
            var landingCell = cell;
            var landingSide = side;
            if (landingCell == null || landingSide == null)
                return;

            var previousController = landingCell.GetHexSide();
            var unloadedAnyCargo = false;

            foreach (var ship in WalkGroupMembersDeployedShips())
            {
                if (ship?.shipClass?.type != ShipType.Transport)
                    continue;

                foreach (var loadedGroupRef in ship.loadedGroups.ToList())
                {
                    if (loadedGroupRef.Get() is not StrategicGroup loadedGroup)
                        continue;

                    if (loadedGroup.containerObjectId != ship.objectId)
                    {
                        ship.loadedGroups.RemoveAll(reference => reference.referenceId == loadedGroup.objectId);
                        continue;
                    }

                    loadedGroup.UnloadFromContainer(false);
                    if (loadedGroup.containerObjectId == null)
                    {
                        loadedGroup.CompleteCurrentNavalTransportTransfer(landingCell);
                        unloadedAnyCargo = true;
                    }
                }
            }

            if (!unloadedAnyCargo)
                return;

            var currentController = landingCell.GetHexSide();
            if (previousController != landingSide &&
                currentController == landingSide &&
                !HasFriendlyDepotConnection(landingCell, landingSide))
            {
                EnsureAdvanceBaseAtCell(landingCell, landingSide);
            }
        }

        static bool HasFriendlyDepotConnection(Cell targetCell, SideState targetSide)
        {
            if (targetCell == null || targetSide == null)
                return false;

            foreach (var baseGroup in StrategicGameState.Instance.strategicGroups.Where(
                group => group != null && group.IsBase() && group.side == targetSide))
            {
                var depot = baseGroup.GetFirstDepot();
                var depotCell = baseGroup.cell;
                if (depot == null || depotCell == null)
                    continue;

                if (depotCell == targetCell)
                    return true;
            }

            var graph = new DynamicLandSupplyNetworkingGraph() { side = targetSide };
            foreach (var baseGroup in StrategicGameState.Instance.strategicGroups.Where(
                group => group != null && group.IsBase() && group.side == targetSide))
            {
                var depot = baseGroup.GetFirstDepot();
                var depotCell = baseGroup.cell;
                if (depot == null || depotCell == null || depotCell == targetCell)
                    continue;

                var result = PathFinding<Cell>.AStar3(graph, targetCell, depotCell);
                if (result.Path?.Count > 0 && !float.IsInfinity(result.Cost))
                    return true;
            }

            return false;
        }

        static StrategicGroup EnsureAdvanceBaseAtCell(Cell targetCell, SideState targetSide)
        {
            if (targetCell == null || targetSide == null)
                return null;

            var existingBase = targetCell.StrategicGroupReferences
                .Select(reference => reference.Get())
                .FirstOrDefault(group => group != null && group.IsBase() && group.side == targetSide);
            if (existingBase?.GetFirstDepot() != null)
                return existingBase;

            var depotTemplate = StrategicGameState.Instance.landUnitTemplates.FirstOrDefault(template =>
                template != null &&
                template.unitType == LandUnitType.Supply &&
                template.name?.EqualsAny("Depot") == true);
            depotTemplate ??= StrategicGameState.Instance.landUnitTemplates.FirstOrDefault(template =>
                template?.unitType == LandUnitType.Supply);
            if (depotTemplate == null)
            {
                ServiceLocator.Get<ILoggerService>()?.LogWarning(
                    $"Naval transport transfer: unable to create advance base at {targetCell.GetLocationSummary()} because no depot template was found.");
                return existingBase;
            }

            var baseGroup = existingBase;
            if (baseGroup == null)
            {
                baseGroup = new StrategicGroup()
                {
                    name = BuildAdvanceBaseName(targetCell),
                    type = Type.Base,
                    size = StrategicUnitSize.Unspecified,
                    country = targetSide.countries.FirstOrDefault(),
                    deployState = DeployState.NotDeployed,
                    nonHistorical = true,
                    navySubMission = NavySubMission.General,
                };
                StrategicGameState.Instance.strategicGroups.Add(baseGroup);
                EntityManager.Instance.Register(baseGroup, null);
                baseGroup.MoveToCell(targetCell, false);
            }

            var depotUnit = new LandUnit()
            {
                name = BuildAdvanceBaseDepotName(targetCell),
                strength = 0,
                landUnitTemplateId = depotTemplate.objectId,
            };
            StrategicGameState.Instance.landUnits.Add(depotUnit);
            EntityManager.Instance.Register(depotUnit, null);
            depotUnit.SetStrategicGroupReference(baseGroup);
            targetCell.RefreshControlState();
            return baseGroup;
        }

        static GlobalString BuildAdvanceBaseName(Cell targetCell)
        {
            return targetCell?.GetLocationSummaryGlobalString()?.Add(advanceBaseSuffix) ?? advanceBaseSuffix.Clone();
        }

        static GlobalString BuildAdvanceBaseDepotName(Cell targetCell)
        {
            return targetCell?.GetLocationSummaryGlobalString()?.Add(depotSuffix) ?? depotSuffix.Clone();
        }

        public StrategicGroup GetDepotGroup()
        {
            var groupDepot = GetCurrentSourceDepot();
            return groupDepot?.parentGroupReference.Get();
        }

        public bool IsInDepotLocation()
        {
            var depotGroup = GetDepotGroup();
            if (depotGroup == null)
                return false;
            return cell == depotGroup.cell;
            // return depotGroup.x == x && depotGroup.y == y;
        }

        public bool IsMovingStrategically => plannedPath.Count > 0;

        public bool TryGetRecordedSupplyPath(out List<XY> pathCells)
        {
            foreach (var landUnit in WalkGroupMembers<LandUnit>())
            {
                if (TryGetDisplayableSupplyPath(landUnit?.GetSupplyTransferState()?.requestRecord?.pathCells, out pathCells))
                    return true;
            }

            foreach (var shipLog in WalkGroupMembersDeployedShips())
            {
                if (TryGetDisplayableSupplyPath(shipLog?.GetSupplyTransferState()?.requestRecord?.pathCells, out pathCells))
                    return true;
            }

            pathCells = null;
            return false;
        }

        public IEnumerable<List<XY>> GetRecordedOutgoingSupplyPaths()
        {
            foreach (var landUnit in WalkGroupMembers<LandUnit>())
            {
                if (landUnit?.GetLandUnitTemplate()?.unitType != LandUnitType.Supply)
                    continue;

                foreach (var record in landUnit.GetSupplyTransferState().requestedRecords)
                {
                    if (record.flowSupplyTons <= 1e-3)
                        continue;

                    if (!TryGetDisplayableSupplyPath(record.pathCells, out var pathCells))
                        continue;

                    pathCells.Reverse();
                    yield return pathCells;
                }
            }
        }

        static bool TryGetDisplayableSupplyPath(List<XY> candidatePath, out List<XY> pathCells)
        {
            pathCells = null;
            if (candidatePath == null || candidatePath.Count < 2)
                return false;

            var firstCell = candidatePath[0]?.GetCell();
            var lastCell = candidatePath[^1]?.GetCell();
            if (firstCell == null || lastCell == null || firstCell == lastCell)
                return false;

            pathCells = new(candidatePath);
            return true;
        }

        public IEnumerable<ShipLog> WalkGroupMembersDeployedShips()
        {
            foreach (var shipLog in WalkGroupMembers<ShipLog>())
            {
                if (shipLog.mapState == MapState.Deployed)
                {
                    yield return shipLog;
                }
            }
        }

        static float baseCommandUnitSize = 500; // battalion
        static float baseCommandUnit = 3;
        static float baseCommandCapacity = baseCommandUnitSize * baseCommandUnit;

        public float GetCommandCapacity() => baseCommandCapacity;

        public float GetCombinedCommandUsage()
        {
            var combinedCommandUsage = directMemberReferences.Sum(subordinateRef =>
            {
                var subordinate = subordinateRef.Get();
                if (subordinate is LandUnit landUnit)
                {
                    return landUnit.GetDirectCommandUsage();
                }
                else if (subordinate is StrategicGroup group && group.deployState == DeployState.Combined)
                {
                    return group.GetCombinedCommandUsage() / 3;
                }
                return 0;
            });

            return combinedCommandUsage;
        }

        public float GetCombinedCommandUsageFlatten()
        {
            var combinedCommandUsage = directMemberReferences.Sum(subordinateRef =>
            {
                var subordinate = subordinateRef.Get();
                if (subordinate is LandUnit landUnit)
                {
                    return landUnit.GetDirectCommandUsage();
                }
                else if (subordinate is StrategicGroup group && group.deployState == DeployState.Combined)
                {
                    return group.GetCombinedCommandUsageFlatten();
                }
                return 0;
            });

            return combinedCommandUsage;
        }

        public class LeaderSkillLevelInfo
        {
            public float chanceCostModifier;
            public float tacticalModifier;
            public float maneuverValue;
        }

        public static Dictionary<LeaderSkillLevel, LeaderSkillLevelInfo> leaderSkillLevelInfo = new()
        {
            { LeaderSkillLevel.Unknown, new() { chanceCostModifier = 0.3f, tacticalModifier = 0, maneuverValue=2} },
            { LeaderSkillLevel.BarelyCompetent, new() { chanceCostModifier = 0.4f, tacticalModifier = -0.1f, maneuverValue=1} },
            { LeaderSkillLevel.Average, new() { chanceCostModifier = 0.3f, tacticalModifier = 0, maneuverValue=2}},
            { LeaderSkillLevel.AboveAverage, new() { chanceCostModifier = 0.2f, tacticalModifier = 0.1f, maneuverValue=3}},
            { LeaderSkillLevel.Outstanding, new() { chanceCostModifier = 0.1f, tacticalModifier = 0.2f, maneuverValue=4}},
            { LeaderSkillLevel.Gifted, new() { chanceCostModifier = 0.0f, tacticalModifier = 0.3f, maneuverValue=5}},
        };

        public static float GetManeuverValue(Leader leader) => leaderSkillLevelInfo[leader?.navalStrategic ?? LeaderSkillLevel.Unknown].maneuverValue;

        public static float GetChanceCostModifier(float usage, float cap, LeaderSkillLevel leaderSkillLevel)
        {
            return leaderSkillLevelInfo[leaderSkillLevel].chanceCostModifier + 0.1f * Math.Max(0, usage - cap) / baseCommandUnitSize;
        }

        public LeaderSkillLevel GetLeaderSkillLevel() => leaderReference.Get()?.landOperational ?? LeaderSkillLevel.Unknown;

        public float GetChanceCostModifier()
        {
            // var usage = GetCombinedCommandUsage();
            // var cap = GetCommandCapacity();

            // var leaderSkillLevel = GetLeaderSkillLevel();
            // return leaderSkillLevelInfo[leaderSkillLevel].chanceCostModifier + 0.1f * Math.Max(0, usage - cap) / baseCommandUnitSize;
            return GetChanceCostModifier(
                GetCombinedCommandUsage(),
                GetCommandCapacity(),
                GetLeaderSkillLevel()
            );
        }

        public static float GetTacticalModifier(float usage, float cap, LeaderSkillLevel leaderSkillLevel)
        {
            var baseMod = leaderSkillLevelInfo[leaderSkillLevel].tacticalModifier;
            return baseMod / Math.Max(1, usage / cap);
        }

        public float GetTacticalModifier()
        {
            var usage = GetCombinedCommandUsage();
            var cap = GetCommandCapacity();

            var leaderSkillLevel = leaderReference.Get()?.landTactical ?? LeaderSkillLevel.Unknown;
            return GetTacticalModifier(usage, cap, leaderSkillLevel);
        }

        public (float, float, float, float) GetAverageAccumulatedChanceCostModifier() // return command usage (direct), command usage (used), acc modifier
        {
            // var currentLayerModifier = GetChanceCostModifier();

            var usageDirect = 0f;
            var usage = 0f;
            var accCostModWeight = 0f;
            foreach(var subordinateRef in directMemberReferences)
            {
                var subordinate = subordinateRef.Get();
                if(subordinate is LandUnit landUnit)
                {
                    var subUsage = landUnit.GetDirectCommandUsage();
                    usageDirect += subUsage;
                    usage += subUsage;
                }
                else if (subordinate is StrategicGroup group && group.deployState == DeployState.Combined)
                {
                    var (subUsageDirect, subUsage, subAccCostMod, _) = group.GetAverageAccumulatedChanceCostModifier();
                    usageDirect += subUsageDirect;
                    usage += subUsage / 3;
                    accCostModWeight += subUsageDirect * subAccCostMod;
                }
            }

            var currentLayerCostMod = GetChanceCostModifier(
                usage,
                GetCommandCapacity(),
                GetLeaderSkillLevel()
            );

            var accCostMod = accCostModWeight / usageDirect + currentLayerCostMod;
            return (usageDirect, usage, accCostMod, currentLayerCostMod);
        }

        public LazyLocalizedString GetCommandDesc()
        {
            var (usageDirect, usage, accCostMod, currentLayerCostMod) = GetAverageAccumulatedChanceCostModifier();
            // var costMod = GetChanceCostModifier();
            var commandCap = GetCommandCapacity();
            var tacMod = GetTacticalModifier();
            return LazyLocalizedString.MakeTemplate(
                "Command: {0}/{1}, Chance Cost: {2} (Acc Avg: {3}), Tactical Modifier: {4}",
                LazyLocalizedString.MakeRaw(usage),
                LazyLocalizedString.MakeRaw(commandCap),
                LazyLocalizedString.MakeRaw($"{currentLayerCostMod:+0.00%;-0.00%;0.00%}"),
                LazyLocalizedString.MakeRaw($"{accCostMod:+0.00%;-0.00%;0.00%}"),
                LazyLocalizedString.MakeRaw($"{tacMod:+0.00%;-0.00%;0.00%}")
            );
        }

        public bool TryGetDistanceToNextLocationInPlannedPathWithoutProgression(out float distanceKm)
        {
            if(plannedPath.Count < 2)
            {
                distanceKm = -1;
                return false;
            }
            var currentCell = cell;
            var nextCell = plannedPath[1].GetCell();
            if (currentCell == null || nextCell == null)
            {
                distanceKm = -1;
                return false;
            }

            return currentCell.TryGetDistance(nextCell, out distanceKm);
        }

        public void CheckOutOfFuelFleetGroupAndForceReturnToBase()
        {
            if(type == Type.Fleet)
            {
                // var groupCell = cell;
                var depotCell = GetDepotGroup()?.cell;
                if(depotCell != null)
                {
                    if(cell == depotCell)
                    {
                        forcedReturningToBase = false;
                    }
                    else
                    {
                        if(forcedReturningToBase || !IsFleetHasSufficientFuelToReturnHome())
                        {
                            forcedReturningToBase = true;

                            if(plannedPath.Count == 0 || (plannedPath.Count >= 1 && plannedPath[^1].GetCell() != depotCell))
                            {
                                StartReturnToBase(0);
                            }
                        }
                    }
                }
            }
        }

        public void Advance1HourForMovement()
        {
            if (!CanActStrategically)
            {
                ClearPlannedPath();
                return;
            }

            var completedStrategicPath = false;
            if (plannedPath.Count == 0)
            {
                moveProgressionKm = 0;
            }
            else
            {
                var speedKmPerHour = GetSpeedKmPerHour();
                var moveKmCap = speedKmPerHour * 1;
                while (moveKmCap > 0 && plannedPath.Count >= 2)
                {
                    var valid = TryGetDistanceToNextLocationInPlannedPathWithoutProgression(out var cellDistKm);
                    if(!valid)
                    {
                        break;
                    }

                    var nextDistKm = Math.Max(0, cellDistKm - moveProgressionKm); // 50km/hex
                    if (moveKmCap < nextDistKm)
                    {
                        moveProgressionKm += moveKmCap;
                        moveKmCap = 0;
                    }
                    else
                    {
                        var nextCell = plannedPath[1].GetCell();
                        if (!CanEnterCell(nextCell))
                        {
                            ClearPlannedPath();
                            break;
                        }

                        moveKmCap -= nextDistKm;
                        if (!MoveToCell(nextCell, true))
                        {
                            ClearPlannedPath();
                            break;
                        }

                        plannedPath.RemoveAt(0);
                        
                        moveProgressionKm = 0;
                        if (plannedPath.Count < 2)
                        {
                            plannedPath.Clear();
                            completedStrategicPath = true;
                        }
                    }
                }
            }

            if (completedStrategicPath)
            {
                HandleCompletedStrategicPath();
            }
        }

        public bool IsOnAreaCell() => cell?.IsAreaCell() ?? false; // independent or combined on area => true, Not Deployed => false
        public bool IsOnGridCell() => cell?.IsGridCell() ?? false;

        // public void ChangeAssignedMissionTo(StrategicMission mission)
        // {
        //     var currentMission = EntityManager.Instance.Get<StrategicMission>(assignedMissionObjectId);
        //     if(currentMission != null)
        //     {
        //         currentMission.groups.RemoveAll(r => r.Get() == this);
        //     }
        //     mission.groups.Add(new StrategicGroupMemberReference { referenceId = objectId });
        //     assignedMissionObjectId = mission.objectId;
        // }

        public GlobalString GetName() => name;
    }
}

