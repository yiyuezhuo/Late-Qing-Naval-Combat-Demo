using System.Collections.Generic;
using System.Data;
using System.Linq;
using System;
using GeographicLib;
using System.Xml.Serialization;

using CoreUtils;
using YYZ;


namespace NavalCombatCore
{
    public enum RapidFiringBatteryLocation // Location => Side? Though it's binded in UITK so keep it now. It's used in searchlight DE as well
    {
        Port,
        Starboard
    }

    public partial class RapidFiringTargettingStatus
    {
        public RapidFiringBatteryLocation location;
        public float processingSeconds;
        public int allocated;
        public string targetObjectId;
        public ShipLog GetTarget()
        {
            return EntityManager.Instance.GetOnMapShipLog(targetObjectId);
        }
    }

    public class RapidFiringLog
    {
        [XmlAttribute]
        public string firingTargetObjectId;

        public ShipLog GetFiringTarget() => EntityManager.Instance.Get<ShipLog>(firingTargetObjectId);

        [XmlAttribute]
        public DateTime firingTime;

        [XmlAttribute]
        public float distanceYards;

        [XmlAttribute]
        public float hitProb;

        [XmlAttribute]
        public bool hit;

        [XmlAttribute]
        public float damagePoint;

        protected static string Localize(string key, params object[] args) => ServiceLocator.Get<ILocalizeService>().Get(key, args);

        public string Summary()
        {
            var target = GetFiringTarget();
            var targetName = target.namedShip?.name?.GetMergedName();
            // var hitDesc = hit ? $"hit -> {damagePoint} DP" : "miss";
            var hitDesc = hit ? Localize(
                "hit -> {0} DP",
                damagePoint
            ) : Localize("miss");

            return Localize(
                "{0} -> {1}, {2} yards, PoH={3}%, {4}",
                firingTime,
                targetName,
                distanceYards,
                hitProb * 100,
                hitDesc
            );
            // return $"{firingTime} -> {targetName}, {distanceYards} yards, P={hitProb * 100}%, {hitDesc}";
        }
    }

    public partial class RapidFiringStatus : IObjectIdLabeled
    {
        public string objectId { get; set; }
        public int portMountHits;
        public int starboardMountHits;
        public int fireControlHits;
        public List<RapidFiringTargettingStatus> targettingRecords = new();
        public int ammunition;

        public List<RapidFiringLog> logs = new();

        // public void ClearLogs()
        // {
        //     logs.Clear();
        // }

        public override string ToString()
        {
            return $"RapidFiringStatus({GetRapidFireBatteryRecord()?.name?.GetMergedNamePure()})";
        }

        public IEnumerable<IObjectIdLabeled> GetSubObjects()
        {
            yield break;
        }

        public enum BreakdownBlockedReason
        {
            None,
            ShooterNotResolved,
            BatteryRecordNotResolved,
            NoTarget,
            DoctrineBlocked,
        }

        public class HitProbabilityBreakdown
        {
            public bool canResolveProbability;
            public BreakdownBlockedReason blockedReason;

            public string targetObjectId;
            public string targetName;
            public float distanceYards;
            public float bearingDeg;
            public RapidFiringBatteryLocation side;
            public int allocatedBarrels;
            public int availableBarrels;
            public bool isMasked;
            public float secondsPerShot;

            public int fireControlRecordIndex;
            public bool usingEffectiveRange;
            public float baseFireControlScore;
            public float visibilityOffset;
            public float dawnDuskOffset;
            public float nightMoonlightOffset;
            public float illuminationOffset;
            public float evasiveActionOffset;
            public float underFireOffset;
            public float targetSizeOffset;
            public float seaStateOffset;
            public float crewQualityOffset;

            public float finalFireControlScore;
            public float hitProbabilityTableP100;
            public float finalHitProbability;
        }

        public string DescribeDetail()
        {
            var lines = new List<string>() { $"Detail: {objectId}" };
            lines.AddRange(logs.Select(r => r.Summary()));
            return string.Join("\n", lines);
        }

