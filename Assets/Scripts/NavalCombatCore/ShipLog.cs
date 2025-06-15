using System.Collections.Generic;
using System.Data;
using System.Linq;
using System;
using GeographicLib;
using System.Xml.Serialization;
using UnityEngine.UIElements;
using Palmmedia.ReportGenerator.Core;
using UnityEngine;


namespace NavalCombatCore
{
    public class BatteryAmmunitionRecord
    {
        public int ArmorPiercing;
        public int semiArmorPiercing;
        public int common;
        public int highExplosive;

        public static Dictionary<AmmunitionType, string> ammunitionTypeAcronymMap = new()
        {
            { AmmunitionType.ArmorPiercing, "AP" },
            { AmmunitionType.SemiArmorPiercing, "SAP" },
            { AmmunitionType.Common, "COM" },
            { AmmunitionType.HighExplosive, "HE" }
        };

        public string Summary()
        {
            var words = new List<string>();
            foreach ((var w, var num) in new (string, int)[]{
                ("AP", ArmorPiercing),
                ("SAP", semiArmorPiercing),
                ("COM", common),
                ("HE", highExplosive)
            })
            {
                if (num > 0)
                {
                    words.Add($"{w}: {num}");
                }
            }

            return string.Join(", ", words);
        }

        public int GetValue(AmmunitionType ammo)
        {
            return ammo switch
            {
                AmmunitionType.ArmorPiercing => ArmorPiercing,
                AmmunitionType.SemiArmorPiercing => semiArmorPiercing,
                AmmunitionType.Common => common,
                AmmunitionType.HighExplosive => highExplosive,
                _ => 0
            };
        }

        public int GetTotalValue() => ArmorPiercing + semiArmorPiercing + common + highExplosive;

        public void CostOne(AmmunitionType ammo)
        {
            switch (ammo)
            {
                case AmmunitionType.ArmorPiercing:
                    ArmorPiercing--;
                    break;
                case AmmunitionType.SemiArmorPiercing:
                    semiArmorPiercing--;
                    break;
                case AmmunitionType.Common:
                    common--;
                    break;
                case AmmunitionType.HighExplosive:
                    highExplosive--;
                    break;
            }
        }
    }

    public enum MountStatus
    {
        Operational,
        Disabled, // may restore after a period of time or by die roll
        Destroyed
    }

    public class MountFiringRecord
    {
        [XmlAttribute]
        public string firingTargetObjectId;

        // public ShipLog GetFiringTarget() => EntityManager.Instance.GetOnMapShipLog(firingTargetObjectId);
        public ShipLog GetFiringTarget() => EntityManager.Instance.Get<ShipLog>(firingTargetObjectId);

        [XmlAttribute]
        public AmmunitionType ammunitionType;

        [XmlAttribute]
        public DateTime firingTime;

        [XmlAttribute]
        public float distanceYards;

        [XmlAttribute]
        public float hitProb;

        [XmlAttribute]
        public bool hit;
        // TODO: Concrete Result
        [XmlAttribute]
        public ArmorLocation armorLocation;

        [XmlAttribute]
        public HitPenDetType hitPenDetType;

        public RuleChart.ShellDamageResult shellDamageResult;

        public string Summary()
        {
            var target = GetFiringTarget();
            var targetName = target.namedShip?.name?.GetMergedName();
            var ammoType = BatteryAmmunitionRecord.ammunitionTypeAcronymMap[ammunitionType];
            var hitDesc = hit ? $"hit {armorLocation} -> {hitPenDetType} -> {shellDamageResult}" : "miss";
            return $"{firingTime} {ammoType} -> {targetName}, {distanceYards} yards, P={hitProb * 100}%, {hitDesc}";
        }

        public override string ToString() => Summary();
    }

    public partial class AbstractMountStatusRecord : IObjectIdLabeled
    {
        public string objectId { get; set; }
        public MountStatus status;

        public string firingTargetObjectId;
        public ShipLog GetFiringTarget()
        {
            var target = EntityManager.Instance.GetOnMapShipLog(firingTargetObjectId);
            if (target == null || !target.IsOnMap())
                return null;
            return target;
        }

        public class MountLocationRecordInfo
        {
            public int recordIndex;
            public int subIndex;
            public MountLocationRecord record;

            public string Summary() // Used in Ship Log Editor
            {
                return $"#{recordIndex + 1} #{subIndex + 1} x{record.barrels} {record.mountLocationAcronym} ({record.SummaryArcs()})";
            }
        }

        protected static MountLocationRecordInfo GetMountLocationRecordInfo(List<MountLocationRecord> mountLocationRecords, int mountIdx)
        {
            if (mountIdx < 0)
                return null;

            var _recordIndex = 0;
            var mntLocRecs = mountLocationRecords;
            while (_recordIndex < mntLocRecs.Count && mntLocRecs[_recordIndex].mounts <= mountIdx)
            {
                mountIdx -= mntLocRecs[_recordIndex].mounts;
                _recordIndex++;
            }
            if (_recordIndex < mntLocRecs.Count && mountIdx < mntLocRecs[_recordIndex].mounts)
            {
                return new()
                {
                    recordIndex = _recordIndex,
                    subIndex = mountIdx,
                    record = mntLocRecs[_recordIndex],
                };
            }
            return null;
        }

    }

    public partial class TorpedoMountStatusRecord : AbstractMountStatusRecord
    {
        public int currentLoad;
        public float reloadingSeconds;
        public int reloadedLoad;


        public MountLocationRecordInfo GetTorpedoMountLocationRecordInfo()
        {
            var shipLog = EntityManager.Instance.GetParent<ShipLog>(this);
            if (shipLog == null)
                return null;

            var mountIdx = shipLog.torpedoSectorStatus.mountStatus.IndexOf(this);

            var shipClass = shipLog.shipClass;
            if (shipClass == null)
                return null;

            if (mountIdx < 0)
                return null;

            // var mountLocationRecord = shipClass.torpedoSector.mountLocationRecords[mountIdx];
            // return mountLocationRecord;
            var ret = GetMountLocationRecordInfo(shipClass.torpedoSector.mountLocationRecords, mountIdx);
            // ret.isTorpedo = true;
            return ret;
        }

        public void ResetDamageExpenditureState()
        {
            var info = GetTorpedoMountLocationRecordInfo().record;
            currentLoad = info.barrels;
            reloadingSeconds = 0;
            reloadedLoad = info.barrels;
        }

        public void SetFiringTarget(ShipLog target)
        {
            firingTargetObjectId = target?.objectId;
        }

