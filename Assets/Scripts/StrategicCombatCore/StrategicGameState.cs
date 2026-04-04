using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using CoreUtils;
using NavalCombatCore;
using YYZ.PathFinding;
using YYZ;

namespace StrategicCombatCore
{


    public class SerializedCells
    {
        public int width;
        public int height;
        public List<Cell> records;
    }

    public partial class SidedLazyLocalizedString
    {
        public LazyLocalizedString log;

        [XmlAttribute]
        public string sideObjectId;

        public SideState GetSide() => EntityManager.Instance.Get<SideState>(sideObjectId);

        public LazyLocalizedString GetSidedLog()
        {
            return LazyLocalizedString.MakeTemplate("[{0}]: {1}", LazyLocalizedString.MakeGlobalStringShort(GetSide()?.name), log);
        }
    }

    public class StrategicGameState : AbstractGameState
    {
        public sealed class TheaterFrontlineWeightRequestedStats
        {
            public int frontlineCount;
            public string frontlineSummary;
            public float min;
            public float max;
            public float average;
            public float standardDeviation;
        }

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
        public List<Theater> theaters = new();

        public List<SidedLazyLocalizedString> logs = new();
        // public List<LazyLocalizedString> logs = new();
        // public List<LazyLocalizedString> logs = new()
        // {
        //     LazyLocalizedString.MakeRaw("Game Started")
        // };

        public event EventHandler<SidedLazyLocalizedString> logAdded;
        public event EventHandler logsRefreshed;

        public List<NavalContactReport> navalContactReports = new();

        [XmlIgnore]
        public Dictionary<Country, SideState> countryToSideStateMap = new();