        public string DescribeFireControlDetail()
        {
            var lines = new List<string>() { $"Rapid Firing Detail: {objectId}" };

            var targetRecords = targettingRecords
                .Where(record => record != null)
                .ToList();

            if (targetRecords.Count == 0)
            {
                lines.Add("");
                lines.Add("No current target.");
                return string.Join("\n", lines);
            }

            using (var fireCtx = GunneryFireContext.Begin())
            {
                foreach (var targetRecord in targetRecords)
                {
                    if (lines.Count > 0 && lines[^1] != "")
                        lines.Add("");

                    lines.AddRange(BuildHitProbabilityBreakdownLines(GetCurrentHitProbabilityBreakdown(targetRecord, fireCtx)));
                }
            }

            return string.Join("\n", lines);
        }

        public void ResetTargetting()
        {
            targettingRecords.Clear();
        }

        public void ResetDamageExpenditureState()
        {
            portMountHits = 0;
            starboardMountHits = 0;
            fireControlHits = 0;

            ResetTargetting();

            ResetExpenditureState();

            logs.Clear();
        }

        public void ResetExpenditureState()
        {
            var rfBtyRec = GetRapidFireBatteryRecord();
            var barrels = rfBtyRec.barrelsLevelStarboard.FirstOrDefault() + rfBtyRec.barrelsLevelPort.FirstOrDefault();
            // ammunition = barrels * 15; // 15 turns RF "max speed firing"
            ammunition = (int)Math.Ceiling(barrels * ShipClass.rapidFiringGunAmmoCapacityTacticalTurns);
        }

        public RapidFireBatteryRecord GetRapidFireBatteryRecord()
        {
            var shipLog = EntityManager.Instance.GetParent<ShipLog>(this);
            if (shipLog == null)
                return null;
            var idx = shipLog.rapidFiringStatus.IndexOf(this);
            var shipClass = shipLog.shipClass;
            if (shipClass == null || idx < 0 || idx >= shipClass.rapidFireBatteryRecords.Count)
                return null;
            return shipClass.rapidFireBatteryRecords[idx];
        }

        (int, int) GetClassCurrentBarrels(List<int> barrelsLevel, int hit)
        {
            hit = Math.Max(0, hit);
            if (barrelsLevel.Count == 0)
                return (0, 0);
            var barrelsClass = barrelsLevel[0];
            var barrelsCurrent = hit >= barrelsLevel.Count ? 0 : barrelsLevel[hit];
            return (barrelsClass, barrelsCurrent);
        }

        public float EvaluateFirepowerScore()
        {
            var r = rapidFireBatteryRecord;
            if (r == null)
                return 0;

            var (portClass, portCurrent) = GetClassCurrentBarrels(r.barrelsLevelPort, portMountHits);
            var (starboardClass, starboardCurrent) = GetClassCurrentBarrels(r.barrelsLevelStarboard, starboardMountHits);
            var barrelsCurrent = portCurrent + starboardCurrent;

            var fcRecord = fireControlHits >= r.fireControlRecords.Count ? null : r.fireControlRecords[fireControlHits];
            var fireControlScore = fcRecord == null ? 0 : fcRecord.fireControlEffectiveRange;

            return fireControlScore * barrelsCurrent * r.damageFactor;
        }

        public float EvaluateFirepowerScore(float distanceYards, float bearingRelativeToBowDeg)
        {
            var r = GetRapidFireBatteryRecord();
            if (distanceYards > r.maxRangeYards)
                return 0;

            // TODO: Add doctrine for 100mm- batteries

            var side = NavalUtils.GetBatterySide(bearingRelativeToBowDeg);
            var barrelsCurrent = GetAvailableBarrels(side);

            // var fcRecord = fireControlHits >= r.fireControlRecords.Count ? null : r.fireControlRecords[fireControlHits];
            // var fireControlScore = fcRecord == null ? 0 : (distanceYards <= r.effectiveRangeYards ? fcRecord.fireControlEffectiveRange : fcRecord.fireControlMaxRange);

            var fireControlScore = GetFireControlScore(distanceYards);

            return fireControlScore * barrelsCurrent * r.damageFactor;
        }

        public float GetFireControlScore(float distanceYards)
        {
            var r = GetRapidFireBatteryRecord();

            var fcRecord = fireControlHits >= r.fireControlRecords.Count ? null : r.fireControlRecords[fireControlHits];
            var fireControlScore = fcRecord == null ? 0 : (distanceYards <= r.effectiveRangeYards ? fcRecord.fireControlEffectiveRange : fcRecord.fireControlMaxRange);
            return fireControlScore;
        }

