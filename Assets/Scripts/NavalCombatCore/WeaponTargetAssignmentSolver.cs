using System.Collections.Generic;
using System.Linq;
using System.Collections;

namespace NavalCombatCore
{
    public interface IWTAObject : IDF4Model
    {
        // float EvaluateArmorScore();
        // float EvaluateArmorScore(TargetAspect targetAspect, RangeBand rangeBand);
        float EvaluateSurvivability();
        // float EvaluateBatteryFirepowerScore();
        // float EvaluateBatteryFirepowerScore(float distanceYards, TargetAspect targetAspect, float targetSpeedKnots, float bearingRelativeToBowDeg);
        // float EvaluateTorpedoThreatScore();
        // float EvaluateRapidFiringFirepowerScore();
        float EvaluateFirepowerScore();
        // float EvaluateGeneralScore();
        IWTAObject GetManualFireTarget();

        IEnumerable<IWTABattery> GetBatteries();
    }

    public interface IWTABattery
    {
        float EvaluateFirepowerScore(float distanceYards, TargetAspect targetAspect, float targetSpeedKnots, float bearingRelativeToBowDeg);
        IWTAObject GetCurrentFiringTarget();
        void SetFiringTarget(IWTAObject target);
        void ResetFiringTarget();
        int GetOverConcentrationCoef();
        bool IsChangeTargetBlocked();
    }

    public class WeaponTargetAssignmentSolver // WTA Problem Solver
    {
        // The primary goal is to reduce hostile potential firepower effectiveness. Thus:
        // 1. Fire Suppression: Deliver minimal firepower to enemy to create "under-fire" debuff to decrease their current fire projection.
        // 2. Mission Kill: Prior to attack low-survivability platform with high firepower.
        // 3. Prevent Over-concentration: it is possible that some available batteries are not used.
        // 4. Firepower stickiness: Firing platforms tend to remain engaged with the same target to avoid target-switching debuffs and to preserve visual coherence.

        // Though global optimal solution seems too "rational" for a era that gunnery officer make decision independently, the algorithm self is greedy and cannot be very closer to the global optimal solution.

        public float underfireCoef = 0.1f;
        public float overconcentrateCoef = 0.2f;
        public float changeTargetCoef = 0f; // Legacy flat stickiness is disabled in favor of state-based retention bonuses.
        public static float currentTargetTrackingEffectivenessBonus = 0.2f; // Bonus when keeping fire on a target already under Tracking/Hitting.

        // Urgency boost for knife-fight distances where enemy torpedo danger is assumed to be imminent.
        public static float torpedoThreatRangeYards = 1000f;
        public static float torpedoThreatTargetUrgencyFactor = 1.5f;

        public class ShooterRecord
        {
            public IWTAObject original;
            // Frozen Values
            public List<BatteryRecord> batteries = new();
            public Dictionary<TargetRecord, MeasureStats> measurements = new();
            public TargetRecord manualFireTarget;
            // Solver States

            public override string ToString()
            {
                return $"ShooterRecord({original})";
            }
        }

        public class TargetRecord
        {
            public IWTAObject original;
            // Frozen Values
            public float survivability;
            public float selfFirepowerScore;
            public float speedKnots;
            // Solver states
            public float underFirepower;
            public int overConcentrationScore;

            public override string ToString()
            {
                return $"TargetRecord({original})";
            }
        }

        public class BatteryRecord
        {
            public IWTABattery original;
            // Frozen Values
            public TargetRecord currentTarget;
            public float currentTargetFireEffectivenessFactor = 1f;
            // Solver States
            public Dictionary<TargetRecord, float> firepowerScoreMap = new();
            public TargetRecord assignedTarget;
            public int overConcentrationCoef = 1; // regular corrected fire: +1, barrage fire: +2, RF Batteries: +0 (DoB) or +2 (Literally)?
                                                  // TODO: Switch to float
            public bool isChangeTargetBlocked;

            public override string ToString()
            {
                return $"BatteryRecord({original})";
            }
        }

        public class DecisionRecord
        {
            public ShooterRecord shooter;
            public BatteryRecord battery;
            public TargetRecord target;
            public float gain;
            public float firepowerScore;

            public override string ToString()
            {
                return $"DecisionRecord({shooter}, {battery} -> {target}, {gain}, {firepowerScore})";
            }
        }

