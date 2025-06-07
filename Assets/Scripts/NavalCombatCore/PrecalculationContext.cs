using System.Collections.Generic;

namespace NavalCombatCore
{
    public class PrecalculationContext
    {
        static PrecalculationContext _instance = new();
        public static PrecalculationContext Instance => _instance;

        public class GunneryFireContext
        {
            public Dictionary<MountStatusRecord, MountStatusSupplementary> mountStatusRecordMap = new();
            public Dictionary<ShipLog, ShipLogSupplementary> shipLogSupplementaryMap = new();
            public Dictionary<(ShipLog, ShipLog), ShipLogPairSupplementary> shooterTargetSupplementaryMap = new();

            public class MountStatusSupplementary
            {
                public MountStatusRecord.FullContext ctx;
                public ShipLog target;
            }

            public class ShipLogSupplementary
            {
                public HashSet<BatteryStatus> batteriesFiredAtMe = new();
            }

            public class ShipLogPairSupplementary
            {
                public ShipLog shooter;
                public ShipLog target;
                public MeasureStats stats;
                MaskCheckResult maskCheckResult;
                public MaskCheckResult GetOrCalcualteMaskCheckResult()
                {
                    if (maskCheckResult == null)
                    {
                        maskCheckResult = ServiceLocator.Get<IMaskCheckService>().Check(shooter, target);
                    }
                    return maskCheckResult;
                }
            }

            public void Reset()
            {
                mountStatusRecordMap.Clear();
                shipLogSupplementaryMap.Clear();
                shooterTargetSupplementaryMap.Clear();
            }

            public ShipLogPairSupplementary GetOrCalcualteShipLogPairSupplementary(ShipLog shooter, ShipLog target)
            {
                if (shooterTargetSupplementaryMap.TryGetValue((shooter, target), out var ret))
                    return ret;
                ret = shooterTargetSupplementaryMap[(shooter, target)] = new()
                {
                    shooter = shooter,
                    target = target,
                    stats = MeasureStats.MeasureApproximation(shooter, target)
                };
                return ret;
            }

            public void Calculate()
            {
                Reset();

                foreach (var shipLog in NavalGameState.Instance.shipLogsOnMap)
                {
                    shipLogSupplementaryMap[shipLog] = new();
                    foreach (var bty in shipLog.batteryStatus)
                    {
                        foreach (var mnt in bty.mountStatus)
                        {
                            mountStatusRecordMap[mnt] = new()
                            {
                                ctx = mnt.GetFullContext(),
                                target = mnt.GetFiringTarget()
                            };
                        }
                    }
                }

                // Collect overconcenteration
                foreach (var mnt in NavalGameState.Instance.mountStatusesFireable)
                {
                    var mntSup = mountStatusRecordMap[mnt];
                    var shooter = mntSup.ctx.shipLog;
                    var target = mntSup.target;

                    if (shooter == null || target == null)
                        continue;

                    var stats = GetOrCalcualteShipLogPairSupplementary(shooter, target).stats;
                    var isInRange = stats.distanceYards <= mntSup.ctx.batteryRecord.rangeYards;
                    var isInArc = mntSup.ctx.mountLocationRecord.IsInArc(stats.observerToTargetBearingRelativeToBowDeg);
                    if (!isInRange || !isInArc)
                        continue;

                    if (mntSup.ctx.batteryStatus.ammunition.GetValue(mnt.ammunitionType) <= 0)
                        continue;

                    shipLogSupplementaryMap[target].batteriesFiredAtMe.Add(mntSup.ctx.batteryStatus);
                }
            }
        }

        public GunneryFireContext gunneryFireContext = new();
    }
}