        static string FormatSigned(float value)
        {
            var v = Math.Abs(value) < 0.0001f ? 0f : value;
            return $"{(v >= 0 ? "+" : "")}{v:0.##}";
        }

        static string FormatNumber(float value)
        {
            var v = Math.Abs(value) < 0.0001f ? 0f : value;
            return $"{v:0.##}";
        }

        static string DescribeBlockedReason(BreakdownBlockedReason reason)
        {
            return reason switch
            {
                BreakdownBlockedReason.None => "No blocking reason.",
                BreakdownBlockedReason.ShooterNotResolved => "Firing ship is not resolved.",
                BreakdownBlockedReason.BatteryRecordNotResolved => "Rapid firing battery record is not resolved.",
                BreakdownBlockedReason.NoTarget => "No current firing target.",
                BreakdownBlockedReason.DoctrineBlocked => "Blocked by maximum firing distance doctrine.",
                _ => "Blocked by an unknown reason."
            };
        }

        public static List<string> BuildHitProbabilityBreakdownLines(HitProbabilityBreakdown breakdown)
        {
            var lines = new List<string>();
            var targetName = string.IsNullOrWhiteSpace(breakdown?.targetName) ? "[No Target]" : breakdown.targetName;
            lines.Add($"Hit probability of {targetName}:");

            if (breakdown == null || !breakdown.canResolveProbability)
            {
                lines.Add("");
                lines.Add("Final Hit Probability => 0%");
                lines.Add($"Reason: {DescribeBlockedReason(breakdown?.blockedReason ?? BreakdownBlockedReason.ShooterNotResolved)}");
                return lines;
            }

            lines.Add($"{FormatNumber(breakdown.distanceYards)} yards");
            lines.Add($"{FormatNumber(breakdown.bearingDeg)} deg => {breakdown.side}");
            lines.Add($"Barrels: allocated {breakdown.allocatedBarrels}, available {breakdown.availableBarrels}");
            lines.Add(breakdown.isMasked
                ? $"Masked: yes, seconds per shot {FormatNumber(breakdown.secondsPerShot)}"
                : $"Masked: no, seconds per shot {FormatNumber(breakdown.secondsPerShot)}");

            lines.Add("");
            lines.Add($"Base Fire Control Value ({(breakdown.usingEffectiveRange ? "Effective" : "Max")} Range / FC Damage Level {breakdown.fireControlRecordIndex}) => {FormatNumber(breakdown.baseFireControlScore)}");
            lines.Add($"Visibility ({NavalGameState.Instance.scenarioState.visibility}): {FormatSigned(breakdown.visibilityOffset)}");
            lines.Add($"Dawn/Dusk (Sun Bearing Sector): {FormatSigned(breakdown.dawnDuskOffset)}");
            lines.Add($"Night/Moonlight: {FormatSigned(breakdown.nightMoonlightOffset)}");
            lines.Add($"Illumination/Afire: {FormatSigned(breakdown.illuminationOffset)}");
            lines.Add($"Evasive Action: {FormatSigned(breakdown.evasiveActionOffset)}");
            lines.Add($"Under Fire (3+ ships): {FormatSigned(breakdown.underFireOffset)}");
            lines.Add($"Target Size: {FormatSigned(breakdown.targetSizeOffset)}");
            lines.Add($"Sea State: {FormatSigned(breakdown.seaStateOffset)}");
            lines.Add($"Crew Quality: {FormatSigned(breakdown.crewQualityOffset)}");

            lines.Add("");
            lines.Add($"Final Fire Control Score => {FormatNumber(breakdown.finalFireControlScore)}");
            lines.Add($"Hit Probability Table => {FormatNumber(breakdown.hitProbabilityTableP100)}%");
            lines.Add($"Final Hit Probability => {FormatNumber(breakdown.finalHitProbability * 100f)}%");
            return lines;
        }

