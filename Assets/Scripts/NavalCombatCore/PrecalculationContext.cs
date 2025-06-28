using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace NavalCombatCore
{
    public class AbstractPrecalculationContext<T> : IDisposable where T: class, new()
    {
        static Stack<T> stack = new();

        public void Dispose()
        {
            stack.Pop();
        }

        // private AbstractPrecalculationContext()
        // {
        // }

        public static T Begin()
        {
            var context = new T();
            stack.Push(context);
            // context.Calculate();
            return context;
        }

        public static T GetCurrentOrCreateTemp()
        {
            if (stack.Count > 0)
                return stack.Peek();
            var logger = ServiceLocator.Get<ILoggerService>();
            logger.LogWarning("Misuse of temp ctx will heavily impact performance");
            var ctx = new T();
            // ctx.Calculate();
            return ctx;
        }
    }

    public class GunneryFireContext : AbstractPrecalculationContext<GunneryFireContext>
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
            public HashSet<ShipLog> shipLogsFiredAtMe = new();
            public float armorScore; // Useless now
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
                shipLogSupplementaryMap[shipLog] = new() { armorScore = shipLog.EvaluateArmorScore() };
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
                var isDoctrineRespected = mntSup.ctx.batteryStatus.IsMaxDistanceDoctrineRespected(stats.distanceYards);
                if (!isInRange || !isInArc || !isDoctrineRespected)
                    continue;

                if (mntSup.ctx.batteryStatus.ammunition.GetValue(mnt.ammunitionType) <= 0) // This should be rechecked in the followed resolution
                    continue;

                shipLogSupplementaryMap[target].batteriesFiredAtMe.Add(mntSup.ctx.batteryStatus);
                shipLogSupplementaryMap[target].shipLogsFiredAtMe.Add(mntSup.ctx.shipLog);
            }
        }

        public GunneryFireContext()
        {
            Calculate();
        }
    }

    // Stack<GunneryFireContext> gunneryFireContextStack = new();
    // public GunneryFireContext GetCurrentOrCreateTempGunneryFireContext()
    // {
    //     if (gunneryFireContextStack.Count > 0)
    //         return gunneryFireContextStack.Peek();
    //     using (var ctx = GunneryFireContext.Begin())
    //     {
    //         return ctx;
    //     }
    // }

    public class TorpedoAttackContext : AbstractPrecalculationContext<TorpedoAttackContext>
    {
        public class ShipLogPairSupplementary
        {
            public ShipLog shooter;
            public ShipLog target;
            public InterceptionPointSolver.Result interceptionPointSolverResult;
        }

        public Dictionary<(ShipLog, ShipLog, float), ShipLogPairSupplementary> fireComplexSupplementaryMap = new();

        public ShipLogPairSupplementary GetOrCalculateFireComplexSupplementary(ShipLog shooter, ShipLog target, float speedKnots)
        {
            var key = (shooter, target, speedKnots);
            if (!fireComplexSupplementaryMap.TryGetValue(key, out var supplementary))
            {
                supplementary = fireComplexSupplementaryMap[key] = new()
                {
                    shooter = shooter,
                    target = target,
                    interceptionPointSolverResult = InterceptionPointSolver.Calcualte(shooter, target, speedKnots)
                };
            }
            return supplementary;
        }
    }
}