        public void Solve(IEnumerable<IWTAObject> shooterObjects, IEnumerable<IWTAObject> targetObjects)
        {
            var shooters = shooterObjects.Select(s => new ShooterRecord()
            {
                original = s,
                batteries = s.GetBatteries().Select(b => new BatteryRecord()
                {
                    original = b,
                    overConcentrationCoef = b.GetOverConcentrationCoef()
                }).ToList()
            }).ToList();

            var targets = targetObjects.Select(t => new TargetRecord()
            {
                original = t,
                survivability = t.EvaluateSurvivability(),
                selfFirepowerScore = t.EvaluateFirepowerScore(),
                speedKnots = t.GetSpeedKnots(),
            }).ToList();

            var oriToTarget = targets.ToDictionary(t => t.original, t => t);

            // pre-calculation
            foreach (var shooter in shooters)
            {
                var manualFireTarget = shooter.original.GetManualFireTarget();
                if(manualFireTarget != null)
                {
                    shooter.manualFireTarget = oriToTarget.GetValueOrDefault(manualFireTarget);
                }

                foreach (var battery in shooter.batteries)
                {
                    var currentTargetObject = battery.original.GetCurrentFiringTarget();
                    if (currentTargetObject != null)
                    {
                        battery.currentTarget = oriToTarget.GetValueOrDefault(currentTargetObject);
                        battery.isChangeTargetBlocked = battery.original.IsChangeTargetBlocked();
                        if (battery.currentTarget != null)
                        {
                            battery.currentTargetFireEffectivenessFactor = GetCurrentTargetFireEffectivenessFactor(battery);
                        }
                    }
                }

                foreach (var target in targets)
                {
                    var stats = shooter.measurements[target] = MeasureStats.Measure(shooter.original, target.original);
                    foreach (var battery in shooter.batteries)
                    {
                        var firepowerScore = battery.original.EvaluateFirepowerScore(stats.distanceYards, stats.targetPresentAspectFromObserver, target.speedKnots, stats.observerToTargetBearingRelativeToBowDeg);
                        battery.firepowerScoreMap[target] = firepowerScore;
                    }
                }
            }

            // Process non-changeable batteries
            foreach (var shooter in shooters)
            {
                foreach (var battery in shooter.batteries)
                {
                    if (battery.assignedTarget != null)
                        continue;

                    if (battery.currentTarget != null && battery.isChangeTargetBlocked)
                    {
                        battery.assignedTarget = battery.currentTarget; // TODO: Is it too harsh to a battery which is capable to shoot multiply targets?
                        battery.currentTarget.underFirepower += battery.firepowerScoreMap[battery.currentTarget] * battery.currentTargetFireEffectivenessFactor;
                        battery.currentTarget.overConcentrationScore += battery.overConcentrationCoef;
                    }
                }
            }

            // Pick a local optimal in every step until decision space become a empty set. 
            while (true)
            {
                // DecisionRecord is good for debugging but degrade too much performance, so switch to local variables
                bool hasDecision = false;
                BatteryRecord bestBattery = null;
                TargetRecord bestTarget = null;
                float bestGain = 0;
                float bestFirepowerScore = 0;

                foreach (var shooter in shooters)
                {
                    // shooter.manualFireTarget
                    foreach (var battery in shooter.batteries)
                    {
                        if (battery.assignedTarget != null)
                            continue;

                        foreach (var target in targets)
                        {
                            if(shooter.manualFireTarget != null && shooter.manualFireTarget != target)
                                continue;

                            var stats = shooter.measurements[target];
                            var targetUrgencyFactor = GetTargetUrgencyFactor(stats.distanceYards);
                            var tryAddedFirepowerScore = battery.firepowerScoreMap[target];
                            if (battery.currentTarget == target)
                            {
                                tryAddedFirepowerScore *= battery.currentTargetFireEffectivenessFactor;
                            }
                            var tryAddedOverconcentrationScore = battery.overConcentrationCoef;
                            var gain = GetTargettingScoreGain(target.selfFirepowerScore, target.survivability, targetUrgencyFactor,
                                    target.underFirepower, target.overConcentrationScore, tryAddedFirepowerScore, tryAddedOverconcentrationScore);
                            if (battery.currentTarget == target)
                            {
                                gain *= 1 + changeTargetCoef;
                            }

                            if (!hasDecision || gain > bestGain)
                            {
                                hasDecision = true;
                                bestBattery = battery;
                                bestTarget = target;
                                bestGain = gain;
                                bestFirepowerScore = tryAddedFirepowerScore;
                            }
                        }
                    }
                }

                if (!hasDecision)
                    break;
                if (bestGain <= 0)
                    break;

                // DEBUG
                // if (bestDecisionRecord.battery.currentTarget != bestDecisionRecord.target)
                // {
                //     var r = bestDecisionRecord;
                //     UnityEngine.Debug.LogWarning($"Retarget: ({r.shooter}, {r.battery}) {r.battery} -> {r.target}");
                // }

                bestBattery.assignedTarget = bestTarget; // TODO: Too harsh to battery which is capable to shoot multiply targets?
                bestTarget.underFirepower += bestFirepowerScore;
                bestTarget.overConcentrationScore += bestBattery.overConcentrationCoef;
            }

            // Apply result
            foreach (var shooter in shooters)
            {
                foreach (var battery in shooter.batteries)
                {
                    // battery.original.ResetFiringTarget();
                    battery.original.SetFiringTarget(battery.assignedTarget?.original);
                }
            }
        }

