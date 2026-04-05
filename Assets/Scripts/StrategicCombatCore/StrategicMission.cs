using System.Collections.Generic;
using CoreUtils;
using NavalCombatCore;
using System.Linq;
using System;
using YYZ.PathFinding;
using System.Xml.Serialization;
using YYZ;


namespace StrategicCombatCore
{
    public partial class LandUnitReference
    {
        public string objectId;

        public LandUnit Get() => EntityManager.Instance.Get<LandUnit>(objectId);
    }

    public partial class StrategicMissionReference
    {
        public string objectId;
        public StrategicMission Get() => EntityManager.Instance.Get<StrategicMission>(objectId);

        public void Clear()
        {
            objectId = null;
        }

        public void SetTo(StrategicMission parentMission, StrategicMission setToMission)
        {
            var prevSetMission = Get();
            if(prevSetMission != null)
            {
                prevSetMission.RemoveFromParent();
            }

            setToMission.TransferTo(parentMission, this);
        }
    }

    public class Rectangle
    {
        public XY xy1;
        public XY xy2;

        public override string ToString()
        {
            return $"Rectangle({xy1}, {xy2})";
        }

        public bool IsValid() => xy1 != null && xy2 != null;
        public void GetBoundary(out int x1, out int x2, out int y1, out int y2)
        {
            x1 = Math.Min(xy1.x, xy2.x);
            x2 = Math.Max(xy1.x, xy2.x);
            y1 = Math.Min(xy1.y, xy2.y);
            y2 = Math.Max(xy1.y, xy2.y);
        }

        public IEnumerable<Cell> IterateNavyPssableCells()
        {
            var mat = StrategicGameState.Instance.cellMatrix;
            GetBoundary(out var x1, out var x2, out var y1, out var y2);
            for(var x = x1; x <= x2; x++)
            {
                for(var y = y1; y <= y2; y++)
                {
                    var cell = mat[x, y];
                    if(cell.IsNavyPassable())
                    {
                        yield return cell;
                    }
                }
            }
        }
    }

    [XmlInclude(typeof(PatrolMission))]
    [XmlInclude(typeof(SupplyMission))]
    [XmlInclude(typeof(NavalTransferMission))]
    [XmlInclude(typeof(GlobalRaidingMission))]
    [XmlInclude(typeof(OneShotPassiveSortieMission))]
    [XmlInclude(typeof(GlobalTradeProtectionMission))]
    [XmlInclude(typeof(OneShotActiveSortieMission))]
    [XmlInclude(typeof(RectAreaPatrolMission))]
    [XmlInclude(typeof(LandOperationMission))]
    public partial class StrategicMission : IObjectIdLabeled, INamed
    {
        public string objectId { get; set; }

        public IEnumerable<IObjectIdLabeled> GetSubObjects()
        {
            yield break;
        }

        // General Parameter
        public GlobalString name = new();
        public List<StrategicGroupMemberReference> groups = new(); // assigned groups
        public List<XY> waypoints = new();

        public enum MissionType
        {
            Patrol,
            Supply, // Transports load supplies from host and transfer to detination.
            NavalTransfer, // Load is handled by player before task launched. Used for unland army unit or amphibious assault.
            GlobalRaiding, // GlobalRaiding will plan OneShotRaiding mission and assign its reserves to the mission.
            OneShotPassiveSortie, // Send ships to random position having hostile trade traffic.
            GlobalTradeProtection, // Plan OneShotTradeProectectionPatrol, OneShotSweepToContact, OneShotSweepToBase for idle strategic groups.
            OneShotActiveSortie, // Move to a random position having trade traffic 
            // OneShotSweepToContact, // Send ships to lastest contact report location
            // OneShotSweepToBase, // Send ships to random hostile base.
            RectPatrolArea,
            LandOperation
        }

        public static StrategicMission Create(MissionType type)
        {
            return type switch
            {
                MissionType.Patrol => new PatrolMission(),
                MissionType.Supply => new SupplyMission(),
                MissionType.NavalTransfer => new NavalTransferMission(),
                MissionType.GlobalRaiding => new GlobalRaidingMission(),
                MissionType.OneShotPassiveSortie => new OneShotPassiveSortieMission(),
                MissionType.GlobalTradeProtection => new GlobalTradeProtectionMission(),
                MissionType.OneShotActiveSortie => new OneShotActiveSortieMission(),
                MissionType.RectPatrolArea => new RectAreaPatrolMission(),
                MissionType.LandOperation => new LandOperationMission(),
                // MissionType.GlobalTradeProctection => new GlobalTradePro
                _ => null
            };
        }