        public void Step(float deltaSeconds)
        {
            // reload
            var platform = EntityManager.Instance.GetParent<ShipLog>(this);
            var recordInfo = GetTorpedoMountLocationRecordInfo();

            var requested = recordInfo.record.barrels - currentLoad;
            var ammunitionCap = platform.torpedoSectorStatus.ammunition;
            var reloadLimitCap = recordInfo.record.reloadLimit == 0 ? int.MaxValue : recordInfo.record.reloadLimit - reloadedLoad;
            var transferred = Math.Min(reloadLimitCap, Math.Min(requested, ammunitionCap));

            if (transferred > 0)
            {
                reloadingSeconds += deltaSeconds;


                while (reloadingSeconds >= 360 && transferred > 0) // 6min torpedo reload time (SK5 & DoB)
                {
                    requested = recordInfo.record.barrels - currentLoad;
                    ammunitionCap = platform.torpedoSectorStatus.ammunition;
                    transferred = Math.Min(reloadLimitCap, Math.Min(requested, ammunitionCap));

                    currentLoad += transferred;
                    platform.torpedoSectorStatus.ammunition -= transferred;
                    reloadedLoad += transferred;

                    reloadingSeconds -= 360;
                }
            }
            else
            {
                reloadingSeconds = 0;
            }

            // fire on target
            var tgt = GetFiringTarget();
            var torpedoAttackCtx = TorpedoAttackContext.GetCurrentOrCreateTemp();
            var classSector = platform.shipClass.torpedoSector;

            if (tgt != null && currentLoad > 0 && classSector.torpedoSettings.Count > 0)
            {
                var (distanceKm, azi1) = MeasureStats.Approximation.CalculateDistanceKmAndBearingDeg(platform.position.LatDeg, platform.position.LonDeg, tgt.position.LatDeg, tgt.position.LonDeg);
                var distYards = (float)distanceKm * MeasureUtils.meterToYard;
                var doctrineRespected = platform.doctrine.GetMaximumFiringDistanceYardsForTorpedo().IsGreaterThanIfSpecified(distYards);
                if (doctrineRespected)
                {
                    var settingPairs = classSector.torpedoSettings.Select(setting => (setting,
                        torpedoAttackCtx.GetOrCalculateFireComplexSupplementary(platform, tgt, setting.speedKnots).interceptionPointSolverResult
                    )).Where(sp => sp.Item2.success && sp.Item2.distanceYards < sp.setting.rangeYards).ToList();
                    if (settingPairs.Count > 0)
                    {
                        var minInterceptionDistYard = settingPairs.Min(sp => sp.Item2.distanceYards);
                        var bestSettingPair = settingPairs.First(sp => sp.Item2.distanceYards == minInterceptionDistYard);
                        var setting = bestSettingPair.setting;
                        var interceptionRes = bestSettingPair.Item2;

                        var bearingRelativeToBowDeg = MeasureUtils.NormalizeAngle(interceptionRes.azimuth - platform.headingDeg);
                        if (recordInfo.record.IsInArc(bearingRelativeToBowDeg))
                        {
                            // Launch Torpedo!
                            var newTorpedo = new LaunchedTorpedo()
                            {
                                sourceName = classSector.name.Clone(),
                                damageClass = classSector.damageClass,
                                headingDeg = interceptionRes.azimuth,
                                position = platform.position.Clone(),
                                shooterId = platform.objectId,
                                desiredTargetObjectId = tgt.objectId,
                                mapState = MapState.Deployed,
                                speedKnots = setting.speedKnots,
                                maxRangeYards = setting.rangeYards,
                                movedDistanceYards = 0
                            };
                            NavalGameState.Instance.launchedTorpedos.Add(newTorpedo);
                            EntityManager.Instance.Register(newTorpedo, null);

                            currentLoad -= 1;
                        }
                    }
                }
            }
        }

    }

    public partial class MountStatusRecord : AbstractMountStatusRecord
    {
        public float processSeconds;
        public AmmunitionType ammunitionType;

        public List<MountFiringRecord> logs = new();

        public string DescribeDetail()
        {
            var ctx = GetFullContext();

            var lines = new List<string>() { $"Detail: {objectId}" };

            lines.AddRange(logs.Select(r => r.Summary()));

            return string.Join("\n", lines);

            // return $"{ctx.shipLog.namedShip?.name.GetMergedName()}";
        }

        public void Step(float deltaSeconds)
        {
            if (status != MountStatus.Operational)
                return;

            var tgt = GetFiringTarget();
            if (tgt != null)
            {
                processSeconds += deltaSeconds;

                var fireCtx = GunneryFireContext.GetCurrentOrCreateTemp();

                // var shooter = GetPlatform();
                var ctx = fireCtx.mountStatusRecordMap[this].ctx;
                if (!ctx.fullyResolved)
                    return;

                var isAmmoSwitchAuto = ctx.shipLog.doctrine.GetAmmunitionSwitchAutomaticType() == AutomaticType.Automatic;
                if (isAmmoSwitchAuto)
                {
                    var tgtArmorScore = tgt.EvaluateArmorScore();
                    if (tgtArmorScore < 0.5)
                    {
                        // ctx.batteryStatus.
                        ammunitionType = ctx.batteryStatus.ChooseAmmunitionByPreferredType(AmmunitionType.HighExplosive);
                    }
                    else
                    {
                        ammunitionType = ctx.batteryStatus.ChooseAmmunitionByPreferredType(AmmunitionType.ArmorPiercing);
                    }
                    // ammunitionType
                }

                var ammoFallbackable = ctx.shipLog.doctrine.GetAmmunitionFallbackable();
                if (!(
                        ctx.batteryStatus.ammunition.GetValue(ammunitionType) >= 0 ||
                        (ammoFallbackable && ctx.batteryStatus.ammunition.GetTotalValue() <= 0)
                    )) // Later will require re-check
                    return;

                var shooter = ctx.shipLog;

                var isFireChecked = fireCtx.shipLogSupplementaryMap[tgt].batteriesFiredAtMe.Contains(ctx.batteryStatus);
                if (!isFireChecked) // include range / arc / Doctrine check (though ammo should be checked dynamiclly since this loop will update ammo state)
                    return;
                var shooterTargetSup = fireCtx.GetOrCalcualteShipLogPairSupplementary(shooter, tgt);
                var stats = shooterTargetSup.stats;


                // var stats = MeasureStats.MeasureApproximation(shooter, tgt); // TODO: Deduplicate redundant geo calculation, cache in the pulse level cache?
                // var isInArc = ctx.mountLocationRecord.IsInArc(stats.observerToTargetBearingRelativeToBowDeg);
                // var isInRange = stats.distanceYards <= ctx.batteryRecord.rangeYards;
                // if (!isInRange || !isInArc)
                //     return;

                var penRecord = ctx.batteryRecord.penetrationTableRecords.FirstOrDefault(r => stats.distanceYards <= r.distanceYards);
                if (penRecord == null)
                    return;

                var shootsPer2Min = penRecord.rateOfFire;
                if (shootsPer2Min == 0)
                    return;
                var secondsPerShoot = 120 / shootsPer2Min;

                // if (processSeconds < secondsPerShoot) // Later will require re-check
                //     return;

                var fireControlRow = ctx.batteryRecord.fireControlTableRecords.FirstOrDefault(r => tgt.speedKnots <= r.speedThresholdKnot);
                if (fireControlRow == null)
                    return;

                var fireControlScoreRaw = fireControlRow.GetValue(penRecord.rangeBand, stats.targetPresentAspectFromObserver);
                if (stats.distanceYards <= 4500)
                {
                    var closeRangeFireControlScore = RuleChart.GetCloseRangeFireControlScore(stats.distanceYards, tgt.speedKnots, stats.targetPresentAspectFromObserver);
                    fireControlScoreRaw = Math.Max(fireControlScoreRaw, closeRangeFireControlScore);
                }

                var maskCheckResult = shooterTargetSup.GetOrCalcualteMaskCheckResult();
                if (maskCheckResult.isMasked)
                {
                    secondsPerShoot *= 2; // ROF / 2 if masked
                }

                var targetSup = fireCtx.shipLogSupplementaryMap[tgt];
                var firedAtTargetBatteriesCount = targetSup.batteriesFiredAtMe.Count; // over-concentration

                // TODO: Stack other buffer

                // skip to log ammo consumption and firing "result"
                while (processSeconds >= secondsPerShoot &&
                        (
                            ctx.batteryStatus.ammunition.GetValue(ammunitionType) >= 0 ||
                            (ammoFallbackable && ctx.batteryStatus.ammunition.GetTotalValue() <= 0)
                        )
                )
                {
                    processSeconds -= secondsPerShoot;
                    ctx.batteryStatus.ammunition.CostOne(ammunitionType);

                    if (ammoFallbackable)
                    {
                        ammunitionType = ctx.batteryStatus.ChooseAmmunitionByPreferredType(ammunitionType); // TODO: Use doctrine suggested value
                    }

                    var fireControlScore = fireControlScoreRaw;

                    // Visibility - apply to all conditions
                    var visibility = NavalGameState.Instance.scenarioState.visibility;
                    if (visibility >= VisibilityDescription.VeryClear1)
                    {
                        // Code 8-9 (very clear): +1
                        fireControlScore += 1;
                    }
                    else if (visibility >= VisibilityDescription.LightHaze)
                    {
                        // Code 6-7 (normal): +0
                        fireControlScore += 0;
                    }
                    else if (visibility >= VisibilityDescription.ThinFog)
                    {
                        // Code 4-5 (haze): -2
                        fireControlScore += -2;
                    }
                    else
                    {
                        // Patchy fog or squalls
                        fireControlScore += -4;
                    }

                    // TODO: Handle Additional for dawn/dusk condition
                    // Target silhouetted by horizon: +1
                    // Target in darkness: -2
                    // None of above: +0

                    // TODO: Handle Additional for night conditions
                    // No moonlight: -4
                    // Moonlight: -2

                    // TODO: Handle Additional for illumination (1b or 1c)
                    // Target afire or illuminated by searchlight: +2
                    // Target using searchlight OR is illuminated: +1

                    // TODO: Blind Fire
                    // Firing ship is using Blind Fire (target cannot be seen): -5

                    // TODO: Smoke (cumulative and does not apply to Blind Fire using Radar)
                    // Target obscured by battle smoke: -1
                    // Target obscured by funnel smokescreen: -3

                    // TODO: Evasive Action / Emergency Turn
                    // Target only in EA: -3
                    // Target ship only in EA: -2
                    // Target and firing ships in EA: -8

                    // TODO: Target Acquisition
                    // Firing on different ship from last turn: -2
                    // Target ship hit by firing ship last turn: +2

                    // TODO: Firing ship under fire
                    // Under fire from 3 or more ships during this turn: -2

                    // TODO: Over Concentration & Barrage
                    // 1 ship firing at target with 1 battery: 0
                    // For each additional primary, secondary or teriary battery of any ship firing at same target: -1
                    // For every primary, secondary or tertiary battery of any ship using barrage fire at same target: -2

                    // Size of target ship
                    // TS (from Ship Log of target ship)
                    fireControlScore += tgt.shipClass.targetSizeModifier;

                    // Pending: Spotter Aircraft
                    // Spotter aircraft (target visible from firing ship): +2

                    // TODO: Battle factor
                    // Sea State + Crew Rating (from Ship Log)

                    // Fire Control Radar Modifier
                    fireControlScore += ctx.batteryRecord.fireControlRadarModifier;

                    var hitProb = RuleChart.GetHitProbP100(fireControlScore) * 0.01f;
                    var hit = (float)RandomUtils.rand.NextDouble() < hitProb;

                    var logRecord = new MountFiringRecord()
                    {
                        firingTargetObjectId = tgt.objectId,
                        ammunitionType = ammunitionType,
                        firingTime = NavalGameState.Instance.scenarioState.dateTime,
                        distanceYards = stats.distanceYards,
                        hitProb = hitProb,
                        hit = hit, // TODO: enable it
                        // hitPenDetType = hitPenDetType,
                        // armorLocation = armorLocation,
                        // shellDamageResult = shellDamageResult
                    };
                    logs.Add(logRecord);

                    if (hit)
                    {
                        var armorLocation = RuleChart.RollArmorLocation(stats.targetPresentAspectFromObserver, penRecord.rangeBand);
                        if (armorLocation != ArmorLocation.Ineffective)
                        {
                            var armorLocationAngleType = RuleChart.armorLocationToAngleType.GetValueOrDefault(armorLocation);
                            var refPenInch = penRecord.GetValue(armorLocationAngleType);
                            var penInch = RuleChart.GetAdjustedPenetrationByType(ctx.batteryRecord.penetrationTableBaseType, refPenInch, ctx.batteryRecord.shellSizeInch, ammunitionType);

                            var armorEffInch = tgt.shipClass.armorRating.GetArmorEffectiveInch(armorLocation);
                            var hitPenDetType = RuleChart.ResolveHitPenDetType(penInch, armorEffInch, ammunitionType);

                            var shellDamageResult = RuleChart.ResolveShellDamageResult(ctx.batteryRecord.damageRating, hitPenDetType, ammunitionType);

                            // var logRecord = new MountFiringRecord()
                            // {
                            //     firingTargetObjectId = tgt.objectId,
                            //     ammunitionType = ammunitionType,
                            //     firingTime = NavalGameState.Instance.scenarioState.dateTime,
                            //     distanceYards = stats.distanceYards,
                            //     hitProb = hitProb,
                            //     hit = hit, // TODO: enable it
                            //     hitPenDetType = hitPenDetType,
                            //     armorLocation = armorLocation,
                            //     shellDamageResult = shellDamageResult
                            // };
                            // logs.Add(logRecord);
                            logRecord.hitPenDetType = hitPenDetType;
                            logRecord.armorLocation = armorLocation;
                            logRecord.shellDamageResult = shellDamageResult;

                            // TODO: Handle damage effect and general (DP caused) damage effect.
                            tgt.damagePoint += shellDamageResult.damagePoint;

                            var logger = ServiceLocator.Get<ILoggerService>();
                            logger.Log($"{ctx.shipLog.namedShip.name.GetMergedName()} {ctx.batteryRecord.name.GetMergedName()} -> {tgt.namedShip.name.GetMergedName()} ({logRecord.Summary()})");

                            // TODO: Apply damage point and process side effect

                        }
                    }
                }
            }
            else
            {
                processSeconds = 0;
            }
        }

