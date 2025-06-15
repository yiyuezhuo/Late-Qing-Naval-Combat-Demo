using System.Collections.Generic;
using System.Linq;
using System.Collections;
using System.Security;
using System.Diagnostics;

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

        IEnumerable<IWTABattery> GetBatteries();
    }

    public interface IWTABattery
    {
        float EvaluateFirepowerScore(float distanceYards, TargetAspect targetAspect, float targetSpeedKnots, float bearingRelativeToBowDeg);
        IWTAObject GetCurrentFiringTarget();
        void SetFiringTarget(IWTAObject target);
        void ResetFiringTarget();
        int GetOverConcentrationCoef();
    }

    public class WeaponTargetAssignmentSolver // WTA Problem Solver
    {
        // The primary goal is to reduce hositile potential firepower effectiveness. Thus:
        // 1. Fire Suppression: Deliver minimal firepower to enemy to create "under-fire" debuff to decrease their current fire projection.
        // 2. Mission Kill: Prior to attack low-survibility platform with high firepower.
        // 3. Prevent Concenertation: 
        // 4. Firepower stickiness: Firing plaform tend to fire at the same target to prevent goal change debuf and visual appearance.

        // Though global optimal solution seems too "rational" for a era that gunnery officier make decision indepently, the algorithm self is greedy and cannot be very closer to the global optimal solution.

        public float underfireCoef = 0.1f;
        public float overconcentrateCoef = 0.2f;
        public float changeTargetCoef = 0.5f; // TODO: Enable it in another implementation?

        public class ShooterRecord
        {
            public IWTAObject original;
            // Frozen Values
            public List<BatteryRecord> batteries = new();
            public Dictionary<TargetRecord, MeasureStats> measurements = new();
            // Solver States
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
        }

        public class BatteryRecord
        {
            public IWTABattery original;
            // Frozen Values
            public TargetRecord currentTarget;
            // Solver States
            public Dictionary<TargetRecord, float> firepowerScoreMap = new();
            public TargetRecord assignedTarget;
            public int overConcentrationCoef = 1; // regular corrected fire: +1, barrage fire: +2, RF Batteries: +0 (DoB) or +2 (Literally)?
            // TODO: Switch to float
        }

        public class DecisionRecord
        {
            public ShooterRecord shooter;
            public BatteryRecord battery;
            public TargetRecord target;
            public float gain;
            public float firepowerScore;
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
                foreach (var target in targets)
                {
                    var stats = shooter.measurements[target] = MeasureStats.Measure(shooter.original, target.original);
                    foreach (var battery in shooter.batteries)
                    {
                        var firepowerScore = battery.original.EvaluateFirepowerScore(stats.distanceYards, stats.targetPresentAspectFromObserver, target.speedKnots, stats.observerToTargetBearingRelativeToBowDeg);
                        battery.firepowerScoreMap[target] = firepowerScore;

                        var currentTargetObject = battery.original.GetCurrentFiringTarget();
                        if (currentTargetObject != null)
                        {
                            battery.currentTarget = oriToTarget.GetValueOrDefault(currentTargetObject);
                        }
                    }
                }
            }

            // Pick a local optimal in every step until decision space become a empty set. 
            while (true)
            {
                var decisionRecords = new List<DecisionRecord>();
                foreach (var shooter in shooters)
                {
                    foreach (var battery in shooter.batteries)
                    {
                        if (battery.assignedTarget != null)
                            continue;

                        foreach (var target in targets)
                        {
                            // var stats = shooter.measurements[target];
                            // var firepowerScore = battery.original.EvaluateFirepowerScore(stats.distanceYards, stats.targetPresentAspectFromObserver, target.speedKnots, stats.observerToTargetViewBearingRelativeToBowDeg);
                            var tryAddedFirepowerScore = battery.firepowerScoreMap[target];
                            var tryAddedOverconcentrationScore = battery.overConcentrationCoef;
                            var gain = GetTargettingScoreGain(target.selfFirepowerScore, target.survivability,
                                    target.underFirepower, target.overConcentrationScore, tryAddedFirepowerScore, tryAddedOverconcentrationScore);
                            if (battery.currentTarget == target)
                            {
                                gain *= 1 + changeTargetCoef;
                            }

                            var decisionRecord = new DecisionRecord()
                            {
                                shooter = shooter,
                                battery = battery,
                                target = target,
                                gain = gain,
                                firepowerScore = tryAddedFirepowerScore
                            };
                            decisionRecords.Add(decisionRecord);
                        }
                    }
                }
                if (decisionRecords.Count == 0)
                    break;
                var maxGain = decisionRecords.Max(r => r.gain);
                if (maxGain <= 0)
                    break;
                
                var bestDecisionRecord = decisionRecords.First(r => r.gain == maxGain);

                // DEBUG
                // if (bestDecisionRecord.battery.currentTarget != bestDecisionRecord.target)
                // {
                //     var r = bestDecisionRecord;
                //     UnityEngine.Debug.LogWarning($"Retarget: ({r.shooter}, {r.battery}) {r.battery} -> {r.target}");
                // }

                bestDecisionRecord.battery.assignedTarget = bestDecisionRecord.target; // TODO: Too harsh to battery which is capable to shoot multiply targets?
                bestDecisionRecord.target.selfFirepowerScore += bestDecisionRecord.firepowerScore;
                bestDecisionRecord.target.overConcentrationScore += bestDecisionRecord.battery.overConcentrationCoef;
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

        public float GetTargettingScore(float targetSelfFirepower, float targetSurvivability, float targetUnderFirepower, int overConcentrationScore)
        {
            var score = targetSelfFirepower / (1 + targetSurvivability) * targetUnderFirepower;
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

        public float GetTargettingScoreGain(float targetSelfFirepower, float targetSurvivability, float currentTargetUnderFirepower, int currentOverConcentrationScore,
            float newBatteryFirepower, int tryAddedOverconcentrationScore)
        {
            var currentScore = GetTargettingScore(targetSelfFirepower, targetSurvivability,
                currentTargetUnderFirepower, currentOverConcentrationScore);
            var newScore = GetTargettingScore(targetSelfFirepower, targetSurvivability,
                currentTargetUnderFirepower + newBatteryFirepower, currentOverConcentrationScore + tryAddedOverconcentrationScore);
            return newScore - currentScore;
        }
    }

}