using System.Collections.Generic;
using CoreUtils;
using NavalCombatCore;
using System.Linq;
using System;
using YYZ.PathFinding;


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
            OneShotRaiding, 
        }

        public MissionType type = MissionType.Patrol;

        public StrategicMissionReference parentMissionRef = new();

        public List<StrategicMissionReference> childrenMissionRefs = new(); // If a children mission is completed or forced interrupted, its assigned group (asset) would go back to its parent mission automatically, parent mission sometimes would cancel its child mission and reclaim asset to do more important things sometimes.  

        public string sideObjectId;
        public SideState GetSide() => EntityManager.Instance.Get<SideState>(sideObjectId);

        public enum PatrolState
        {
            Assembling,
            StartToDestination,
            DestinationToStart
        }

        public PatrolState patrolState;

        public enum SupplyState
        {
            AssemblingAndLoading,
            StartToDestinationAndUnloading,
            DestinationToStartAndLoading
        }

        public SupplyState supplyState;

        // public StrategicGroupMemberReference startHq;
        // public StrategicGroupMemberReference destinationHq;
        // public string sourceDepotObjectId;
        // public string targetDepotObjectId;
        public LandUnitReference sourceDepotReference = new();
        public LandUnitReference targetDepotReference = new();

        public bool completed;
        public bool active = true;

        public enum NavalTransferState
        {
            Assembling,
            StartToDestination,
            DestinationToStart,
            Completed
        }

        public NavalTransferState navalTransferState;

        public enum OneShotRaidingState
        {
            Assembling,
            StartToDestination,
            DestinationToStart,
            // Completed is represented using new added flag instead
        }

        public OneShotRaidingState oneShotRaidingState;

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
            
            if (IsValidPatrolMission())
            {
                TransitionPatrolMission();
            }
            else if (IsValidSupplyMission())
            {
                TransitionSupplyMission();
            }
            else if(IsValidNavalTransferMission())
            {
                TransitionNavalTransferMission();
            }
            else if(IsValidGlobalRaidingMission())
            {
                TransitionGlobalRaidingMission();
            }
            else if(IsValidOneShotRaidingMission())
            {
                TransitionOneWayRaidingMission();
            }


            // Move assigned group to parent mission if it has parent mission and is completed
            if(completed)
            {
                var parentMission = parentMissionRef.Get();
                if(parentMission != null)
                {
                    foreach(var g in IterAssignedStrategicGroups().ToList())
                    {
                        g.SetAssignedMission(parentMission);
                    }
                }
                RemoveFromParent();
                StrategicGameState.Instance.missions.Remove(this);
                // Ignoring manual de-registering since full-scan based re-registering happens very frequently now
            }
        }

        public IEnumerable<StrategicGroup> IterAssignedStrategicGroups() => groups.Select(r => r.Get() as StrategicGroup).Where(g => g is StrategicGroup).Where(g => g != null);
        public IEnumerable<StrategicGroup> IterAssignedFleetGroups() => IterAssignedStrategicGroups().Where(g => g.type == StrategicGroup.Type.Fleet);

        static GlobalString oneShotRaidingPrefix = new()
        {
            english = "Raiding to ",
            japanese = "通商破壊 ",
            chineseSimplified = "破交 ",
            chineseTraditional = "破交 "
        };

        public void TransitionGlobalRaidingMission()
        {
            // Assign strategic groups with enough "integrity" to one-shot raiding missions
            
            // Ignore integrity criteria in the current version.
            var assignedFleetGroups = IterAssignedFleetGroups().ToList();
            if(assignedFleetGroups.Count > 0)
            {
                var leadGroup = assignedFleetGroups.First();
                var leadGroupSide = leadGroup.side;

                var srcCell = leadGroup.cell;

                var hostileMerchantShipTrafficCells = StrategicGameState.Instance.IterCells().Where(
                    cell => cell.CellSideInfos.Count > 0 
                        && (cell.CellSideInfos.FirstOrDefault(si => si.sideObjectId != leadGroupSide.objectId)?.merchantShipTraffic ?? 0) > 0
                ).ToList();
                var weights = hostileMerchantShipTrafficCells.Select(cell => cell.CellSideInfos.First(si => si.sideObjectId != leadGroupSide.objectId).merchantShipTraffic).ToList();
                var sampledCell = RandomUtils.Sample(hostileMerchantShipTrafficCells, weights);
                var dstCell = sampledCell;

                IGraphEnumerable<Cell> graph = new DynamicCellGraphNavy(); // TODO: Generalize to army?
                var pathCells = PathFinding<Cell>.AStar(graph, srcCell, dstCell);

                var newOneShotRaidingMission = new StrategicMission()
                {
                    name = oneShotRaidingPrefix.Add(dstCell.GetLocationSummaryGlobalString()),
                    sideObjectId = leadGroupSide.objectId,
                    type = MissionType.OneShotRaiding
                };
                StrategicGameState.Instance.missions.Add(newOneShotRaidingMission);
                EntityManager.Instance.Register(newOneShotRaidingMission, null);
                newOneShotRaidingMission.TransferTo(this);

                foreach(var g in assignedFleetGroups)
                {
                    g.SetAssignedMission(newOneShotRaidingMission);
                }

                newOneShotRaidingMission.waypoints = pathCells.Select(c => c.ToXY()).ToList();
            }
        }

        public bool IsValidGlobalRaidingMission() => type == MissionType.GlobalRaiding;

        public bool IsValidOneShotRaidingMission() => type == MissionType.OneShotRaiding && waypoints.Count >= 2;

        public void TransitionOneWayRaidingMission()
        {
            var cells = groups.Select(groupRef => (groupRef.Get() as StrategicGroup)?.cell).ToHashSet(); // is assigned groups assembled to the same hex?
            if (cells.Count == 1)
            {
                var groupingCell = cells.First();
                if (oneShotRaidingState == OneShotRaidingState.Assembling && groupingCell == GetWaypointStartCell())
                {
                    oneShotRaidingState = OneShotRaidingState.StartToDestination;
                }
                else if (oneShotRaidingState == OneShotRaidingState.StartToDestination && groupingCell == GetWaypointDestinationCell())
                {
                    oneShotRaidingState = OneShotRaidingState.DestinationToStart;
                }
                else if (oneShotRaidingState == OneShotRaidingState.DestinationToStart && groupingCell == GetWaypointStartCell())
                {
                    // oneShotRaidingState = OneShotRaidingState.StartToDestination;
                    completed = true;
                }
            }
        }

        public bool IsValidPatrolMission() => type == MissionType.Patrol && waypoints.Count >= 2;

        public void TransitionPatrolMission()
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

        public bool IsValidSupplyMission() => type == MissionType.Supply && waypoints.Count >= 2;

        public void TransitionSupplyMission()
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

        public bool IsValidNavalTransferMission() => type == MissionType.NavalTransfer && waypoints.Count >= 2;

        public void TransitionNavalTransferMission()
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
                    completed = true;
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


        public void UpdateStrategicGroup(StrategicGroup strategicGroup)
        {
            if(!active)
            {
                return;
            }

            if (type == MissionType.Patrol)
            {
                UpdateStrategicGroupPatrol(strategicGroup);
            }
            else if (type == MissionType.Supply)
            {
                UpdateStrategicGroupSupply(strategicGroup);
            }
            else if(type == MissionType.NavalTransfer)
            {
                UpdateStrategicGroupNavalTransfer(strategicGroup);
            }
            else if(type == MissionType.OneShotRaiding)
            {
                UpdateStrategicGroupOneShotRaiding(strategicGroup);
            }
            // TODO: GlobalOneShotRaiding move ships to its base?
        }

        void UpdateStrategicGroupOneShotRaiding(StrategicGroup strategicGroup)
        {
            if (strategicGroup.plannedPath.Count == 0) // Create new path if path is empty (manual waypoints has higher priority)
            {
                if (oneShotRaidingState == OneShotRaidingState.Assembling) // Assembling => Move groups to the waypoint of start
                {
                    HandleMissionAssembly(strategicGroup);
                }
                else if (oneShotRaidingState == OneShotRaidingState.StartToDestination) // StartToDestination => Move groups from start to destination
                {
                    HandleMissionStartToDestination(strategicGroup);
                }
                else if (oneShotRaidingState == OneShotRaidingState.DestinationToStart) // DestinationToStart => Move groups from destination to start
                {
                    HandleMissionDestinationToStart(strategicGroup);
                }
            }
        }

        void UpdateStrategicGroupPatrol(StrategicGroup strategicGroup)
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

        void UpdateStrategicGroupSupply(StrategicGroup strategicGroup)
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
                var targetDepot = targetDepotReference.Get();
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

        void UpdateStrategicGroupNavalTransfer(StrategicGroup strategicGroup)
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

        void HandleMissionAssembly(StrategicGroup strategicGroup)
        {
            var groupCell = strategicGroup.cell;
            var waypointStartCell = GetWaypointStartCell();
            if (groupCell != waypointStartCell)
            {
                IGraphEnumerable<Cell> graph = new DynamicCellGraphNavy();
                var pathCells = PathFinding<Cell>.AStar(graph, groupCell, waypointStartCell);
                if (pathCells.Count >= 2)
                {
                    // strategicGroup.plannedPath.AddRange(pathCells.Select(cell => new XY() { x = cell.x, y = cell.y }));
                    strategicGroup.plannedPath.AddRange(pathCells.Select(cell => cell.ToXY()));
                }
            }
        }

        void HandleMissionStartToDestination(StrategicGroup strategicGroup)
        {
            var groupCell = strategicGroup.cell;
            var waypointStartCell = GetWaypointStartCell();
            if (groupCell == waypointStartCell)
            {
                strategicGroup.plannedPath.Clear();
                strategicGroup.plannedPath.AddRange(waypoints);
            }
        }
        
        void HandleMissionDestinationToStart(StrategicGroup strategicGroup)
        {
            var groupCell = strategicGroup.cell;
            var waypointDestinationCell = GetWaypointDestinationCell();
            if (groupCell != null && waypointDestinationCell != null && groupCell == waypointDestinationCell)
            {
                strategicGroup.plannedPath.Clear();
                strategicGroup.plannedPath.AddRange(waypoints);
                strategicGroup.plannedPath.Reverse();
            }
        }
    }
}