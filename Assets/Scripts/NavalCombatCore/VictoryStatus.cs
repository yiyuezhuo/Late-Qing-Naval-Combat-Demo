using System;
using System.Collections.Generic;
using System.Linq;
using CoreUtils;


namespace NavalCombatCore
{
    public enum DamageDiscreteLevel
    {
        None, // DP == 0% 
        Light, // 0% < DP < 25%
        Medium, // 25% < DP < 70%
        Heavy, // DP > 70%
        Sunk // Sunk
    }

    public enum VictoryLevel
    {
        DecisiveDefeat,
        MajorDefeat,
        Defeat,
        MinorDefeat,
        MarginalDefeat,
        Draw, // 100%~110%
        MarginalVictory, // 110%~130%
        MinorVictory, // 130%~150%
        Victory, // 150%~200%
        MajorVictory, // 200%~500%, and opposite lost 25%+ (otherwise it degrade to a Victory)
        DecisiveVictory, // 500%+, and opposite lost 50%+ (otherwise it degrade to a Victory or MarjorVictory)
    }

    public class VictoryStatusBaseline
    {
        public List<SideVictoryStatusBaseline> sideVictoryStatusBaselines = new();
    }

    public class SideVictoryStatusBaseline
    {
        public string sideObjectId;
        public string name;
        public float initialLossVictoryPoint;
        public float commitVictoryPoint;
        public List<ShipTypeLossBaseline> shipTypeLossBaselines = new();
        public List<ShipVictoryDetailBaseline> shipDetailBaselines = new();
    }

    public class ShipTypeLossBaseline
    {
        public ShipType shipType;
        public int undamaged;
        public int light;
        public int medium;
        public int heavy;
        public int sunk;
        public float lossVictoryPoint;
        public float commitVictroyPoint;
    }

    public class ShipVictoryDetailBaseline
    {
        public string shipObjectId;
        public float damageRatio;
    }

    public class VictoryStatus
    {
        public List<SideVictoryStatus> sideVictoryStatuses = new();

        static List<ShipLog> GetShipsInOobOrder(ShipGroup rootGroup)
        {
            var ships = new List<ShipLog>();
            if (rootGroup == null)
                return ships;

            void Visit(ShipGroup group)
            {
                foreach (var childObjectId in group.childrenObjectIds ?? Enumerable.Empty<string>())
                {
                    var child = EntityManager.Instance.Get<IShipGroupMember>(childObjectId);
                    switch (child)
                    {
                        case ShipLog shipLog:
                            ships.Add(shipLog);
                            break;
                        case ShipGroup childGroup:
                            Visit(childGroup);
                            break;
                    }
                }
            }

            Visit(rootGroup);
            return ships;
        }

        static float GetDamageRatio(ShipLog shipLog)
        {
            if (shipLog == null)
                return 0f;
            if (shipLog.mapState == MapState.Destroyed)
                return 1f;

            return shipLog.damagePoint / Math.Max(1f, shipLog.shipClass?.damagePoint ?? 0f);
        }