        // public MissionType type = MissionType.Patrol;

        public StrategicMissionReference parentMissionRef = new();

        public List<StrategicMissionReference> childrenMissionRefs = new(); // Child mission ends by immediately returning assigned groups to its parent when present.

        public string sideObjectId;
        public SideState GetSide() => EntityManager.Instance.Get<SideState>(sideObjectId);

        public bool active = true; // Active is expected to be set by Player in the current semantic

        // public List<StrategicGroupMemberReference> loadTargetGroups = new();
        // Non-Fleet groups in assigned groups are transported groups.
        // Naval Transfer's destination is the end cell of waypoint.

        public Cell GetWaypointStartCell()
        {
            // var xy = waypoints[0];
            // return waypoints.Count == 0 ? null : StrategicGameState.Instance.cellMatrix[xy.x, xy.y];
            return waypoints.Count == 0 ? null : waypoints[0].GetCell();
        }

        public Cell GetWaypointDestinationCell()
        {
            // var xy = waypoints[^1];
            // return waypoints.Count == 0 ? null : StrategicGameState.Instance.cellMatrix[xy.x, xy.y];
            return waypoints.Count == 0 ? null : waypoints[^1].GetCell();
        }


        public IEnumerable<T> WalkGroupMembers<T>() where T : IStrategicGroupMemberReferenceable
        {
            foreach (var groupRef in groups)
            {
                var group = groupRef.Get() as StrategicGroup;
                if (group != null)
                {
                    foreach (var obj in group.WalkGroupMembers<T>())
                    {
                        yield return obj;
                    }
                }
            }
        }
        
        public IEnumerable<ShipLog> WalkGroupMembersDeployedShips()
        {
            foreach(var shipLog in WalkGroupMembers<ShipLog>())
            {
                if(shipLog.mapState == MapState.Deployed)
                {
                    yield return shipLog;
                }
            }
        }

        public void RemoveFromParent()
        {
            var parentMission = parentMissionRef.Get();
            if(parentMission != null)
            {
                parentMission.childrenMissionRefs.RemoveAll(r => r.Get() == this);
            }

            parentMissionRef.Clear();
        }

        public void AddToMission(StrategicMission newParentMission, StrategicMissionReference missionRef = null)
        {
            if(missionRef == null)
            {
                newParentMission.childrenMissionRefs.Add(new StrategicMissionReference() { objectId = objectId });
            }
            else
            {
                missionRef.objectId = objectId;
            }
            parentMissionRef.objectId = newParentMission.objectId;
        }

        public void TransferTo(StrategicMission mission, StrategicMissionReference missionRef = null)
        {
            RemoveFromParent();
            AddToMission(mission, missionRef);
        }

        public GlobalString GetName()
        {
            return name;
        }

        public void TransitionMission()
        {
            if(!active)
                return;

            DoTransition();
        }

        public void RemoveAndTransferAssignedTo(StrategicMission other)
        {
            foreach(var g in IterAssignedStrategicGroups().ToList())
            {
                g.SetAssignedMission(other);
            }

            StrategicGameState.Instance.missions.Remove(this);
            RemoveCleanup();
        }

        protected virtual void DoTransition()
        {
            
        }

        public IEnumerable<StrategicGroup> IterAssignedStrategicGroups() => groups.Select(r => r.Get() as StrategicGroup).Where(g => g is StrategicGroup).Where(g => g != null);
        public IEnumerable<StrategicGroup> IterAssignedFleetGroups() => IterAssignedStrategicGroups().Where(g => g.type == StrategicGroup.Type.Fleet);
        public IEnumerable<StrategicGroup> IterAssignedStationedAtBaseGroups() => IterAssignedFleetGroups().Where(g => g.cell == g.GetDepotGroup()?.cell && !g.IsMovingStrategically);

        public void UpdateStrategicGroups()
        {
            if(!active)
            {
                return;
            }

            // Check supply condition

            foreach (var g in IterAssignedStrategicGroups())
            {
                UpdateStrategicGroup(g);
            }
        }

        public void UpdateStrategicGroup(StrategicGroup strategicGroup)
        {
            DoUpdateStrategicGroup(strategicGroup);
        }

        protected virtual void DoUpdateStrategicGroup(StrategicGroup strategicGroup)
        {
            
        }

