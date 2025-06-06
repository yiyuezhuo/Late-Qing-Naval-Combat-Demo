
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;
using GeographicLib;
using UnityEngine.UIElements;


namespace NavalCombatCore
{
    public interface IExtrapolable : IDF4Model
    {
        // Firing Platform attributes
        float EvaluateSurvivability();
        float EvaluateFirepowerScore();
        float EvaluateBowFirepowerScore();
        float EvaluateStarboardFirepowerScore();
        float EvaluateSternFirepowerScore();
        float EvaluatePortFirepowerScore();

        // Control related
        ControlMode GetControlMode();
        IExtrapolable GetFollowedTarget();
        float GetFollowDistanceYards();
        IExtrapolable GetRelativeToTarget();
        float GetRelativeToTargetDistanceYards();
        float GetRelativeToTargetAzimuth();

        // Effect related
        void SetDesiredHeadingDeg(float desiredHeadingDeg);
    }

    public class LowLevelCoursePlanner
    {
        public float angleStepDeg = 18; // 360 / 18 = 20 test angles => (0, 18, 36, 54, ...)
        public float attackCoef = 1f;
        public float defenceCoef = 1f;

        public class ExtrapolatedRecord : IDF4Model
        {
            public IExtrapolable original;

            // Frozen States
            public ControlMode controlMode;
            public ExtrapolatedRecord followedTarget;
            public float followDistanceYards;
            public ExtrapolatedRecord relativeToTarget;
            public float relativeToTargetDistanceYards;
            public float relativeToTargetAzimuth;

            public float survivability;
            public float firepowerScore;
            public float bowFirepowerScore;
            public float starboardFirepowerScore;
            public float sternFirepowerScore;
            public float portFirepowerScore;

            // Planner State
            public float latitudeDeg;
            public float longitudeDeg;
            public float headingDeg;
            public float speedKnots;

            public float GetLatitudeDeg() => latitudeDeg;
            public float GetLongitudeDeg() => longitudeDeg;
            public float GetHeadingDeg() => headingDeg;
            public float GetSpeedKnots() => speedKnots;

            public ExtrapolatedRecord ResetDF4ToOriginal()
            {
                latitudeDeg = original.GetLatitudeDeg();
                longitudeDeg = original.GetLongitudeDeg();
                headingDeg = original.GetHeadingDeg();
                speedKnots = original.GetSpeedKnots();
                return this;
            }

            public void DoExtrapolate(float simulationSeconds)
            {
                if (controlMode == ControlMode.FollowTarget && followedTarget != null)
                {
                    var distanceMeter = followDistanceYards * MeasureUtils.yardToMeter;
                    var inverseLine = Geodesic.WGS84.InverseLine(followedTarget.latitudeDeg, followedTarget.longitudeDeg, latitudeDeg, longitudeDeg);
                    inverseLine.Position(distanceMeter, out var lat2, out var lon2, out var azi2);
                    latitudeDeg = (float)lat2;
                    longitudeDeg = (float)lon2;
                    headingDeg = (float)azi2;
                }
                else if (controlMode == ControlMode.RelativeToTarget && relativeToTarget != null)
                {
                    var azi = MeasureUtils.NormalizeAngle(relativeToTargetAzimuth + relativeToTarget.headingDeg);
                    var distMeter = relativeToTargetDistanceYards * MeasureUtils.yardToMeter;
                    Geodesic.WGS84.Direct(relativeToTarget.latitudeDeg, relativeToTarget.longitudeDeg, azi, distMeter, out var lat2, out var lon2);
                    latitudeDeg = (float)lat2;
                    longitudeDeg = (float)lon2;
                    headingDeg = relativeToTarget.headingDeg;
                }
                else // (controlMode == ControlMode.Independent)
                {
                    var moveDistanceMeter = speedKnots / 3600 * simulationSeconds * MeasureUtils.navalMileToMeter;
                    double arcLength = Geodesic.WGS84.Direct(latitudeDeg, longitudeDeg, headingDeg, moveDistanceMeter, out var lat2, out var lon2);
                    latitudeDeg = (float)lat2;
                    longitudeDeg = (float)lon2;
                }
            }