        static void PopulateHistoryStats(ShipVictoryDetailItem shipDetailItem, ShipLog shipLog, NavalGameState gameState)
        {
            if (shipDetailItem == null || shipLog == null)
                return;

            var batteryLogs = shipLog.batteryStatus
                .SelectMany(batteryStatus => batteryStatus.mountStatus)
                .SelectMany(mountStatus => mountStatus.logs)
                .ToList();
            var rapidLogs = shipLog.rapidFiringStatus
                .SelectMany(rapidStatus => rapidStatus.logs)
                .ToList();
            var torpedoLogs = gameState?.launchedTorpedos?
                .Where(torpedo => torpedo.shooterId == shipLog.objectId)
                .ToList()
                ?? new List<LaunchedTorpedo>();

            shipDetailItem.shotsFiredCount =
                batteryLogs.Count +
                rapidLogs.Count +
                torpedoLogs.Count;
            shipDetailItem.hitsLandedCount =
                batteryLogs.Count(log => log.hit) +
                rapidLogs.Count(log => log.hit) +
                torpedoLogs.Count(torpedo => torpedo.endgameType == LaunchedTorpedoEndgameType.Hit);
            shipDetailItem.hitsTakenCount =
                shipLog.logs.OfType<ShipLogBatteryHitLog>().Count() +
                shipLog.logs.OfType<ShipLogRapidFiringGunHitLog>().Count() +
                shipLog.logs.OfType<ShipLogTorpedoHitLog>().Count();
            shipDetailItem.damagePointLost =
                Math.Max(0f, shipLog.damagePoint + shipLog.pendingDamagePoint);
            shipDetailItem.damagePointInflicted =
                batteryLogs.Where(log => log.hit).Sum(log => log.ShellDamageResult?.damagePoint ?? 0f) +
                rapidLogs.Where(log => log.hit).Sum(log => log.damagePoint) +
                torpedoLogs.Where(torpedo => torpedo.endgameType == LaunchedTorpedoEndgameType.Hit).Sum(torpedo => torpedo.inflictDamagePoint);
        }

        static List<SideVictoryStatus> GenerateCurrentSnapshot(NavalGameState gameState)
        {
            var sideVictoryStatuses = new List<SideVictoryStatus>();
            if (gameState == null)
                return sideVictoryStatuses;

            var topGroups = ShipGroupOrderUtils.GetTopLevelShipGroupsInOobOrder(gameState);
            var topGroupSet = new HashSet<string>(
                topGroups
                    .Where(group => group?.objectId != null)
                    .Select(group => group.objectId)
            );

            foreach (var rootGroup in gameState.shipLogs
                         .Select(shipLog => (shipLog as IShipGroupMember)?.GetRootParent() as ShipGroup)
                         .Where(group => group != null && group.objectId != null && !topGroupSet.Contains(group.objectId))
                         .GroupBy(group => group.objectId)
                         .Select(grouping => grouping.First()))
            {
                topGroups.Add(rootGroup);
                topGroupSet.Add(rootGroup.objectId);
            }

            foreach (var rootGroup in topGroups.Where(group => group != null))
            {
                var ships = GetShipsInOobOrder(rootGroup);
                var sideVictoryStatus = new SideVictoryStatus()
                {
                    sideObjectId = rootGroup.objectId,
                    name = rootGroup.GetMemberName()
                };

                foreach (var shipLog in ships.Where(shipLog => shipLog != null))
                {
                    sideVictoryStatus.shipDetailItems.Add(new ShipVictoryDetailItem()
                    {
                        shipObjectId = shipLog.objectId,
                        name = shipLog.GetMemberName(),
                        currentDamageRatio = GetDamageRatio(shipLog),
                        isSunk = shipLog.mapState == MapState.Destroyed,
                    });
                    PopulateHistoryStats(sideVictoryStatus.shipDetailItems[^1], shipLog, gameState);
                }

                var typeGroupings = ships
                    .Where(shipLog => shipLog?.shipClass != null)
                    .GroupBy(shipLog => shipLog.shipClass.type)
                    .ToList();
                foreach (var typeGrouping in typeGroupings)
                {
                    var shipTypeLossItem = new ShipTypeLossItem()
                    {
                        shipType = typeGrouping.Key,
                    };

                    foreach (var shipLog in typeGrouping)
                    {
                        shipTypeLossItem.AddToCount(shipLog);
                    }

                    sideVictoryStatus.shipTypeLossItems.Add(shipTypeLossItem);
                }

                sideVictoryStatus.lossVictoryPoint = sideVictoryStatus.shipTypeLossItems.Sum(item => item.lossVictoryPoint);
                sideVictoryStatus.commitVictoryPoint = sideVictoryStatus.shipTypeLossItems.Sum(item => item.commitVictroyPoint);
                sideVictoryStatus.selfLossRatio = (sideVictoryStatus.lossVictoryPoint + 1) / (sideVictoryStatus.commitVictoryPoint + 1);

                sideVictoryStatuses.Add(sideVictoryStatus);
            }

            return sideVictoryStatuses;
        }

