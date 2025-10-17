using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using CoreUtils;
using NavalCombatCore;
using YYZ.PathFinding;

namespace StrategicCombatCore
{


    public class SerializedCells
    {
        public int width;
        public int height;
        public List<Cell> records;
    }

    public class StrategicGameState : AbstractGameState
    {
        [XmlIgnore]
        public Cell[,] cellMatrix;

        public SerializedCells serializedCells
        {
            get
            {
                var records = new List<Cell>();
                for (var x = 0; x < GetMapWidth(); x++)
                {
                    for (var y = 0; y < GetMapHeight(); y++)
                    {
                        records.Add(cellMatrix[x, y]);
                    }
                }
                return new()
                {
                    width = GetMapWidth(),
                    height = GetMapHeight(),
                    records=records
                };
            }
            set
            {
                cellMatrix = new Cell[value.width, value.height];

                foreach (var cell in value.records)
                {
                    cellMatrix[cell.x, cell.y] = cell;
                }

                mapRebuilt?.Invoke(this, EventArgs.Empty);
            }
        }

        // public List<StrategicLocationLabel> labels = new();

        public HighCommand highCommand = new();

        public List<LandUnitTemplate> landUnitTemplates = new();
        public List<LandUnit> landUnits = new();
        public List<Weapon> weapons = new();
        public List<StrategicGroup> strategicGroups = new();

        public StrategicScenarioState scenarioState = new();

        public List<SideState> sideStates = new();

        public List<StrategicMission> missions = new();

        [XmlIgnore]
        public Dictionary<Country, SideState> countryToSideStateMap = new();

        public event EventHandler mapRebuilt;
        public event EventHandler<(int, int)> mapCellUpdated;
        public event EventHandler edgeFeatureUpdated;

        public void InvokeMapCellUpdated(int x, int y) => mapCellUpdated?.Invoke(this, (x, y));

        public void RebuildCacheForSideStates()
        {
            countryToSideStateMap.Clear();
            foreach (var sideState in sideStates)
            {
                foreach (var country in sideState.countries)
                {
                    countryToSideStateMap[country] = sideState;
                }
            }
        }

        public void AddEdgeFeature(Cell cell1, Cell cell2, EdgeFeatureType edgeFeatureType)
        {

            if (cell1.TryGetDirection(cell2, out var edgeDirection))
            {
                cell1.GetEdgeDirectionsFor(edgeFeatureType).Add(edgeDirection);
            }
            if (cell2.TryGetDirection(cell1, out edgeDirection))
            {
                cell2.GetEdgeDirectionsFor(edgeFeatureType).Add(edgeDirection);
            }
            edgeFeatureUpdated?.Invoke(this, EventArgs.Empty);
        }

        public void DeleteEdgeFeature(Cell cell1, Cell cell2, EdgeFeatureType edgeFeatureType)
        {
            if (cell1.TryGetDirection(cell2, out var edgeDirection))
            {
                cell1.GetEdgeDirectionsFor(edgeFeatureType).RemoveAll(d => d == edgeDirection);
            }
            if (cell2.TryGetDirection(cell1, out edgeDirection))
            {
                cell2.GetEdgeDirectionsFor(edgeFeatureType).RemoveAll(d => d == edgeDirection);
            }
            edgeFeatureUpdated?.Invoke(this, EventArgs.Empty);
        }

        public int GetMapWidth() => cellMatrix.GetLength(0);
        public int GetMapHeight() => cellMatrix.GetLength(1);


        public void SetMapCellTerrain(int x, int y, TerrainType terrainType)
        {
            // terrainMatrix[x, y] = terrainType;
            cellMatrix[x, y].terrain = terrainType;

            mapCellUpdated?.Invoke(this, (x, y));
        }

        public void SetMapControlSide(int x, int y, string sideStateObjectId)
        {
            cellMatrix[x, y].sideObjectIdHex = sideStateObjectId;

            mapCellUpdated?.Invoke(this, (x, y));
        }

        public void ToggleCoast(int x, int y)
        {
            var cell = cellMatrix[x, y];
            cell.IsCoast = !cell.IsCoast;

            mapCellUpdated?.Invoke(this, (x, y));
        }

        public void UpdateTo(StrategicGameState newInstance)
        {
            // terrainMatrix = newInstance.terrainMatrix;
            cellMatrix = newInstance.cellMatrix;
            // labels = newInstance.labels;
            highCommand = newInstance.highCommand;
            landUnitTemplates = newInstance.landUnitTemplates;
            landUnits = newInstance.landUnits;
            weapons = newInstance.weapons;
            strategicGroups = newInstance.strategicGroups;
            // hexInfoMap = newInstance.hexInfoMap;
            scenarioState = newInstance.scenarioState;

            shipLogs = newInstance.shipLogs;

            sideStates = newInstance.sideStates;

            missions = newInstance.missions;

            mapRebuilt?.Invoke(this, EventArgs.Empty);
            edgeFeatureUpdated?.Invoke(this, EventArgs.Empty);

            RebuildCacheForSideStates();
        }

