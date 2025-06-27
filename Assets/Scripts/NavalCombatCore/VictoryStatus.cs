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

                sideVictoryStatuses.Add(sideVictoryStatus);
            }

            foreach (var meSideVictoryStatus in sideVictoryStatuses)
            {
                meSideVictoryStatus.victoryPoint = -meSideVictoryStatus.lossVictoryPoint;
                foreach (var otherSideVictoryStatus in sideVictoryStatuses)
                {
                    if (otherSideVictoryStatus == meSideVictoryStatus)
                        continue;
                    meSideVictoryStatus.victoryPoint += otherSideVictoryStatus.lossVictoryPoint;
                }
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

        public void AddToCount(ShipLog shipLog)
        {
            if (shipLog.mapState == MapState.Destroyed)
            {
                sunk += 1;
                lossVictoryPoint += shipLog.shipClass.EvaluateGeneralScore();
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

                lossVictoryPoint += Math.Clamp(dp, 0, 1) * 0.5f * shipLog.shipClass.EvaluateGeneralScore();
            }
        }
    }

    public class SideVictoryStatus
    {
        public string name;
        public float victoryPoint;
        public float lossVictoryPoint;
        public List<ShipTypeLossItem> shipTypeLossItems = new();
    }
}