        public class FullContext
        {
            public BatteryStatus batteryStatus;
            public ShipLog shipLog;
            public ShipClass shipClass;
            public int batteryIdx;
            public BatteryRecord batteryRecord;
            public int mountStatusIdx;
            public int mountRecordIdx;
            public int mountRecordSubIdx;
            public MountLocationRecord mountLocationRecord;
            public bool fullyResolved;

            public void Build(MountStatusRecord mountStatus) // FIXME: Well the code smell is too much
            {
                batteryStatus = EntityManager.Instance.GetParent<BatteryStatus>(mountStatus);
                if (batteryStatus == null)
                    return;

                shipLog = EntityManager.Instance.GetParent<ShipLog>(batteryStatus);
                if (shipLog == null)
                    return;

                shipClass = shipLog.shipClass;
                if (shipClass == null)
                    return;

                batteryIdx = shipLog.batteryStatus.IndexOf(batteryStatus);
                if (batteryIdx < 0 || batteryIdx >= shipClass.batteryRecords.Count)
                    return;

                batteryRecord = shipClass.batteryRecords[batteryIdx];

                mountStatusIdx = batteryStatus.mountStatus.IndexOf(mountStatus);
                if (mountStatusIdx < 0)
                    return;

                var mountIdx = mountStatusIdx;
                var _recordIndex = 0;
                var mntLocRecs = batteryRecord.mountLocationRecords;
                while (_recordIndex < mntLocRecs.Count && mntLocRecs[_recordIndex].mounts <= mountIdx)
                {
                    mountIdx -= mntLocRecs[_recordIndex].mounts;
                    _recordIndex++;
                }
                if (_recordIndex < mntLocRecs.Count && mountIdx < mntLocRecs[_recordIndex].mounts)
                {
                    mountRecordIdx = _recordIndex;
                    mountRecordSubIdx = mountIdx;
                    mountLocationRecord = mntLocRecs[_recordIndex];

                    fullyResolved = true;
                }
            }
        }

        public FullContext GetFullContext()
        {
            var ctx = new FullContext();
            ctx.Build(this);
            return ctx;
        }

        public MountLocationRecordInfo GetMountLocationRecordInfo()
        {

            var battery = EntityManager.Instance.GetParent<BatteryStatus>(this);

            if (battery == null)
                return null;

            var mountIdx = battery.mountStatus.IndexOf(this);

            var batteryRecord = battery.GetBatteryRecord();
            if (batteryRecord == null)
                return null;

            var ret = GetMountLocationRecordInfo(batteryRecord.mountLocationRecords, mountIdx);
            // ret.isTorpedo = false;
            return ret;
        }

        public void SetFiringTarget(ShipLog target)
        {
            if (target == null)
            {
                firingTargetObjectId = null;
                processSeconds = 0;
                return;
            }
            if (target.objectId == firingTargetObjectId)
            {
                return;
            }
            firingTargetObjectId = target.objectId;
            processSeconds = 0;
        }

        public void ResetDamageExpenditureState()
        {
            status = MountStatus.Operational;
            firingTargetObjectId = null;
            processSeconds = 0;

            logs.Clear();
        }
    }
    