        // Helpers
        protected void HandleMissionAssembly(StrategicGroup strategicGroup)
        {
            var groupCell = strategicGroup.cell;
            var waypointStartCell = GetWaypointStartCell();
            if (groupCell != waypointStartCell)
            {
                TryPlanPathWithSupplyCheck(strategicGroup, waypointStartCell);
                // IGraphEnumerable<Cell> graph = new DynamicCellGraphNavy();
                // var pathCells = PathFinding<Cell>.AStar(graph, groupCell, waypointStartCell);
                // if (pathCells.Count >= 2)
                // {
                //     // strategicGroup.plannedPath.AddRange(pathCells.Select(cell => new XY() { x = cell.x, y = cell.y }));
                //     strategicGroup.plannedPath.AddRange(pathCells.Select(cell => cell.ToXY()));
                // }
            }
        }


        public void TryPlanPathWithSupplyCheck(StrategicGroup strategicGroup, Cell dst)
        {
            // Check supply condition if fleet group is in its home port (depot base cell)
            if(!strategicGroup.IsFleetReadyForMissionDeparture())
            {
                return;
            }

            strategicGroup.TryPlanPathTo(dst);
        }

        public void TrySetPathWithSupplyCheck(StrategicGroup strategicGroup, List<XY> _waypoints)
        {
            if(!strategicGroup.IsFleetReadyForMissionDeparture())
            {
                return;
            }

            strategicGroup.SetPlannedPath(_waypoints);
        }

        protected void HandleMissionStartToDestination(StrategicGroup strategicGroup)
        {
            var groupCell = strategicGroup.cell;
            var waypointStartCell = GetWaypointStartCell();
            if (groupCell == waypointStartCell)
            {
                // strategicGroup.plannedPath.Clear();
                // strategicGroup.plannedPath.AddRange(waypoints);
                TrySetPathWithSupplyCheck(strategicGroup, waypoints);
            }
        }
        
        protected void HandleMissionDestinationToStart(StrategicGroup strategicGroup)
        {
            var groupCell = strategicGroup.cell;
            var waypointDestinationCell = GetWaypointDestinationCell();
            if (groupCell != null && waypointDestinationCell != null && groupCell == waypointDestinationCell)
            {
                // strategicGroup.plannedPath.Clear();
                // strategicGroup.plannedPath.AddRange(waypoints);
                // strategicGroup.plannedPath.Reverse();
                var reversedWaypoints = waypoints.ToList();
                reversedWaypoints.Reverse();
                TrySetPathWithSupplyCheck(strategicGroup, reversedWaypoints);
            }
        }

        public void RemoveCleanup()
        {
            // Maintain mission tree
            RemoveFromParent();

            foreach(var missionRef in childrenMissionRefs.ToList())
            {
                missionRef.Get()?.RemoveFromParent();
            }

            // Maintain assigned membership
            foreach(var group in IterAssignedStrategicGroups().ToList())
            {
                group.SetAssignedMission(null);
            }
        }

        protected Cell RollFriendlyTrafficCell(SideState sideMe)
        {
            var friendlyMerchantShipTrafficCells = StrategicGameState.Instance.IterCells().Where(
                cell => cell.CellSideInfos.Count > 0 
                    && (cell.CellSideInfos.FirstOrDefault(si => si.sideObjectId == sideMe.objectId)?.merchantShipTraffic ?? 0) > 0
            ).ToList();
            var weights = friendlyMerchantShipTrafficCells.Select(cell => cell.CellSideInfos.First(si => si.sideObjectId == sideMe.objectId).merchantShipTraffic).ToList();
            var sampledCell = RandomUtils.Sample(friendlyMerchantShipTrafficCells, weights);
            return sampledCell;
        }

        protected Cell RollHostileTrafficCell(SideState sideMe)
        {
            var friendlyMerchantShipTrafficCells = StrategicGameState.Instance.IterCells().Where(
                cell => cell.CellSideInfos.Count > 0 
                    && (cell.CellSideInfos.FirstOrDefault(si => si.sideObjectId != sideMe.objectId)?.merchantShipTraffic ?? 0) > 0
            ).ToList();
            var weights = friendlyMerchantShipTrafficCells.Select(cell => cell.CellSideInfos.First(si => si.sideObjectId != sideMe.objectId).merchantShipTraffic).ToList();
            var sampledCell = RandomUtils.Sample(friendlyMerchantShipTrafficCells, weights);
            return sampledCell;
        }

        protected Cell RollHostilePortCell(SideState sideMe)
        {
            var hostilePortCells = StrategicGameState.Instance.landUnits.Where(landUnit =>
            {
                var template = landUnit.GetLandUnitTemplate();
                if(template == null)
                    return false;
                
                var isSupply = template.unitType == LandUnitType.Supply;
                if(!isSupply)
                    return false;
                
                if(landUnit.side == sideMe)
                    return false;
                
                var cell = landUnit.cell;
                return cell.IsNavyPassable();
            }).Select(port => port.cell).ToList();

            if(hostilePortCells.Count == 0)
                return null;

            return RandomUtils.Sample(hostilePortCells);
        }

