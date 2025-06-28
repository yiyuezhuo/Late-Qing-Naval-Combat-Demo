using System;
using System.Collections.Generic;
using System.Linq;


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

    public class VictoryStatus
    {
        public List<SideVictoryStatus> sideVictoryStatuses = new();

        public static VictoryStatus Generate(NavalGameState gameState)
        {
            var sideVictoryStatuses = new List<SideVictoryStatus>();

            var rootGroupings = gameState.shipLogs.GroupBy(shipLog => (shipLog as IShipGroupMember).GetRootParent()).ToList();

            foreach (var grouping in rootGroupings)
            {
                var sideVictoryStatus = new SideVictoryStatus()
                {
                    name = grouping.Key.GetMemberName()
                };

                var typeGroupings = grouping.ToList().GroupBy(shipLog => shipLog.shipClass.type).ToList();
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

                var level = VictoryLevel.Draw;

                if (otherOverSelfLossRatio > 5 && otherLossRatio > 0.5)
                    level = VictoryLevel.DecisiveVictory;
                else if (otherOverSelfLossRatio > 2 && otherLossRatio > 0.25)
                    level = VictoryLevel.MajorVictory;
                else if (otherOverSelfLossRatio > 1.5)
                    level = VictoryLevel.Victory;
                else if (otherOverSelfLossRatio > 1.3)
                    level = VictoryLevel.MinorDefeat;
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

    public class SideVictoryStatus
    {
        public string name;
        public float victoryPoint;
        public float lossVictoryPoint;
        public float commitVictoryPoint;
        public float selfLossRatio;
        public float otherOverSelfLossRatio;
        public VictoryLevel victoryLevel;

        public List<ShipTypeLossItem> shipTypeLossItems = new();
    }
}