    public enum TrackingSystemState
    {
        // If a shooter fire at a target without tracking (FCS is destroyed, too many targeting or barrage fire), the firing is subject to Local Control modifier (-50% FC)
        Idle, // Tracking Position is not tracking anything.
        Destroyed,
        BeginTracking, // -50% ROF, -2 FC, BeginTracking will transition to Tracking once tracking is maintained at least 2 min 
        Tracking,
        Hitting // +2 FC, If target is hit by shooter, Hitting state will matain 2 min unless another hit is scored. 
    }

    public partial class FireControlSystemStatusRecord : IObjectIdLabeled
    {
        public string objectId { set; get; }
        public string targetObjectId { get; set; }
        public ShipLog GetTarget() => EntityManager.Instance.GetOnMapShipLog(targetObjectId);
        public TrackingSystemState trackingState;
        public float trackingSeconds;

        public void Step(float deltaSeconds)
        {
            var target = GetTarget();
            if (target != null)
            {
                trackingSeconds += deltaSeconds;
                if (trackingSeconds >= 120)
                {
                    if (trackingState == TrackingSystemState.BeginTracking)
                    {
                        trackingState = TrackingSystemState.Tracking;
                    }
                }
            }
        }

        public void ResetToIntegrityState()
        {
            targetObjectId = null;
            trackingState = TrackingSystemState.Idle;
            trackingSeconds = 0;
        }

        public void SetTrackingTarget(ShipLog target)
        {
            if (target == null)
            {
                targetObjectId = null;
                trackingState = TrackingSystemState.Idle;
                trackingSeconds = 0;
            }
            var currentTarget = GetTarget();
            if (currentTarget == target)
                return;

            targetObjectId = target?.objectId;
            trackingState = TrackingSystemState.BeginTracking;
            trackingSeconds = 0;
        }

        public int GetSubIndex()
        {
            var batteryStatus = EntityManager.Instance.GetParent<BatteryStatus>(this);
            return batteryStatus.fireControlSystemStatusRecords.IndexOf(this);
        }
    }

    public partial class BatteryStatus : IObjectIdLabeled, IWTABattery
    {
        public string objectId { get; set; }
        public BatteryAmmunitionRecord ammunition = new(); // TODO: based on mount instead of battery?
        public List<MountStatusRecord> mountStatus = new();
        // public int fireControlHits;
        public List<FireControlSystemStatusRecord> fireControlSystemStatusRecords = new();

        public IEnumerable<IObjectIdLabeled> GetSubObjects()
        {
            foreach (var mount in mountStatus)
            {
                yield return mount;
            }
            foreach (var fireControlSystemStatusRecord in fireControlSystemStatusRecords)
            {
                yield return fireControlSystemStatusRecord;
            }
        }

        public BatteryRecord GetBatteryRecord()
        {
            var shipLog = EntityManager.Instance.GetParent<ShipLog>(this);
            if (shipLog == null)
                return null;
            var idx = shipLog.batteryStatus.IndexOf(this);
            var shipClass = shipLog.shipClass;
            if (shipClass == null || idx < 0 || idx >= shipClass.batteryRecords.Count)
                return null;
            return shipClass.batteryRecords[idx];
        }

        public void ResetDamageExpenditureState()
        {
            var batteryRecord = GetBatteryRecord();
            ammunition.ArmorPiercing = batteryRecord.ammunitionCapacity / 2;
            ammunition.common = batteryRecord.ammunitionCapacity / 2;

            var expectedLength = batteryRecord.mountLocationRecords.Sum(r => r.mounts);
            Utils.SyncListToLength(expectedLength, mountStatus, this);
            foreach (var s in mountStatus)
                s.ResetDamageExpenditureState();

            // fireControlHits = 0;
            Utils.SyncListToLength(batteryRecord.fireControlPositions, fireControlSystemStatusRecords, this);
            foreach (var s in fireControlSystemStatusRecords)
                s.ResetToIntegrityState();
        }

        public string Summary() // Used in information panel
        {
            var batteryRecord = GetBatteryRecord();
            if (batteryRecord == null)
                return "[Not Specified]";
            var barrels = batteryRecord.mountLocationRecords.Sum(r => r.barrels * r.mounts);
            // var availableBarrels = mountStatus.Where(m => m.status == MountStatus.Operational).Sum(m => (m.mountLocationRecord.mounts - m.mountsDestroyed) * m.mountLocationRecord.barrels);
            var availableBarrels = mountStatus.Where(m => m.status == MountStatus.Operational).Sum(m => m.GetMountLocationRecordInfo().record.barrels);
            return $"{availableBarrels}/{barrels} {batteryRecord.name.mergedName} ({ammunition.Summary()})";
        }

        public float EvaluateFirepowerScore()
        {
            var batteryRecord = GetBatteryRecord();
            var firepoweScorePerBarrel = batteryRecord.EvaluateFirepowerPerBarrel();
            // var availableBarrels = mountStatus.Where(m => m.status == MountStatus.Operational).Sum(m => (m.mountLocationRecord.mounts - m.mountsDestroyed) * m.mountLocationRecord.barrels);
            var availableBarrels = mountStatus.Where(m => m.status == MountStatus.Operational).Sum(m => m.GetMountLocationRecordInfo().record.barrels);
            return availableBarrels * firepoweScorePerBarrel;
        }

        public float EvaluateFirepowerScore(float distanceYards, TargetAspect targetAspect, float targetSpeedKnots, float bearingRelativeToBowDeg)
        {
            var batteryRecord = GetBatteryRecord();
            if (distanceYards > batteryRecord.rangeYards)
                return 0;

            if (!IsMaxDistanceDoctrineRespected(distanceYards))
                return 0;

            var firepowerPerBarrel = batteryRecord.EvaluateFirepowerPerBarrel(distanceYards, targetAspect, targetSpeedKnots);
            var barrels = mountStatus.Where(
                m => m.status == MountStatus.Operational &&
                    m.GetMountLocationRecordInfo().record.IsInArc(bearingRelativeToBowDeg)
            ).Sum(m => m.GetMountLocationRecordInfo().record.barrels);
            return barrels * firepowerPerBarrel;
        }

        public bool IsMaxDistanceDoctrineRespected(float distanceYards)
        {
            var shellSize = GetBatteryRecord()?.shellSizeInch ?? 0;
            var doctrine = EntityManager.Instance.GetParent<ShipLog>(this)?.doctrine;
            if (doctrine == null || shellSize == 0)
                return true;

            Unspecifiable<float> d = null;
            if (shellSize > 8)
            {
                d = doctrine.GetMaximumFiringDistanceYardsFor200mmPlus();
            }
            else if (shellSize > 4)
            {
                d = doctrine.GetMaximumFiringDistanceYardsFor100mmTo200mm();
            }
            if (d == null || !d.isSpecified)
                return true;
            return distanceYards <= d.value;
        }

        public void SetFiringTargetAutomatic(ShipLog target) // For automatic fire
        {

            if (target == null)
            {
                foreach (var mnt in mountStatus)
                {
                    mnt.SetFiringTarget(null);
                }
                foreach (var fcs in fireControlSystemStatusRecords)
                {
                    fcs.SetTrackingTarget(null);
                }
                return;
            }

            var shipLog = EntityManager.Instance.GetParent<ShipLog>(this);
            if (shipLog == null)
                return;

            var stats = MeasureStats.Measure(shipLog, target);

            if (!IsMaxDistanceDoctrineRespected(stats.distanceYards))
                return;

            foreach (var fcs in fireControlSystemStatusRecords)
            {
                fcs.SetTrackingTarget(target);
            }

            foreach (var mnt in mountStatus)
            {
                if (mnt.status == MountStatus.Operational &&
                    mnt.GetMountLocationRecordInfo().record.IsInArc(stats.observerToTargetBearingRelativeToBowDeg))
                {
                    // TODO: Check Range? Though effect of range shoul have been handled in the evaluation. 
                    mnt.SetFiringTarget(target);
                }
            }
        }

