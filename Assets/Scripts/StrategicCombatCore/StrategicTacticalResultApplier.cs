using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CoreUtils;
using NavalCombatCore;
using YYZ;

namespace StrategicCombatCore
{
    public sealed class StrategicTacticalResultApplier
    {
        readonly StrategicGameState state;

        List<ShipLog> shipLogs => state.shipLogs;
        List<StrategicGroup> strategicGroups => state.strategicGroups;
        List<PendingNavalCombat> pendingNavalCombats => state.pendingNavalCombats;
        StrategicScenarioState scenarioState => state.scenarioState;
        Cell[,] cellMatrix => state.cellMatrix;

        public StrategicTacticalResultApplier(StrategicGameState state)
        {
            this.state = state ?? throw new ArgumentNullException(nameof(state));
        }

        int GetMapWidth() => state.GetMapWidth();
        int GetMapHeight() => state.GetMapHeight();
        void ResetAndRegisterAll() => state.ResetAndRegisterAll();
        bool TryAutoDetachDamagedShips(StrategicGroup group) => state.TryAutoDetachDamagedShips(group);

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
            CleanupDestroyedStrategicGroupsFromPendingNavalCombat();
            HandlePendingNavalCombat(victoryStatus);
        }

        void CleanupDestroyedStrategicGroupsFromPendingNavalCombat()
        {
            var pendingNavalCombat = EntityManager.Instance.Get<PendingNavalCombat>(scenarioState.pendingNavalCombatId);
            if (pendingNavalCombat == null)
                return;

            foreach (var group in pendingNavalCombat.sideState0.GetGroups())
            {
                MarkDestroyedNavalGroupsRecursive(group);
            }

            foreach (var group in pendingNavalCombat.sideState1.GetGroups())
            {
                MarkDestroyedNavalGroupsRecursive(group);
            }
        }

        bool MarkDestroyedNavalGroupsRecursive(StrategicGroup group)
        {
            if (group == null)
                return false;

            foreach (var subGroup in group.WalkDirectMembers<StrategicGroup>())
            {
                if (subGroup.deployState == StrategicGroup.DeployState.Combined)
                {
                    MarkDestroyedNavalGroupsRecursive(subGroup);
                }
            }

            if (group.type != StrategicGroup.Type.Fleet)
                return false;

            var hasRemainingShip = group
                .WalkDirectMembers()
                .Any(member =>
                {
                    if (member is ShipLog shipLog)
                        return shipLog.mapState != MapState.Destroyed;
                    if (member is StrategicGroup subGroup && subGroup.deployState == StrategicGroup.DeployState.Combined)
                        return !subGroup.destroyed;
                    return false;
                });

            if (hasRemainingShip)
                return false;

            group.MarkAsDestroyed();
            return true;
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
                    if(assignedMission != null && assignedMission.ShouldInterruptOnCombatFailure())
                    {
                        assignedMission.InterruptNow();
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

            foreach (var group in groups)
            {
                TryAutoDetachDamagedShips(group);
            }
        }

    }
}
