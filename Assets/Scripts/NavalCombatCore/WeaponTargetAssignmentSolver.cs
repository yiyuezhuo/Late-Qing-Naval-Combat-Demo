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
        public static float torpedoThreatRangeYards = 1500f;
        public static float torpedoThreatTargetUrgencyFactor = 2.0f;
        public static float subjectiveCloseRangePreferenceMinFactor = 0.5f;

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

        readonly struct CandidateEvaluation
        {
            public readonly MeasureStats stats;
            public readonly float targetUrgencyFactor;
            public readonly float tryAddedFirepowerScoreBase;
            public readonly float tryAddedFirepowerScoreEffective;
            public readonly int tryAddedOverconcentrationScore;
            public readonly float rawGainBeforeStickiness;
            public readonly float changeTargetMultiplier;
            public readonly float finalGain;
            public readonly bool isCurrentTarget;

            public CandidateEvaluation(
                MeasureStats stats,
                float targetUrgencyFactor,
                float tryAddedFirepowerScoreBase,
                float tryAddedFirepowerScoreEffective,
                int tryAddedOverconcentrationScore,
                float rawGainBeforeStickiness,
                float changeTargetMultiplier,
                float finalGain,
                bool isCurrentTarget)
            {
                this.stats = stats;
                this.targetUrgencyFactor = targetUrgencyFactor;
                this.tryAddedFirepowerScoreBase = tryAddedFirepowerScoreBase;
                this.tryAddedFirepowerScoreEffective = tryAddedFirepowerScoreEffective;
                this.tryAddedOverconcentrationScore = tryAddedOverconcentrationScore;
                this.rawGainBeforeStickiness = rawGainBeforeStickiness;
                this.changeTargetMultiplier = changeTargetMultiplier;
                this.finalGain = finalGain;
                this.isCurrentTarget = isCurrentTarget;
            }
        }

        public void Solve(IEnumerable<IWTAObject> shooterObjects, IEnumerable<IWTAObject> targetObjects)
        {
            var state = InitializeSolverState(shooterObjects, targetObjects);
            ApplyNonChangeableAssignments(state);

            // Pick a local optimal in every step until decision space become a empty set. 
            while (true)
            {
                var hasDecision = TryFindBestDecision(state, out var bestBattery, out var bestTarget, out var bestGain, out var bestFirepowerScore);

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

                ApplyDecision(bestBattery, bestTarget, bestFirepowerScore);
            }

            // Apply result
            ApplyAssignmentsToOriginal(state);
        }

        public WeaponTargetAssignmentInspectionSession CreateInspectionSession(IEnumerable<IWTAObject> shooterObjects, IEnumerable<IWTAObject> targetObjects)
        {
            var state = InitializeSolverState(shooterObjects, targetObjects);
            ApplyNonChangeableAssignments(state);
            return new WeaponTargetAssignmentInspectionSession(this, state);
        }

        internal WeaponTargetAssignmentSolverState InitializeSolverState(IEnumerable<IWTAObject> shooterObjects, IEnumerable<IWTAObject> targetObjects)
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

            foreach (var shooter in shooters)
            {
                var manualFireTarget = shooter.original.GetManualFireTarget();
                if (manualFireTarget != null)
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
                        var firepowerScore = battery.original.EvaluateFirepowerScore(
                                stats.distanceYards,
                                stats.targetPresentAspectFromObserver,
                                target.speedKnots,
                                stats.observerToTargetBearingRelativeToBowDeg)
                            * GetSubjectiveCloseRangePreferenceFactor(stats.distanceYards);
                        battery.firepowerScoreMap[target] = firepowerScore;
                    }
                }
            }

            return new WeaponTargetAssignmentSolverState(shooters, targets);
        }

        internal void ApplyNonChangeableAssignments(WeaponTargetAssignmentSolverState state)
        {
            foreach (var shooter in state.shooters)
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
        }

        internal bool TryFindBestDecision(WeaponTargetAssignmentSolverState state, out BatteryRecord bestBattery, out TargetRecord bestTarget, out float bestGain, out float bestFirepowerScore)
        {
            bool hasDecision = false;
            bestBattery = null;
            bestTarget = null;
            bestGain = 0f;
            bestFirepowerScore = 0f;

            foreach (var shooter in state.shooters)
            {
                foreach (var battery in shooter.batteries)
                {
                    if (battery.assignedTarget != null)
                        continue;

                    foreach (var target in state.targets)
                    {
                        if (shooter.manualFireTarget != null && shooter.manualFireTarget != target)
                            continue;

                        var evaluation = EvaluateCandidate(shooter, battery, target);
                        if (!hasDecision || evaluation.finalGain > bestGain)
                        {
                            hasDecision = true;
                            bestBattery = battery;
                            bestTarget = target;
                            bestGain = evaluation.finalGain;
                            bestFirepowerScore = evaluation.tryAddedFirepowerScoreEffective;
                        }
                    }
                }
            }

            return hasDecision;
        }

        internal List<WeaponTargetAssignmentGainRow> BuildGainRows(WeaponTargetAssignmentSolverState state, out WeaponTargetAssignmentGainRow bestRow)
        {
            var rows = new List<WeaponTargetAssignmentGainRow>();
            bestRow = null;
            var bestGain = 0f;
            var hasDecision = false;
            var scanOrder = 0;

            foreach (var shooter in state.shooters)
            {
                foreach (var battery in shooter.batteries)
                {
                    if (battery.assignedTarget != null)
                        continue;

                    foreach (var target in state.targets)
                    {
                        if (shooter.manualFireTarget != null && shooter.manualFireTarget != target)
                            continue;

                        scanOrder++;
                        var evaluation = EvaluateCandidate(shooter, battery, target);
                        var row = new WeaponTargetAssignmentGainRow
                        {
                            scanOrder = scanOrder,
                            shooterName = ResolveObjectShortName(shooter.original),
                            batteryName = ResolveBatteryShortName(battery.original),
                            targetName = ResolveObjectShortName(target.original),
                            finalGain = evaluation.finalGain,
                            rawGainBeforeStickiness = evaluation.rawGainBeforeStickiness,
                            distanceYards = evaluation.stats.distanceYards,
                            targetSelfFirepower = target.selfFirepowerScore,
                            targetSurvivability = target.survivability,
                            targetUrgencyFactor = evaluation.targetUrgencyFactor,
                            currentUnderFirepower = target.underFirepower,
                            currentOverConcentrationScore = target.overConcentrationScore,
                            tryAddedFirepowerScoreBase = evaluation.tryAddedFirepowerScoreBase,
                            tryAddedFirepowerScoreEffective = evaluation.tryAddedFirepowerScoreEffective,
                            tryAddedOverconcentrationScore = evaluation.tryAddedOverconcentrationScore,
                            isCurrentTarget = evaluation.isCurrentTarget,
                            currentTargetFireEffectivenessFactor = battery.currentTargetFireEffectivenessFactor,
                            changeTargetMultiplier = evaluation.changeTargetMultiplier,
                            batteryRecord = battery,
                            targetRecord = target,
                        };
                        rows.Add(row);

                        if (!hasDecision || row.finalGain > bestGain)
                        {
                            hasDecision = true;
                            bestGain = row.finalGain;
                            bestRow = row;
                        }
                    }
                }
            }

            return rows;
        }

        CandidateEvaluation EvaluateCandidate(ShooterRecord shooter, BatteryRecord battery, TargetRecord target)
        {
            var stats = shooter.measurements[target];
            var targetUrgencyFactor = GetTargetUrgencyFactor(stats.distanceYards);
            var tryAddedFirepowerScoreBase = battery.firepowerScoreMap[target];
            var isCurrentTarget = battery.currentTarget == target;
            var tryAddedFirepowerScoreEffective = tryAddedFirepowerScoreBase;
            if (isCurrentTarget)
            {
                tryAddedFirepowerScoreEffective *= battery.currentTargetFireEffectivenessFactor;
            }

            var tryAddedOverconcentrationScore = battery.overConcentrationCoef;
            var rawGainBeforeStickiness = GetTargettingScoreGain(
                target.selfFirepowerScore,
                target.survivability,
                targetUrgencyFactor,
                target.underFirepower,
                target.overConcentrationScore,
                tryAddedFirepowerScoreEffective,
                tryAddedOverconcentrationScore);
            var changeTargetMultiplier = isCurrentTarget ? 1 + changeTargetCoef : 1f;
            var finalGain = rawGainBeforeStickiness * changeTargetMultiplier;

            return new CandidateEvaluation(
                stats,
                targetUrgencyFactor,
                tryAddedFirepowerScoreBase,
                tryAddedFirepowerScoreEffective,
                tryAddedOverconcentrationScore,
                rawGainBeforeStickiness,
                changeTargetMultiplier,
                finalGain,
                isCurrentTarget);
        }

        internal void ApplyDecision(BatteryRecord bestBattery, TargetRecord bestTarget, float bestFirepowerScore)
        {
            bestBattery.assignedTarget = bestTarget; // TODO: Too harsh to battery which is capable to shoot multiply targets?
            bestTarget.underFirepower += bestFirepowerScore;
            bestTarget.overConcentrationScore += bestBattery.overConcentrationCoef;
        }

        internal void ApplyAssignmentsToOriginal(WeaponTargetAssignmentSolverState state)
        {
            foreach (var shooter in state.shooters)
            {
                foreach (var battery in shooter.batteries)
                {
                    battery.original.SetFiringTarget(battery.assignedTarget?.original);
                }
            }
        }

        static string ResolveObjectShortName(IWTAObject obj)
        {
            if (obj is ShipLog shipLog)
            {
                return shipLog.namedShip?.name?.GetShortName()
                    ?? shipLog.namedShip?.name?.GetMergedName()
                    ?? shipLog.objectId
                    ?? "[Invalid]";
            }

            return obj?.ToString() ?? "[Invalid]";
        }

        static string ResolveBatteryShortName(IWTABattery battery)
        {
            if (battery is BatteryStatus gunBattery)
            {
                return gunBattery.GetBatteryRecord()?.name?.GetShortName()
                    ?? "Battery";
            }

            if (battery is RapidFiringBatteryStatusOneSide rapidBattery)
            {
                var baseName = rapidBattery.original.GetRapidFireBatteryRecord()?.name?.GetShortName()
                    ?? "Rapid Firing";
                return $"{baseName} ({rapidBattery.side})";
            }

            if (battery is TorpedoBattery torpedoBattery)
            {
                return torpedoBattery.original?.shipClass?.torpedoSector?.name?.GetShortName()
                    ?? "Torpedo";
            }

            return battery?.ToString() ?? "[Invalid]";
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

        static float GetSubjectiveCloseRangePreferenceFactor(float distanceYards)
        {
            if (distanceYards <= 2000f)
            {
                return LerpByDistance(1f, 0.9f, distanceYards, 0f, 2000f);
            }

            if (distanceYards <= 4500f)
            {
                return LerpByDistance(0.9f, 0.7f, distanceYards, 2000f, 4500f);
            }

            if (distanceYards <= 8000f)
            {
                return LerpByDistance(0.7f, 0.5f, distanceYards, 4500f, 8000f);
            }

            return subjectiveCloseRangePreferenceMinFactor;
        }

        static float LerpByDistance(float startValue, float endValue, float distanceYards, float startDistanceYards, float endDistanceYards)
        {
            if (endDistanceYards <= startDistanceYards)
                return endValue;

            var t = (distanceYards - startDistanceYards) / (endDistanceYards - startDistanceYards);
            if (t < 0f)
                t = 0f;
            else if (t > 1f)
                t = 1f;
            return startValue + (endValue - startValue) * t;
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

    internal sealed class WeaponTargetAssignmentSolverState
    {
        public readonly List<WeaponTargetAssignmentSolver.ShooterRecord> shooters;
        public readonly List<WeaponTargetAssignmentSolver.TargetRecord> targets;

        public WeaponTargetAssignmentSolverState(
            List<WeaponTargetAssignmentSolver.ShooterRecord> shooters,
            List<WeaponTargetAssignmentSolver.TargetRecord> targets)
        {
            this.shooters = shooters;
            this.targets = targets;
        }
    }

    public sealed class WeaponTargetAssignmentGainRow
    {
        public int scanOrder;
        public string shooterName;
        public string batteryName;
        public string targetName;
        public float finalGain;
        public float rawGainBeforeStickiness;
        public float distanceYards;
        public float targetSelfFirepower;
        public float targetSurvivability;
        public float targetUrgencyFactor;
        public float currentUnderFirepower;
        public int currentOverConcentrationScore;
        public float tryAddedFirepowerScoreBase;
        public float tryAddedFirepowerScoreEffective;
        public int tryAddedOverconcentrationScore;
        public bool isCurrentTarget;
        public float currentTargetFireEffectivenessFactor;
        public float changeTargetMultiplier;

        internal WeaponTargetAssignmentSolver.BatteryRecord batteryRecord;
        internal WeaponTargetAssignmentSolver.TargetRecord targetRecord;
    }

    public sealed class WeaponTargetAssignmentInspectionSession
    {
        readonly WeaponTargetAssignmentSolver solver;
        readonly WeaponTargetAssignmentSolverState state;

        public List<WeaponTargetAssignmentGainRow> CurrentRows { get; private set; } = new();
        public WeaponTargetAssignmentGainRow BestRow { get; private set; }
        public int StepIndex { get; private set; }
        public bool CanStep => BestRow != null && BestRow.finalGain > 0f;

        internal WeaponTargetAssignmentInspectionSession(WeaponTargetAssignmentSolver solver, WeaponTargetAssignmentSolverState state)
        {
            this.solver = solver;
            this.state = state;
            RecomputeDecisionSpace();
        }

        public void RecomputeDecisionSpace()
        {
            CurrentRows = solver.BuildGainRows(state, out var bestRow);
            BestRow = bestRow;
        }

        public bool StepNext()
        {
            if (!CanStep)
                return false;

            solver.ApplyDecision(BestRow.batteryRecord, BestRow.targetRecord, BestRow.tryAddedFirepowerScoreEffective);
            BestRow.batteryRecord.original.SetFiringTarget(BestRow.targetRecord.original);
            StepIndex++;
            RecomputeDecisionSpace();
            return true;
        }
    }

}