        public HitProbabilityBreakdown GetCurrentHitProbabilityBreakdown(
            RapidFiringTargettingStatus targetRecord,
            GunneryFireContext fireCtx = null,
            int? allocatedBarrelsOverride = null,
            float? secondsPerShotOverride = null)
        {
            var breakdown = new HitProbabilityBreakdown()
            {
                blockedReason = BreakdownBlockedReason.ShooterNotResolved,
                targetObjectId = targetRecord?.targetObjectId,
                targetName = targetRecord?.GetTarget()?.namedShip?.name?.GetMergedName(),
                allocatedBarrels = allocatedBarrelsOverride ?? Math.Max(0, targetRecord?.allocated ?? 0)
            };

            var shooter = EntityManager.Instance.GetParent<ShipLog>(this);
            if (shooter == null)
                return breakdown;

            var r = GetRapidFireBatteryRecord();
            if (r == null)
            {
                breakdown.blockedReason = BreakdownBlockedReason.BatteryRecordNotResolved;
                return breakdown;
            }

            var target = targetRecord?.GetTarget();
            if (target == null)
            {
                breakdown.blockedReason = BreakdownBlockedReason.NoTarget;
                return breakdown;
            }

            fireCtx ??= GunneryFireContext.GetCurrentOrCreateTemp();

            var stTgtSup = fireCtx.GetOrCalcualteShipLogPairSupplementary(shooter, target);
            var stats = stTgtSup.stats;
            breakdown.distanceYards = stats.distanceYards;
            breakdown.bearingDeg = stats.observerToTargetBearingRelativeToBowDeg;
            breakdown.side = NavalUtils.GetBatterySide(stats.observerToTargetBearingRelativeToBowDeg);
            breakdown.availableBarrels = GetAvailableBarrels(breakdown.side);
            breakdown.targetObjectId = target.objectId;
            breakdown.targetName = target.namedShip?.name?.GetMergedName();

            var doctrineRespected = shooter.doctrine.GetMaximumFiringDistanceYardsFor100mmLess().IsGreaterThanIfSpecified(stats.distanceYards);
            if (!doctrineRespected)
            {
                breakdown.blockedReason = BreakdownBlockedReason.DoctrineBlocked;
                return breakdown;
            }

            breakdown.canResolveProbability = true;
            breakdown.blockedReason = BreakdownBlockedReason.None;

            var maskCheckResult = stTgtSup.GetOrCalcualteMaskCheckResult();
            breakdown.isMasked = maskCheckResult.isMasked;
            if (secondsPerShotOverride.HasValue)
            {
                breakdown.secondsPerShot = secondsPerShotOverride.Value;
            }
            else if (breakdown.allocatedBarrels > 0)
            {
                breakdown.secondsPerShot = 120f / breakdown.allocatedBarrels * (breakdown.isMasked ? 2f : 1f);
            }

            var fcRecord = fireControlHits >= r.fireControlRecords.Count ? null : r.fireControlRecords[fireControlHits];
            breakdown.fireControlRecordIndex = fireControlHits;
            breakdown.usingEffectiveRange = stats.distanceYards <= r.effectiveRangeYards;
            breakdown.baseFireControlScore = fcRecord == null
                ? 0
                : (breakdown.usingEffectiveRange ? fcRecord.fireControlEffectiveRange : fcRecord.fireControlMaxRange);

            var targetIlluminationSup = fireCtx.GetOrCalculateTargetIlluminationSupplementary(target);
            var targetSunState = targetIlluminationSup?.targetSunState;
            if ((targetIlluminationSup?.fireControlOffset ?? 0) <= 0)
            {
                breakdown.dawnDuskOffset = NavalUtils.GetDawnDuskFireControlOffset(targetSunState, stats.observerToTargetTrueBearingRelativeToNorthDeg);
                if (targetSunState.GetDayNightLevel() == DayNightLevel.Night)
                    breakdown.nightMoonlightOffset = NavalGameState.Instance.scenarioState.hasMoonlight ? -2 : -4;
            }

            breakdown.illuminationOffset = targetIlluminationSup?.fireControlOffset ?? 0;

            var visibility = NavalGameState.Instance.scenarioState.visibility;
            if (visibility >= VisibilityDescription.LightHaze)
                breakdown.visibilityOffset = 0;
            else if (visibility >= VisibilityDescription.ThinFog)
                breakdown.visibilityOffset = -2;
            else
                breakdown.visibilityOffset = -4;

            var firingShipEA = shooter.IsEvasiveManeuvering();
            var targetShipEA = target.IsEvasiveManeuvering();
            if (firingShipEA && targetShipEA)
                breakdown.evasiveActionOffset = -8;
            else if (targetShipEA)
                breakdown.evasiveActionOffset = -3;
            else if (firingShipEA)
                breakdown.evasiveActionOffset = -2;

            if (fireCtx.shipLogSupplementaryMap.TryGetValue(shooter, out var meShipLogSup) &&
                meShipLogSup.shipLogsFiredAtMe.Count >= 3)
            {
                breakdown.underFireOffset = -2;
            }

            breakdown.targetSizeOffset = target.shipClass.targetSizeModifier;
            breakdown.seaStateOffset = RuleChart.ResolveSeaStateOffset(
                shooter.shipClass.displacementTons,
                NavalGameState.Instance.scenarioState.seaStateBeaufort,
                out bool blocked
            );
            breakdown.crewQualityOffset = shooter.GetEffectiveCrewQualityForFloatUsage();

            breakdown.finalFireControlScore = breakdown.baseFireControlScore
                + breakdown.visibilityOffset
                + breakdown.dawnDuskOffset
                + breakdown.nightMoonlightOffset
                + breakdown.illuminationOffset
                + breakdown.evasiveActionOffset
                + breakdown.underFireOffset
                + breakdown.targetSizeOffset
                + breakdown.seaStateOffset
                + breakdown.crewQualityOffset;

            breakdown.hitProbabilityTableP100 = RuleChart.GetHitProbP100(breakdown.finalFireControlScore);
            breakdown.finalHitProbability = breakdown.hitProbabilityTableP100 * 0.01f;

            return breakdown;
        }