        protected void PlanReturnToBasePathForNonBasedFleet(bool force=false)
        {
            foreach(var group in IterAssignedFleetGroups())
            {
                if(force || group.plannedPath.Count == 0)
                {
                    group.StartReturnToBase(0);
                    // var depotCell = group.GetDepotGroup()?.cell;
                    // if(depotCell != null && group.cell != depotCell)
                    // {
                    //     group.TryPlanPathTo(depotCell);
                    // }
                }
            }
        }

        protected bool IsOperational() => active;

        void EndNow()
        {
            if(!StrategicGameState.Instance.missions.Contains(this))
            {
                return;
            }

            var parentMission = parentMissionRef.Get();
            if(parentMission != null)
            {
                RemoveAndTransferAssignedTo(parentMission);
            }
            else
            {
                StrategicGameState.Instance.missions.Remove(this);
                RemoveCleanup();
            }
        }

        public void CompleteNow()
        {
            EndNow();
        }

        public void InterruptNow()
        {
            EndNow();
        }

        public virtual bool ShouldInterruptOnCombatFailure() => false;

        public virtual bool IsNavyOnly() => false;
    }

    public class NavyMission : StrategicMission
    {
        public override bool IsNavyOnly() => true;
    }

    public class PatrolMission : NavyMission
    {
        public enum PatrolState
        {
            Assembling,
            StartToDestination,
            DestinationToStart
        }

        public PatrolState patrolState;

        // public bool IsValidPatrolMission() => type == MissionType.Patrol && waypoints.Count >= 2;
        public bool IsValid() => waypoints.Count >= 2;

        protected override void DoTransition()
        {
            var cells = groups.Select(groupRef => (groupRef.Get() as StrategicGroup)?.cell).ToHashSet(); // is assigned groups assembled to the same hex?
            if (cells.Count == 1)
            {
                var groupingCell = cells.First();
                if (patrolState == PatrolState.Assembling && groupingCell == GetWaypointStartCell())
                {
                    patrolState = PatrolState.StartToDestination;
                }
                else if (patrolState == PatrolState.StartToDestination && groupingCell == GetWaypointDestinationCell())
                {
                    patrolState = PatrolState.DestinationToStart;
                }
                else if (patrolState == PatrolState.DestinationToStart && groupingCell == GetWaypointStartCell())
                {
                    patrolState = PatrolState.StartToDestination;
                }
            }
        }

        protected override void DoUpdateStrategicGroup(StrategicGroup strategicGroup)
        {
            if (strategicGroup.plannedPath.Count == 0) // Create new path if path is empty (manual waypoints has higher priority)
            {
                if (patrolState == PatrolState.Assembling) // Assembling => Move groups to the waypoint of start
                {
                    HandleMissionAssembly(strategicGroup);
                }
                else if (patrolState == PatrolState.StartToDestination) // StartToDestination => Move groups from start to destination
                {
                    HandleMissionStartToDestination(strategicGroup);
                }
                else if (patrolState == PatrolState.DestinationToStart) // DestinationToStart => Move groups from destination to start
                {
                    HandleMissionDestinationToStart(strategicGroup);
                }
            }
        }
    }

    public class SupplyMission : NavyMission
    {
        public enum SupplyState
        {
            AssemblingAndLoading,
            StartToDestinationAndUnloading,
            DestinationToStartAndLoading
        }

        public SupplyState supplyState;

        // public bool IsValidSupplyMission() => type == MissionType.Supply && waypoints.Count >= 2;
        public bool IsValidSupplyMission() => waypoints.Count >= 2;

        LandUnit GetWaypointDestinationDepot()
        {
            var destinationCell = GetWaypointDestinationCell();
            if (destinationCell == null)
                return null;

            var missionSide = GetSide();
            var baseGroups = destinationCell.StrategicGroupReferences
                .Select(reference => reference.Get())
                .Where(group => group != null && group.IsBase())
                .OrderByDescending(group => group.side == missionSide);

            foreach (var baseGroup in baseGroups)
            {
                var depot = baseGroup.GetFirstDepot();
                if (depot != null)
                    return depot;
            }

            return null;
        }