        void IWTABattery.SetFiringTarget(IWTAObject target) => SetFiringTargetAutomatic(target as ShipLog); // TODO: Support other IWTAObject (land targets?)

        public void ResetFiringTarget()
        {
            foreach (var fcs in fireControlSystemStatusRecords)
            {
                fcs.SetTrackingTarget(null);
                // fcs.
            }

            foreach (var mnt in mountStatus)
            {
                mnt.SetFiringTarget(null);
            }
        }

        IWTAObject IWTABattery.GetCurrentFiringTarget()
        {
            var targetMounts = mountStatus.Where(m => m.GetFiringTarget() != null)
                .GroupBy(m => m.GetFiringTarget())
                .Select(g => (g.Key, g.Count()))
                .ToList();
            if (targetMounts.Count == 0)
                return null;
            var maxCount = targetMounts.Max(r => r.Item2);
            return targetMounts.First(r => r.Item2 == maxCount).Item1;
        }

        public void Step(float deltaSeconds)
        {
            // TODO: Use general SubChildren and active to propagate Step command?

            // TODO: Do actual firing resolution
            foreach (var fcs in fireControlSystemStatusRecords)
                fcs.Step(deltaSeconds);
            foreach (var mnt in mountStatus)
                mnt.Step(deltaSeconds);
        }

        public string DescribeDetail()
        {
            var lines = new List<string>();

            lines.Add($"Battery Detail: {objectId}");

            var logsFlatten = mountStatus.SelectMany(mount => mount.logs).ToList();
            logsFlatten.Sort((log1, log2) => log1.firingTime.CompareTo(log2.firingTime));
            lines.AddRange(logsFlatten.Select(log => log.Summary()));

            return string.Join("\n", lines);
        }

        static Dictionary<AmmunitionType, List<AmmunitionType>> ammunitionTypeFallbackChain = new()
        {
            { AmmunitionType.ArmorPiercing, new() { AmmunitionType.SemiArmorPiercing, AmmunitionType.Common, AmmunitionType.HighExplosive} },
            { AmmunitionType.SemiArmorPiercing, new() { AmmunitionType.Common, AmmunitionType.ArmorPiercing, AmmunitionType.HighExplosive} },
            { AmmunitionType.Common, new() { AmmunitionType.SemiArmorPiercing, AmmunitionType.HighExplosive, AmmunitionType.ArmorPiercing} },
            { AmmunitionType.HighExplosive, new() { AmmunitionType.Common, AmmunitionType.SemiArmorPiercing, AmmunitionType.ArmorPiercing} },
        };

        /// <summary>
        /// Choose "closest" ammunition type which has > 0 capacity.
        /// </summary>
        public AmmunitionType ChooseAmmunitionByPreferredType(AmmunitionType preferAmmu)
        {
            var fallbackChain = ammunitionTypeFallbackChain[preferAmmu];
            return fallbackChain.Prepend(preferAmmu).Where(t => ammunition.GetValue(t) >= 1).DefaultIfEmpty(preferAmmu).First();
        }

        public int GetOverConcentrationCoef()
        {
            return 1; // TODO: Handle barrage fire 
        }
    }

    public class TorpedoSectorStatus
    {
        public int ammunition; // Denotes torpedos in magazine, torpedos that had been loaded in tubes is not counted here.
        public List<TorpedoMountStatusRecord> mountStatus = new();

        public void Step(float deltaSeconds)
        {
            foreach (var mountStatusRecord in mountStatus)
            {
                mountStatusRecord.Step(deltaSeconds);
            }
        }
    }

    public enum RapidFiringBatteryLocation // Location => Side? Though it's binded in UITK so keep it now.
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

                            tgt.damagePoint += damagePoint;
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

    public class DynamicStatus
    {
        public float speedKnotsDirectModifier;
        public int engineRoomHits;
        public int propulsionShaftHits;
        public int boilerRoomHits;

        public void ResetDamageExpenditureState()
        {
            speedKnotsDirectModifier = 1;
            engineRoomHits = 0;
            propulsionShaftHits = 0;
            boilerRoomHits = 0;
        }
    }

    public class SearchLightStatus
    {
        public int portHit;
        public int starboardHit;

        public void ResetDamageExpenditureState()
        {
            portHit = 0;
            starboardHit = 0;
        }
    }

    public enum DamageEffectCode
    {
        Invalid,
        CannotToRight
    }

    public class DamageEffectRecord
    {
        public DamageEffectCode code;
        public float severity;
        public float countdownSeconds;
    }

    public class ShipboardFireStatus
    {
        public string description; // location & start time description
        public float severity;
        public float countdownSeconds;
    }

    public interface IDF3Model
    {
        float GetLatitudeDeg();
        float GetLongitudeDeg();
        float GetHeadingDeg();
    }

    public interface IDF4Model: IDF3Model
    {
        float GetSpeedKnots();
    }

    public enum MapState
    {
        NotDeployed,
        Deployed,
        Destroyed
    }

    public enum ControlMode
    {
        Independent, // Player/Top AI set desired speed, heading and etc to control the ship directly.
        FollowTarget,
        RelativeToTarget,
    }

    public class TorpedoBattery : IWTABattery
    {
        public ShipLog original;

        public float EvaluateFirepowerScore(float distanceYards, TargetAspect targetAspect, float targetSpeedKnots, float bearingRelativeToBowDeg)
        {
            var threat = original.EvaluateTorpedoThreatScore(distanceYards, bearingRelativeToBowDeg);

            return threat * 200;
        }
        public IWTAObject GetCurrentFiringTarget()
        {
            var grouping = original.torpedoSectorStatus.mountStatus
                .Where(m => m.GetFiringTarget() != null)
                .GroupBy(m => m.GetFiringTarget())
                .OrderByDescending(g => g.Count())
                .FirstOrDefault();

            return grouping?.Key;
        }

        public void SetFiringTargetAutomatic(ShipLog target)
        {
            if (target == null)
            {
                foreach (var mnt in original.torpedoSectorStatus.mountStatus)
                    mnt.SetFiringTarget(null);
                return;
            }

            // TODO: Enforce doctrine (torpedo should be handled in specialized doctrine I guess)
            
            var stats = MeasureStats.Measure(original, target);
            var relaxedAngle = original.shipClass.emergencyTurnDegPer2Min / 2;

            foreach (var mnt in original.torpedoSectorStatus.mountStatus)
            {
                if (mnt.status == MountStatus.Operational)
                {
                    var recordInfo = mnt.GetTorpedoMountLocationRecordInfo();

                    if (recordInfo.record.IsInArcRelaxed(stats.observerToTargetBearingRelativeToBowDeg, relaxedAngle))
                    {
                        mnt.SetFiringTarget(target);
                    }
                }
            }
        }

        void IWTABattery.SetFiringTarget(IWTAObject target) => SetFiringTargetAutomatic(target as ShipLog); // TODO: Handle other target

        public void ResetFiringTarget()
        {
            foreach (var mount in original.torpedoSectorStatus.mountStatus)
                mount.SetFiringTarget(null);
        }

        public int GetOverConcentrationCoef() => 0;
    }


    public partial class ShipLog : IObjectIdLabeled, IDF4Model, IShipGroupMember, IWTAObject, IExtrapolable, ICollider
    {
        public string objectId { get; set; }
        // public ShipClass shipClass;
        // public string shipClassObjectId;
        public string namedShipObjectId;
        public NamedShip namedShip
        {
            get => EntityManager.Instance.Get<NamedShip>(namedShipObjectId);
        }
        // public string shipClassStr;
        public ShipClass shipClass
        {
            // get => NavalGameState.Instance.shipClasses.FirstOrDefault(x => x.name.english == shipClassStr);
            // get => EntityManager.Instance.Get<ShipClass>(shipClassObjectId);
            get
            {
                var _shipClassObjectId = namedShip?.shipClassObjectId;
                if (_shipClassObjectId == null)
                    return null;
                return EntityManager.Instance.Get<ShipClass>(_shipClassObjectId);
            }
        }
        public float damagePoint; // current taken damage point vs "max" damage point defined in the class
        public LatLon position = new();
        public float speedKnots; // current speed vs "max" speed defined in the class
        public float headingDeg;
        public float desiredSpeedKnots;
        public float desiredHeadingDeg;
        public MapState mapState;
        // DCR Modifier or DCR modifier type?