        public float GetTargettingScore(float targetSelfFirepower, float targetSurvivability, float targetUrgencyFactor, float targetUnderFirepower, int overConcentrationScore)
        {
            // var score = targetSelfFirepower / (1 + targetSurvivability) * targetUnderFirepower;
            var targetValue = (1 + targetSelfFirepower) / (1 + targetSurvivability) * targetUrgencyFactor;
            var score = targetValue * targetUnderFirepower;
            if (overConcentrationScore == 1)
            {
                score *= 1 + underfireCoef;
            }
            else if (overConcentrationScore >= 2)
            {
                score *= 1 - overconcentrateCoef * (overConcentrationScore - 1);
            }
            return score;
        }

        public float GetTargettingScoreGain(float targetSelfFirepower, float targetSurvivability, float targetUrgencyFactor,
            float currentTargetUnderFirepower, int currentOverConcentrationScore,
            float newBatteryFirepower, int tryAddedOverconcentrationScore)
        {
            var currentScore = GetTargettingScore(targetSelfFirepower, targetSurvivability, targetUrgencyFactor,
                currentTargetUnderFirepower, currentOverConcentrationScore);
            var newScore = GetTargettingScore(targetSelfFirepower, targetSurvivability, targetUrgencyFactor,
                currentTargetUnderFirepower + newBatteryFirepower, currentOverConcentrationScore + tryAddedOverconcentrationScore);
            return newScore - currentScore;
        }

        static float GetTargetUrgencyFactor(float distanceYards)
        {
            return distanceYards < torpedoThreatRangeYards ? torpedoThreatTargetUrgencyFactor : 1f;
        }

        static float GetCurrentTargetFireEffectivenessFactor(BatteryRecord battery)
        {
            if (battery.currentTarget == null)
                return 1f;

            var factor = 1f;

            if (battery.original is BatteryStatus gunBattery)
            {
                var currentTargetShip = battery.currentTarget.original as ShipLog;
                var hasTrackingBonus = gunBattery.fireControlSystemStatusRecords.Any(fcs =>
                    fcs.IsOperational() &&
                    fcs.GetTarget() == currentTargetShip &&
                    (fcs.trackingState == TrackingSystemState.Tracking || fcs.trackingState == TrackingSystemState.Hitting)
                );
                if (hasTrackingBonus)
                {
                    factor += currentTargetTrackingEffectivenessBonus;
                }

                var averageProcessSeconds = gunBattery.mountStatus
                    .Where(mnt => mnt.IsOperational() && mnt.GetFiringTarget() == currentTargetShip)
                    .Select(mnt => mnt.processSeconds)
                    .DefaultIfEmpty(0f)
                    .Average();
                factor += averageProcessSeconds / 120f;
                return factor;
            }

            if (battery.original is RapidFiringBatteryStatusOneSide rapidBattery)
            {
                var currentTargetShip = battery.currentTarget.original as ShipLog;
                var processingSeconds = rapidBattery.original.targettingRecords
                    .Where(r => r.location == rapidBattery.side && r.GetTarget() == currentTargetShip)
                    .Select(r => r.processingSeconds)
                    .DefaultIfEmpty(0f)
                    .First();
                factor += processingSeconds / 120f;
            }

            return factor;
        }
    }

}