        public int GetAvailableBarrels(RapidFiringBatteryLocation side)
        {
            var r = GetRapidFireBatteryRecord();

            var (barrelsClass, barrelsCurrent) = side == RapidFiringBatteryLocation.Starboard ?
                GetClassCurrentBarrels(r.barrelsLevelStarboard, starboardMountHits) :
                GetClassCurrentBarrels(r.barrelsLevelPort, portMountHits);
            return barrelsCurrent;
        }

        public IEnumerable<RapidFiringBatteryStatusOneSide> GetSideBatteries()
        {
            foreach (var side in new[] { RapidFiringBatteryLocation.Port, RapidFiringBatteryLocation.Starboard })
            {
                if (GetAvailableBarrels(side) > 0)
                {
                    yield return new RapidFiringBatteryStatusOneSide()
                    {
                        original = this,
                        side = side
                    };
                }
            }
        }

        public static bool disableAmmunitionCost = false;

        public void Step(float deltaSeconds)
        {
            var r = GetRapidFireBatteryRecord();
            var shooter = EntityManager.Instance.GetParent<ShipLog>(this);
            if (shooter != null && shooter.IsSurpriseRestricted())
                return;

            var fireCtx = GunneryFireContext.GetCurrentOrCreateTemp();

            var unfiredBarrels = (new[] { RapidFiringBatteryLocation.Starboard, RapidFiringBatteryLocation.Port }).ToDictionary(
                side => side,
                side => GetAvailableBarrels(side)
            );

            foreach (var tgtRec in targettingRecords)
            {
                var tgt = tgtRec.GetTarget();
                if (tgt != null)
                {
                    tgtRec.processingSeconds += deltaSeconds;

                    var stTgtSup = fireCtx.GetOrCalcualteShipLogPairSupplementary(shooter, tgt);
                    var stats = stTgtSup.stats;


                    var doctrineRespected = shooter.doctrine.GetMaximumFiringDistanceYardsFor100mmLess().IsGreaterThanIfSpecified(stats.distanceYards);
                    if (!doctrineRespected)
                        continue;

                    var side = NavalUtils.GetBatterySide(stats.observerToTargetBearingRelativeToBowDeg);
                    var used = 0;
                    if (unfiredBarrels[side] <= 0)
                    {
                        tgtRec.allocated = 0;
                    }
                    else
                    {
                        used = Math.Min(unfiredBarrels[side], tgtRec.allocated);
                        tgtRec.allocated = used;
                        unfiredBarrels[side] = unfiredBarrels[side] - used;
                    }

                    if (used == 0)
                        continue;

                    var secondsPerShot = 120 / used;

                    var maskCheckResult = stTgtSup.GetOrCalcualteMaskCheckResult();
                    if (maskCheckResult.isMasked)
                    {
                        secondsPerShot *= 2; // ROF / 2 if masked
                    }

                    var hitProbabilityBreakdown = GetCurrentHitProbabilityBreakdown(tgtRec, fireCtx, used, secondsPerShot);

                    while (tgtRec.processingSeconds >= secondsPerShot && ammunition > 0)
                    {
                        tgtRec.processingSeconds -= secondsPerShot;

                        if(!disableAmmunitionCost)
                        {
                            ammunition -= 1;
                        }

                        var hitProb = hitProbabilityBreakdown.finalHitProbability;
                        var hit = (float)RandomUtils.rand.NextDouble() < hitProb;

                        var log = new RapidFiringLog()
                        {
                            firingTargetObjectId = tgt.objectId,
                            firingTime = NavalGameState.Instance.scenarioState.dateTime,
                            distanceYards = stats.distanceYards,
                            hitProb = hitProb,
                            hit = hit
                        };
                        logs.Add(log);
                        tgt.MarkAttackedAtCurrentMinute();

                        if (hit)
                        {
                            var damagePoint = RuleChart.RollRapidFireBatteryDamage(r.damageFactor);
                            log.damagePoint = damagePoint;

                            var tgtLog = new ShipLogRapidFiringGunHitLog()
                            {
                                shooterId = shooter.objectId,
                                time = NavalGameState.Instance.scenarioState.dateTime,
                                damagePoint = damagePoint
                            };
                            // tgt.logs.Add(tgtLog);
                            tgt.AddLog(tgtLog);

                            // tgt.damagePoint += damagePoint;
                            tgt.AddDamagePoint(damagePoint);
                        }
                    }
                }
            }

            targettingRecords.RemoveAll(r => r.allocated == 0);

            if(ammunition <= 0)
            {
                targettingRecords.Clear();
            }
        }
    }

