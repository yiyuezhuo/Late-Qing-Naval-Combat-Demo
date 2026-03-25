using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using CoreUtils;
using NavalCombatCore;
using Unity.VisualScripting;
using YYZ.PathFinding;

namespace StrategicCombatCore
{
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
        
        // Independent sub states:
        public bool autoCombinable; // if true, it will convert from independent to combining when applicable 
        public bool dissolvable; // if true, it would "dissolve" automatically if combine is applicable.
        
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
                    return strategicGroupReference.Get()?.x ?? -1;
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
                    return strategicGroupReference.Get()?.y ?? -1;
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
                    return strategicGroupReference.Get()?.areaCellObjectId;
                }
                return independentAreaCellObjectId;
            }
        }

        public LeaderReference leaderReference = new();

        public List<StrategicGroupMemberReference> subordinatesCombined = new();
        // public List<StrategicGroupMemberReference> subordinatesInCommandOfChain = new();

        // public string strategicGroupId;
        public StrategicGroupReference strategicGroupReference { get; set; } = new(); // parent strategic group
        public string assignedMissionObjectId;
        public string homeBaseObjectId;

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
                var parentGroup = strategicGroupReference.Get();
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
            foreach (var subordinateRef in subordinatesCombined)
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

        public SideState side => StrategicGameState.Instance.countryToSideStateMap.GetValueOrDefault(country);
        // public HexInfo hexInfo => StrategicGameState.Instance.hexInfoMap.GetValueOrDefault((x, y));

        [XmlIgnore]
        public List<StrategicGroup> currentStack
        {
            get
            {
                var currentSide = side;
                // return hexInfo.strategicGroupReferences.Select(r => r.Get()).Where(g => g.side == currentSide).ToList();
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

        public bool IsNavy() => type == Type.Fleet;
        public bool IsArmy() => type != Type.Fleet;

        public bool IsIndependent() => deployState == DeployState.Independent;

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
                pt = pt.strategicGroupReference.Get(); 
            }
            return false;
        }

        public int GetSubUnitSize() => subordinatesCombined.Sum(r => r.GetSubUnitSize());
        public int GetStrengthMen() => subordinatesCombined.Sum(r => r.GetStrengthMen());
        public float GetShipTons() => subordinatesCombined.Sum(r => r.GetShipTons());
        public float GetCombatShipTons() => WalkGroupMembersDeployedShips().Select(shipLog => shipLog.shipClass).Where(shipClass => shipClass.IsCombatShip()).Sum(shipClass => shipClass.displacementTons);
        // WalkGroupMembersDeployedShips
        public float GetCombinedPowerPoint(bool isTop)
        {
            if (!isTop && deployState != DeployState.Combined)
                return 0;
            return subordinatesCombined.Sum(r => r.GetCombinedPowerPoint(false));
        }

        public float GetSupplyCostTonsPerDay() // Combined
        {
            var supplySum = 0f;
            foreach (var subordinateRef in subordinatesCombined)
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

            if (subordinatesCombined.All(reference => reference.referenceId != memberObjectId))
            {
                subordinatesCombined.Add(new StrategicGroupMemberReference() { referenceId = memberObjectId });
            }
        }

        internal void RemoveDirectMemberReference(string memberObjectId)
        {
            if (string.IsNullOrWhiteSpace(memberObjectId))
                return;

            subordinatesCombined.RemoveAll(reference => reference.referenceId == memberObjectId);
        }

        public static void ReassignMember(IStrategicGroupMemberReferenceable member, StrategicGroup newParentGroup)
        {
            if (member == null)
                return;

            var oldParentGroup = member.strategicGroupReference.Get();
            oldParentGroup?.RemoveDirectMemberReference(member.objectId);
            newParentGroup?.EnsureDirectMemberReference(member.objectId);
            member.strategicGroupReference.referenceId = newParentGroup?.objectId;
        }

        public void AttachMember(IStrategicGroupMemberReferenceable member) => ReassignMember(member, this);
        public void DetachMember(IStrategicGroupMemberReferenceable member) => ReassignMember(member, null);
        public void MoveMemberToGroup(IStrategicGroupMemberReferenceable member, StrategicGroup otherGroup) => ReassignMember(member, otherGroup);

        public bool ReplaceDirectMemberReference(StrategicGroupMemberReference slot, IStrategicGroupMemberReferenceable newMember)
        {
            if (slot == null || !subordinatesCombined.Contains(slot))
                return false;

            if (newMember is StrategicGroup newGroup && newGroup.objectId == objectId)
                return false;

            var oldMember = slot.Get();
            if (oldMember != null)
            {
                oldMember.strategicGroupReference.referenceId = null;
            }

            slot.referenceId = null;
            if (newMember == null)
                return true;

            var oldParentGroup = newMember.strategicGroupReference.Get();
            oldParentGroup?.RemoveDirectMemberReference(newMember.objectId);

            slot.referenceId = newMember.objectId;
            newMember.strategicGroupReference.referenceId = objectId;
            subordinatesCombined.RemoveAll(reference => !ReferenceEquals(reference, slot) && reference.referenceId == newMember.objectId);
            return true;
        }

        public bool TryRelocateIndependentGroup(Cell toCell, bool moveThroughEdge = false)
        {
            if (deployState != DeployState.Independent || toCell == null)
                return false;

            MoveToCell(toCell, moveThroughEdge);
            return true;
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
        public void MoveToCell(Cell toCell, bool moveThroughEdge)
        {
            if (toCell == null)
                return;

            // var toCell = StrategicGameState.Instance.cellMatrix[toX, toY];
            var prevCell = cell;

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

            if (moveThroughEdge && IsArmy())
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
        }
        
        public void UnloadFromContainer()
        {
            if(containerObjectId != null)
            {
                var container = EntityManager.Instance.Get<ShipLog>(containerObjectId);
                if(container != null)
                {
                    var containerGroup = container.strategicGroupReference.Get();
                    if(containerGroup != null)
                    {
                        // MoveToCell(containerGroup.x, containerGroup.y, false);
                        MoveToCell(containerGroup.cell, false);
                        container.loadedGroups.RemoveAll(r => r.referenceId == objectId);
                        containerObjectId = null;
                        ClearPlannedPath();
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
                // cellInfo.strategicGroupReferences.RemoveAll(gp => gp.referenceId == objectId);
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
                var parentGroup = strategicGroupReference.Get();
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
            return subordinatesCombined.Sum(r => r.GetCombinedSubUnitSize());
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
                var nextCell = GetPathNextCell();
                if (nextCell != null && cell.TryGetDirection(nextCell, out var edge))
                {
                    if (cell.GetEdgeSide(edge).objectId != side.objectId)
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

        public Cell GetPathNextCell()
        {
            if (plannedPath.Count >= 2)
            {
                return plannedPath[1].GetCell();
            }
            return null;
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
            foreach (var subordinateRef in subordinatesCombined)
            {
                var subordinate = subordinateRef.Get();

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
            if (subordinatesCombined.Count < 2)
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

            newGroup.AttachTo(strategicGroupReference.Get());

            if (deployState == DeployState.Independent)
            {
                // newGroup.MoveToXY(independentX, independentY, false);
                newGroup.MoveToCell(cell, false);
            }

            var transferElements = Enumerable.Range(0, subordinatesCombined.Count)
                .Where(idx => idx % 2 == 1)
                .Select(idx => subordinatesCombined[idx])
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

        public bool Combatable() => deployState == DeployState.Independent && posture != GroupPostureType.Disengaged;
        public bool NavalCombatable() => deployState == DeployState.Independent && posture != GroupPostureType.Disengaged && type == Type.Fleet;
        public bool LandCombatable() => deployState == DeployState.Independent && posture != GroupPostureType.Disengaged && type != Type.Fleet;

        /// <summary>
        /// Drive the group to its base, if disengagedHours == 0, it's a normal return, otherwise it's a retreat return.
        /// </summary>
        /// <param name="disengagedHours"></param>
        public void StartReturnToBase(int disengagedHours)
        {
            if (deployState != DeployState.Independent)
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
            plannedPath.Clear();
            
            IGraphEnumerable<Cell> graph = IsNavy() ? new DynamicCellGraphNavy() : new DynamicLandRetreatGraph(){side=side};
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
            posture = GroupPostureType.Disengaged;
            restoredHours = 24; // TODO: It's questionable to "return" to Active state sometimes, looks like we should separated those types of states.

            DoLandDisengage();
        }

        public void SetPlannedPath(List<XY> newPlannedPath)
        {
            if(newPlannedPath.Count < 2)
            {
                plannedPath.Clear();
                moveProgressionKm = 0;
                return;
            }

            var moveProgressionKmMaintained = plannedPath.Count >= 2 && plannedPath[1].GetCell() == newPlannedPath[1].GetCell();
            if(!moveProgressionKmMaintained)
            {
                moveProgressionKm = 0;
            }
            plannedPath.Clear();
            plannedPath.AddRange(newPlannedPath);
        }

        public void TryPlanPathTo(Cell dstCell)
        {
            plannedPath.Clear();
            
            IGraphEnumerable<Cell> graph = IsNavy() ? new DynamicCellGraphNavy() : new DynamicCellGraphArmy();
            var pathCells = PathFinding<Cell>.AStar(graph, cell, dstCell);
            var pathXY = pathCells.Select(c => c.ToXY()).ToList();

            SetPlannedPath(pathXY);
        }

        public bool IsFleetOnSeaOrSuppliedInHomePort()
        {
            var groupCell = cell;
            if(type == Type.Fleet)
            {
                var depotCell = GetDepotGroup()?.cell;
                if(depotCell != null && groupCell == depotCell)
                {
                    var ships = WalkGroupMembersDeployedShips().ToList();
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
                    var graph = new DynamicCellGraphNavy();
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
            RemoveFromMap();
            deployState = DeployState.NotDeployed;
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
            moveProgressionKm = 0;
        }

        public StrategicGroup GetDepotGroup()
        {
            var groupDepot = GetCurrentSourceDepot();
            return groupDepot?.strategicGroupReference.Get();
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
            var combinedCommandUsage = subordinatesCombined.Sum(subordinateRef =>
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
            var combinedCommandUsage = subordinatesCombined.Sum(subordinateRef =>
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
            foreach(var subordinateRef in subordinatesCombined)
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

                            var mission = GetAssignedMission();
                            if(mission != null && !mission.interrupted)
                            {
                                mission.interrupted = true;
                                // TODO: Notify other group assigned to this mission to return?
                            }

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

                    var nextDistKm = cellDistKm - moveProgressionKm; // 50km/hex
                    if (moveKmCap < nextDistKm)
                    {
                        moveProgressionKm += moveKmCap;
                        moveKmCap = 0;
                    }
                    else
                    {
                        moveKmCap -= nextDistKm;
                        plannedPath.RemoveAt(0);
                        // strategicGroup.MoveToXY(strategicGroup.plannedPath[0].x, strategicGroup.plannedPath[0].y, true);
                        MoveToCell(plannedPath[0].GetCell(), true); // TODO: Generalize to Area System
                        
                        moveProgressionKm = 0;
                        if (plannedPath.Count < 2)
                        {
                            plannedPath.Clear();
                        }
                    }
                }
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