        public float GetLatitudeDeg() => position.LatDeg;
        public float GetLongitudeDeg() => position.LonDeg;
        public float GetHeadingDeg() => headingDeg;
        public float GetSpeedKnots() => speedKnots;

        // public void InflictDamagePoint(float damagePointDelta)
        // {
        //     damagePoint += damagePointDelta;
        //     if (damagePoint > shipClass.damagePoint) // TODO: Temp prototyping purpose workaround, in true manner of SK5, the sinking is generally the result of Damage Effect instead of uniform accumulation of DP.
        //     {
        //         mapState = MapState.Destroyed; // TODO: How to handle destroyed but how to represent "remain an obstruction" state?
        //     }
        // }

        public List<BatteryStatus> batteryStatus = new();
        public TorpedoSectorStatus torpedoSectorStatus = new();
        public List<RapidFiringStatus> rapidFiringStatus = new();
        public DynamicStatus dynamicStatus = new();
        public SearchLightStatus searchLightHits = new();
        public int damageControlRatingHits;
        public List<DamageEffectRecord> damageEffectRecords = new();
        public List<ShipboardFireStatus> shipboardFireStatus = new();

        public string parentObjectId { get; set; } // OOB perspective
        // Get Parent / Root Parent method is defined in IShipGroupMember

        public string leaderObjectId;
        public Leader leader
        {
            get => EntityManager.Instance.Get<Leader>(leaderObjectId);
        }

        public bool emergencyRudder;
        public bool assistedDeceleration = true;
        public ControlMode controlMode;
        public string followedTargetObjectId;
        public ShipLog followedTarget
        {
            get => EntityManager.Instance.GetOnMapShipLog(followedTargetObjectId);
        }
        public float followDistanceYards = 500;
        public string relativeTargetObjectId;
        public ShipLog relativeToTarget
        {
            get => EntityManager.Instance.GetOnMapShipLog(relativeTargetObjectId);
        }
        public ControlMode GetEffectiveControlMode()
        {
            if (controlMode == ControlMode.FollowTarget && followedTarget != null)
                return ControlMode.FollowTarget;
            if (controlMode == ControlMode.RelativeToTarget && relativeToTarget != null)
                return ControlMode.RelativeToTarget;
            return ControlMode.Independent;
        }

        public float relativeToTargetDistanceYards = 250;
        public float relativeToTargetAzimuth = 135; // right-after position

        public string GetMemberName() => namedShip.name.mergedName ?? "[Not Speicified]";// name.mergedName;

        public IEnumerable<IObjectIdLabeled> GetSubObjects()
        {
            foreach (var obj in batteryStatus)
            {
                yield return obj;
            }

            foreach (var obj in torpedoSectorStatus.mountStatus)
            {
                yield return obj;
            }

            foreach (var obj in rapidFiringStatus)
            {
                yield return obj;
            }

            yield return doctrine;
        }

        public Doctrine doctrine { get; set; } = new();

        public bool IsOnMap() => mapState == MapState.Deployed;

        public void ResetDamageExpenditureState()
        {
            desiredHeadingDeg = headingDeg;
            desiredSpeedKnots = speedKnots;

            damagePoint = 0;
            // foreach (var batteryStatusRec in batteryStatus)
            // {
            //     batteryStatusRec.
            // }
            var _shipClass = shipClass;
            Utils.SyncListPairLength(_shipClass.batteryRecords, batteryStatus, this);
            foreach (var batteryStatusRec in batteryStatus)
                batteryStatusRec.ResetDamageExpenditureState();

            Utils.SyncListToLength(
                _shipClass.torpedoSector.mountLocationRecords.Sum(r => r.mounts),
                torpedoSectorStatus.mountStatus,
                this
            );
            foreach (var m in torpedoSectorStatus.mountStatus)
                m.ResetDamageExpenditureState();
            torpedoSectorStatus.ammunition = _shipClass.torpedoSector.ammunitionCapacity - torpedoSectorStatus.mountStatus.Sum(m => m.reloadedLoad);

            Utils.SyncListPairLength(_shipClass.rapidFireBatteryRecords, rapidFiringStatus, this);
            foreach (var r in rapidFiringStatus)
                r.ResetDamageExpenditureState();

            dynamicStatus.ResetDamageExpenditureState();
            searchLightHits.ResetDamageExpenditureState();

            damageControlRatingHits = 0;
            damageEffectRecords.Clear();
            shipboardFireStatus.Clear();
        }

        public string Summary()
        {
            var _shipClass = shipClass;
            var lines = new List<string>();

            lines.Add("Battery:");
            lines.AddRange(batteryStatus.Select(bs => bs.Summary()));

            lines.Add("Torpedo:");
            var torpedoBarrels = _shipClass.torpedoSector.mountLocationRecords.Sum(r => r.barrels * r.mounts);
            // var torpedoBarrelsAvailable = torpedoSectorStatus.mountStatus.Where(m => m.status == MountStatus.Operational).Sum(m => (m.torpedoMountLocationRecord.mounts - m.mountsDestroyed) * m.torpedoMountLocationRecord.barrels);
            var torpedoBarrelsAvailable = torpedoSectorStatus.mountStatus.Where(m => m.status == MountStatus.Operational).Sum(m => m.GetTorpedoMountLocationRecordInfo().record.barrels);
            var torpedoAmmu = torpedoSectorStatus.ammunition;
            lines.Add($"x{torpedoBarrelsAvailable}/{torpedoBarrels} {_shipClass.torpedoSector.name.mergedName} ({torpedoAmmu})");

            lines.Add("Rapid Firing Battery:");
            lines.AddRange(rapidFiringStatus.Select(s => s.GetInfo()));

            // lines.Add("DP")
            return string.Join("\n", lines);
        }

        public void StepProcessTurn(float deltaSeconds)
        {
            if (speedKnots >= 4) // Turn and induced speed change
            {
                var useEmergencyRudder = emergencyRudder && speedKnots >= 12;

                var turnCapPer2Min = useEmergencyRudder ? shipClass.emergencyTurnDegPer2Min : shipClass.standardTurnDegPer2Min;
                var turnCapThisPulse = turnCapPer2Min / 120 * deltaSeconds;
                var absDeltaDeg = Math.Min(MeasureUtils.GetPositiveAngleDifference(headingDeg, desiredHeadingDeg), turnCapThisPulse);
                var usePercent = absDeltaDeg / turnCapThisPulse;

                headingDeg = MeasureUtils.MoveAngleTowards(headingDeg, desiredHeadingDeg, turnCapThisPulse);

                var decayPercentPer2Min = (useEmergencyRudder ? 0.5f : 0.75f) * usePercent + 1 * (1 - usePercent);
                var decayPercentThisPulse = (float)Math.Pow(decayPercentPer2Min, deltaSeconds / 120f);
                speedKnots *= decayPercentThisPulse;
            }
        }