        protected override void DoTransition()
        {
            var _groups = groups.Select(groupRef => groupRef.Get() as StrategicGroup).Where(g => g != null).ToList();
            var cells = _groups.Select(g => g.cell).Where(cell => cell != null).ToHashSet(); // is assigned groups assembled to the same hex?
            var ships = WalkGroupMembersDeployedShips().ToList();
            if (cells.Count == 1)
            {
                var groupingCell = cells.First();
                if (supplyState == SupplyState.AssemblingAndLoading && groupingCell == GetWaypointStartCell())
                {
                    if (ships.All(ship => ship.GetSupplyPercent() >= 0.95))
                    {
                        supplyState = SupplyState.StartToDestinationAndUnloading;
                    }
                }
                else if (supplyState == SupplyState.StartToDestinationAndUnloading && groupingCell == GetWaypointDestinationCell())
                {
                    if (ships.All(ship => ship.GetSupplyPercent() <= 0.5))
                    {
                        supplyState = SupplyState.DestinationToStartAndLoading;
                    }
                }
                else if (supplyState == SupplyState.DestinationToStartAndLoading && groupingCell == GetWaypointStartCell())
                {
                    if (ships.All(ship => ship.GetSupplyPercent() >= 0.95))
                    {
                        supplyState = SupplyState.StartToDestinationAndUnloading;
                    }
                }
            }
        }


        protected override void DoUpdateStrategicGroup(StrategicGroup strategicGroup)
        {
            if (strategicGroup.plannedPath.Count == 0)
            {
                if (supplyState == SupplyState.AssemblingAndLoading) // Assembling => Move groups to the waypoint of start
                {
                    HandleMissionAssembly(strategicGroup);
                }
                else if (supplyState == SupplyState.StartToDestinationAndUnloading) // StartToDestination => Move groups from start to destination
                {
                    HandleMissionStartToDestination(strategicGroup);
                }
                else if (supplyState == SupplyState.DestinationToStartAndLoading) // DestinationToStart => Move groups from destination to start
                {
                    HandleMissionDestinationToStart(strategicGroup);
                }
            }

            // Transfer supply from ship to destination here or in the supply step
            if (supplyState == SupplyState.StartToDestinationAndUnloading)
            {
                var targetDepot = GetWaypointDestinationDepot();
                if (targetDepot != null && strategicGroup.cell == targetDepot.cell)
                {
                    foreach (var ship in WalkGroupMembersDeployedShips())
                    {
                        if (ship?.shipClass.type == ShipType.Transport)
                        {
                            var returnToBaseThresholdTons = ship.GetSupplyCapTons() * 0.1;
                            var transferableTons = Math.Max(0, ship.supplyTons - returnToBaseThresholdTons);
                            if (transferableTons > 0)
                            {
                                ship.supplyTons -= transferableTons;
                                targetDepot.supplyTons += transferableTons;

                                ServiceLocator.Get<ILoggerService>().Log($"Supply Transfer: {ship.namedShip.name.GetMergedName()} -> {targetDepot.name.GetMergedName()} ({transferableTons})");
                            }
                        }
                    }
                }
            }
        }
    }

    public class NavalTransferMission : StrategicMission
    {
        public enum NavalTransferState
        {
            Assembling,
            StartToDestination,
            DestinationToStart,
            Completed
        }

        public NavalTransferState navalTransferState;


        // public bool IsValidNavalTransferMission() => type == MissionType.NavalTransfer && waypoints.Count >= 2;
        public bool IsValidNavalTransferMission() => waypoints.Count >= 2;

