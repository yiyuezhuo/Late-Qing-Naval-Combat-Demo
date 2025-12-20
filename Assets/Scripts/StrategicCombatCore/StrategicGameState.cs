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

        public List<Cell> areaCells = new();

        // public List<StrategicLocationLabel> labels = new();

        public HighCommand highCommand = new();

        public List<LandUnitTemplate> landUnitTemplates = new();
        public List<LandUnit> landUnits = new();
        public List<Weapon> weapons = new();
        public List<StrategicGroup> strategicGroups = new();

        public StrategicScenarioState scenarioState = new();

        public List<SideState> sideStates = new();

        public List<StrategicMission> missions = new();

        public List<PendingNavalCombat> pendingNavalCombats = new();
        public List<LandBattle> landBattles = new();

        public List<LazyLocalizedString> logs = new();
        // public List<LazyLocalizedString> logs = new()
        // {
        //     LazyLocalizedString.MakeRaw("Game Started")
        // };

        [XmlIgnore]
        public Dictionary<Country, SideState> countryToSideStateMap = new();

        public event EventHandler mapRebuilt;
        // public event EventHandler<(int, int)> mapCellUpdated;
        public event EventHandler<Cell> mapCellUpdated;
        public event EventHandler edgeFeatureUpdated;

        // public void InvokeMapCellUpdated(int x, int y) => mapCellUpdated?.Invoke(this, (x, y));
        public void InvokeMapCellUpdated(Cell cell) => mapCellUpdated?.Invoke(this, cell);
        public void InvokeMapRebuilt()  => mapRebuilt?.Invoke(this, EventArgs.Empty);

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


        // public void SetMapCellTerrain(int x, int y, TerrainType terrainType)
        // {
        //     // terrainMatrix[x, y] = terrainType;
        //     cellMatrix[x, y].terrain = terrainType;

        //     mapCellUpdated?.Invoke(this, (x, y));
        // }

        public void SetMapCellTerrain(Cell activeCell, TerrainType terrainType)
        {
            // terrainMatrix[x, y] = terrainType;
            activeCell.terrain = terrainType;

            // mapCellUpdated?.Invoke(this, (activeCell.x, activeCell.y));
            mapCellUpdated?.Invoke(this, activeCell);
        }


        // public void SetMapControlSide(int x, int y, string sideStateObjectId)
        // {
        //     cellMatrix[x, y].sideObjectIdHex = sideStateObjectId;

        //     mapCellUpdated?.Invoke(this, (x, y));
        // }
        public void SetMapControlSide(Cell activeCell, string sideStateObjectId)
        {
            activeCell.sideObjectIdHex = sideStateObjectId;

            // mapCellUpdated?.Invoke(this, (activeCell.x, activeCell.y));
            mapCellUpdated?.Invoke(this, activeCell);
        }


        // public void ToggleCoast(int x, int y)
        // {
        //     var cell = cellMatrix[x, y];
        //     cell.IsCoast = !cell.IsCoast;

        //     mapCellUpdated?.Invoke(this, (x, y));
        // }

        public void ToggleCoast(Cell activeCell)
        {
            // var cell = cellMatrix[x, y];
            activeCell.IsCoast = !activeCell.IsCoast;

            // mapCellUpdated?.Invoke(this, (activeCell.x, activeCell.y));
            mapCellUpdated?.Invoke(this, activeCell);
        }


        public void UpdateTo(StrategicGameState newInstance)
        {
            // terrainMatrix = newInstance.terrainMatrix;
            cellMatrix = newInstance.cellMatrix;
            areaCells = newInstance.areaCells;

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
            pendingNavalCombats = newInstance.pendingNavalCombats;
            landBattles = newInstance.landBattles;

            logs = newInstance.logs;

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
                    cellMatrix[x, y] = new(){x=x, y=y};

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
            // var relatedCells = strategicGroups.Where(group => group.deployState == StrategicGroup.DeployState.Independent).Select(group => group.cell).ToHashSet();
            var independentGroups = strategicGroups.Where(group => group.deployState == StrategicGroup.DeployState.Independent).ToList();
            var independentCells = independentGroups.Select(group => group.cell).ToList();
            var relatedCells = independentCells.ToHashSet();
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

        static string Localize(string key, params object[] args) => ServiceLocator.Get<ILocalizeService>().Get(key, args);

        public void UpdatePartialShipLogs(List<ShipLog> otherShipLogs)
        {
            var pendingNavalCombat = EntityManager.Instance.Get<PendingNavalCombat>(scenarioState.pendingNavalCombatId);

            // Or add this log in the tactical game start?
            var engagedLog = "Engaged in a combat";
            if(pendingNavalCombat != null)
            {
                engagedLog = Localize(
                    "Engaged in combat at ({0}, {1}) in {2}",
                    pendingNavalCombat.xy.x,
                    pendingNavalCombat.xy.y,
                    CoreParameter.Instance.GetReferenceTimeZoneDateTimeOffsetString(scenarioState.dateTime)
                );
            }

            foreach (var otherShipLog in otherShipLogs)
            {
                var idx = shipLogs.FindIndex(shipLog => shipLog.objectId == otherShipLog.objectId);
                if (idx != -1)
                {
                    var oldShipLog = shipLogs[idx];

                    // Post-Housekeeping
                    otherShipLog.TacticalToStrategicPostHousekeeping();
                    // Re-attach olg log trimmed in generated game.
                    otherShipLog.logs.Insert(0, new ShipLogStringLog(){
                        time = scenarioState.dateTime,
                        description = engagedLog
                    });
                    otherShipLog.InsertLogs(oldShipLog);

                    shipLogs[idx] = otherShipLog;
                }
            }

            // ResetAndRegisterAll(); // Handled by external
        }

        public void CleanupIndependentStrategicGroups()
        {
            // Reset independent but empty groups (generally caused by combat) in conflict hex deploy-state to combined. So they may be "rebuilt" in the location of higher command.
            foreach (var cellGroupsGrouping in strategicGroups
                .Where(g => g.deployState == StrategicGroup.DeployState.Independent)
                .GroupBy(g => g.cell))
            {
                var sideGroupsGroupings = cellGroupsGrouping.GroupBy(g => g.side).ToList();
                if (sideGroupsGroupings.Count >= 2)
                {
                    foreach (var group in cellGroupsGrouping)
                    {
                        if (group.GetCombinedSubUnitSize() == 0)
                        {
                            // group.deployState = StrategicGroup.DeployState.Combined;
                            // group.RemoveFromMap();
                            group.SetDeployState(StrategicGroup.DeployState.Combined);
                        }
                    }
                }
            }
        }

        public void UpdateFromTacticalResult(List<ShipLog> syncShipLogs, VictoryStatus victoryStatus)
        {
            ResetAndRegisterAll(); // to resolve pendingNavalCombat

            if (syncShipLogs != null)
            {
                UpdatePartialShipLogs(syncShipLogs);
            }

            ResetAndRegisterAll(); // to update ShipLog to new one

            // Other update
            // TODO: Move to Core

            CleanupIndependentStrategicGroups();
            HandlePendingNavalCombat(victoryStatus);
        }

        public void HandlePendingNavalCombat(VictoryStatus victoryStatus)
        {
            var pendingNavalCombat = EntityManager.Instance.Get<PendingNavalCombat>(scenarioState.pendingNavalCombatId);

            if (victoryStatus != null && victoryStatus.sideVictoryStatuses.Count > 0) // soft-skip victory status present by "look at only" mode.
            {
                DialogRoot.Instance.PopupVictoryStatusDialog(victoryStatus);
            }
            if (pendingNavalCombat != null)
            {
                pendingNavalCombats.RemoveAll(c => c.objectId == pendingNavalCombat.objectId);
            }
            if (victoryStatus != null && victoryStatus.sideVictoryStatuses.Count >= 2 && pendingNavalCombat != null)
            {
                HandleVictoryStatus(pendingNavalCombat.sideState0, victoryStatus.sideVictoryStatuses[0]);
                HandleVictoryStatus(pendingNavalCombat.sideState1, victoryStatus.sideVictoryStatuses[1]);
            }

            scenarioState.pendingNavalCombatId = null;
        }
        
        void HandleVictoryStatus(PendingNavalCombat.PendingNavalCombatSideState sideState, SideVictoryStatus sideVictoryStatus)
        {
            var side = sideState.side;
            var groups = sideState.GetGroups();

            if (sideVictoryStatus.victoryLevel < VictoryLevel.Draw)
            {
                side.victoryPoints -= 1;
            }
            if (sideVictoryStatus.victoryLevel > VictoryLevel.Draw)
            {
                side.victoryPoints += 1;
            }
            if (sideVictoryStatus.victoryLevel <= VictoryLevel.Draw)
            {
                foreach (var group in groups)
                {
                    group.StartReturnToBase(24);
                }
            }
            else
            {
                foreach (var group in groups)
                {
                    group.StartReorgnize(12);
                }
                // reorgnize for a given time interval. (Combat time + 12h)
            }
        }

        public void Advance1Hour()
        {
            scenarioState.dateTime = scenarioState.dateTime.AddHours(1);

            if (scenarioState.dateTime.Hour == 0)
            {
                AddLog(LazyLocalizedString.MakeTemplate(
                    "Tick: {0}",
                    LazyLocalizedString.MakeRaw(CoreParameter.Instance.GetReferenceTimeZoneDateTimeOffsetString(scenarioState.dateTime))
                ));
            }

            Advance1HourForSupply();
            Advance1HourForMission();
            Advance1HourForMovement();
            Advance1HourForGroupPosture();
            Advance1HourForRepair();

            CombinedAutoCombinableAndDissolvable();

            RefreshPendingNavalCombats();

            RestoreLandUnitEffectivness(); // Restore here so player can check states after damage

            HandleLandBattleBeginEnd();

            ForceDisengageStaticGroup();
        }

        public void ForceDisengageStaticGroup()
        {
            foreach(var group in GetIndependentStrategicGroups())
            {
                if(group.posture == StrategicGroup.GroupPostureType.Disengaged && group.plannedPath.Count == 0)
                {
                    group.DoLandDisengage();
                }
            }
        }

        void RestoreLandUnitEffectivness()
        {
           foreach(var landUnit in IterOnMapLandUnits())
            {
                landUnit.RestoreEffectivness();
            }
        }

        public void Advance1HourForRepair()
        {
            if (scenarioState.dateTime.Hour == 0) // per day
            {
                var damageRepairResolver = new DamageRepairResolver();
                damageRepairResolver.Resolve();
            }
        }

        public void Advance1HourForGroupPosture()
        {
            foreach (var group in GetIndependentStrategicGroups())
            {
                if (group.restoredHours > 0)
                {
                    group.restoredHours -= 1;
                }
                if (group.restoredHours == 0 && group.posture != StrategicGroup.GroupPostureType.Active)
                {
                    if(group.posture == StrategicGroup.GroupPostureType.Reorganized)
                    {
                        group.posture = StrategicGroup.GroupPostureType.Active;
                    }
                    else if(group.posture == StrategicGroup.GroupPostureType.Disengaged)
                    {
                        var hostileGroup = group.cell.StrategicGroupReferences
                            .Select(r => r.Get())
                            .Where(g => g.side != group.side && g != null && g.IsArmy() && g.posture != StrategicGroup.GroupPostureType.Disengaged).
                            FirstOrDefault();
                        if(hostileGroup == null)
                        {
                            group.posture = StrategicGroup.GroupPostureType.Active;
                        }
                    }
                }
            }
        }

        HashSet<(Cell, SideState, SideState)> CollectHappeningBattleKeys()
        {
            var happeningBattleKeys = new HashSet<(Cell, SideState, SideState)>(); // Cell, Attacker, Defender

            foreach (var g in strategicGroups.Where(g => g.LandCombatable()).GroupBy(g => g.cell))
            {
                var cell = g.Key;
                cell.RefreshControlState(); // TODO: Code smell? Extract it to the top level?

                var side2GroupsGp = g.GroupBy(g => g.side).ToList();
                var hexSide = cell.GetHexSide();
                if (hexSide != null && side2GroupsGp.Count >= 2)
                {
                    var g0 = side2GroupsGp[0];
                    var g1 = side2GroupsGp[1];

                    var g0hasActive = g0.Any(g => g.posture == StrategicGroup.GroupPostureType.Active);
                    var g1hasActive = g1.Any(g => g.posture == StrategicGroup.GroupPostureType.Active);
                    if (g0hasActive || g1hasActive)
                    {
                        SideState attacker = null;
                        SideState defender = null;
                        if (g0hasActive && g1hasActive)
                        {
                            var isG0HexController = g0.Key == hexSide;
                            if (isG0HexController)
                            {
                                attacker = g1.Key;
                                defender = g0.Key;
                            }
                            else
                            {
                                attacker = g0.Key;
                                defender = g1.Key;
                            }
                        }
                        else if (g0hasActive)
                        {
                            attacker = g0.Key;
                            defender = g1.Key;
                        }
                        else // if(g1hasActive)
                        {
                            attacker = g1.Key;
                            defender = g0.Key;
                        }
                        happeningBattleKeys.Add((cell, attacker, defender));
                    }
                }
            }

            return happeningBattleKeys;
        }
        
        void HandleLandBattleBeginEnd()
        {
            CreateNewLandBattles();
            ConcludeLandBattles();

            // Resolve undetermined battle
            foreach(var landBattle in landBattles.Where(b => !b.end))
            {
                landBattle.Step();
            }

            ConcludeLandBattles();
        }

        void CreateNewLandBattles()
        {
            var happeningBattleKeys = CollectHappeningBattleKeys();
            var prevHappendBattlesMap = landBattles.Where(b => !b.end).ToDictionary(b => b.GetKey(), b => b);
            var prevHappendBattleKeys = prevHappendBattlesMap.Keys.ToHashSet();

            // Create new battle

            foreach (var happenningBattleKey in happeningBattleKeys)
            {
                if (!prevHappendBattleKeys.Contains(happenningBattleKey))
                {
                    var (cell, attacker, defender) = happenningBattleKey;
                    var battle = new LandBattle()
                    {
                        cellXY = new() { x = cell.x, y = cell.y },
                        attacker = new() { sideId = attacker.objectId },
                        defender = new() { sideId = defender.objectId },
                        beginDateTime = scenarioState.dateTime
                    };
                    EntityManager.Instance.Register(battle, null); // ID assigned here

                    landBattles.Add(battle);

                    cell.landBattleId = battle.objectId;

                    // AddLog($"New land battle begin: {battle.cellXY} {attacker.name.GetShortName()} vs {defender.name.GetShortName()}");
                    AddLog(LazyLocalizedString.MakeTemplate(
                        "New land battle begin: {0} {1} vs {2}",
                        GetCellNameLazyStr(battle.cellXY),
                        LazyLocalizedString.MakeGlobalStringShort(attacker.name),
                        LazyLocalizedString.MakeGlobalStringShort(defender.name)
                    ));
                }
            }
        }

        void ConcludeLandBattles()
        {
            var happeningBattleKeys = CollectHappeningBattleKeys();
            var prevHappendBattlesMap = landBattles.Where(b => !b.end).ToDictionary(b => b.GetKey(), b => b);
            var prevHappendBattleKeys = prevHappendBattlesMap.Keys.ToHashSet();

            // Set concluded/invalid battle to ended. ("Natural Disengagement")
            foreach(var prevHappendBattleKey in prevHappendBattleKeys)
            {
                if(!happeningBattleKeys.Contains(prevHappendBattleKey))
                {
                    var battle = prevHappendBattlesMap[prevHappendBattleKey];
                    // battle.end = true;
                    // battle.endDateTime = scenarioState.dateTime;
                    battle.GoToEnd();

                    var (cell, attacker, defender) = prevHappendBattleKey;
                    var cellGroups = cell.StrategicGroupReferences.Select(gr => gr.Get());
                    // battle.attackerVictory = cellGroups.Any(
                    //     g => g.IsOnMap() &&
                    //     g.posture != StrategicGroup.GroupPostureType.Disengaged &&
                    //     g.side == attacker &&
                    //     g.type != StrategicGroup.Type.Fleet
                    // );
                    battle.attackerVictory = cellGroups.Any(
                        g => g.IsOnMap() &&
                        g.posture == StrategicGroup.GroupPostureType.Active &&
                        g.side == attacker &&
                        g.type != StrategicGroup.Type.Fleet
                    );

                    cell.landBattleId = null;

                    var vicDesc = battle.attackerVictory ? "Attacker Victory" : "Defender Victory";
                    // AddLog($"Land battle end: {battle.cellXY} {attacker.name.GetShortName()} vs {defender.name.GetShortName()}, {vicDesc}");
                    AddLog(LazyLocalizedString.MakeTemplate(
                        "Land battle end: {0} {1}, {2} ({3}) vs {4} ({5})",
                        LazyLocalizedString.MakeRaw(battle.cellXY),
                        LazyLocalizedString.MakeLocalizedRequired(vicDesc),
                        LazyLocalizedString.MakeGlobalStringShort(attacker.name),
                        battle.attacker.GetSummary(),
                        LazyLocalizedString.MakeGlobalStringShort(defender.name),
                        battle.defender.GetSummary()
                    ));
                }
            }
        }


        public void RefreshPendingNavalCombats()
        {
            pendingNavalCombats.Clear();

            foreach (var g in strategicGroups.Where(g => g.NavalCombatable()).GroupBy(g => g.cell))
            {
                var cell = g.Key;
                var side2GroupsGp = g.GroupBy(g => g.side).ToList();
                if (side2GroupsGp.Count >= 2)
                {
                    var pendingCombat = new PendingNavalCombat()
                    {
                        // xy = new XY() { x = cell.x, y = cell.y },
                        xy = cell.ToXY(),
                        sideState0 = new()
                        {
                            sideObjectId = side2GroupsGp[0].Key.objectId,
                            groupObjectIds = side2GroupsGp[0].Select(g => g.objectId).ToList()
                        },
                        sideState1=new(){
                            sideObjectId = side2GroupsGp[1].Key.objectId,
                            groupObjectIds = side2GroupsGp[1].Select(g => g.objectId).ToList()
                        }
                    };
                    EntityManager.Instance.Register(pendingCombat, null);
                    pendingNavalCombats.Add(pendingCombat);
                }
            }
        }

        void CombinedAutoCombinableAndDissolvable()
        {
            foreach(var group in strategicGroups.ToList())
            {
                var parentGroup = group.strategicGroupReference.Get();
                if (group.autoCombinable && group.deployState == StrategicGroup.DeployState.Independent)
                {
                    if (parentGroup.x == group.x && parentGroup.y == group.y)
                    {
                        group.RemoveFromMap();
                        group.deployState = StrategicGroup.DeployState.Combined;
                        group.autoCombinable = false;
                    }
                }
                if(group.dissolvable && group.deployState == StrategicGroup.DeployState.Combined)
                {
                    foreach (var memberRef in group.subordinatesCombined.ToList())
                    {
                        var member = memberRef.Get();
                        group.MoveElementTo(member, parentGroup);
                    }
                    group.AttachTo(null);

                    EntityManager.Instance.Unregister(group);
                    strategicGroups.Remove(group);
                }
            }
        }

        public void Advance1HourForSupply()
        {
            foreach (var group in GetIndependentStrategicGroups())
            {
                foreach (var landUnit in group.WalkGroupMembers<LandUnit>())
                {
                    landUnit.supplyTons = Math.Max(0, landUnit.supplyTons + (landUnit.supplyGeneratedTons - landUnit.GetSupplyCostTonsPerDay()) / 24);
                }
            }

            foreach (var group in GetIndependentStrategicGroups())
            {
                if (group.plannedPath.Count > 0) // Only moving (has plannedPath) ship cost supply.
                {
                    foreach (var shipLog in group.WalkGroupMembersDeployedShips())
                    {
                        shipLog.supplyTons = Math.Max(0, shipLog.supplyTons - shipLog.GetSupplyCostTonsPerDay() / 24);
                    }
                }
                // else
                // {
                //     // Ship ammunition replenishment if it's in home port (hex where its depot is't located in)
                //     if(group.IsInDepotLocation())
                //     {
                //         foreach(var shipLog in group.WalkGroupMembersDeployedShips())
                //         {
                //             // TODO: Introduce RTW-like side level ammo doctrine.
                //         }
                //     }
                // }
            }


            if (scenarioState.dateTime.Hour == 0) // per day
            {
                DoLandSupplyNetworkTransfer();

                // Ship ammunition replenishment, if supply percentage >= 10% of displacement (standard fuel capacity), convert supply to ammo
                DoShipAmmunitionReplenishment();
            }
        }
        
        public void DoShipAmmunitionReplenishment()
        {
            foreach (var group in GetIndependentStrategicGroups())
            {
                if (group.plannedPath.Count == 0 && group.IsInDepotLocation())
                {
                    foreach (var shipLog in group.WalkGroupMembersDeployedShips())
                    {
                        // TODO: Introduce RTW-like side level ammo doctrine.
                        var ammoGapTons = shipLog.GetGapAmmoWeightsPounds() / 2204.623f;
                        if (ammoGapTons > 0 && shipLog.supplyTons >= shipLog.GetSupplyCapTons() * 0.1)
                        {
                            shipLog.supplyTons -= ammoGapTons;
                            // shipLog.ResetDamageExpenditureState();
                            var ctx = shipLog.side?.GetResetDamageExpenditureStateContext() ?? new();
                            shipLog.ResetExpenditureState(ctx);
                        }
                    }
                }
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
                        var valid = strategicGroup.TryGetDistanceToNextLocationInPlannedPathWithoutProgression(out var cellDistKm);
                        if(!valid)
                        {
                            break;
                        }

                        var nextDistKm = cellDistKm - strategicGroup.moveProgressionKm; // 50km/hex
                        if (moveKmCap < nextDistKm)
                        {
                            strategicGroup.moveProgressionKm += moveKmCap;
                            moveKmCap = 0;
                        }
                        else
                        {
                            moveKmCap -= nextDistKm;
                            strategicGroup.plannedPath.RemoveAt(0);
                            // strategicGroup.MoveToXY(strategicGroup.plannedPath[0].x, strategicGroup.plannedPath[0].y, true);
                            strategicGroup.MoveToCell(strategicGroup.plannedPath[0].GetCell(), true); // TODO: Generalize to Area System
                            
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
                    var ships = mission.WalkGroupMembersDeployedShips().ToList();
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
                else if(mission.type == StrategicMission.MissionType.NavalTransfer && mission.waypoints.Count >= 2)
                {
                    var groups = mission.groups.Select(groupRef => groupRef.Get() as StrategicGroup).Where(g => g != null).ToList();
                    var cells = groups.Select(g => g.cell).Where(cell => cell != null).ToHashSet();
                    if (mission.navalTransferState == StrategicMission.NavalTransferState.Assembling)
                    {
                        if (cells.Count == 1)
                        {
                            var groupingCell = cells.First();
                            if (groupingCell == mission.GetWaypointStartCell())
                            {
                                // Do Split & Load
                                var transportShips = mission.WalkGroupMembersDeployedShips().Where(shipLog => shipLog?.shipClass?.type == ShipType.Transport).ToList();
                                var cargoGroups = groups.Where(g => g.type != StrategicGroup.Type.Fleet).ToList();
                                TransferSplitter.SequenceSplit(transportShips, cargoGroups);

                                mission.navalTransferState = StrategicMission.NavalTransferState.StartToDestination;
                            }
                        }
                    }
                    else if (mission.navalTransferState == StrategicMission.NavalTransferState.StartToDestination)
                    {
                        var fleetGroups = groups.Where(g => g.type == StrategicGroup.Type.Fleet).ToList();
                        var fleetCells = fleetGroups.Select(g => g.cell).Where(cell => cell != null).ToHashSet();
                        if (fleetCells.Count == 1)
                        {
                            var groupingCell = fleetCells.First();
                            if (groupingCell == mission.GetWaypointDestinationCell())
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

                                mission.navalTransferState = StrategicMission.NavalTransferState.DestinationToStart;
                            }
                        }
                    }
                    else if(mission.navalTransferState == StrategicMission.NavalTransferState.DestinationToStart)
                    {
                        var cargoGroupsInStartCell = groups.Where(g =>
                            g.cell == mission.GetWaypointStartCell() &&
                            g.type != StrategicGroup.Type.Fleet &&
                            g.deployState == StrategicGroup.DeployState.Independent // Though they're independent when assigned, they may become NotDeployed in trasport process.
                        ).ToList();

                        if (cargoGroupsInStartCell.Count == 0)
                        {
                            mission.navalTransferState = StrategicMission.NavalTransferState.Completed;
                        }
                        else
                        {
                            var fleetGroups = groups.Where(g => g.type == StrategicGroup.Type.Fleet).ToList();
                            var fleetCells = fleetGroups.Select(g => g.cell).Where(cell => cell != null).ToHashSet();

                            if (fleetCells.Count == 1)
                            {
                                var groupingCell = cells.First();
                                if (groupingCell == mission.GetWaypointStartCell())
                                {
                                    // Do Split & Load
                                    var transportShips = mission.WalkGroupMembersDeployedShips().Where(shipLog => shipLog?.shipClass?.type == ShipType.Transport).ToList();
                                    TransferSplitter.SequenceSplit(transportShips, cargoGroupsInStartCell);

                                    mission.navalTransferState = StrategicMission.NavalTransferState.StartToDestination;
                                }
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
                        if (mission.supplyState == StrategicMission.SupplyState.StartToDestinationAndUnloading)
                        {
                            var targetDepot = mission.targetDepotReference.Get();
                            if (targetDepot != null && strategicGroup.cell == targetDepot.cell)
                            {
                                foreach (var ship in mission.WalkGroupMembersDeployedShips())
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
                    else if(mission.type == StrategicMission.MissionType.NavalTransfer)
                    {
                        if (strategicGroup.plannedPath.Count == 0)
                        {
                            if (mission.navalTransferState == StrategicMission.NavalTransferState.Assembling)
                            {
                                HandleMissionAssembly(strategicGroup, mission);
                            }
                            else if (mission.navalTransferState == StrategicMission.NavalTransferState.StartToDestination && strategicGroup.type == StrategicGroup.Type.Fleet)
                            {
                                HandleMissionStartToDestination(strategicGroup, mission);
                            }
                            else if(mission.navalTransferState == StrategicMission.NavalTransferState.DestinationToStart && strategicGroup.type == StrategicGroup.Type.Fleet)
                            {
                                HandleMissionDestinationToStart(strategicGroup, mission);
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
                    // strategicGroup.plannedPath.AddRange(pathCells.Select(cell => new XY() { x = cell.x, y = cell.y }));
                    strategicGroup.plannedPath.AddRange(pathCells.Select(cell => cell.ToXY()));
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

        public IEnumerable<LandUnit> IterOnMapLandUnits()
        {
            foreach(var group in IterIndependentStrategicGroups())
            {
                foreach(var landUnit in group.WalkGroupMembers<LandUnit>())
                {
                    yield return landUnit;
                }
            }
        }

        public void CreateDefaultAndResetShipLogStates()
        {
            var createdObjectIds = shipLogs.Select(shipLog => shipLog.namedShip.objectId).Where(id => id != null && id != "").ToHashSet();

            // StrategicGameState.Instance.shipLogs = StrategicGameState.Instance.namedShips
            shipLogs.AddRange(namedShips
                .Where(namedShip => !createdObjectIds.Contains(namedShip.objectId) && !namedShip.notAvailableForFirstSinoJapaneseWar)
                .Select(namedShip =>
                {
                    // Debug.LogWarning($"Create new ship log for: {namedShip.name.GetMergedName()}");
                    ServiceLocator.Get<ILoggerService>().LogWarning($"Create new ship log for: {namedShip.name.GetMergedName()}");

                    var shipLog = new ShipLog();
                    shipLog.namedShipObjectId = namedShip.objectId;
                    return shipLog;
                })
            );

            ResetAndRegisterAll();

            foreach (var shipLog in shipLogs)
            {
                var ctx = shipLog.side?.GetResetDamageExpenditureStateContext() ?? new();

                shipLog.ResetDamageExpenditureState(ctx); // Impose SideState's doctrine

                if (shipLog.mapState == MapState.NotDeployed) // NotDeployed in strategic game is not defined now
                    shipLog.mapState = MapState.Deployed;
            }

            ResetAndRegisterAll();
        }

        public void AddLog(LazyLocalizedString log)
        {
            logs.Insert(0, log);
        }

        public void AddLog(string rawLog) // mainly for debug purpose
        {
            logs.Insert(0, LazyLocalizedString.MakeRaw(rawLog));
        }

        public void ClearLogs() => logs.Clear();

        public string GetCellName(XY cellXY)
        {
            // var cell = cellMatrix[cellXY.x, cellXY.y];
            // return cell?.Label?.GetShortName() ?? $"({cellXY.x}, {cellXY.y})";
            return GetCellNameLazyStr(cellXY).Resolve();
        }

        public LazyLocalizedString GetCellNameLazyStr(XY cellXY)
        {
            var cell = cellMatrix[cellXY.x, cellXY.y];
            if(cell == null || cell.Label == null)
            {
                return LazyLocalizedString.MakeRaw($"({cellXY.x}, {cellXY.y})"); // TODO: Add WITP-like "near XXX" desc
            }
            return LazyLocalizedString.MakeGlobalStringShort(cell.Label);
        }


        public override void ResetAndRegisterAll()
        {
            base.ResetAndRegisterAll();

            foreach(var areaCell in areaCells)
                EntityManager.Instance.Register(areaCell, null);

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

            foreach (var pendingNavalCombat in pendingNavalCombats)
                EntityManager.Instance.Register(pendingNavalCombat, null);

            foreach (var landBattle in landBattles)
                EntityManager.Instance.Register(landBattle, null);

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