        static VictoryStatusBaseline GenerateBaselineFromSnapshot(List<SideVictoryStatus> sideVictoryStatuses)
        {
            return new VictoryStatusBaseline()
            {
                sideVictoryStatusBaselines = sideVictoryStatuses.Select(sideVictoryStatus => new SideVictoryStatusBaseline()
                {
                    sideObjectId = sideVictoryStatus.sideObjectId,
                    name = sideVictoryStatus.name,
                    initialLossVictoryPoint = sideVictoryStatus.lossVictoryPoint,
                    commitVictoryPoint = sideVictoryStatus.commitVictoryPoint,
                    shipTypeLossBaselines = sideVictoryStatus.shipTypeLossItems.Select(shipTypeLossItem => new ShipTypeLossBaseline()
                    {
                        shipType = shipTypeLossItem.shipType,
                        undamaged = shipTypeLossItem.undamaged,
                        light = shipTypeLossItem.light,
                        medium = shipTypeLossItem.medium,
                        heavy = shipTypeLossItem.heavy,
                        sunk = shipTypeLossItem.sunk,
                        lossVictoryPoint = shipTypeLossItem.lossVictoryPoint,
                        commitVictroyPoint = shipTypeLossItem.commitVictroyPoint
                    }).ToList(),
                    shipDetailBaselines = sideVictoryStatus.shipDetailItems.Select(shipDetailItem => new ShipVictoryDetailBaseline()
                    {
                        shipObjectId = shipDetailItem.shipObjectId,
                        damageRatio = shipDetailItem.currentDamageRatio,
                    }).ToList()
                }).ToList()
            };
        }

        public static void CaptureInitialBaselineIfMissing(NavalGameState gameState)
        {
            if (gameState?.scenarioState == null)
                return;

            if (gameState.scenarioState.initialVictoryStatusBaseline != null)
                return;

            var snapshot = GenerateCurrentSnapshot(gameState);
            gameState.scenarioState.initialVictoryStatusBaseline = GenerateBaselineFromSnapshot(snapshot);
        }