        public void GenerateTerrainMatrix(int width, int height)
        {
            // terrainMatrix = new TerrainType[width, height];
            cellMatrix = new Cell[width, height];

            for (int x = 0; x < cellMatrix.GetLength(0); x++)
                for (int y = 0; y < cellMatrix.GetLength(1); y++)
                    cellMatrix[x, y] = new();

            mapRebuilt?.Invoke(this, EventArgs.Empty);
        }

        public IEnumerable<(Cell, Cell, EdgeDirection)> IterateCellPairsFor(EdgeFeatureType edgeFeatureType)
        {
            for (int x = 0; x < cellMatrix.GetLength(0); x++)
            {
                for (int y = 0; y < cellMatrix.GetLength(1); y++)
                {
                    var cell = cellMatrix[x, y];

                    foreach (var edgeDirection in cell.GetEdgeDirectionsFor(edgeFeatureType))
                    {
                        var neighbor = cell.GetNeighbor(edgeDirection);
                        yield return (cell, neighbor, edgeDirection);
                    }
                }
            }
        }

        // public IEnumerable<StrategicGroup> GetIndependentStrategicGroups() => strategicGroups.Where(group => group.deployState == StrategicGroup.DeployState.Independent);
        public IEnumerable<StrategicGroup> GetIndependentStrategicGroups()
        {
            foreach (var group in strategicGroups)
            {
                if (group.deployState == StrategicGroup.DeployState.Independent)
                    yield return group;
            }
        }

        public IEnumerable<StrategicGroup> GetObservabledStrategicGroups()
        {
            foreach (var group in strategicGroups)
            {
                if (group.deployState == StrategicGroup.DeployState.Independent)
                {
                    if (IsGroupObservable(group))
                        yield return group;
                }
            }
        }

        public IEnumerable<StrategicGroup> GetOrderedObservableStrategicGroups()
        {
            var relatedCells = strategicGroups.Where(group => group.deployState == StrategicGroup.DeployState.Independent).Select(group => group.cell).ToHashSet();
            foreach (var cell in relatedCells)
            {
                foreach (var group in cell.StrategicGroupReferences.Select(rp => rp.Get()).Where(group => group != null && IsGroupObservable(group)))
                {
                    yield return group;
                }
            }
        }

        public bool IsGroupObservable(StrategicGroup group)
        {
            if (!scenarioState.enableFogOfWar)
            {
                return true;
            }
            var viewerSideId = scenarioState.fogOfWarViewerSideObjectId;
            var groupSide = group?.side;
            if (groupSide.objectId == viewerSideId)
                return true;

            return group.cell.GetNeighbors().Prepend(group.cell).Any(cell =>
            {
                if (cell.sideObjectIdHex == viewerSideId)
                    return true;
                if (cell.StrategicGroupReferences.Any(g => g.Get()?.side?.objectId == viewerSideId))
                    return true;
                return false;
            });
        }

        public void UpdatePartialShipLogs(List<ShipLog> otherShipLogs)
        {
            foreach (var otherShipLog in otherShipLogs)
            {
                var idx = shipLogs.FindIndex(shipLog => shipLog.objectId == otherShipLog.objectId);
                if (idx != -1)
                {
                    shipLogs[idx] = otherShipLog;
                }
            }

            // ResetAndRegisterAll();
        }

        public void Advance1Hour()
        {
            scenarioState.dateTime = scenarioState.dateTime.AddHours(1);

            Advance1HourForSupply();
            Advance1HourForMission();
            Advance1HourForMovement();
        }

        public void Advance1HourForSupply()
        {
            foreach (var landUnit in landUnits)
            {
                landUnit.supplyTons = Math.Max(0, landUnit.supplyTons + (landUnit.supplyGeneratedTons - landUnit.GetSupplyCostTonsPerDay()) / 24);
            }
            foreach (var shipLog in shipLogs)
            {
                shipLog.supplyTons = Math.Max(0, shipLog.supplyTons - shipLog.GetSupplyCostTonsPerDay() / 24);
            }

            if (scenarioState.dateTime.Hour == 0) // per day
            {
                DoLandSupplyNetworkTransfer();
            }
        }

        public void DoLandSupplyNetworkTransfer()
        {
            var resolver = new LandSupplyNetworkResolver();
            resolver.Resolve();
        }

