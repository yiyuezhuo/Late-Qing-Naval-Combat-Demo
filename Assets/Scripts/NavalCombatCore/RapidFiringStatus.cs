using System.Collections.Generic;
using System.Data;
using System.Linq;
using System;
using GeographicLib;
using System.Xml.Serialization;


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

        public string Summary()
        {
            var target = GetFiringTarget();
            var targetName = target.namedShip?.name?.GetMergedName();
            var hitDesc = hit ? $"hit -> {damagePoint} DP" : "miss";
            return $"{firingTime} -> {targetName}, {distanceYards} yards, P={hitProb * 100}%, {hitDesc}";
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

        public IEnumerable<IObjectIdLabeled> GetSubObjects()
        {
            yield break;
        }

        public string DescribeDetail()
        {
            var lines = new List<string>() { $"Detail: {objectId}" };
            lines.AddRange(logs.Select(r => r.Summary()));
            return string.Join("\n", lines);
        }

        public void ResetDamageExpenditureState()
        {
            portMountHits = 0;
            starboardMountHits = 0;
            fireControlHits = 0;
            targettingRecords.Clear();

            var rfBtyRec = GetRapidFireBatteryRecord();
            var barrels = rfBtyRec.barrelsLevelStarboard.FirstOrDefault() + rfBtyRec.barrelsLevelPort.FirstOrDefault();
            ammunition = barrels * 15;

            logs.Clear();
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

        public string GetInfo()
        {
            var r = rapidFireBatteryRecord;
            if (r == null)
                return "Not Valid";

            var (portClass, portCurrent) = GetClassCurrentBarrels(r.barrelsLevelPort, portMountHits);
            var (starboardClass, starboardCurrent) = GetClassCurrentBarrels(r.barrelsLevelStarboard, starboardMountHits);

            return $"{portClass}({portCurrent})/{starboardClass}({starboardCurrent}) {r.name.mergedName}";
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

            var isStarboard = MeasureUtils.GetPositiveAngleDifference(bearingRelativeToBowDeg, 45) < 90;
            var barrelsCurrent = GetAvailableBarrels(isStarboard ? RapidFiringBatteryLocation.Starboard : RapidFiringBatteryLocation.Port);

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

        public void Step(float deltaSeconds)
        {
            var r = GetRapidFireBatteryRecord();
            var shooter = EntityManager.Instance.GetParent<ShipLog>(this);
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

                    var side = MeasureUtils.GetPositiveAngleDifference(stats.observerToTargetBearingRelativeToBowDeg, 45) <= 90 ? RapidFiringBatteryLocation.Starboard : RapidFiringBatteryLocation.Port;
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

                    while (tgtRec.processingSeconds >= secondsPerShot && ammunition > 0)
                    {
                        tgtRec.processingSeconds -= secondsPerShot;
                        ammunition -= 1;

                        var fireControlScore = GetFireControlScore(stats.distanceYards);

                        // Visibility - apply to all conditions
                        var visibility = NavalGameState.Instance.scenarioState.visibility;
                        if (visibility >= VisibilityDescription.LightHaze)
                        {
                            fireControlScore += 0;
                        }
                        else if (visibility >= VisibilityDescription.ThinFog)
                        {
                            fireControlScore += -2;
                        }
                        else
                        {
                            fireControlScore += -4;
                        }

                        // TODO: Handle Additional for dawn/dusk condition
                        // Target silhouetted by horizon: +1
                        // Target in darkness: -2
                        // None of above: +0
                        // (EQ to Batteries)

                        // TODO: Handle Additional for night conditions
                        // No moonlight: -4
                        // Moonlight: -2
                        // (EQ to Batteries)

                        // TODO: Handle Additional for illumination (1b or 1c)
                        // Target afire or illuminated by searchlight: +2
                        // Target using searchlight OR is illuminated: +1
                        // (EQ to Batteries)

                        // TODO: Smoke 
                        // Target obscured by battle smoke or funnel smokescreen: -2

                        // TODO: Evasive Action / Emergency Turn
                        // Target only in EA: -3
                        // Target ship only in EA: -2
                        // Target and firing ships in EA: -8
                        // (EQ to Batteries)

                        // TODO: Firing ship under fire
                        // Under fire from 3 or more ships during this turn: -2
                        // (EQ to Batteries)

                        // Size of target ship
                        // TS (from Ship Log of target ship)
                        fireControlScore += tgt.shipClass.targetSizeModifier;

                        // TODO: Battle factor
                        // Sea State + Crew Rating (from Ship Log)
                        // (EQ to Batteries)

                        var hitProb = RuleChart.GetHitProbP100(fireControlScore) * 0.01f;
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

                        if (hit)
                        {
                            var damagePoint = RuleChart.RollRapidFireBatteryDamage(r.damageFactor);
                            log.damagePoint = damagePoint;

                            // tgt.damagePoint += damagePoint;
                            tgt.AddDamagePoint(damagePoint);
                        }
                    }
                }
            }

            targettingRecords.RemoveAll(r => r.allocated == 0);
        }
    }

    public class RapidFiringBatteryStatusOneSide : IWTABattery
    {
        public RapidFiringStatus original;
        public RapidFiringBatteryLocation side;

        public float EvaluateFirepowerScore(float distanceYards, TargetAspect targetAspect, float targetSpeedKnots, float bearingRelativeToBowDeg)
        {
            var isStarboard = MeasureUtils.GetPositiveAngleDifference(bearingRelativeToBowDeg, 45) < 90;
            if ((isStarboard && side != RapidFiringBatteryLocation.Starboard) || (!isStarboard && side == RapidFiringBatteryLocation.Starboard))
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
            matched.processingSeconds = 0;
            matched.allocated = original.GetAvailableBarrels(side);
            matched.targetObjectId = target?.objectId;
            // original.targettingRecords.
        }

        void IWTABattery.SetFiringTarget(IWTAObject target) => SetFiringTargetAutomatic(target as ShipLog); // TODO: Support other IWTAObject (land targets?)

        public void ResetFiringTarget()
        {
            original.targettingRecords.RemoveAll(r => r.location == side);
        }

        int IWTABattery.GetOverConcentrationCoef() => 0; // DoB gives 0, though literally it should be 2
    }
}