        protected override void DoTransition()
        {
            var _groups = groups.Select(groupRef => groupRef.Get() as StrategicGroup).Where(g => g != null).ToList();
            var cells = _groups.Select(g => g.cell).Where(cell => cell != null).ToHashSet();
            if (navalTransferState == NavalTransferState.Assembling)
            {
                if (cells.Count == 1)
                {
                    var groupingCell = cells.First();
                    if (groupingCell == GetWaypointStartCell())
                    {
                        // Do Split & Load
                        var transportShips = WalkGroupMembersDeployedShips().Where(shipLog => shipLog?.shipClass?.type == ShipType.Transport).ToList();
                        var cargoGroups = _groups.Where(g => g.type != StrategicGroup.Type.Fleet).ToList();
                        TransferSplitter.SequenceSplit(transportShips, cargoGroups);

                        navalTransferState = NavalTransferState.StartToDestination;
                    }
                }
            }
            else if (navalTransferState == NavalTransferState.StartToDestination)
            {
                var fleetGroups = _groups.Where(g => g.type == StrategicGroup.Type.Fleet).ToList();
                var fleetCells = fleetGroups.Select(g => g.cell).Where(cell => cell != null).ToHashSet();
                if (fleetCells.Count == 1)
                {
                    var groupingCell = fleetCells.First();
                    if (groupingCell == GetWaypointDestinationCell())
                    {
                        // Do Unload & pre-recombine
                        foreach(var fleetGroup in fleetGroups)
                        {
                            foreach(var shipLog in fleetGroup.WalkGroupMembersDeployedShips())
                            {
                                if(shipLog?.shipClass?.type == ShipType.Transport)
                                {
                                    foreach(var loadedGroupRef in shipLog.loadedGroups.ToList())
                                    {
                                        var loadedGroup = loadedGroupRef.Get() as StrategicGroup;
                                        if(loadedGroup != null)
                                        {
                                            // loadedGroup.MoveToXY(groupingCell.x, groupingCell.y, false);
                                            loadedGroup.UnloadFromContainer();
                                        }
                                    }
                                }
                            }
                        }

                        navalTransferState = NavalTransferState.DestinationToStart;
                    }
                }
            }
            else if(navalTransferState == NavalTransferState.DestinationToStart)
            {
                var cargoGroupsInStartCell = _groups.Where(g =>
                    g.cell == GetWaypointStartCell() &&
                    g.type != StrategicGroup.Type.Fleet &&
                    g.deployState == StrategicGroup.DeployState.Independent // Though they're independent when assigned, they may become NotDeployed in trasport process.
                ).ToList();

                if (cargoGroupsInStartCell.Count == 0)
                {
                    navalTransferState = NavalTransferState.Completed;
                    CompleteNow();
                }
                else
                {
                    var fleetGroups = _groups.Where(g => g.type == StrategicGroup.Type.Fleet).ToList();
                    var fleetCells = fleetGroups.Select(g => g.cell).Where(cell => cell != null).ToHashSet();

                    if (fleetCells.Count == 1)
                    {
                        var groupingCell = cells.First();
                        if (groupingCell == GetWaypointStartCell())
                        {
                            // Do Split & Load
                            var transportShips = WalkGroupMembersDeployedShips().Where(shipLog => shipLog?.shipClass?.type == ShipType.Transport).ToList();
                            TransferSplitter.SequenceSplit(transportShips, cargoGroupsInStartCell);

                            navalTransferState = NavalTransferState.StartToDestination;
                        }
                    }
                }
            }
        }

        protected override void DoUpdateStrategicGroup(StrategicGroup strategicGroup)
        {
            if (strategicGroup.plannedPath.Count == 0)
            {
                if (navalTransferState == NavalTransferState.Assembling)
                {
                    HandleMissionAssembly(strategicGroup);
                }
                else if (navalTransferState == NavalTransferState.StartToDestination && strategicGroup.type == StrategicGroup.Type.Fleet)
                {
                    HandleMissionStartToDestination(strategicGroup);
                }
                else if(navalTransferState == NavalTransferState.DestinationToStart && strategicGroup.type == StrategicGroup.Type.Fleet)
                {
                    HandleMissionDestinationToStart(strategicGroup);
                }
            }
        }
    }

    public class GlobalSortiePlannarMission : NavyMission
    {
        public StrategicMission MakeOneChildMissionAndAssignGroups(List<StrategicGroup> assignedFleetGroups, bool active)
        {
            if(assignedFleetGroups.Count == 0)
                return null;

            var leadGroupSide = assignedFleetGroups.First().side;

            OneShotSortieMission newMission = active ? new OneShotActiveSortieMission() : new OneShotPassiveSortieMission();
            // newMission.name = missionName;
            newMission.sideObjectId = leadGroupSide.objectId;

            StrategicGameState.Instance.missions.Add(newMission);
            EntityManager.Instance.Register(newMission, null);
            newMission.TransferTo(this);

            foreach(var g in assignedFleetGroups)
            {
                g.SetAssignedMission(newMission);
            }

            return newMission;
        }

        public StrategicMission MakeOneChildMissionAndAssignGroupsToCell(List<StrategicGroup> assignedFleetGroups, bool active, Cell dstCell, GlobalString prefix)
        {
            if(dstCell == null)
                return null;

            var leadGroup = assignedFleetGroups.First();
            var leadGroupSide = leadGroup.side;
            var srcCell = leadGroup.cell;

            IGraphEnumerable<Cell> graph = new DynamicCellGraphNavy(){movingSide=leadGroupSide}; // TODO: Generalize to army?
            var pathCells = PathFinding<Cell>.AStar(graph, srcCell, dstCell);

            var missionName = prefix.Add(dstCell.GetLocationSummaryGlobalString());
            var newMission = MakeOneChildMissionAndAssignGroups(assignedFleetGroups, active);
            newMission.name = missionName;
            newMission.waypoints = pathCells.Select(c => c.ToXY()).ToList();
            
            return newMission;
        }
    }