            public ExtrapolatedRecord GetControlLead()
            {
                if (controlMode == ControlMode.RelativeToTarget)
                {
                    return relativeToTarget;
                }
                else if (controlMode == ControlMode.FollowTarget)
                {
                    return followedTarget.GetControlLead();
                }
                return this; // Independent or unsupported mode control itself.
            }

            public float EvaluateSmoothedFirepower(float observerToTargetwBearingRelativeToBowDeg)
            {
                var angle = MeasureUtils.NormalizeAngle(observerToTargetwBearingRelativeToBowDeg);
                if (angle <= 90)
                {
                    return bowFirepowerScore * (90 - angle) / 90 + starboardFirepowerScore * angle / 90;
                }
                else if (angle < 180)
                {
                    return starboardFirepowerScore * (180 - angle) / 90 + sternFirepowerScore * (angle - 90) / 90;
                }
                else if (angle < 270)
                {
                    return sternFirepowerScore * (270 - angle) / 90 + portFirepowerScore * (angle - 180) / 90;
                }
                else
                {
                    return portFirepowerScore * (360 - angle) / 90 + bowFirepowerScore * (angle - 270) / 90;
                }
            }
        }

        public (List<ExtrapolatedRecord>, Dictionary<IExtrapolable, ExtrapolatedRecord>, Dictionary<ExtrapolatedRecord, List<ExtrapolatedRecord>>) Setup(IEnumerable<IExtrapolable> objects)
        {
            var records = objects.Select(f => new ExtrapolatedRecord()
            {
                original = f,
                controlMode = f.GetControlMode(),
                followDistanceYards = f.GetFollowDistanceYards(), // Calculate the current distance or just use the setting distance?
                relativeToTargetDistanceYards = f.GetRelativeToTargetDistanceYards(),
                relativeToTargetAzimuth = f.GetRelativeToTargetAzimuth(),
                survivability = f.EvaluateSurvivability(),
                firepowerScore = f.EvaluateFirepowerScore(),
                bowFirepowerScore = f.EvaluateBowFirepowerScore(),
                starboardFirepowerScore = f.EvaluateStarboardFirepowerScore(),
                sternFirepowerScore = f.EvaluateSternFirepowerScore(),
                portFirepowerScore = f.EvaluatePortFirepowerScore(),
                // followedTarget = f.GetFollowedTarget(),

            }.ResetDF4ToOriginal()).ToList();

            var originalToRecords = records.ToDictionary(r => r.original, r => r);
            foreach (var record in records)
            {
                var followedTarget = record.original.GetFollowedTarget();
                var relativeToTarget = record.original.GetRelativeToTarget();
                if (followedTarget != null)
                    record.followedTarget = originalToRecords.GetValueOrDefault(followedTarget);
                if (relativeToTarget != null)
                    record.relativeToTarget = originalToRecords.GetValueOrDefault(relativeToTarget);
            }

            // var decisionRecords = records.Where(r => r.controlMode == ControlMode.Independent).ToList();
            // var inducedRecords = records.Where(r => r.controlMode != ControlMode.Independent).ToList();
            // var leadToSubInducedRecords = inducedRecords.GroupBy(g => g.controlMode switch
            // {
            //     ControlMode.FollowTarget => g.followedTarget,
            //     ControlMode.RelativeToTarget => g.relativeToTarget,
            //     _ => throw new ArgumentOutOfRangeException($"Unknown Supported ControlMode: {g.controlMode}")
            // }).ToDictionary(g => g.Key, g => g.ToList());

            var leadToSubInducedRecords = records.GroupBy(g => g.GetControlLead()).ToDictionary(g => g.Key, g => g.ToList());

            return (records, originalToRecords, leadToSubInducedRecords);
        }

        public void DoExtrapolatePair(ExtrapolatedRecord leadRecord, List<ExtrapolatedRecord> subRecords, float extrapolateSeconds)
        {
            leadRecord.DoExtrapolate(extrapolateSeconds);
            foreach (var subRecord in subRecords)
            {
                subRecord.DoExtrapolate(extrapolateSeconds);
            }
        }