        public event EventHandler mapRebuilt;
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
                cell1.AddEdgeFeature(edgeDirection, edgeFeatureType);
            }
            if (cell2.TryGetDirection(cell1, out edgeDirection))
            {
                cell2.AddEdgeFeature(edgeDirection, edgeFeatureType);
            }
            edgeFeatureUpdated?.Invoke(this, EventArgs.Empty);
        }

        public void DeleteEdgeFeature(Cell cell1, Cell cell2, EdgeFeatureType edgeFeatureType)
        {
            if (cell1.TryGetDirection(cell2, out var edgeDirection))
            {
                cell1.RemoveEdgeFeature(edgeDirection, edgeFeatureType);
            }
            if (cell2.TryGetDirection(cell1, out edgeDirection))
            {
                cell2.RemoveEdgeFeature(edgeDirection, edgeFeatureType);
            }
            edgeFeatureUpdated?.Invoke(this, EventArgs.Empty);
        }

        public void ToggleEdgeFeature(Cell cell1, Cell cell2, EdgeFeatureType edgeFeatureType)
        {
            if (cell1 == null || cell2 == null)
                return;

            if (!cell1.TryGetDirection(cell2, out _) && !cell2.TryGetDirection(cell1, out _))
                return;

            var shouldDelete = cell1.HasEdgeFeatureTo(cell2, edgeFeatureType) || cell2.HasEdgeFeatureTo(cell1, edgeFeatureType);
            if (shouldDelete)
            {
                DeleteEdgeFeature(cell1, cell2, edgeFeatureType);
            }
            else
            {
                AddEdgeFeature(cell1, cell2, edgeFeatureType);
            }
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
            theaters = newInstance.theaters ?? new();

            logs = newInstance.logs;
            navalContactReports = newInstance.navalContactReports;
            customBoolMap = newInstance.customBoolMap;

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

        // public IEnumerable<StrategicGroup> GetObservabledStrategicGroups()
        // {
        //     foreach (var group in strategicGroups)
        //     {
        //         if (group.deployState == StrategicGroup.DeployState.Independent)
        //         {
        //             if (IsGroupObservable(group))
        //                 yield return group;
        //         }
        //     }
        // }

        // public IEnumerable<StrategicGroup> GetOrderedObservableStrategicGroups(SideState side)
        // {
        //     // var relatedCells = strategicGroups.Where(group => group.deployState == StrategicGroup.DeployState.Independent).Select(group => group.cell).ToHashSet();
        //     var independentGroups = strategicGroups.Where(group => group.deployState == StrategicGroup.DeployState.Independent).ToList();
        //     var independentCells = independentGroups.Select(group => group.cell).ToList();
        //     var relatedCells = independentCells.ToHashSet();
        //     foreach (var cell in relatedCells)
        //     {
        //         foreach (var group in cell.StrategicGroupReferences.Select(rp => rp.Get()).Where(group => group != null && IsGroupObservable(side, group)))
        //         {
        //             yield return group;
        //         }
        //     }
        // }

        // public bool IsGroupObservable(SideState viewerSide, StrategicGroup group)
        // {
        //     var groupSide = group?.side;
        //     if (groupSide == viewerSide)
        //         return true;

        //     return group.cell.GetNeighbors().Prepend(group.cell).Any(cell =>
        //     {
        //         if (cell.GetHexSide() == viewerSide)
        //             return true;
        //         if (cell.StrategicGroupReferences.Any(g => g.Get()?.side == viewerSide))
        //             return true;
        //         return false;
        //     });
        // }

        static string Localize(string key, params object[] args) => ServiceLocator.Get<ILocalizeService>().Get(key, args);

        string FormatShipName(string objectId, IReadOnlyDictionary<string, ShipLog> shipMap)
        {
            if (string.IsNullOrWhiteSpace(objectId))
                return "[Unknown]";
            if (shipMap != null && shipMap.TryGetValue(objectId, out var ship) && ship?.namedShip?.name != null)
                return ship.namedShip.name.GetShortName();
            return EntityManager.Instance.Get<ShipLog>(objectId)?.namedShip?.name?.GetShortName() ?? objectId;
        }

        string FormatPercent(int hit, int total)
        {
            if (total <= 0)
                return "0%";
            return $"{((float)hit / total) * 100f:0.#}%";
        }

        string FormatDamage(float damagePoint)
        {
            return $"{damagePoint:0.##}";
        }

        string GetBattleLocationSummary(PendingNavalCombat pendingNavalCombat)
        {
            if (pendingNavalCombat == null)
                return Localize(
                    "an unknown location on {0}",
                    CoreParameter.Instance.GetReferenceTimeZoneDateTimeOffsetString(scenarioState.dateTime)
                );

            var xy = pendingNavalCombat.xy;
            var cell = cellMatrix != null &&
                       xy.x >= 0 && xy.x < GetMapWidth() &&
                       xy.y >= 0 && xy.y < GetMapHeight()
                ? cellMatrix[xy.x, xy.y]
                : null;
            var cellSummary = cell?.GetLocationSummary();
            if (!string.IsNullOrWhiteSpace(cellSummary))
                return $"{cellSummary} ({xy.x}, {xy.y}) on {CoreParameter.Instance.GetReferenceTimeZoneDateTimeOffsetString(scenarioState.dateTime)}";
            return $"({xy.x}, {xy.y}) on {CoreParameter.Instance.GetReferenceTimeZoneDateTimeOffsetString(scenarioState.dateTime)}";
        }

        string GetBatteryLabel(ShipLog shipLog, int idx)
        {
            if (shipLog?.shipClass?.batteryRecords != null &&
                idx >= 0 &&
                idx < shipLog.shipClass.batteryRecords.Count)
            {
                return shipLog.shipClass.batteryRecords[idx]?.name?.GetShortName() ?? $"Battery {idx + 1}";
            }
            return $"Battery {idx + 1}";
        }

        string GetRapidFiringLabel(ShipLog shipLog, int idx)
        {
            if (shipLog?.shipClass?.rapidFireBatteryRecords != null &&
                idx >= 0 &&
                idx < shipLog.shipClass.rapidFireBatteryRecords.Count)
            {
                return shipLog.shipClass.rapidFireBatteryRecords[idx]?.name?.GetShortName() ?? $"Rapid Firing Battery {idx + 1}";
            }
            return $"Rapid Firing Battery {idx + 1}";
        }

        string BuildBattleSummary(
            ShipLog oldShipLog,
            ShipLog otherShipLog,
            PendingNavalCombat pendingNavalCombat,
            IReadOnlyDictionary<string, ShipLog> shipMap,
            IReadOnlyDictionary<string, LaunchedTorpedo> torpedoMap,
            IReadOnlyList<ShipLog> tacticalShipLogs)
        {
            var lines = new List<string>
            {
                Localize(
                    "Naval combat summary at {0}",
                    GetBattleLocationSummary(pendingNavalCombat)
                )
            };

            var outcome = new List<string>();
            if (otherShipLog.mapState == MapState.Destroyed)
                outcome.Add(Localize("ship sunk"));
            if (otherShipLog.operationalState == ShipOperationalState.AbandonShip)
                outcome.Add(Localize("ship abandoned"));
            lines.Add(outcome.Count > 0
                ? Localize("Outcome: {0}", string.Join(", ", outcome))
                : Localize("Outcome: survived"));

            var battleDpLoss = Math.Max(0, otherShipLog.damagePoint - oldShipLog.damagePoint);
            lines.Add(Localize("DP loss: {0}", FormatDamage(battleDpLoss)));

            var incomingLines = new List<string>();
            var totalIncomingHits = 0;
            var totalIncomingDamage = 0f;

            foreach (var grouping in (tacticalShipLogs ?? Array.Empty<ShipLog>())
                         .Where(shipLog => shipLog != null && shipLog.objectId != otherShipLog.objectId)
                         .SelectMany(shooterShip => shooterShip.batteryStatus.Select((batteryStatus, batteryIdx) => new { shooterShip, batteryStatus, batteryIdx }))
                         .SelectMany(x => x.batteryStatus.mountStatus.SelectMany(mount => mount.logs)
                             .Where(log => log.hit && log.firingTargetObjectId == otherShipLog.objectId)
                             .Select(log => new
                             {
                                 x.shooterShip,
                                 x.batteryIdx,
                                 damagePoint = log.ShellDamageResult?.damagePoint ?? 0f
                             }))
                         .GroupBy(x => (x.shooterShip.objectId, x.batteryIdx))
                         .OrderByDescending(g => g.Count()))
            {
                var hitCount = grouping.Count();
                var damagePoint = grouping.Sum(x => x.damagePoint);
                totalIncomingHits += hitCount;
                totalIncomingDamage += damagePoint;
                incomingLines.Add(
                    Localize(
                        "- {0} by {1}: {2} hits, {3} DP",
                        FormatShipName(grouping.Key.objectId, shipMap),
                        GetBatteryLabel(shipMap.GetValueOrDefault(grouping.Key.objectId), grouping.Key.batteryIdx),
                        hitCount,
                        FormatDamage(damagePoint)
                    ));
            }

            foreach (var grouping in (tacticalShipLogs ?? Array.Empty<ShipLog>())
                         .Where(shipLog => shipLog != null && shipLog.objectId != otherShipLog.objectId)
                         .SelectMany(shooterShip => shooterShip.rapidFiringStatus.Select((rapidFiringStatus, rfIdx) => new { shooterShip, rapidFiringStatus, rfIdx }))
                         .SelectMany(x => x.rapidFiringStatus.logs
                             .Where(log => log.hit && log.firingTargetObjectId == otherShipLog.objectId)
                             .Select(log => new
                             {
                                 x.shooterShip,
                                 x.rfIdx,
                                 damagePoint = log.damagePoint
                             }))
                         .GroupBy(x => (x.shooterShip.objectId, x.rfIdx))
                         .OrderByDescending(g => g.Count()))
            {
                var hitCount = grouping.Count();
                var damagePoint = grouping.Sum(x => x.damagePoint);
                totalIncomingHits += hitCount;
                totalIncomingDamage += damagePoint;
                incomingLines.Add(
                    Localize(
                        "- {0} by {1}: {2} hits, {3} DP",
                        FormatShipName(grouping.Key.objectId, shipMap),
                        GetRapidFiringLabel(shipMap.GetValueOrDefault(grouping.Key.objectId), grouping.Key.rfIdx),
                        hitCount,
                        FormatDamage(damagePoint)
                    ));
            }

            foreach (var grouping in otherShipLog.logs.OfType<ShipLogTorpedoHitLog>()
                         .GroupBy(log =>
                         {
                             if (!string.IsNullOrWhiteSpace(log.torpedoObjectId) &&
                                 torpedoMap != null &&
                                 torpedoMap.TryGetValue(log.torpedoObjectId, out var torpedo))
                             {
                                 return torpedo.shooterId;
                             }
                             return null;
                         })
                         .OrderByDescending(g => g.Count()))
            {
                var hitCount = grouping.Count();
                var damagePoint = grouping.Sum(log => log.damagePoint);
                totalIncomingHits += hitCount;
                totalIncomingDamage += damagePoint;
                incomingLines.Add(
                    Localize(
                        "- {0} by {1}: {2} hits, {3} DP",
                        FormatShipName(grouping.Key, shipMap),
                        Localize("Torpedo"),
                        hitCount,
                        FormatDamage(damagePoint)
                    ));
            }

            if (incomingLines.Count > 0)
            {
                lines.Add(Localize("Hits taken:"));
                lines.Add(Localize("- Total: {0} hits, {1} DP", totalIncomingHits, FormatDamage(totalIncomingDamage)));
                lines.AddRange(incomingLines);
            }

            var outgoingLines = new List<string>();
            var usageLines = new List<string>();
            var totalOutgoingHits = 0;
            var totalOutgoingDamage = 0f;

            for (var batteryIdx = 0; batteryIdx < otherShipLog.batteryStatus.Count; batteryIdx++)
            {
                var batteryStatus = otherShipLog.batteryStatus[batteryIdx];
                var batteryLabel = GetBatteryLabel(otherShipLog, batteryIdx);
                var batteryLogs = batteryStatus.mountStatus.SelectMany(mount => mount.logs).ToList();
                var batteryHits = batteryLogs.Where(log => log.hit).ToList();

                foreach (var targetGroup in batteryHits
                             .GroupBy(log => log.firingTargetObjectId)
                             .OrderByDescending(g => g.Count()))
                {
                    var hitCount = targetGroup.Count();
                    var damagePoint = targetGroup.Sum(log => log.ShellDamageResult?.damagePoint ?? 0f);
                    totalOutgoingHits += hitCount;
                    totalOutgoingDamage += damagePoint;
                    outgoingLines.Add(
                        Localize(
                            "- {0} -> {1}: {2} hits, {3} DP",
                            batteryLabel,
                            FormatShipName(targetGroup.Key, shipMap),
                            hitCount,
                            FormatDamage(damagePoint)
                        ));
                }

                usageLines.Add(
                    Localize(
                        "- {0}: fired {1}, hit {2}, accuracy {3}",
                        batteryLabel,
                        batteryLogs.Count,
                        batteryHits.Count,
                        FormatPercent(batteryHits.Count, batteryLogs.Count)
                    ));
            }

            for (var rfIdx = 0; rfIdx < otherShipLog.rapidFiringStatus.Count; rfIdx++)
            {
                var rapidFiringStatus = otherShipLog.rapidFiringStatus[rfIdx];
                var rapidFiringLabel = GetRapidFiringLabel(otherShipLog, rfIdx);
                var rapidFiringLogs = rapidFiringStatus.logs.ToList();
                var rapidFiringHits = rapidFiringLogs.Where(log => log.hit).ToList();

                foreach (var targetGroup in rapidFiringHits
                             .GroupBy(log => log.firingTargetObjectId)
                             .OrderByDescending(g => g.Count()))
                {
                    var hitCount = targetGroup.Count();
                    var damagePoint = targetGroup.Sum(log => log.damagePoint);
                    totalOutgoingHits += hitCount;
                    totalOutgoingDamage += damagePoint;
                    outgoingLines.Add(
                        Localize(
                            "- {0} -> {1}: {2} hits, {3} DP",
                            rapidFiringLabel,
                            FormatShipName(targetGroup.Key, shipMap),
                            hitCount,
                            FormatDamage(damagePoint)
                        ));
                }

                usageLines.Add(
                    Localize(
                        "- {0}: fired {1}, hit {2}, accuracy {3}",
                        rapidFiringLabel,
                        rapidFiringLogs.Count,
                        rapidFiringHits.Count,
                        FormatPercent(rapidFiringHits.Count, rapidFiringLogs.Count)
                    ));
            }

            var torpedosFired = torpedoMap?.Values
                .Where(torpedo => torpedo != null && torpedo.shooterId == otherShipLog.objectId)
                .ToList() ?? new List<LaunchedTorpedo>();
            var torpedoHits = torpedosFired
                .Where(torpedo => torpedo.endgameType == LaunchedTorpedoEndgameType.Hit && !string.IsNullOrWhiteSpace(torpedo.hitTargetObjectId))
                .ToList();

            foreach (var targetGroup in torpedoHits
                         .GroupBy(torpedo => torpedo.hitTargetObjectId)
                         .OrderByDescending(g => g.Count()))
            {
                var hitCount = targetGroup.Count();
                var damagePoint = targetGroup.Sum(torpedo => torpedo.inflictDamagePoint);
                totalOutgoingHits += hitCount;
                totalOutgoingDamage += damagePoint;
                outgoingLines.Add(
                    Localize(
                        "- {0} -> {1}: {2} hits, {3} DP",
                        Localize("Torpedo"),
                        FormatShipName(targetGroup.Key, shipMap),
                        hitCount,
                        FormatDamage(damagePoint)
                    ));
            }

            usageLines.Add(
                Localize(
                    "- {0}: fired {1}, hit {2}, accuracy {3}",
                    Localize("Torpedo"),
                    torpedosFired.Count,
                    torpedoHits.Count,
                    FormatPercent(torpedoHits.Count, torpedosFired.Count)
                ));

            if (outgoingLines.Count > 0)
            {
                lines.Add(Localize("Hits inflicted:"));
                lines.Add(Localize("- Total: {0} hits, {1} DP", totalOutgoingHits, FormatDamage(totalOutgoingDamage)));
                lines.AddRange(outgoingLines);
            }

            lines.Add(Localize("Weapon usage:"));
            lines.AddRange(usageLines);

            var sb = new StringBuilder();
            for (var i = 0; i < lines.Count; i++)
            {
                if (i > 0)
                    sb.Append('\n');
                sb.Append(lines[i]);
            }
            return sb.ToString();
        }

        public void UpdatePartialShipLogs(List<ShipLog> otherShipLogs, List<LaunchedTorpedo> launchedTorpedos = null)
        {
            var pendingNavalCombat = EntityManager.Instance.Get<PendingNavalCombat>(scenarioState.pendingNavalCombatId);
            var shipMap = shipLogs
                .Concat(otherShipLogs ?? new List<ShipLog>())
                .Where(shipLog => shipLog != null && !string.IsNullOrWhiteSpace(shipLog.objectId))
                .GroupBy(shipLog => shipLog.objectId)
                .ToDictionary(grouping => grouping.Key, grouping => grouping.Last());
            var torpedoMap = (launchedTorpedos ?? new List<LaunchedTorpedo>())
                .Where(torpedo => torpedo != null && !string.IsNullOrWhiteSpace(torpedo.objectId))
                .GroupBy(torpedo => torpedo.objectId)
                .ToDictionary(grouping => grouping.Key, grouping => grouping.Last());

            foreach (var otherShipLog in otherShipLogs)
            {
                var idx = shipLogs.FindIndex(shipLog => shipLog.objectId == otherShipLog.objectId);
                if (idx != -1)
                {
                    var oldShipLog = shipLogs[idx];
                    var battleSummary = BuildBattleSummary(oldShipLog, otherShipLog, pendingNavalCombat, shipMap, torpedoMap, otherShipLogs);

                    // Post-Housekeeping
                    otherShipLog.TacticalToStrategicPostHousekeeping();
                    var preservedLogs = oldShipLog.logs?.ToList() ?? new List<ShipLogLog>();
                    preservedLogs.Add(new ShipLogStringLog()
                    {
                        time = scenarioState.dateTime,
                        description = battleSummary
                    });
                    otherShipLog.logs = preservedLogs;

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

        public void UpdateFromTacticalResult(List<ShipLog> syncShipLogs, VictoryStatus victoryStatus, List<LaunchedTorpedo> launchedTorpedos = null)
        {
            ResetAndRegisterAll(); // to resolve pendingNavalCombat

            if (syncShipLogs != null)
            {
                UpdatePartialShipLogs(syncShipLogs, launchedTorpedos);
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
                    var assignedMission = group.GetAssignedMission();
                    if(assignedMission != null)
                    {
                        assignedMission.interrupted = true;
                    }
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
                ), null);
            }

            Advance1HourForReinforcement();

            Advance1HourForSupply();
            Advance1HourForMission();
            Advance1HourForOutOfFuelFleetCheck();
            Advance1HourForMovement();
            Advance1HourForGroupPosture();
            Advance1HourForRepair();
            Advance1HourForDetachedAutoReattach();

            CombinedAutoCombinableAndDissolvable();

            Advance1HourForContactReport();
            Advance1HourForRaiding();

            RefreshPendingNavalCombats();

            RestoreLandUnitEffectivness(); // Restore here so player can check states after damage

            HandleLandBattleBeginEnd();

            ForceDisengageStaticGroup();


            Advance1HourForScripts();

            if (scenarioState.dateTime.Hour == 0)
            {
                RefreshDailyPowerInfluenceMapCaches();
                RefreshTheaters();
            }
        }

        public void RefreshDailyPowerInfluenceMapCaches()
        {
            foreach (var sideState in sideStates.Where(side => side != null))
            {
                sideState.powerInfluenceMapCache = StrategicInfluenceMapUtility.BuildPowerCache(this, sideState);
            }
        }

        void Advance1HourForReinforcement()
        {
            foreach(var group in strategicGroups)
            {
                var arriveState = group.arriveState;
                if(arriveState != null && !arriveState.arrived && arriveState.arriveTime <= scenarioState.dateTime && !group.IsOnMap())
                {
                    var toCell = arriveState.arriveTo.GetCell();
                    if (group.MoveToCell(toCell, false))
                    {
                        // TODO: Use "Mobilisation"?
                        AddLog($"{group.name.GetShortName()} arrived at {toCell.GetLocationSummary()}", group.side);
                    }
                }
                // if(group.arragroup.IsOnMap())
            }
        }

        void Advance1HourForScripts()
        {
            if(scenarioState.enableVladivostokSquadronScript)
            {
                BuiltinScenarioScripts.RunVladivostokSquadronScript(this);
            }
        }

        static TimeSpan oneWeekTimeSpan = TimeSpan.FromDays(7);

        public void Advance1HourForContactReport()
        {
            var observerObservedCellToContactReport = navalContactReports.ToDictionary(
                c => (c.GetObserverSide(), c.GetObservedSide(), c.GetCell()),
                c => c
            );

            // Create or update Contact Report
            foreach(var cellFleetGroupsGrouping in IterIndependentStrategicGroups()
                    .Where(g => g.type == StrategicGroup.Type.Fleet)
                    .GroupBy(g => g.cell))
            {
                var cell = cellFleetGroupsGrouping.Key;
                var sideFleetGroupsGroupings = cellFleetGroupsGrouping.GroupBy(g => g.side).ToList();

                foreach(var observedSideFleetGroupsGrouping in sideFleetGroupsGroupings)
                {
                    var observedSide = observedSideFleetGroupsGrouping.Key;
                    var observedSideFleetGroups = observedSideFleetGroupsGrouping.ToList();
                    // ObservedSide may be observed by static observe point (coast watcher, friendly merchant, or not explicily modeled aux ships) or other side's ship
                    var observedSideDeployedShipLogs = observedSideFleetGroups.SelectMany(g => g.WalkGroupMembersDeployedShips()).ToList();
                    var footprint = observedSideDeployedShipLogs.Count * 1f;

                    // Collect internal hide value for observed side
                    var observedSideInternalHideValue = cell.CellSideInfos.FirstOrDefault(info => info.GetSide() == observedSide)?.interalHideValue ?? 0;
                    var totalFootprint = footprint - observedSideInternalHideValue;

                    // Collect Interval search value for observer side
                    // var observerSideToSearchValue = cell.CellSideInfos.ToDictionary(info => info.GetSide(), info => info.internalSearchValue + info.merchantShipTraffic);
                    var observerSideToSearchValue = cell.CellSideInfos.ToDictionary(info => info.GetSide(), info => info.internalSearchValue);
                    foreach(var observerSideFleetGroupsGrouping in sideFleetGroupsGroupings)
                    {
                        var observerSide = observerSideFleetGroupsGrouping.Key;
                        var observerDeployShipCount = observerSideFleetGroupsGrouping.Sum(g => g.WalkGroupMembersDeployedShips().Count());
                        if(!observerSideToSearchValue.ContainsKey(observerSide))
                        {
                            observerSideToSearchValue[observerSide] = 0;
                        }
                        observerSideToSearchValue[observerSide] += observerDeployShipCount; // Use a coef?
                    }

                    foreach(var (observerSide, observerSideSearchValue) in observerSideToSearchValue)
                    {
                        if(observerSide != observedSide)
                        {
                            var combinedSearchValue = observerSideSearchValue + totalFootprint;
                            combinedSearchValue *= GetSearchValueDayNightCoef(cell);

                            // Cancel cell.SearchAreaSqKm to favor of hard-coded parameter to find a good "feel" now, poor "analytic" model should be abandoned at this point.
                            // var searchAreaCoef = Math.Max(cell.SearchAreaSqKm, 1) / 2500; // TODO: Redesign is expected, now handle it as a const 1
                            var searchAreaCoef = cell.IsAreaCell() ? 3 : 0.5f;
                            if(RandomUtils.NextFloat() <= combinedSearchValue / (100 * searchAreaCoef))
                            {
                                var key = (observerSide, observedSide, cell);
                                var matchedContactReport = observerObservedCellToContactReport.GetValueOrDefault(key);
                                UpdateContactReport(observerSide, observedSide, cell, observedSideDeployedShipLogs, matchedContactReport);

                                // var key = (observerSide, observedSide, cell);
                                // if(!observerObservedCellToContactReport.TryGetValue(key, out var matchedContactReport))
                                // {
                                //     matchedContactReport = new()
                                //     {
                                //         observerSideId = observerSide.objectId,
                                //         observedSideId = observedSide.objectId,
                                //         position = cell.ToXY(),
                                //     };
                                //     EntityManager.Instance.Register(matchedContactReport, this);
                                //     navalContactReports.Add(matchedContactReport);
                                // }

                                // // matchedContactReport.UpdateTo(scenarioState.dateTime, shipLogs);
                                // matchedContactReport.UpdateTo(scenarioState.dateTime, observedSideDeployedShipLogs);
                                
                                // var msg = $"Contact Report: {matchedContactReport}";
                                // ServiceLocator.Get<ILoggerService>().Log(msg);
                                // AddLog(msg, observerSide);
                            }
                        }
                    }
                }
            }

            // Delete outdated Contact Report
            // var outdatedContactReports = navalContactReports.Where(c => scenarioState.dateTime - c.dateTime > oneWeekTimeSpan).ToList();
            var outdatedContactReports = navalContactReports.Where(c => c.GetHoursToCurrent() > NavalContactReport.threatMaintainedHours).ToList();
            foreach(var outdatedContactReport in outdatedContactReports)
            {
                navalContactReports.Remove(outdatedContactReport);

                // erviceLocator.Get<ILoggerService>().Log($"Lost Contact: {outdatedContactReport}");
            }
        }

        public void UpdateContactReport(SideState observerSide, SideState observedSide, Cell cell, List<ShipLog> observedSideDeployedShipLogs, NavalContactReport matchedContactReport)
        {
            if(matchedContactReport == null)
            {
                matchedContactReport = new()
                {
                    observerSideId = observerSide.objectId,
                    observedSideId = observedSide.objectId,
                    position = cell.ToXY(),
                };
                EntityManager.Instance.Register(matchedContactReport, this);
                navalContactReports.Add(matchedContactReport);
            }

            // matchedContactReport.UpdateTo(scenarioState.dateTime, shipLogs);
            matchedContactReport.UpdateTo(scenarioState.dateTime, observedSideDeployedShipLogs);
            
            var msg = $"Contact Report: {matchedContactReport}";
            ServiceLocator.Get<ILoggerService>().Log(msg);
            
            // AddLog(msg, observerSide);
            AddLog(matchedContactReport.ToLazyLocalizedString(), observerSide);
        }

        public void Advance1HourForRaiding()
        {
            foreach(var cellFleetGroupsGrouping in IterIndependentStrategicGroups()
                    .Where(g => g.type == StrategicGroup.Type.Fleet)
                    .GroupBy(g => g.cell))
            {
                var cell = cellFleetGroupsGrouping.Key;
                var sideFleetGroupsGroupings = cellFleetGroupsGrouping.GroupBy(g => g.side).ToList();
                var searchAreaCoef = cell.IsAreaCell() ? 3 : 0.5f;

                foreach(var sideFleetGroupsGrouping in sideFleetGroupsGroupings)
                {
                    var raidingSide = sideFleetGroupsGrouping.Key;
                    var raidingFleetGroups = sideFleetGroupsGrouping.ToList();
                    var raidingSideSearchShips = raidingFleetGroups.Sum(g => g.WalkGroupMembersDeployedShips().Count());
                    // var raidingSideSearchValue = raidingSideSearchShips * 1f;
                    // var raidingSideSearchValue = raidingSideSearchShips * 2f; // Increase the base raiding probability
                    var raidingSideSearchValue = raidingSideSearchShips * 8f; // Increase the base raiding probability
                    raidingSideSearchValue *= GetSearchValueDayNightCoef(cell);

                    foreach(var raidedSideInfo in cell.CellSideInfos.Where(info => info.sideObjectId != raidingSide.objectId && info.merchantShipTraffic > 0))
                    {
                        // var merchantShipProb = raidedSideInfo.merchantShipTraffic / 100;
                        var merchantShipProb = 2 * raidedSideInfo.merchantShipTraffic / 100;
                        if(RandomUtils.NextFloat() <= merchantShipProb) // A potential target appear
                        {
                            if(RandomUtils.NextFloat() <= raidingSideSearchValue / (100 * searchAreaCoef))
                            {
                                // Locate and destroy a cargo (assume no ammu is used at the current setting)
                                raidingSide.victoryPoints += 1;
                                // AddLog($"Raiders of {raidingSide.name.GetShortName()} sink a merchant ship in the {cell.GetLocationSummary()}", null);
                                
                                var log = LazyLocalizedString.MakeTemplate(
                                    "Raiders of {0} sank a merchant ship in the {1}",
                                    LazyLocalizedString.MakeGlobalStringShort(raidingSide.name),
                                    LazyLocalizedString.MakeGlobalStringShort(cell.GetLocationSummaryGlobalString())
                                );
                                AddLog(log, null);

                                var raidedSide = raidedSideInfo.GetSide();
                                UpdateContactReport(
                                    raidedSide,
                                    raidingSide,
                                    cell,
                                    raidingFleetGroups.SelectMany(g => g.WalkGroupMembersDeployedShips()).ToList(),
                                    navalContactReports.FirstOrDefault(c => c.observerSideId == raidedSide.objectId && c.observedSideId == raidingSide.objectId && c.GetCell() == cell)
                                );
                            }
                        }
                    }
                }
            }
        }

        public void ForceDisengageStaticGroup()
        {
            foreach(var group in IterIndependentStrategicGroups())
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

        public void Advance1HourForDetachedAutoReattach()
        {
            foreach (var member in IterAllStrategicGroupMembers().ToList())
            {
                if (member == null || !member.enableAutoReattach)
                    continue;

                if (member.GetDetachedFromGroup() == null)
                {
                    IStrategicGroupMemberReferenceable.ClearDetachedFromGroupState(member);
                    continue;
                }

                if (!member.IsOnSameCellWithDetachedFromGroup())
                    continue;

                IStrategicGroupMemberReferenceable.TryReattachToDetachedFromGroup(member);
            }
        }

        public void Advance1HourForGroupPosture()
        {
            foreach (var group in IterIndependentStrategicGroups())
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
                            .FirstOrDefault(g => g.side != group.side && g != null && g.IsArmy() && g.posture != StrategicGroup.GroupPostureType.Disengaged);
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
                    ), null);
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
                    ), null);
                }
            }
        }

        public static Leader FindStrategicLeaderFromGroups(List<StrategicGroup> groups)
        {
            groups = groups.Where(g => g.leaderReference.Get() != null).ToList();
            if(groups.Count == 0)
                return null;

            var groupTons = groups.Select(g => g.GetShipTons()).ToList();
            var maxGroupTons = groupTons.Max();
            var maxIdx = groupTons.IndexOf(maxGroupTons);
            return groups[maxIdx].leaderReference.Get(); // TODO: Infer leader from subordinate
        }

        public void RefreshPendingNavalCombats()
        {
            pendingNavalCombats.Clear();

            // var recentContactReportPairSets = navalContactReports.Where(c => c.GetTimeSpanToCurrent().TotalHours <= 2).Select(c => (c.GetObserverSide(), c.GetObservedSide())).ToHashSet();
            var recentContactReportPairSets = navalContactReports.Where(c => c.GetTimeSpanToCurrent().TotalHours <= 0).Select(c => (c.GetObserverSide(), c.GetObservedSide())).ToHashSet();

            foreach (var g in strategicGroups.Where(g => g.NavalCombatable()).GroupBy(g => g.cell))
            {
                var cell = g.Key;
                var side2GroupsGp = g.GroupBy(g => g.side).ToList();
                if (side2GroupsGp.Count >= 2)
                {
                    var g0 = side2GroupsGp[0];
                    var g1 = side2GroupsGp[1];

                    var g0hasActive = g0.Any(g => g.posture == StrategicGroup.GroupPostureType.Active);
                    var g1hasActive = g1.Any(g => g.posture == StrategicGroup.GroupPostureType.Active);

                    var g0attackable = g0hasActive;
                    var g1attackable = g1hasActive;

                    if(scenarioState.enableContactReportBasedNavalCombat)
                    {
                        g0attackable = g0attackable && recentContactReportPairSets.Contains((g0.Key, g1.Key)); // g0 is active and detect enemy
                        g1attackable = g1attackable && recentContactReportPairSets.Contains((g1.Key, g0.Key));
                    }

                    var pendingCombatGenerated = g0attackable || g1attackable;

                    // Strategic Maneuver Disengagement Roll
                    if(scenarioState.enableStrategicDisengagementRoll && pendingCombatGenerated && !(g0hasActive && g1hasActive))
                    {
                        var g0leader = FindStrategicLeaderFromGroups(g0.ToList());
                        var g1leader = FindStrategicLeaderFromGroups(g1.ToList());
                        var g0maneuverValue = StrategicGroup.GetManeuverValue(g0leader);
                        var g1maneuverValue = StrategicGroup.GetManeuverValue(g1leader);
                        var g0roll = RandomUtils.D6();
                        var g1roll = RandomUtils.D6();
                        var g0finalValue = g0maneuverValue + g0roll;
                        var g1finalValue = g1maneuverValue + g1roll;

                        var side0Name = g0.Key.name.GetShortName();
                        var side1Name = g1.Key.name.GetShortName();

                        // var generalDesc = $"Maneuver Roll: {side0Name} {g0leader?.name?.GetShortName()} ({g0maneuverValue} + {g0roll} = {g0finalValue}) vs {side1Name} {g1leader?.name?.GetShortName()} ({g1maneuverValue} + {g1roll} = {g1finalValue})";
                        var generalDesc = LazyLocalizedString.MakeTemplate(
                            "Maneuver Roll: {0} [{1} ({2}) + {3} (D6) = {4}] vs {5} [{6} ({7}) + {8} (D6) = {9}]",

                            LazyLocalizedString.MakeGlobalStringShort(g0.Key.name),
                            LazyLocalizedString.MakeRaw(g0maneuverValue),
                            LazyLocalizedString.MakeGlobalStringShort(g0leader?.name),
                            LazyLocalizedString.MakeRaw(g0roll),
                            LazyLocalizedString.MakeRaw(g0finalValue),

                            LazyLocalizedString.MakeGlobalStringShort(g1.Key.name),
                            LazyLocalizedString.MakeRaw(g1maneuverValue),
                            LazyLocalizedString.MakeGlobalStringShort(g1leader?.name),
                            LazyLocalizedString.MakeRaw(g1roll),
                            LazyLocalizedString.MakeRaw(g1finalValue)
                        );
                        // var resultDesc = "";
                        LazyLocalizedString resultDesc = null;

                        if(g0hasActive && !g1hasActive) // g1 try to disengage from g0
                        {
                            if(g0finalValue >= g1finalValue)
                            {
                                pendingCombatGenerated = true;

                                // resultDesc = $"{side1Name} failed to disengage";
                                resultDesc = LazyLocalizedString.MakeTemplate(
                                    "{0} failed to disengage",
                                    LazyLocalizedString.MakeGlobalStringShort(g1.Key.name)
                                );
                            }
                            else
                            {
                                pendingCombatGenerated = false;

                                // resultDesc = $"{side1Name} success to disengage";
                                resultDesc = LazyLocalizedString.MakeTemplate(
                                    "{0} success to disengage",
                                    LazyLocalizedString.MakeGlobalStringShort(g1.Key.name)
                                );
                            }
                        }
                        else if(!g0hasActive && g1hasActive)
                        {
                            if(g0finalValue <= g1finalValue)
                            {
                                pendingCombatGenerated = true;

                                // resultDesc = $"{side0Name} failed to disengage";
                                resultDesc = LazyLocalizedString.MakeTemplate(
                                    "{0} failed to disengage",
                                    LazyLocalizedString.MakeGlobalStringShort(g0.Key.name)
                                );
                            }
                            else
                            {
                                pendingCombatGenerated = false;

                                // resultDesc = $"{side0Name} success to disengage";
                                resultDesc = LazyLocalizedString.MakeTemplate(
                                    "{0} success to disengage",
                                    LazyLocalizedString.MakeGlobalStringShort(g0.Key.name)
                                );
                            }
                        }

                        var log = LazyLocalizedString.MakeTemplate("{0} {1}", generalDesc, resultDesc);

                        // AddLog($"{generalDesc} {resultDesc}", null);
                        AddLog(log, null);
                    }

                    if(pendingCombatGenerated)
                    {
                        var pendingCombat = new PendingNavalCombat()
                        {
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
        }

        void CombinedAutoCombinableAndDissolvable()
        {
            foreach(var group in strategicGroups.ToList())
            {
                var parentGroup = group.parentGroupReference.Get();
                if (group.autoCombinable && group.deployState == StrategicGroup.DeployState.Independent)
                {
                    // if (parentGroup.x == group.x && parentGroup.y == group.y)
                    if (parentGroup.cell == group.cell)
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

            foreach (var group in strategicGroups.ToList())
            {
                if (!group.detachedRepair || group.deployState != StrategicGroup.DeployState.Independent)
                    continue;

                var parentGroup = group.parentGroupReference.Get();
                if (parentGroup == null)
                    continue;

                foreach (var memberRef in group.subordinatesCombined.ToList())
                {
                    if (memberRef.Get() is not ShipLog shipLog || StrategicGroupSubGroupUtility.NeedsDetachForRepair(shipLog))
                        continue;

                    var member = (IStrategicGroupMemberReferenceable)shipLog;
                    var detachedFromGroup = member.GetDetachedFromGroup();
                    if (detachedFromGroup != null)
                    {
                        if (member.IsOnSameCellWithDetachedFromGroup())
                        {
                            IStrategicGroupMemberReferenceable.TryReattachToDetachedFromGroup(shipLog);
                        }

                        continue;
                    }

                    if (parentGroup.cell == group.cell)
                    {
                        group.MoveElementTo(shipLog, parentGroup);
                    }
                }

                if (group.subordinatesCombined.Count > 0)
                    continue;

                group.ClearPlannedPath();
                group.RemoveFromMap();
                group.AttachTo(null);

                EntityManager.Instance.Unregister(group);
                strategicGroups.Remove(group);
            }
        }

        public void Advance1HourForSupply()
        {
            foreach (var group in IterIndependentStrategicGroups())
            {
                foreach (var landUnit in group.WalkGroupMembers<LandUnit>())
                {
                    landUnit.supplyTons = Math.Max(0, landUnit.supplyTons + (landUnit.supplyGeneratedTons - landUnit.GetSupplyCostTonsPerDay()) / 24);
                }
            }

            foreach (var group in IterIndependentStrategicGroups())
            {
                if (group.plannedPath.Count > 0) // Only moving (has plannedPath) ship cost supply.
                {
                    foreach (var shipLog in group.WalkGroupMembersDeployedShips())
                    {
                        shipLog.supplyTons = Math.Max(0, shipLog.supplyTons - shipLog.GetSupplyCostTonsPerHour());
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

        public bool TryDestroyGroupIfEmptyRecursive(StrategicGroup group)
        {
            var destroyedAny = false;
            while (group != null && group.subordinatesCombined.Count == 0)
            {
                var parentGroup = group.parentGroupReference.Get();
                group.ClearPlannedPath();
                group.RemoveFromMap();
                ClearDetachedFromReferenceFromAllMembers(group.objectId);
                IStrategicGroupMemberReferenceable.ClearDetachedFromGroupState(group);
                group.AttachTo(null);

                EntityManager.Instance.Unregister(group);
                strategicGroups.Remove(group);

                destroyedAny = true;
                group = parentGroup;
            }

            return destroyedAny;
        }
        
        public void DoShipAmmunitionReplenishment()
        {
            foreach (var group in IterIndependentStrategicGroups())
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

        public void Advance1HourForOutOfFuelFleetCheck()
        {
            foreach (var strategicGroup in IterIndependentStrategicGroups())
            {
                strategicGroup.CheckOutOfFuelFleetGroupAndForceReturnToBase();
            }
        }

        public void Advance1HourForMovement()
        {
            foreach (var strategicGroup in IterIndependentStrategicGroups())
            {
                strategicGroup.Advance1HourForMovement();

                // if (strategicGroup.plannedPath.Count == 0)
                // {
                //     strategicGroup.moveProgressionKm = 0;
                // }
                // else
                // {
                //     var speedKmPerHour = strategicGroup.GetSpeedKmPerHour();
                //     var moveKmCap = speedKmPerHour * 1;
                //     while (moveKmCap > 0 && strategicGroup.plannedPath.Count >= 2)
                //     {
                //         var valid = strategicGroup.TryGetDistanceToNextLocationInPlannedPathWithoutProgression(out var cellDistKm);
                //         if(!valid)
                //         {
                //             break;
                //         }

                //         var nextDistKm = cellDistKm - strategicGroup.moveProgressionKm; // 50km/hex
                //         if (moveKmCap < nextDistKm)
                //         {
                //             strategicGroup.moveProgressionKm += moveKmCap;
                //             moveKmCap = 0;
                //         }
                //         else
                //         {
                //             moveKmCap -= nextDistKm;
                //             strategicGroup.plannedPath.RemoveAt(0);
                //             // strategicGroup.MoveToXY(strategicGroup.plannedPath[0].x, strategicGroup.plannedPath[0].y, true);
                //             strategicGroup.MoveToCell(strategicGroup.plannedPath[0].GetCell(), true); // TODO: Generalize to Area System
                            
                //             strategicGroup.moveProgressionKm = 0;
                //             if (strategicGroup.plannedPath.Count < 2)
                //             {
                //                 strategicGroup.plannedPath.Clear();
                //             }
                //         }
                //     }
                // }
            }
        }

        public void Advance1HourForMission()
        {
            // Mission state transition
            foreach (var mission in missions.ToList()) // Advance Mission may generate new mission
            {
                mission.TransitionMission();
            }

            // UpdateStrategicGroups
            foreach(var mission in missions)
            {
                mission.UpdateStrategicGroups();
            }
        }

        public IEnumerable<Cell> IterCells()
        {
            foreach(var areaCell in areaCells)
            {
                yield return areaCell;
            }

            var width = GetMapWidth();
            var height = GetMapHeight();
            for(var x=0; x<width; x++)
            {
                for(var y=0;y<height;y++)
                {
                    yield return cellMatrix[x, y];
                }
            }
        }


        public IEnumerable<StrategicGroup> IterIndependentStrategicGroups()
        {
            return strategicGroups.Where(group => group.deployState == StrategicGroup.DeployState.Independent);
        }

        public HashSet<Cell> GetCellsHasStrategicGroup()
        {
            return IterIndependentStrategicGroups().Select(g => g.cell).ToHashSet();
        }

        public IEnumerable<StrategicGroup> IterIndependentStrategicGroupsOrderedByCell()
        {
            foreach(var cell in GetCellsHasStrategicGroup())
            {
                foreach (var group in cell.StrategicGroupReferences.Select(rp => rp.Get()))
                {
                    yield return group;
                }
            }
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

        public void RefreshTheaters()
        {
            theaters ??= new();
            var oldTheaters = theaters.Where(theater => theater != null).ToList();
            var scannedTheaters = ScanTheaters().ToList();

            var oldCellSets = oldTheaters
                .Select(theater => BuildTheaterCellKeySet(theater.cells))
                .ToList();
            var newCellSets = scannedTheaters
                .Select(theater => BuildTheaterCellKeySet(theater.cells))
                .ToList();

            var candidates = new List<TheaterMatchCandidate>();
            for (var oldIdx = 0; oldIdx < oldTheaters.Count; oldIdx++)
            {
                for (var newIdx = 0; newIdx < scannedTheaters.Count; newIdx++)
                {
                    var intersectionCount = CountIntersection(oldCellSets[oldIdx], newCellSets[newIdx]);
                    if (intersectionCount <= 0)
                        continue;

                    var unionCount = oldCellSets[oldIdx].Count + newCellSets[newIdx].Count - intersectionCount;
                    if (unionCount <= 0)
                        continue;

                    candidates.Add(new TheaterMatchCandidate()
                    {
                        oldIndex = oldIdx,
                        newIndex = newIdx,
                        intersectionCount = intersectionCount,
                        jaccard = (float)intersectionCount / unionCount
                    });
                }
            }

            candidates.Sort((left, right) =>
            {
                var cmp = right.jaccard.CompareTo(left.jaccard);
                if (cmp != 0)
                    return cmp;

                cmp = right.intersectionCount.CompareTo(left.intersectionCount);
                if (cmp != 0)
                    return cmp;

                cmp = left.oldIndex.CompareTo(right.oldIndex);
                if (cmp != 0)
                    return cmp;

                return left.newIndex.CompareTo(right.newIndex);
            });

            var matchedOldIndices = new HashSet<int>();
            var matchedNewIndices = new HashSet<int>();
            foreach (var candidate in candidates)
            {
                if (!matchedOldIndices.Add(candidate.oldIndex))
                    continue;
                if (!matchedNewIndices.Add(candidate.newIndex))
                {
                    matchedOldIndices.Remove(candidate.oldIndex);
                    continue;
                }

                var oldTheater = oldTheaters[candidate.oldIndex];
                var scannedTheater = scannedTheaters[candidate.newIndex];
                oldTheater.sideObjectId = scannedTheater.sideObjectId;
                oldTheater.cells = scannedTheater.cells;
                oldTheater.frontlineCellInfos = scannedTheater.frontlineCellInfos;
            }

            foreach (var oldIdx in Enumerable.Range(0, oldTheaters.Count))
            {
                if (matchedOldIndices.Contains(oldIdx))
                    continue;

                var removeTheater = oldTheaters[oldIdx];
                theaters.Remove(removeTheater);
                EntityManager.Instance.Unregister(removeTheater);
            }

            foreach (var newIdx in Enumerable.Range(0, scannedTheaters.Count))
            {
                if (matchedNewIndices.Contains(newIdx))
                    continue;

                var newTheater = scannedTheaters[newIdx];
                newTheater.name = GenerateNameForNewTheater(newTheater);
                theaters.Add(newTheater);
                EntityManager.Instance.Register(newTheater, this);
            }

            RefreshTheaterFrontlineWeightRequested();
        }

        public void ClearTheaters()
        {
            if (theaters == null)
                return;

            foreach (var theater in theaters.Where(theater => theater != null).ToList())
            {
                EntityManager.Instance.Unregister(theater);
            }
            theaters.Clear();
        }

        IEnumerable<Theater> ScanTheaters()
        {
            if (cellMatrix == null)
                yield break;

            var visited = new HashSet<(int, int)>();
            var width = GetMapWidth();
            var height = GetMapHeight();

            for (var x = 0; x < width; x++)
            {
                for (var y = 0; y < height; y++)
                {
                    if (!visited.Add((x, y)))
                        continue;

                    var startCell = cellMatrix[x, y];
                    var side = startCell?.GetHexSide();
                    if (side == null)
                        continue;

                    var queue = new Queue<Cell>();
                    var memberCells = new List<XY>();
                    queue.Enqueue(startCell);

                    while (queue.Count > 0)
                    {
                        var cell = queue.Dequeue();
                        if (cell == null)
                            continue;

                        var cellSide = cell.GetHexSide();
                        if (cellSide == null || cellSide.objectId != side.objectId)
                            continue;

                        memberCells.Add(cell.ToXY());

                        foreach (var neighbor in cell.GetNeighbors())
                        {
                            if (neighbor == null || neighbor.IsAreaCell())
                                continue;

                            var neighborSide = neighbor.GetHexSide();
                            if (neighborSide != null &&
                                neighborSide.objectId == side.objectId &&
                                visited.Add((neighbor.x, neighbor.y)))
                            {
                                queue.Enqueue(neighbor);
                            }
                        }
                    }

                    if (memberCells.Count == 0)
                        continue;

                    yield return new Theater()
                    {
                        sideObjectId = side.objectId,
                        cells = memberCells
                    };
                }
            }
        }

        List<XY> BuildTheaterFrontlineCells(IEnumerable<XY> theaterCells)
        {
            var cellList = (theaterCells ?? Enumerable.Empty<XY>())
                .Where(cell => cell != null && string.IsNullOrWhiteSpace(cell.areaCellObjectId))
                .ToList();
            var cellSet = BuildTheaterCellKeySet(cellList);
            var result = new List<XY>();
            var directions = Enum.GetValues(typeof(EdgeDirection)).Cast<EdgeDirection>();
            foreach (var cellXY in cellList)
            {
                var cell = cellXY.GetCell();
                if (cell == null || !cell.IsGridCell() || !cell.IsArmyPassable())
                    continue;

                var isFrontline = false;
                foreach (var direction in directions)
                {
                    var neighbor = cell.GetNeighbor(direction);
                    if (neighbor == null ||
                        neighbor.IsAreaCell() ||
                        !neighbor.IsArmyPassable())
                    {
                        continue;
                    }

                    if (!cellSet.Contains((neighbor.x, neighbor.y)))
                    {
                        isFrontline = true;
                        break;
                    }
                }

                if (isFrontline)
                {
                    result.Add(cellXY);
                }
            }
            return result;
        }

        public Dictionary<(int, int), string> BuildTheaterFrontlineOverlayTexts(Theater theater)
        {
            var result = new Dictionary<(int, int), string>();
            if (theater == null)
                return result;

            var theaterCellSet = BuildTheaterCellKeySet(theater.cells);
            var passableFrontlineCells = EnumerateFrontlineCells(theater)
                .Select(cellXY => cellXY?.GetCell())
                .Where(cell => cell != null && cell.IsGridCell() && cell.IsArmyPassable())
                .GroupBy(cell => (cell.x, cell.y))
                .Select(group => group.First())
                .ToList();

            foreach (var cell in passableFrontlineCells)
            {
                result[(cell.x, cell.y)] = "0";
            }

            var insideDistances = BuildTheaterSignedLayerDistances(
                passableFrontlineCells,
                neighbor => theaterCellSet.Contains((neighbor.x, neighbor.y)),
                includeSeedCell: false
            );
            foreach (var (xy, distance) in insideDistances)
            {
                result[xy] = $"-{distance}";
            }

            var outsideDistances = BuildTheaterSignedLayerDistances(
                passableFrontlineCells,
                neighbor => !theaterCellSet.Contains((neighbor.x, neighbor.y)),
                includeSeedCell: false
            );
            foreach (var (xy, distance) in outsideDistances)
            {
                result[xy] = $"+{distance}";
            }

            return result;
        }

        public Dictionary<(int, int), float> BuildTheaterFrontlineWeightRequestedValues(Theater theater)
        {
            var result = new Dictionary<(int, int), float>();
            if (theater == null)
                return result;

            foreach (var info in theater.frontlineCellInfos ?? Enumerable.Empty<FrontlineCellInfo>())
            {
                var cell = info?.xy?.GetCell();
                if (cell == null || !cell.IsGridCell())
                    continue;

                result[(cell.x, cell.y)] = info.weightRequested;
            }

            return result;
        }

        public Dictionary<(int, int), string> BuildTheaterFrontlineWeightRequestedOverlayTexts(Theater theater)
        {
            var result = new Dictionary<(int, int), string>();
            if (theater == null)
                return result;

            foreach (var cell in (theater.cells ?? Enumerable.Empty<XY>())
                .Select(xy => xy?.GetCell())
                .Where(cell => cell != null && cell.IsGridCell())
                .GroupBy(cell => (cell.x, cell.y))
                .Select(group => group.First()))
            {
                result[(cell.x, cell.y)] = "X";
            }

            foreach (var cell in EnumerateFrontlineCells(theater)
                .Select(xy => xy?.GetCell())
                .Where(cell => cell != null && cell.IsGridCell() && cell.IsArmyPassable())
                .GroupBy(cell => (cell.x, cell.y))
                .Select(group => group.First()))
            {
                result[(cell.x, cell.y)] = "0";
            }

            foreach (var (xy, value) in BuildTheaterFrontlineWeightRequestedValues(theater))
            {
                result[xy] = StrategicInfluenceMapUtility.FormatValue(value);
            }

            return result;
        }

        public TheaterFrontlineWeightRequestedStats GetTheaterFrontlineWeightRequestedStats(Theater theater)
        {
            var frontlineCells = EnumerateFrontlineCells(theater).ToList();
            var storedValueMap = BuildTheaterFrontlineWeightRequestedValues(theater);
            var values = frontlineCells
                .Select(xy => xy?.GetCell())
                .Where(cell => cell != null && cell.IsGridCell() && cell.IsArmyPassable())
                .GroupBy(cell => (cell.x, cell.y))
                .Select(group => storedValueMap.GetValueOrDefault(group.Key))
                .ToList();
            var stats = new TheaterFrontlineWeightRequestedStats()
            {
                frontlineCount = values.Count,
                frontlineSummary = BuildCellSummaryText(frontlineCells)
            };

            if (values.Count == 0)
                return stats;

            stats.min = values.Min();
            stats.max = values.Max();
            stats.average = values.Average();

            var variance = values.Sum(value => Math.Pow(value - stats.average, 2f)) / values.Count;
            stats.standardDeviation = (float)Math.Sqrt(variance);
            return stats;
        }

        IEnumerable<SideState> GetHostileSides(SideState side)
        {
            if (side == null)
                yield break;

            foreach (var candidate in sideStates ?? Enumerable.Empty<SideState>())
            {
                if (candidate == null || candidate == side)
                    continue;

                if (IsHostile(side, candidate))
                    yield return candidate;
            }
        }

        static bool IsHostile(SideState left, SideState right)
        {
            if (left == null || right == null || left == right)
                return false;

            return GetDiplomacyState(left, right) == DiplomacyState.War ||
                   GetDiplomacyState(right, left) == DiplomacyState.War;
        }

        static DiplomacyState? GetDiplomacyState(SideState from, SideState to)
        {
            return from?.diplomacyRelations?
                .FirstOrDefault(relation => relation?.sideObjectId == to?.objectId)?
                .state;
        }

        public string BuildCellSummaryText(IEnumerable<XY> cells)
        {
            var cellList = (cells ?? Enumerable.Empty<XY>())
                .Where(cell => cell != null)
                .ToList();
            var count = cellList.Count;
            if (count <= 0)
                return "0";

            var names = cellList
                .Take(3)
                .Select(cell => GetCellName(cell))
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToList();
            if (names.Count == 0)
                return count.ToString();

            var ellipsis = count > 3 ? "..." : string.Empty;
            return $"{count} ({string.Join(", ", names)}{ellipsis})";
        }

        void RefreshTheaterFrontlineWeightRequested()
        {
            foreach (var theater in theaters ?? Enumerable.Empty<Theater>())
            {
                if (theater == null)
                    continue;

                theater.frontlineCellInfos = BuildFrontlineCellInfos(theater);
            }
        }

        IEnumerable<XY> EnumerateFrontlineCells(Theater theater)
        {
            if (theater == null)
                yield break;

            var hasStoredInfo = false;
            foreach (var info in theater.frontlineCellInfos ?? Enumerable.Empty<FrontlineCellInfo>())
            {
                if (info == null)
                    continue;

                hasStoredInfo = true;
                yield return info.xy;
            }

            if (hasStoredInfo)
                yield break;

            foreach (var xy in BuildTheaterFrontlineCells(theater.cells))
            {
                if (xy != null)
                    yield return xy;
            }
        }

        List<FrontlineCellInfo> BuildFrontlineCellInfos(Theater theater)
        {
            var infos = new List<FrontlineCellInfo>();
            if (theater == null)
                return infos;

            var hostileFields = GetHostileSides(theater.GetSide())
                .Select(side =>
                {
                    if (StrategicInfluenceMapUtility.TryBuildFieldFromValidPowerCache(side, scenarioState, out var field))
                        return field;

                    return StrategicInfluenceMapUtility.BuildPowerField(this, side.objectId);
                })
                .ToList();

            foreach (var cell in BuildTheaterFrontlineCells(theater.cells)
                .Select(xy => xy?.GetCell())
                .Where(cell => cell != null && cell.IsGridCell() && cell.IsArmyPassable())
                .GroupBy(cell => (cell.x, cell.y))
                .Select(group => group.First()))
            {
                var total = 0f;
                foreach (var hostileField in hostileFields)
                    total += StrategicInfluenceMapUtility.GetValueAtCell(hostileField, cell);

                infos.Add(new FrontlineCellInfo
                {
                    x = cell.x,
                    y = cell.y,
                    weightRequested = total
                });
            }

            return infos;
        }

        static Dictionary<(int, int), int> BuildTheaterSignedLayerDistances(
            IEnumerable<Cell> seedCells,
            Func<Cell, bool> canTraverse,
            bool includeSeedCell)
        {
            var distances = new Dictionary<(int, int), int>();
            var queue = new Queue<Cell>();
            foreach (var cell in seedCells ?? Enumerable.Empty<Cell>())
            {
                if (cell == null)
                    continue;

                var key = (cell.x, cell.y);
                if (distances.ContainsKey(key))
                    continue;

                distances[key] = 0;
                queue.Enqueue(cell);
            }

            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                var currentDistance = distances[(cell.x, cell.y)];
                foreach (var neighbor in cell.GetNeighbors())
                {
                    if (neighbor == null ||
                        neighbor.IsAreaCell() ||
                        !neighbor.IsArmyPassable() ||
                        !canTraverse(neighbor))
                    {
                        continue;
                    }

                    var neighborKey = (neighbor.x, neighbor.y);
                    if (distances.ContainsKey(neighborKey))
                        continue;

                    distances[neighborKey] = currentDistance + 1;
                    queue.Enqueue(neighbor);
                }
            }

            if (!includeSeedCell)
            {
                foreach (var cell in seedCells ?? Enumerable.Empty<Cell>())
                {
                    if (cell != null)
                    {
                        distances.Remove((cell.x, cell.y));
                    }
                }
            }

            return distances;
        }

        HashSet<(int, int)> BuildTheaterCellKeySet(IEnumerable<XY> cells)
        {
            var result = new HashSet<(int, int)>();
            foreach (var cell in cells ?? Enumerable.Empty<XY>())
            {
                if (cell?.areaCellObjectId != null)
                    continue;
                result.Add((cell.x, cell.y));
            }
            return result;
        }

        static int CountIntersection(HashSet<(int, int)> left, HashSet<(int, int)> right)
        {
            if (left == null || right == null || left.Count == 0 || right.Count == 0)
                return 0;

            if (left.Count > right.Count)
            {
                (left, right) = (right, left);
            }

            var count = 0;
            foreach (var cell in left)
            {
                if (right.Contains(cell))
                {
                    count++;
                }
            }
            return count;
        }

        GlobalString GenerateNameForNewTheater(Theater theater)
        {
            var side = theater?.GetSide();
            var candidateGroups = IterateTheaterNamingGroups(theater?.cells, side);
            StrategicGroup bestGroup = null;
            var bestPower = float.MinValue;
            foreach (var group in candidateGroups)
            {
                var power = GetTheaterNamingPower(group);
                if (power > bestPower ||
                    (Math.Abs(power - bestPower) < 0.0001f &&
                     TheaterNamingGroupHasNoSuperior(group) &&
                     !TheaterNamingGroupHasNoSuperior(bestGroup)))
                {
                    bestPower = power;
                    bestGroup = group;
                }
            }

            if (bestGroup != null)
            {
                return CombineGlobalStrings(bestGroup.name, TheaterSuffixName);
            }

            foreach (var cellXY in theater?.cells ?? Enumerable.Empty<XY>())
            {
                var label = cellXY?.GetCell()?.Label;
                if (label != null)
                {
                    return label.Clone();
                }
            }

            return NewTheaterName.Clone();
        }

        float GetTheaterNamingPower(StrategicGroup group)
        {
            if (group == null)
                return 0f;

            var landPower = group
                .WalkGroupMembers<LandUnit>(includeNotCombined: true)
                .Sum(landUnit => landUnit?.GetCombinedPowerPoint(false) ?? 0f);
            var shipPower = group
                .WalkGroupMembers<ShipLog>(includeNotCombined: true)
                .Sum(shipLog => shipLog?.GetCombinedPowerPoint(false) ?? 0f);
            return landPower + shipPower;
        }

        static bool TheaterNamingGroupHasNoSuperior(StrategicGroup group)
        {
            var parentGroup = group?.parentGroupReference?.Get();
            return parentGroup == null || parentGroup == group;
        }

        IEnumerable<StrategicGroup> IterateTheaterNamingGroups(IEnumerable<XY> theaterCells, SideState side)
        {
            if (side == null)
                yield break;

            var yieldedGroupIds = new HashSet<string>();
            foreach (var cellXY in theaterCells ?? Enumerable.Empty<XY>())
            {
                var cell = cellXY?.GetCell();
                if (cell == null)
                    continue;

                foreach (var groupRef in cell.StrategicGroupReferences)
                {
                    var group = groupRef.Get();
                    if (group == null ||
                        group.side != side ||
                        group.deployState != StrategicGroup.DeployState.Independent)
                    {
                        continue;
                    }

                    if (yieldedGroupIds.Add(group.objectId))
                    {
                        yield return group;
                    }
                }
            }
        }

        static GlobalString CombineGlobalStrings(GlobalString left, GlobalString right)
        {
            return new()
            {
                english = CombineLocalizedText(left?.english, right?.english),
                japanese = CombineLocalizedText(left?.japanese, right?.japanese),
                chineseSimplified = CombineLocalizedText(left?.chineseSimplified, right?.chineseSimplified),
                chineseTraditional = CombineLocalizedText(left?.chineseTraditional, right?.chineseTraditional)
            };
        }

        static string CombineLocalizedText(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
                return null;
            return $"{left}{right}";
        }

        static readonly GlobalString TheaterSuffixName = new()
        {
            english = " Theater",
            japanese = "\u6226\u533A",
            chineseSimplified = "\u6218\u533A",
            chineseTraditional = "\u6230\u5340"
        };

        static readonly GlobalString NewTheaterName = new()
        {
            english = "New Theater",
            japanese = "\u65B0\u6226\u533A",
            chineseSimplified = "\u65B0\u6218\u533A",
            chineseTraditional = "\u65B0\u6230\u5340"
        };

        class TheaterMatchCandidate
        {
            public int oldIndex;
            public int newIndex;
            public int intersectionCount;
            public float jaccard;
        }

        public void CreateDefaultShipLog()
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
        }

        public void ResetShipLogStates()
        {
            foreach (var shipLog in shipLogs)
            {
                var ctx = shipLog.side?.GetResetDamageExpenditureStateContext() ?? new();

                shipLog.ResetDamageExpenditureState(ctx); // Impose SideState's doctrine

                if (shipLog.mapState == MapState.NotDeployed) // NotDeployed in strategic game is not defined now
                    shipLog.mapState = MapState.Deployed;
            }

            ResetAndRegisterAll();
        }

        public void AddLog(LazyLocalizedString log, SideState side)
        {
            var s = new SidedLazyLocalizedString()
            {
                log=log,
                sideObjectId=side?.objectId
            };
            logs.Insert(0, s);

            logAdded.Invoke(this, s);
        }

        public void AddLog(string rawLog, SideState side) // mainly for debug purpose
        {
            // logs.Insert(0, LazyLocalizedString.MakeRaw(rawLog), side);
            AddLog(LazyLocalizedString.MakeRaw(rawLog), side);
        }

        public void ClearLogs()
        {
            logs.Clear();

            logsRefreshed?.Invoke(this, EventArgs.Empty);
        }

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

        public NavalContactReport PickNavalContactReportByThreat(SideState sideMe)
        {
            var contacts = navalContactReports.Where(c => c.observerSideId == sideMe.objectId).ToList();
            
            if(contacts.Count == 0)
            {
                return null;
            }

            var weights = contacts.Select(c => c.GetThreatScore()).ToList();
            var maxWeight = weights.Max();
            var maxIdx = weights.IndexOf(maxWeight);
            return contacts[maxIdx];
            // return RandomUtils.Sample(samplingContacts, weights);
        }

        public float GetSearchValueDayNightCoef(Cell cell)
        {
            var sunPos = NavalUtils.GetSunPosition(scenarioState.dateTime, new LatLon(cell.latitude, cell.longitude));
            return sunPos.GetDayNightLevel() switch
            {
                DayNightLevel.Day => 1,
                DayNightLevel.Twilight => 0.25f,
                DayNightLevel.Night => 0.01f,
                _ => 1
            };
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

            foreach(var navalContactReport in navalContactReports)
                EntityManager.Instance.Register(navalContactReport, null);

            foreach (var theater in theaters ?? Enumerable.Empty<Theater>())
                EntityManager.Instance.Register(theater, null);

            NormalizeStrategicGroupMembership();
            RebuildCacheForSideStates();
            RebuildCellStrategicGroupReferences();
        }

        IEnumerable<IStrategicGroupMemberReferenceable> IterAllStrategicGroupMembers()
        {
            foreach (var strategicGroup in strategicGroups.Where(group => group != null))
                yield return strategicGroup;

            foreach (var landUnit in landUnits.Where(unit => unit != null))
                yield return landUnit;

            foreach (var shipLog in shipLogs.Where(log => log != null))
                yield return shipLog;
        }

        bool HasAcyclicParentChain(StrategicGroup group, Dictionary<string, StrategicGroup> groupMap)
        {
            if (group == null || string.IsNullOrWhiteSpace(group.objectId))
                return true;

            var visitedGroupIds = new HashSet<string>() { group.objectId };
            var parentId = group.parentGroupReference.referenceId;
            while (!string.IsNullOrWhiteSpace(parentId))
            {
                if (!groupMap.TryGetValue(parentId, out var parentGroup))
                    return false;

                if (!visitedGroupIds.Add(parentId))
                    return false;

                parentId = parentGroup.parentGroupReference.referenceId;
            }

            return true;
        }

        void RemoveDirectMemberReferenceFromAllGroups(string memberObjectId)
        {
            if (string.IsNullOrWhiteSpace(memberObjectId))
                return;

            foreach (var group in strategicGroups.Where(group => group != null))
            {
                group.RemoveDirectMemberReference(memberObjectId);
            }
        }

        void ClearDetachedFromReferenceFromAllMembers(string groupObjectId)
        {
            if (string.IsNullOrWhiteSpace(groupObjectId))
                return;

            foreach (var member in IterAllStrategicGroupMembers())
            {
                if (member?.detachedFromGroupReference?.referenceId != groupObjectId)
                    continue;

                IStrategicGroupMemberReferenceable.ClearDetachedFromGroupState(member);
            }
        }

        static void RemoveIndicesDescending<T>(List<T> list, List<int> indices)
        {
            for (var idx = indices.Count - 1; idx >= 0; idx--)
            {
                list.RemoveAt(indices[idx]);
            }
        }

        void NormalizeStrategicGroupMembership()
        {
            var groupMap = strategicGroups
                .Where(group => group != null && !string.IsNullOrWhiteSpace(group.objectId))
                .GroupBy(group => group.objectId)
                .ToDictionary(group => group.Key, group => group.First());

            var memberMap = IterAllStrategicGroupMembers()
                .Where(member => member != null && !string.IsNullOrWhiteSpace(member.objectId))
                .GroupBy(member => member.objectId)
                .ToDictionary(group => group.Key, group => group.First());

            foreach (var member in memberMap.Values)
            {
                member.detachedFromGroupReference ??= new();

                var parentId = member.parentGroupReference.referenceId;
                if (string.IsNullOrWhiteSpace(parentId) || !groupMap.ContainsKey(parentId))
                {
                    member.parentGroupReference.referenceId = null;
                }
                else if (member is StrategicGroup memberGroup && memberGroup.objectId == parentId)
                {
                    memberGroup.parentGroupReference.referenceId = null;
                }

                var detachedFromId = member.detachedFromGroupReference.referenceId;
                if (string.IsNullOrWhiteSpace(detachedFromId) ||
                    !groupMap.ContainsKey(detachedFromId) ||
                    detachedFromId == member.objectId ||
                    detachedFromId == member.parentGroupReference.referenceId)
                {
                    member.detachedFromGroupReference.referenceId = null;
                    member.enableAutoReattach = false;
                }
            }

            foreach (var group in strategicGroups.Where(group => group != null))
            {
                group.subordinatesCombined ??= new List<StrategicGroupMemberReference>();

                var seenMemberIds = new HashSet<string>();
                var indicesToRemove = new List<int>();
                for (var idx = 0; idx < group.subordinatesCombined.Count; idx++)
                {
                    var memberId = group.subordinatesCombined[idx]?.referenceId;
                    if (string.IsNullOrWhiteSpace(memberId) ||
                        memberId == group.objectId ||
                        !memberMap.ContainsKey(memberId) ||
                        !seenMemberIds.Add(memberId))
                    {
                        indicesToRemove.Add(idx);
                    }
                }

                RemoveIndicesDescending(group.subordinatesCombined, indicesToRemove);
            }

            var claimedParentIds = new Dictionary<string, string>();
            foreach (var group in strategicGroups.Where(group => group != null))
            {
                var duplicateIndices = new List<int>();
                for (var idx = 0; idx < group.subordinatesCombined.Count; idx++)
                {
                    var memberId = group.subordinatesCombined[idx].referenceId;
                    if (claimedParentIds.ContainsKey(memberId))
                    {
                        duplicateIndices.Add(idx);
                        continue;
                    }

                    claimedParentIds.Add(memberId, group.objectId);
                }

                RemoveIndicesDescending(group.subordinatesCombined, duplicateIndices);
            }

            foreach (var claimedParentId in claimedParentIds)
            {
                memberMap[claimedParentId.Key].parentGroupReference.referenceId = claimedParentId.Value;
            }

            var groupsWithCycles = strategicGroups
                .Where(group => group != null && !string.IsNullOrWhiteSpace(group.parentGroupReference.referenceId))
                .ToList();
            foreach (var group in groupsWithCycles)
            {
                if (HasAcyclicParentChain(group, groupMap))
                    continue;

                RemoveDirectMemberReferenceFromAllGroups(group.objectId);
                group.parentGroupReference.referenceId = null;
            }

            var finalClaimedIds = strategicGroups
                .Where(group => group != null)
                .SelectMany(group => group.subordinatesCombined)
                .Where(reference => reference != null && !string.IsNullOrWhiteSpace(reference.referenceId))
                .Select(reference => reference.referenceId)
                .ToHashSet();

            foreach (var member in memberMap.Values)
            {
                if (!finalClaimedIds.Contains(member.objectId))
                {
                    member.parentGroupReference.referenceId = null;
                }
            }
        }

        void RebuildCellStrategicGroupReferences()
        {
            foreach (var cell in IterCells())
            {
                cell.StrategicGroupReferences.Clear();
            }

            foreach (var areaCell in areaCells)
            {
                areaCell.StrategicGroupReferences.Clear();
            }

            foreach (var strategicGroup in strategicGroups.Where(g => g.deployState == StrategicGroup.DeployState.Independent))
            {
                var cell = strategicGroup.cell;
                if (cell == null)
                    continue;

                cell.StrategicGroupReferences.Add(new StrategicGroupReference() { referenceId = strategicGroup.objectId });
            }

            foreach (var cell in IterCells())
            {
                cell.RefreshControlState();
            }

            foreach (var areaCell in areaCells)
            {
                areaCell.RefreshControlState();
            }
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