    public class GlobalRaidingMission : GlobalSortiePlannarMission
    {
        static GlobalString oneShotRaidingPrefix = new()
        {
            english = "Raiding to ",
            japanese = "通商破壊 ",
            chineseSimplified = "破交 ",
            chineseTraditional = "破交 "
        };

        // public bool IsValidOneShotRaidingMission() => type == MissionType.OneShotRaiding && waypoints.Count >= 2;
        

        protected override void DoTransition()
        {
            // Assign strategic groups with enough "integrity" to one-shot raiding missions
            
            // Ignore integrity criteria in the current version.
            // var assignedFleetGroups = IterAssignedFleetGroups().ToList();
            var assignedFleetGroups = IterAssignedStationedAtBaseGroups().ToList();

            if(assignedFleetGroups.Count > 0)
            {
                var leadGroupSide = assignedFleetGroups.First().side;
                var dstCell = RollHostileTrafficCell(leadGroupSide);
                MakeOneChildMissionAndAssignGroupsToCell(assignedFleetGroups, false, dstCell, oneShotRaidingPrefix);
            }
        }

        // public bool IsValidGlobalRaidingMission() => type == MissionType.GlobalRaiding;
        public bool IsValid() => true;

        protected override void DoUpdateStrategicGroup(StrategicGroup strategicGroup)
        {
            // TODO: Move groups back to its base
            PlanReturnToBasePathForNonBasedFleet();
        }
    }

    public class GlobalTradeProtectionMission : GlobalSortiePlannarMission
    {
        static GlobalString tradeProtectionPatrolPrefix = new()
        {
            english = "Trade Protection Patrol to ",
            japanese = "貿易保護パトロールへ ",
            chineseSimplified = "贸易保护巡逻至 ",
            chineseTraditional = "貿易保護巡邏至 "
        };

        static GlobalString sweepPrefix = new()
        {
            english = "Sweep to ",
            japanese = "掃討へ ",
            chineseSimplified = "扫荡至 ",
            chineseTraditional = "掃蕩至 "
        };

        static GlobalString interceptionPrefix = new()
        {
            english = "Intercept to ",
            japanese = "対抗へ ",
            chineseSimplified = "拦截至 ",
            chineseTraditional = "拦截至 "
        };

        protected override void DoTransition()
        {
            // Assign strategic groups with enough "integrity" to one-shot raiding missions
            
            // Ignore integrity criteria in the current version.
            // If no contact, create random patrol
            // TODO: Ships should have a tendency to be reservation instead of run random sortie

            var runningDirectInterception = false;
            var missionSide = GetSide();

            if(missionSide != null)
            {
                var gameState = StrategicGameState.Instance;
                var threatContact = gameState.PickNavalContactReportByThreat(missionSide);
                if(threatContact != null)
                {
                    runningDirectInterception = true;

                    // Cancel random search sortie missions and send ships to threat contact location
                    foreach(var childMissionRef in childrenMissionRefs.ToList())
                    {
                        var childMission = childMissionRef.Get();
                        if(childMission != null)
                        {
                            childMission.RemoveAndTransferAssignedTo(this);
                        }
                    }

                    var threatContactCell = threatContact.cell;
                    // Don't create a mission, send direct controlled group directly
                    // But for a more complex scenario, we may need to assembly somewhere and then sortie to the target.
                    var availableFleetGroups = IterAssignedFleetGroups().Where(g => !g.forcedReturningToBase).ToList();
                    foreach(var assignedFleetGroup in availableFleetGroups)
                    {
                        if(assignedFleetGroup.plannedPath.Count == 0 || assignedFleetGroup.plannedPath[^1].GetCell() != threatContactCell)
                        {
                            TryPlanPathWithSupplyCheck(assignedFleetGroup, threatContactCell);
                        }
                    }
                }
            }
            
            if(!runningDirectInterception)
            {
                // Assign idle group to do random sortie
                // var assignedFleetGroups = IterAssignedFleetGroups().ToList();
                var assignedFleetGroups = IterAssignedStationedAtBaseGroups().ToList();

                if(assignedFleetGroups.Count > 0)
                {
                    var leadGroupSide = assignedFleetGroups.First().side;

                    Cell dstCell;
                    GlobalString prefix;

                    // TODO: Add Idle probability
                    if(RandomUtils.D100F() < 75)
                    {
                        dstCell = RollFriendlyTrafficCell(leadGroupSide);
                        prefix = tradeProtectionPatrolPrefix;
                    }
                    else
                    {
                        dstCell = RollHostilePortCell(leadGroupSide);
                        prefix = sweepPrefix;
                    }

                    MakeOneChildMissionAndAssignGroupsToCell(assignedFleetGroups, true, dstCell, prefix);
                }
            }
        }