    public class RapidFiringBatteryStatusOneSide : IWTABattery
    {
        public RapidFiringStatus original;
        public RapidFiringBatteryLocation side;

        public override string ToString()
        {
            return $"RapidFiringBatteryStatusOneSide({original}, {side})";
        }

        public float EvaluateFirepowerScore(float distanceYards, TargetAspect targetAspect, float targetSpeedKnots, float bearingRelativeToBowDeg)
        {
            var resolvedSide = NavalUtils.GetBatterySide(bearingRelativeToBowDeg);
            if (resolvedSide != side)
                return 0;
            return original.EvaluateFirepowerScore(distanceYards, bearingRelativeToBowDeg);

            // TODO: Add doctrine for 100mm- batteries
        }

        IWTAObject IWTABattery.GetCurrentFiringTarget()
        {
            var targetCounts = original.targettingRecords.Where(r => r.GetTarget() != null)
                .GroupBy(r => r.GetTarget())
                .Select(g => (g.Key, g.Count()))
                .ToList();

            if (targetCounts.Count == 0)
                return null;

            var maxCount = targetCounts.Max(g => g.Item2);
            return targetCounts.First(g => g.Item2 == maxCount).Item1;
        }

        public void SetFiringTargetAutomatic(ShipLog target)
        {
            var matched = original.targettingRecords.FirstOrDefault(r => r.location == side);
            if (matched == null)
            {
                matched = new RapidFiringTargettingStatus
                {
                    location = side,
                    // processingSeconds = 0,
                    // allocated = 0,
                    // targetObjectId = target?.objectId
                };
                original.targettingRecords.Add(matched);
            }

            var targetObjectId = target?.objectId;
            if (matched.targetObjectId == targetObjectId)
            {
                matched.allocated = original.GetAvailableBarrels(side);
                return;
            }

            matched.processingSeconds = 0;
            matched.allocated = original.GetAvailableBarrels(side);
            matched.targetObjectId = targetObjectId;
            // original.targettingRecords.
        }

        void IWTABattery.SetFiringTarget(IWTAObject target) => SetFiringTargetAutomatic(target as ShipLog); // TODO: Support other IWTAObject (land targets?)

        public void ResetFiringTarget()
        {
            original.targettingRecords.RemoveAll(r => r.location == side);
        }

        int IWTABattery.GetOverConcentrationCoef() => 0; // DoB gives 0, though literally it should be 2
        public bool IsChangeTargetBlocked() => false;
    }
}