        public static VictoryStatus Generate(NavalGameState gameState)
        {
            CaptureInitialBaselineIfMissing(gameState);

            var sideVictoryStatuses = GenerateCurrentSnapshot(gameState);

            var baseline = gameState?.scenarioState?.initialVictoryStatusBaseline;
            var baselineBySideObjectId =
                baseline?.sideVictoryStatusBaselines?
                    .Where(sideBaseline => sideBaseline.sideObjectId != null)
                    .GroupBy(sideBaseline => sideBaseline.sideObjectId)
                    .ToDictionary(grouping => grouping.Key, grouping => grouping.First())
                ?? new Dictionary<string, SideVictoryStatusBaseline>();

            foreach (var sideVictoryStatus in sideVictoryStatuses)
            {
                SideVictoryStatusBaseline sideBaseline = null;
                if (sideVictoryStatus.sideObjectId != null)
                    baselineBySideObjectId.TryGetValue(sideVictoryStatus.sideObjectId, out sideBaseline);

                sideVictoryStatus.initialCommitVictoryPoint = sideBaseline?.commitVictoryPoint ?? sideVictoryStatus.commitVictoryPoint;
                sideVictoryStatus.initialLossVictoryPoint = sideBaseline?.initialLossVictoryPoint ?? 0f;
                sideVictoryStatus.currentLossVictoryPoint = sideVictoryStatus.lossVictoryPoint;
                sideVictoryStatus.lossVictoryPoint = Math.Max(0f, sideVictoryStatus.currentLossVictoryPoint - sideVictoryStatus.initialLossVictoryPoint);

                var sideBaselineByShipType =
                    sideBaseline?.shipTypeLossBaselines?
                        .GroupBy(shipTypeBaseline => shipTypeBaseline.shipType)
                        .ToDictionary(grouping => grouping.Key, grouping => grouping.First())
                    ?? new Dictionary<ShipType, ShipTypeLossBaseline>();
                var sideBaselineByShipObjectId =
                    sideBaseline?.shipDetailBaselines?
                        .Where(shipDetailBaseline => shipDetailBaseline.shipObjectId != null)
                        .GroupBy(shipDetailBaseline => shipDetailBaseline.shipObjectId)
                        .ToDictionary(grouping => grouping.Key, grouping => grouping.First())
                    ?? new Dictionary<string, ShipVictoryDetailBaseline>();

                foreach (var shipTypeLossItem in sideVictoryStatus.shipTypeLossItems)
                {
                    if (sideBaselineByShipType.TryGetValue(shipTypeLossItem.shipType, out var shipTypeBaseline))
                    {
                        shipTypeLossItem.initialUndamaged = shipTypeBaseline.undamaged;
                        shipTypeLossItem.initialLight = shipTypeBaseline.light;
                        shipTypeLossItem.initialMedium = shipTypeBaseline.medium;
                        shipTypeLossItem.initialHeavy = shipTypeBaseline.heavy;
                        shipTypeLossItem.initialSunk = shipTypeBaseline.sunk;
                    }
                    else
                    {
                        shipTypeLossItem.initialUndamaged = 0;
                        shipTypeLossItem.initialLight = 0;
                        shipTypeLossItem.initialMedium = 0;
                        shipTypeLossItem.initialHeavy = 0;
                        shipTypeLossItem.initialSunk = 0;
                    }
                }

                foreach (var shipDetailItem in sideVictoryStatus.shipDetailItems)
                {
                    if (shipDetailItem.shipObjectId != null &&
                        sideBaselineByShipObjectId.TryGetValue(shipDetailItem.shipObjectId, out var shipDetailBaseline))
                    {
                        shipDetailItem.initialDamageRatio = shipDetailBaseline.damageRatio;
                    }
                    else
                    {
                        shipDetailItem.initialDamageRatio = 0f;
                    }

                    shipDetailItem.RefreshSegmentRatios();
                }
            }

            foreach (var meSideVictoryStatus in sideVictoryStatuses)
            {
                var selfInitialLossVictoryPoint = meSideVictoryStatus.initialLossVictoryPoint;
                var otherInitialLossVictoryPoint = sideVictoryStatuses
                    .Where(s => s != meSideVictoryStatus)
                    .Sum(s => s.initialLossVictoryPoint);
                meSideVictoryStatus.initialVictoryPoint = otherInitialLossVictoryPoint - selfInitialLossVictoryPoint;
            }

            foreach (var meSideVictoryStatus in sideVictoryStatuses)
            {
                var selfLossVictoryPoint = meSideVictoryStatus.lossVictoryPoint;
                var selfCommitVictoryPoint = meSideVictoryStatus.commitVictoryPoint;

                var otherLossVictoryPoint = sideVictoryStatuses.Where(s => s != meSideVictoryStatus).Sum(s => s.lossVictoryPoint);
                var otherCommitVictoryPoint = sideVictoryStatuses.Where(s => s != meSideVictoryStatus).Sum(s => s.commitVictoryPoint);

                meSideVictoryStatus.victoryPoint = otherLossVictoryPoint - selfLossVictoryPoint; // Diff based VP (JTS Style)

                // TODO: Determine Victroy Level
                var otherOverSelfLossRatio = meSideVictoryStatus.otherOverSelfLossRatio = (otherLossVictoryPoint + 1) / (selfLossVictoryPoint + 1);
                var otherLossRatio = (otherLossVictoryPoint + 1) / (otherCommitVictoryPoint + 1);
                var selfLossRatio = (selfLossVictoryPoint + 1) / (selfCommitVictoryPoint + 1);
                meSideVictoryStatus.selfLossRatio = selfLossRatio;

                var level = VictoryLevel.Draw;

                if (otherOverSelfLossRatio > 5 && otherLossRatio > 0.5)
                    level = VictoryLevel.DecisiveVictory;
                else if (otherOverSelfLossRatio > 2 && otherLossRatio > 0.25)
                    level = VictoryLevel.MajorVictory;
                else if (otherOverSelfLossRatio > 1.5)
                    level = VictoryLevel.Victory;
                else if (otherOverSelfLossRatio > 1.3)
                    level = VictoryLevel.MinorVictory;
                else if (otherOverSelfLossRatio > 1.1)
                    level = VictoryLevel.MarginalVictory;
                else if (otherOverSelfLossRatio < 1f / 5 && selfLossRatio > 0.5)
                    level = VictoryLevel.DecisiveDefeat;
                else if (otherOverSelfLossRatio < 1f / 2 && selfLossRatio > 0.25)
                    level = VictoryLevel.MajorDefeat;
                else if (otherOverSelfLossRatio < 1f / 1.5f)
                    level = VictoryLevel.Defeat;
                else if (otherOverSelfLossRatio < 1f / 1.3f)
                    level = VictoryLevel.MinorDefeat;
                else if (otherOverSelfLossRatio < 1f / 1.1f)
                    level = VictoryLevel.MarginalDefeat;
                else
                    level = VictoryLevel.Draw;

                meSideVictoryStatus.victoryLevel = level;
            }

            return new()
            {
                sideVictoryStatuses = sideVictoryStatuses
            };
        }
    }