        public void DoExtrapolate(Dictionary<ExtrapolatedRecord, List<ExtrapolatedRecord>> leadToSubInducedRecords, float extrapolateSeconds)
        {
            foreach (var (leadRecord, subRecords) in leadToSubInducedRecords)
            {
                DoExtrapolatePair(leadRecord, subRecords, extrapolateSeconds);
            }
        }

        public float EvaluateAttackScore(ExtrapolatedRecord shooter, ExtrapolatedRecord target)
        {
            // Exact method
            // var stats = MeasureStats.Measure(shooter, target);
            // var distanceYards = stats.distanceYards;
            // var shooterToTargetBearingRelativeToBowDeg = stats.observerToTargetBearingRelativeToBowDeg;
            // Approximation method
            var distanceYards = (float)MeasureStats.Approximation.HaversineDistanceYards(shooter, target);
            var shooterToTargetBearingRelativeToBowDeg = MeasureUtils.GetPositiveAngleDifference((float)MeasureStats.Approximation.CalculateInitialBearing(shooter, target), shooter.headingDeg);

            var distanceScore = Math.Max(0, (36000 - distanceYards) / 36000);
            var angleScore = shooter.EvaluateSmoothedFirepower(shooterToTargetBearingRelativeToBowDeg);
            var firepowerScore = distanceScore * angleScore;
            var valueScore = target.firepowerScore / target.survivability;
            return firepowerScore * valueScore;
        }

        public float EvaluateAttackScore(ExtrapolatedRecord shooter, List<ExtrapolatedRecord> targets)
        {
            return targets.Select(t => EvaluateAttackScore(shooter, t)).DefaultIfEmpty(0).Max();
        }

        public float EvaluateAttackScore(List<ExtrapolatedRecord> shooters, List<ExtrapolatedRecord> targets)
        {
            return shooters.Sum(s => EvaluateAttackScore(s, targets));
        }

        public float EvaluateFirefightScore(List<ExtrapolatedRecord> freidnly, List<ExtrapolatedRecord> enemy)
        {
            var attackScore = EvaluateAttackScore(freidnly, enemy);
            var defenceScore = -EvaluateAttackScore(enemy, freidnly);
            return attackScore * attackCoef + defenceScore * defenceCoef;
        }

        public class TrialRecord
        {
            public float headingDeg;
            public float firefightScore;
        }

        public void Plan(IEnumerable<IExtrapolable> friendlyObjects, IEnumerable<IExtrapolable> enemyObjects, float extrapolateSeconds)
        {
            var (friendlyRecords, objectToFriendlyRecord, friendlyLeadToSubInducedRecords) = Setup(friendlyObjects);
            var (enemyRecords, objectToEnemyRecord, enemyLeadToSubInducedRecords) = Setup(enemyObjects);

            // Naive Enemy Extrapolation
            DoExtrapolate(enemyLeadToSubInducedRecords, extrapolateSeconds);

            // Greedy friendly extrapolation based search
            foreach (var (friendlyLeadRecord, friendlySubRecords) in friendlyLeadToSubInducedRecords)
            {
                // TODO: Add speed change (-20%, 0%, +20%)
                var trials = new List<TrialRecord>();
                for (var angle = 0f; angle < 360; angle += angleStepDeg)
                {
                    friendlyLeadRecord.ResetDF4ToOriginal();
                    friendlyLeadRecord.headingDeg = angle;
                    DoExtrapolatePair(friendlyLeadRecord, friendlySubRecords, extrapolateSeconds);
                    var firefightScore = EvaluateFirefightScore(friendlySubRecords, enemyRecords);
                    trials.Add(new()
                    {
                        headingDeg = angle,
                        firefightScore = firefightScore
                    });
                }
                var maxScore = trials.Max(t => t.firefightScore);
                var bestTrial = trials.First(t => t.firefightScore == maxScore);
                // Apply decision greedtly
                friendlyLeadRecord.original.SetDesiredHeadingDeg(bestTrial.headingDeg);
            }
        }
    }
}