        public void StepProcessControl()
        {
            var decelerationKnotsCapPer2Min = shipClass.speedKnots * (assistedDeceleration ? 0.6f : 0.2f);
            var decelerationKnotsCapPerSec = decelerationKnotsCapPer2Min / 120f;

            if (controlMode == ControlMode.FollowTarget)
            {
                if (followedTarget != null)
                {
                    var followedPosition = followedTarget?.position;
                    var inverseLine = Geodesic.WGS84.InverseLine(
                        position.LatDeg, position.LonDeg,
                        followedPosition.LatDeg, followedPosition.LonDeg
                    );
                    var currentFollowedDistM = inverseLine.Distance;
                    var currentFollowedDistYards = currentFollowedDistM * 1.09361f;

                    var expectedDistanceDiffYards = currentFollowedDistYards - followDistanceYards;

                    var extraDistanceYards = 0f;
                    if (speedKnots > followedTarget.speedKnots)
                    {
                        var diffKnots = speedKnots - followedTarget.speedKnots;
                        var decelerationSeconds = diffKnots / decelerationKnotsCapPerSec;
                        var extraDistanceNm = (diffKnots / 3600) * decelerationSeconds / 2;
                        extraDistanceYards = extraDistanceNm * 2025.37f;
                    }

                    if (Math.Abs(expectedDistanceDiffYards) < 20 && Math.Abs(speedKnots - followedTarget.speedKnots) < 1)
                    {
                        desiredSpeedKnots = followedTarget.speedKnots;
                    }
                    else if (expectedDistanceDiffYards > 0)
                    {
                        if (speedKnots < followedTarget.speedKnots)
                        {
                            desiredSpeedKnots = shipClass.speedKnots; // max speed
                        }
                        else
                        {
                            if (currentFollowedDistYards > followDistanceYards + extraDistanceYards)
                            {
                                desiredSpeedKnots = shipClass.speedKnots; // max speed
                            }
                            else
                            {
                                desiredSpeedKnots = followedTarget.speedKnots;
                            }
                        }
                    }
                    else
                    {
                        if (speedKnots < followedTarget.speedKnots)
                        {
                            desiredSpeedKnots = followedTarget.speedKnots * 0.9f;
                        }
                        else if (currentFollowedDistM > extraDistanceYards)
                        {
                            desiredSpeedKnots = followedTarget.speedKnots * 0.8f;
                        }
                        else
                        {
                            desiredSpeedKnots = 0; // Too harsh? But in fact it
                        }
                    }
                    desiredHeadingDeg = MeasureUtils.NormalizeAngle((float)inverseLine.Azimuth);
                }
            }
            else if (controlMode == ControlMode.RelativeToTarget)
            {
                if (relativeToTarget != null)
                {
                    var rtp = relativeToTarget.position;
                    var azi = MeasureUtils.NormalizeAngle(relativeToTarget.headingDeg + relativeToTargetAzimuth);
                    var s12 = relativeToTargetDistanceYards * 0.9144;
                    Geodesic.WGS84.Direct(rtp.LatDeg, rtp.LonDeg, azi, s12, out var targetLat, out var targetLon);

                    var inverseLine = Geodesic.WGS84.InverseLine(
                        position.LatDeg, position.LonDeg,
                        targetLat, targetLon
                    );
                    var distanceToTargetM = inverseLine.Distance;
                    var distanceToTargetYards = distanceToTargetM * 1.09361f;

                    var targetPointIsFrontOfShip = MeasureUtils.GetPositiveAngleDifference((float)inverseLine.Azimuth, headingDeg) < 90;
                    var shipIsFrontOfTarget = MeasureUtils.GetPositiveAngleDifference((float)inverseLine.Azimuth + 180, relativeToTarget.headingDeg) < 90;

                    if (distanceToTargetYards < 20)
                    {
                        desiredSpeedKnots = relativeToTarget.speedKnots;
                        desiredHeadingDeg = relativeToTarget.headingDeg;
                    }
                    else if (shipIsFrontOfTarget && !targetPointIsFrontOfShip) // Wait target to "close"
                    {
                        desiredSpeedKnots = relativeToTarget.speedKnots * 0.75f;
                        desiredHeadingDeg = relativeToTarget.headingDeg;
                    }
                    else // Move to target
                    {
                        desiredHeadingDeg = MeasureUtils.NormalizeAngle((float)inverseLine.Azimuth);

                        if (speedKnots < relativeToTarget.speedKnots)
                        {
                            desiredSpeedKnots = shipClass.speedKnots; // max speed
                        }
                        else
                        {
                            var diffKnots = speedKnots - relativeToTarget.speedKnots;
                            var decelerationSeconds = diffKnots / decelerationKnotsCapPerSec;
                            var extraDistanceNm = (diffKnots / 3600) * decelerationSeconds / 2;
                            var extraDistanceYards = extraDistanceNm * 2025.37f;
                            if (distanceToTargetYards > extraDistanceYards)
                            {
                                desiredSpeedKnots = shipClass.speedKnots; // max speed
                            }
                            else
                            {
                                desiredSpeedKnots = relativeToTarget.speedKnots * 0.8f;
                            }
                        }
                    }
                }
            }
        }

        public void StepProcessSpeed(float deltaSeconds)
        {
            if (desiredSpeedKnots > speedKnots)
            {
                var accelerationKnotsCapPer2Min = shipClass.speedIncreaseRecord.Where(r => speedKnots >= r.thresholdSpeedKnots).Select(r => r.increaseSpeedKnots).Min();
                var accelerationKnotsCapPerSec = accelerationKnotsCapPer2Min / 120;

                var accelerationKnotsCapThisPulse = accelerationKnotsCapPerSec * deltaSeconds;
                speedKnots += Math.Min(desiredSpeedKnots - speedKnots, accelerationKnotsCapThisPulse);
            }
            else if (desiredSpeedKnots < speedKnots)
            {
                var decelerationKnotsCapPer2Min = shipClass.speedKnots * (assistedDeceleration ? 0.6f : 0.2f);
                var decelerationKnotsCapPerSec = decelerationKnotsCapPer2Min / 120f;

                var decelerationKnotsCapThisPulse = decelerationKnotsCapPerSec * deltaSeconds;
                speedKnots -= Math.Min(speedKnots - desiredSpeedKnots, decelerationKnotsCapThisPulse);
            }
        }

        public void StepTryMoveToNewPosition(float deltaSeconds)
        {
            var distNm = speedKnots / 3600 * deltaSeconds;
            var distM = distNm * 1852;
            double arcLength = Geodesic.WGS84.Direct(position.LatDeg, position.LonDeg, headingDeg, distM, out double lat2, out double lon2);

            var newPosition = new LatLon((float)lat2, (float)lon2);

            var newPositionBlocked = false;
            if (CoreParameter.Instance.checkLandCollision)
            {
                newPositionBlocked = ElevationService.Instance.GetElevation(newPosition) > 0;
            }
            if (!newPositionBlocked && CoreParameter.Instance.checkShipCollision)
            {
                ShipLog collided = null; // "Collider" check
                foreach (var other in NavalGameState.Instance.shipLogsOnMap)
                {
                    if (other == this)
                        continue;
                    var isCollided = CollideUtils.IsCollided(newPosition, this, other);
                    if (isCollided)
                    {
                        collided = other;
                    }
                    // var otherPos = other.position;

                    // // Exact Method
                    // // Geodesic.WGS84.Inverse(newPosition.LatDeg, newPosition.LonDeg, otherPos.LatDeg, otherPos.LonDeg, out var distanceM, out var azi1, out var azi2);
                    // // Approximation Method
                    // var (distanceKm, azi1) = MeasureStats.Approximation.CalculateDistanceKmAndBearingDeg(newPosition.LatDeg, newPosition.LonDeg, otherPos.LatDeg, otherPos.LonDeg);
                    // var distanceM = distanceKm * 1000;
                    // var azi2 = azi1;

                    // if (MeasureUtils.GetPositiveAngleDifference(headingDeg, (float)azi1) > 90)
                    //     continue;

                    // var distanceFoot = distanceM * MeasureUtils.meterToFoot;
                    // var lengthFoot = shipClass.lengthFoot;
                    // var otherLengthFoot = other.shipClass.lengthFoot;
                    // if (distanceFoot < lengthFoot / 2 + otherLengthFoot / 2)
                    // {
                    //     var diff = MeasureUtils.GetPositiveAngleDifference(other.headingDeg, (float)azi2);
                    //     var coef = Math.Abs(diff - 90) / 90;
                    //     var otherMix = otherLengthFoot * coef + other.shipClass.beamFoot * (1 - coef);
                    //     if (distanceFoot < lengthFoot / 2 + otherMix / 2)
                    //     {
                    //         collided = other;
                    //         break;
                    //     }
                    // }
                }
                newPositionBlocked = collided != null; // TODO: Handle deliberately hostile ramming and speed change
            }

            if (!newPositionBlocked)
            {
                position = newPosition;
            }
            else
            {
                speedKnots = 0; // TODO: Use a smoother method
            }
        }