        protected override void DoUpdateStrategicGroup(StrategicGroup strategicGroup)
        {
            // TODO: Move groups back to its base
            PlanReturnToBasePathForNonBasedFleet();
        }
    }

    public class OneShotSortieMission : NavyMission
    {
        public enum OneShotSortieState
        {
            Assembling,
            StartToDestination,
            DestinationToStart,
            // Completed is represented using new added flag instead
        }

        public OneShotSortieState oneShotSortieState;

        public bool IsValidOneShotRaidingMission() => waypoints.Count >= 2;


        protected override void DoTransition()
        {
            var cells = groups.Select(groupRef => (groupRef.Get() as StrategicGroup)?.cell).ToHashSet(); // is assigned groups assembled to the same hex?
            if (cells.Count == 1)
            {
                var groupingCell = cells.First();
                if (oneShotSortieState == OneShotSortieState.Assembling && groupingCell == GetWaypointStartCell())
                {
                    oneShotSortieState = OneShotSortieState.StartToDestination;
                }
                else if (oneShotSortieState == OneShotSortieState.StartToDestination && groupingCell == GetWaypointDestinationCell())
                {
                    oneShotSortieState = OneShotSortieState.DestinationToStart;
                }
                else if (oneShotSortieState == OneShotSortieState.DestinationToStart && groupingCell == GetWaypointStartCell())
                {
                    // oneShotRaidingState = OneShotRaidingState.StartToDestination;
                    CompleteNow();
                }
            }
        }

        protected override void DoUpdateStrategicGroup(StrategicGroup strategicGroup)
        {
            if (strategicGroup.plannedPath.Count == 0) // Create new path if path is empty (manual waypoints has higher priority)
            {
                if (oneShotSortieState == OneShotSortieState.Assembling) // Assembling => Move groups to the waypoint of start
                {
                    HandleMissionAssembly(strategicGroup);
                }
                else if (oneShotSortieState == OneShotSortieState.StartToDestination) // StartToDestination => Move groups from start to destination
                {
                    HandleMissionStartToDestination(strategicGroup);
                }
                else if (oneShotSortieState == OneShotSortieState.DestinationToStart) // DestinationToStart => Move groups from destination to start
                {
                    HandleMissionDestinationToStart(strategicGroup);
                }
            }

            UpdatePosture(strategicGroup);
        }

        protected virtual void UpdatePosture(StrategicGroup strategicGroup)
        {
        }
    }

    public class OneShotPassiveSortieMission : OneShotSortieMission
    {
        public override bool ShouldInterruptOnCombatFailure() => true;

        protected override void UpdatePosture(StrategicGroup strategicGroup)
        {
            if(strategicGroup.posture == StrategicGroup.GroupPostureType.Active)
            {
                strategicGroup.posture = StrategicGroup.GroupPostureType.Passive;
            }
        }
    }

    public class OneShotActiveSortieMission : OneShotSortieMission
    {
        public override bool ShouldInterruptOnCombatFailure() => true;

        protected override void UpdatePosture(StrategicGroup strategicGroup)
        {
            if(strategicGroup.posture == StrategicGroup.GroupPostureType.Passive)
            {
                strategicGroup.posture = StrategicGroup.GroupPostureType.Active;
            }
        }
    }

    public partial class RectAreaPatrolMission : NavyMission
    {
        public Rectangle rectangle = new();

        protected override void DoTransition()
        {
            var availableFleetGroups = IterAssignedStationedAtBaseGroups().ToList();
            foreach(var assignedFleetGroup in availableFleetGroups)
            {
                if(assignedFleetGroup.plannedPath.Count == 0)
                {
                    var dstCell = RollRectangleCell();
                    if(dstCell != null)
                    {
                        TryPlanPathWithSupplyCheck(assignedFleetGroup, dstCell);
                    }
                }
            }
        }

        protected Cell RollRectangleCell()
        {
            var cells = rectangle.IterateNavyPssableCells().ToList();
            if(cells.Count > 0)
            {
                var sampledCell = RandomUtils.Sample(cells);
                return sampledCell;
            }
            return null;
        }

        protected override void DoUpdateStrategicGroup(StrategicGroup strategicGroup)
        {
            PlanReturnToBasePathForNonBasedFleet();
        }
    }

    public class LandOperationMission : StrategicMission
    {
        
    }
}