        public void Advance1HourForMovement()
        {
            foreach (var strategicGroup in IterIndependentStrategicGroups())
            {
                if (strategicGroup.plannedPath.Count == 0)
                {
                    strategicGroup.moveProgressionKm = 0;
                }
                else
                {
                    var speedKmPerHour = strategicGroup.GetSpeedKmPerHour();
                    var moveKmCap = speedKmPerHour * 1;
                    while (moveKmCap > 0 && strategicGroup.plannedPath.Count >= 2)
                    {
                        var nextDistKm = 50 - strategicGroup.moveProgressionKm;
                        if (moveKmCap < nextDistKm)
                        {
                            strategicGroup.moveProgressionKm += moveKmCap;
                            moveKmCap = 0;
                        }
                        else
                        {
                            moveKmCap -= nextDistKm;
                            strategicGroup.plannedPath.RemoveAt(0);
                            strategicGroup.MoveToXY(strategicGroup.plannedPath[0].x, strategicGroup.plannedPath[0].y, true);
                            strategicGroup.moveProgressionKm = 0;
                            if (strategicGroup.plannedPath.Count < 2)
                            {
                                strategicGroup.plannedPath.Clear();
                            }
                        }
                    }
                }
            }
        }

        public void Advance1HourForMission()
        {
            // Mission state transition
            foreach (var mission in missions)
            {
                if (mission.type == StrategicMission.MissionType.Patrol && mission.waypoints.Count >= 2)
                {
                    var cells = mission.groups.Select(groupRef => (groupRef.Get() as StrategicGroup)?.cell).ToHashSet(); // is assigned groups assembled to the same hex?
                    if (cells.Count == 1)
                    {
                        var groupingCell = cells.First();
                        if (mission.patrolState == StrategicMission.PatrolState.Assembling && groupingCell == mission.GetWaypointStartCell())
                        {
                            mission.patrolState = StrategicMission.PatrolState.StartToDestination;
                        }
                        else if (mission.patrolState == StrategicMission.PatrolState.StartToDestination && groupingCell == mission.GetWaypointDestinationCell())
                        {
                            mission.patrolState = StrategicMission.PatrolState.DestinationToStart;
                        }
                        else if (mission.patrolState == StrategicMission.PatrolState.DestinationToStart && groupingCell == mission.GetWaypointStartCell())
                        {
                            // mission.patrolState = StrategicMission.PatrolState.Assembling;
                            mission.patrolState = StrategicMission.PatrolState.StartToDestination;
                        }
                    }
                }
                else if (mission.type == StrategicMission.MissionType.Supply && mission.waypoints.Count >= 2)
                {
                    var groups = mission.groups.Select(groupRef => groupRef.Get() as StrategicGroup).Where(g => g != null).ToList();
                    var cells = groups.Select(g => g.cell).Where(cell => cell != null).ToHashSet(); // is assigned groups assembled to the same hex?
                    var ships = mission.WalkGroupMembers<ShipLog>().ToList();
                    if (cells.Count == 1)
                    {
                        var groupingCell = cells.First();
                        if (mission.supplyState == StrategicMission.SupplyState.AssemblingAndLoading && groupingCell == mission.GetWaypointStartCell())
                        {
                            if (ships.All(ship => ship.GetSupplyPercent() >= 0.95))
                            {
                                mission.supplyState = StrategicMission.SupplyState.StartToDestinationAndUnloading;
                            }
                        }
                        else if (mission.supplyState == StrategicMission.SupplyState.StartToDestinationAndUnloading && groupingCell == mission.GetWaypointDestinationCell())
                        {
                            if (ships.All(ship => ship.GetSupplyPercent() <= 0.5))
                            {
                                mission.supplyState = StrategicMission.SupplyState.DestinationToStartAndLoading;
                            }
                        }
                        else if (mission.supplyState == StrategicMission.SupplyState.DestinationToStartAndLoading && groupingCell == mission.GetWaypointStartCell())
                        {
                            if (ships.All(ship => ship.GetSupplyPercent() >= 0.95))
                            {
                                mission.supplyState = StrategicMission.SupplyState.StartToDestinationAndUnloading;
                            }
                        }
                    }
                }
            }

            // Update Strategic Groups
            foreach (var strategicGroup in IterIndependentStrategicGroups())
            {
                var mission = EntityManager.Instance.Get<StrategicMission>(strategicGroup.assignedMissionObjectId);
                if (mission != null && mission.waypoints.Count >= 2)
                {
                    if (mission.type == StrategicMission.MissionType.Patrol)
                    {
                        if (strategicGroup.plannedPath.Count == 0) // Create new path if path is empty (manual waypoints has higher priority)
                        {
                            if (mission.patrolState == StrategicMission.PatrolState.Assembling) // Assembling => Move groups to the waypoint of start
                            {
                                HandleMissionAssembly(strategicGroup, mission);
                            }
                            else if (mission.patrolState == StrategicMission.PatrolState.StartToDestination) // StartToDestination => Move groups from start to destination
                            {
                                HandleMissionStartToDestination(strategicGroup, mission);
                            }
                            else if (mission.patrolState == StrategicMission.PatrolState.DestinationToStart) // DestinationToStart => Move groups from destination to start
                            {
                                HandleMissionDestinationToStart(strategicGroup, mission);
                            }
                        }
                    }
                    else if (mission.type == StrategicMission.MissionType.Supply)
                    {
                        if (strategicGroup.plannedPath.Count == 0)
                        {
                            if (mission.supplyState == StrategicMission.SupplyState.AssemblingAndLoading) // Assembling => Move groups to the waypoint of start
                            {
                                HandleMissionAssembly(strategicGroup, mission);
                            }
                            else if (mission.supplyState == StrategicMission.SupplyState.StartToDestinationAndUnloading) // StartToDestination => Move groups from start to destination
                            {
                                HandleMissionStartToDestination(strategicGroup, mission);
                            }
                            else if (mission.supplyState == StrategicMission.SupplyState.DestinationToStartAndLoading) // DestinationToStart => Move groups from destination to start
                            {
                                HandleMissionDestinationToStart(strategicGroup, mission);
                            }
                        }

                        // Transfer supply from ship to destination here or in the supply step
                        if(mission.supplyState == StrategicMission.SupplyState.StartToDestinationAndUnloading)
                        {
                            var targetDepot = mission.targetDepotReference.Get();
                            if(targetDepot != null && strategicGroup.cell == targetDepot.cell)
                            {
                                foreach(var ship in mission.WalkGroupMembers<ShipLog>())
                                {
                                    if(ship?.shipClass.type == ShipType.Transport)
                                    {
                                        var returnToBaseThresholdTons = ship.GetSupplyCapTons() * 0.1;
                                        var transferableTons = Math.Max(0, ship.supplyTons - returnToBaseThresholdTons);
                                        if(transferableTons > 0)
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
            }
        }

        void HandleMissionAssembly(StrategicGroup strategicGroup, StrategicMission mission)
        {
            var groupCell = strategicGroup.cell;
            var waypointStartCell = mission.GetWaypointStartCell();
            if (groupCell != waypointStartCell)
            {
                IGraphEnumerable<Cell> graph = new DynamicCellGraphNavy();
                var pathCells = PathFinding<Cell>.AStar(graph, groupCell, waypointStartCell);
                if (pathCells.Count >= 2)
                {
                    strategicGroup.plannedPath.AddRange(pathCells.Select(cell => new XY() { x = cell.x, y = cell.y }));
                }
            }
        }

        void HandleMissionStartToDestination(StrategicGroup strategicGroup, StrategicMission mission)
        {
            var groupCell = strategicGroup.cell;
            var waypointStartCell = mission.GetWaypointStartCell();
            if (groupCell == waypointStartCell)
            {
                strategicGroup.plannedPath.Clear();
                strategicGroup.plannedPath.AddRange(mission.waypoints);
            }
        }
        
        void HandleMissionDestinationToStart(StrategicGroup strategicGroup, StrategicMission mission)
        {
            var groupCell = strategicGroup.cell;
            var waypointDestinationCell = mission.GetWaypointDestinationCell();
            if (groupCell != null && waypointDestinationCell != null && groupCell == waypointDestinationCell)
            {
                strategicGroup.plannedPath.Clear();
                strategicGroup.plannedPath.AddRange(mission.waypoints);
                strategicGroup.plannedPath.Reverse();
            }
        }

        public IEnumerable<StrategicGroup> IterIndependentStrategicGroups()
        {
            return strategicGroups.Where(group => group.deployState == StrategicGroup.DeployState.Independent);
        }

        public override void ResetAndRegisterAll()
        {
            base.ResetAndRegisterAll();

            foreach (var landUnit in landUnits)
            {
                EntityManager.Instance.Register(landUnit, null);
            }

            foreach (var landUnitTemplate in landUnitTemplates)
            {
                EntityManager.Instance.Register(landUnitTemplate, null);
            }

            foreach (var weapon in weapons)
                EntityManager.Instance.Register(weapon, null);

            foreach (var strategicGroup in strategicGroups)
                EntityManager.Instance.Register(strategicGroup, null);

            foreach (var sideState in sideStates)
                EntityManager.Instance.Register(sideState, null);

            foreach (var mission in missions)
                EntityManager.Instance.Register(mission, null);
        }

        static StrategicGameState _instance;
        public static StrategicGameState Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new();
                }
                return _instance;
            }
        }
    }
}