        public void StepTorpedoSector(float deltaSeconds)
        {
            torpedoSectorStatus.Step(deltaSeconds);
        }

        public void StepBatteryStatus(float deltaSeconds)
        {
            foreach (var bs in batteryStatus)
            {
                bs.Step(deltaSeconds);
            }

            foreach (var rf in rapidFiringStatus)
            {
                rf.Step(deltaSeconds);
            }
        }

        public void StepDamageResolution(float deltaSeconds)
        {
            if (damagePoint > shipClass.damagePoint) // TODO: Temp workaround, this will be replaced with DE based implementation. 
            {
                mapState = MapState.Destroyed;
                var logger = ServiceLocator.Get<ILoggerService>();
                logger.LogWarning($"{namedShip.name.GetMergedName()} ({objectId}) is destroyed");
            }
        }

        // public void Step(float deltaSeconds)
        // {
        //     StepProcessTurn(deltaSeconds);

        //     // var accelerationKnotsCapPer2Min = shipClass.speedIncreaseRecord.Where(r => speedKnots >= r.thresholdSpeedKnots).Select(r => r.increaseSpeedKnots).Min();
        //     // var accelerationKnotsCapPerSec = accelerationKnotsCapPer2Min / 120;
        //     // var decelerationKnotsCapPer2Min = shipClass.speedKnots * (assistedDeceleration ? 0.6f : 0.2f);
        //     // var decelerationKnotsCapPerSec = decelerationKnotsCapPer2Min / 120f;

        //     StepProcessControl();

        //     StepProcessSpeed(deltaSeconds);

        //     StepTryMoveToNewPosition(deltaSeconds);

        //     StepSubObjects(deltaSeconds);
        // }

        public float EvaluateArmorScore()
        {
            return EvaluateArmorScore(TargetAspect.Broad, RangeBand.Short);
        }

        public float EvaluateArmorScore(TargetAspect targetAspect, RangeBand rangeBand)
        {
            return shipClass.EvaluateArmorScore(targetAspect, rangeBand);
        }

        public float EvaluateSurvivability()
        {
            var armorScoreSmoothed = 1 + EvaluateArmorScore();
            var dp = shipClass.damagePoint - damagePoint;
            return dp * armorScoreSmoothed;
        }

        public float EvaluateBatteryFirepowerScore()
        {
            return batteryStatus.Sum(bs => bs.EvaluateFirepowerScore());
        }

        public float EvaluateBatteryFirepowerScore(float distanceYards, TargetAspect targetAspect, float targetSpeedKnots, float bearingRelativeToBowDeg)
        {
            return batteryStatus.Sum(bs => bs.EvaluateFirepowerScore(distanceYards, targetAspect, targetSpeedKnots, bearingRelativeToBowDeg));
        }

        public float EvaluateTorpedoThreatScore()
        {
            // var torpedoBarrelsAvailable = torpedoSectorStatus.mountStatus.Where(m => m.status == MountStatus.Operational).Sum(m => (m.torpedoMountLocationRecord.mounts - m.mountsDestroyed) * m.torpedoMountLocationRecord.barrels);
            var torpedoBarrelsAvailable = torpedoSectorStatus.mountStatus.Where(m => m.status == MountStatus.Operational).Sum(m => m.GetTorpedoMountLocationRecordInfo().record.barrels);
            return torpedoBarrelsAvailable * shipClass.torpedoSector.EvaluateTorpedoThreatPerBarrel();
        }

        public float EvaluateTorpedoThreatScore(float distanceYards, float bearingRelativeToBowDeg)
        {
            var sc = shipClass;
            var classSector = sc.torpedoSector;
            // AngleDifferenceFromArc
            var setting = classSector.torpedoSettings.LastOrDefault();
            if (setting == null)
                return 0;

            var rangeCoef = 1 - distanceYards / (100 + setting.rangeYards * CoreParameter.Instance.automaticTorpedoFiringRangeRelaxedCoef);
            if (rangeCoef <= 0)
                return 0;

            var effBarrels = torpedoSectorStatus.mountStatus
                .Where(m => m.status == MountStatus.Operational && m.currentLoad > 0)
                .Select(m => (m, m.GetTorpedoMountLocationRecordInfo().record))
                .Where(p => p.Item2.IsInArcRelaxed(bearingRelativeToBowDeg, sc.emergencyTurnDegPer2Min / 2))
                .Sum(
                    p =>
                        Math.Min(p.m.currentLoad, p.Item2.barrels) *
                        (1 - p.Item2.AngleDifferenceFromArc(bearingRelativeToBowDeg) / 360)
                );
            return rangeCoef * effBarrels * classSector.EvaluateTorpedoThreatPerBarrel();
        }

        public float EvaluateRapidFiringFirepowerScore()
        {
            return rapidFiringStatus.Sum(rf => rf.EvaluateFirepowerScore());
        }

        public float EvaluateFirepowerScore()
        {
            var batteryFirepower = EvaluateBatteryFirepowerScore();
            // Torpedo is not handled here
            var torpedoThreat = EvaluateTorpedoThreatScore();
            var rapidFiringFirepower = EvaluateRapidFiringFirepowerScore();

            return 1f * batteryFirepower + 1f * torpedoThreat + 1f * rapidFiringFirepower;
        }

        public float EvaluateGeneralScore()
        {
            var armorScore = EvaluateArmorScore();
            var firepowerScore = EvaluateFirepowerScore();
            return 1f * armorScore + 1f * firepowerScore;
        }

        public HashSet<ShipLog> GetFiringToTargets()
        {
            var targets = new HashSet<ShipLog>();
            foreach (var bs in batteryStatus)
            {
                foreach (var mnt in bs.mountStatus)
                {
                    var target = mnt.GetFiringTarget();
                    if (target != null)
                    {
                        targets.Add(mnt.GetFiringTarget());
                    }
                }
            }
            // TODO: Add torpedos?
            foreach (var rfs in rapidFiringStatus)
            {
                foreach (var r in rfs.targettingRecords)
                {
                    var tgt = r.GetTarget();
                    if (tgt != null)
                    {
                        targets.Add(tgt);
                    }
                }
            }
            return targets;
        }

        public IEnumerable<IWTABattery> GetBatteries()
        {
            foreach (var bs in batteryStatus)
                yield return bs;

            foreach (var rf in rapidFiringStatus)
            {
                foreach (var b in rf.GetSideBatteries())
                    yield return b;
            }

            if (shipClass.torpedoSector.torpedoSettings.Count > 0)
            {
                yield return new TorpedoBattery()
                {
                    original = this
                };
            } 
        }

        public float EvaluateBowFirepowerScore() => EvaluateBatteryFirepowerScore(0, TargetAspect.Broad, 0, 0);
        public float EvaluateStarboardFirepowerScore() => EvaluateBatteryFirepowerScore(0, TargetAspect.Broad, 0, 90);
        public float EvaluateSternFirepowerScore() => EvaluateBatteryFirepowerScore(0, TargetAspect.Broad, 0, 180);
        public float EvaluatePortFirepowerScore() => EvaluateBatteryFirepowerScore(0, TargetAspect.Broad, 0, 270);
        public ControlMode GetControlMode() => controlMode;
        IExtrapolable IExtrapolable.GetFollowedTarget() => followedTarget;
        float IExtrapolable.GetFollowDistanceYards() => followDistanceYards;
        IExtrapolable IExtrapolable.GetRelativeToTarget() => relativeToTarget;
        float IExtrapolable.GetRelativeToTargetDistanceYards() => relativeToTargetDistanceYards;
        float IExtrapolable.GetRelativeToTargetAzimuth() => relativeToTargetAzimuth;
        void IExtrapolable.SetDesiredHeadingDeg(float desiredHeadingDeg) => this.desiredHeadingDeg = desiredHeadingDeg;

        public LatLon GetPosition() => position;
        // public float GetHeadingDeg() => headingDeg;
        public float GetLengthFoot() => shipClass.lengthFoot;
        public float GetBeamFoot() => shipClass.beamFoot;

    }
}