    public partial class ShipTypeLossItem
    {
        public ShipType shipType;
        public int undamaged;
        public int light;
        public int medium;
        public int heavy;
        public int sunk;
        public int initialUndamaged;
        public int initialLight;
        public int initialMedium;
        public int initialHeavy;
        public int initialSunk;
        public float lossVictoryPoint;
        public float commitVictroyPoint; // May be better to call it "strength"?

        public void AddToCount(ShipLog shipLog)
        {
            var shipLogScore = shipLog.shipClass.EvaluateGeneralScore();
            commitVictroyPoint += shipLogScore;

            if (shipLog.mapState == MapState.Destroyed)
            {
                sunk += 1;
                lossVictoryPoint += shipLogScore;
            }
            else if (shipLog.damagePoint == 0)
            {
                undamaged += 1;
            }
            else
            {
                var dp = shipLog.damagePoint / Math.Max(1, shipLog.shipClass.damagePoint);
                if (dp > 0.7f)
                    heavy += 1;
                else if (dp > 0.25)
                    medium += 1;
                else
                    light += 1;

                lossVictoryPoint += Math.Clamp(dp, 0, 1) * 0.5f * shipLogScore;
            }
        }
    }

    public partial class SideVictoryStatus
    {
        public string sideObjectId;
        public string name;
        public float victoryPoint;
        public float initialCommitVictoryPoint;
        public float initialVictoryPoint;
        public float lossVictoryPoint;
        public float initialLossVictoryPoint;
        public float currentLossVictoryPoint;
        public float commitVictoryPoint;
        public float selfLossRatio;
        public float otherOverSelfLossRatio;
        public VictoryLevel victoryLevel;

        public List<ShipTypeLossItem> shipTypeLossItems = new();
        public List<ShipVictoryDetailItem> shipDetailItems = new();
    }

    public partial class ShipVictoryDetailItem
    {
        public string shipObjectId;
        public string name;
        public float initialDamageRatio;
        public float currentDamageRatio;
        public bool isSunk;
        public float yellowRatio;
        public float redRatio;
        public float transparentRatio;
        public int shotsFiredCount;
        public int hitsLandedCount;
        public int hitsTakenCount;
        public float damagePointLost;
        public float damagePointInflicted;

        public void RefreshSegmentRatios()
        {
            var clampedInitial = Math.Clamp(initialDamageRatio, 0f, 1f);
            var clampedCurrent = Math.Clamp(currentDamageRatio, 0f, 1f);

            yellowRatio = Math.Max(0f, 1f - clampedCurrent);
            transparentRatio = Math.Min(clampedInitial, clampedCurrent);
            redRatio = Math.Max(0f, 1f - yellowRatio - transparentRatio);
        }
    }
}
