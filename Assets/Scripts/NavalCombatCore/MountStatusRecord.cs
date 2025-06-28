using System.Collections.Generic;
using System.Data;
using System.Linq;
using System;
using GeographicLib;
using System.Xml.Serialization;


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

        public void CostPercent(float percent)
        {
            ArmorPiercing -= (int)Math.Ceiling(ArmorPiercing * percent);
            semiArmorPiercing -= (int)Math.Ceiling(semiArmorPiercing * percent);
            common -= (int)Math.Ceiling(common * percent);
            highExplosive -= (int)Math.Ceiling(highExplosive * percent);
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

        [XmlAttribute]
        public ArmorLocation armorLocation;

        [XmlAttribute]
        public HitPenDetType hitPenDetType;

        public RuleChart.ShellDamageResult shellDamageResult;

        [XmlAttribute]
        public string damageEffectId;

        public string Summary()
        {
            var target = GetFiringTarget();
            var targetName = target.namedShip?.name?.GetMergedName();
            var ammoType = BatteryAmmunitionRecord.ammunitionTypeAcronymMap[ammunitionType];
            var hitDesc = hit ? $"hit {armorLocation} -> {hitPenDetType} -> {shellDamageResult}" : "miss";
            return $"{firingTime} {ammoType} -> {targetName}, {distanceYards} yards, P={hitProb * 100}%, {hitDesc} {damageEffectId}";
        }

        public override string ToString() => Summary();
    }

    public abstract partial class AbstractMountStatusRecord : UnitModule
    {
        // public string objectId { get; set; }
        public MountStatus status;
        public int barrels;

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

        // public override IEnumerable<IObjectIdLabeled> GetSubObjects()
        // {
        //     yield break;
        // }

        public MountStatus GetModifiedStatus()
        {
            var s = status;
            foreach (var ms in GetSubStates<ITorpedoMountStatusModifier>().Select(m => m.GetTorpedoMountStatus()))
            {
                s = DamageEffectChart.MaxEnum(s, ms);
            }
            return s;
        }

        public bool IsOperational()
        {
            return GetModifiedStatus() == MountStatus.Operational;
        }

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
            barrels = info.barrels;
            currentLoad = barrels;
            reloadingSeconds = 0;
            reloadedLoad = barrels;
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

            var requested = barrels - currentLoad;
            var ammunitionCap = platform.torpedoSectorStatus.ammunition;
            var reloadLimitCap = recordInfo.record.reloadLimit == 0 ? int.MaxValue : recordInfo.record.reloadLimit - reloadedLoad;
            var transferred = Math.Min(reloadLimitCap, Math.Min(requested, ammunitionCap));

            if (transferred > 0)
            {
                reloadingSeconds += deltaSeconds;


                while (reloadingSeconds >= 360 && transferred > 0) // 6min torpedo reload time (SK5 & DoB)
                {
                    requested = barrels - currentLoad;
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

        // public override IEnumerable<IObjectIdLabeled> GetSubObjects()
        // {
        //     yield break;
        // }

        public MountStatus GetModifiedStatus()
        {
            var s = status;
            foreach (var ms in GetSubStates<IBatteryMountStatusModifier>().Select(m => m.GetBatteryMountStatus()))
            {
                s = DamageEffectChart.MaxEnum(s, ms);
            }
            return s;
        }

        public bool IsOperational()
        {
            return GetModifiedStatus() == MountStatus.Operational;
        }

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
                    )) // Re-check will be required in the following code
                    return;

                var shooter = ctx.shipLog;

                var targetSup = fireCtx.shipLogSupplementaryMap[tgt];
                var isFireChecked = targetSup.batteriesFiredAtMe.Contains(ctx.batteryStatus);
                if (!isFireChecked) // Include range / arc / Doctrine check (though ammo should be checked dynamiclly since this loop will update ammo state)
                    return;
                var shooterTargetSup = fireCtx.GetOrCalcualteShipLogPairSupplementary(shooter, tgt);
                var stats = shooterTargetSup.stats;

                var penRecord = ctx.batteryRecord.penetrationTableRecords.FirstOrDefault(r => stats.distanceYards <= r.distanceYards);
                if (penRecord == null)
                    return;

                // Rate of Fire Resolution

                var shootsPer2MinBase = penRecord.rateOfFire;
                var shootsPer2Min = shootsPer2MinBase * GetSubStates<IRateOfFireModifier>().Select(m => m.GetRateOfFireCoef()).DefaultIfEmpty(1).Min();

                if (shootsPer2Min == 0)
                    return;
                var secondsPerShoot = 120 / shootsPer2Min;

                var maskCheckResult = shooterTargetSup.GetOrCalcualteMaskCheckResult();
                if (maskCheckResult.isMasked)
                {
                    secondsPerShoot *= 2; // ROF / 2 if masked
                }

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

                    // Fire Control Value Resolution

                    var fireControlRow = ctx.batteryRecord.fireControlTableRecords.FirstOrDefault(r => tgt.speedKnots <= r.speedThresholdKnot);
                    if (fireControlRow == null)
                        return;

                    var fireControlScoreRaw = fireControlRow.GetValue(penRecord.rangeBand, stats.targetPresentAspectFromObserver);

                    // Positive Modifier

                    if (stats.distanceYards <= 4500)
                    {
                        var closeRangeFireControlScore = RuleChart.GetCloseRangeFireControlScore(stats.distanceYards, tgt.speedKnots, stats.targetPresentAspectFromObserver);
                        fireControlScoreRaw = Math.Max(fireControlScoreRaw, closeRangeFireControlScore);
                    }

                    // Negative Modifiers

                    var fireControlValueModifiers = GetSubStates<IFireControlValueModifier>().ToList();
                    var fireControlValueModifierOffset = fireControlValueModifiers.Select(m => m.GetFireControlValueOffset()).Sum();

                    // var mountLocation = ctx.mountLocationRecord.mountLocation;
                    fireControlValueModifierOffset += GetSubStates<ILocalizedDirectionalFireControlValueModifier>().Select(
                        m => m.GetFireControlValueOffset(ctx.mountLocationRecord.mountLocation, stats.observerToTargetBearingRelativeToBowDeg)
                    ).DefaultIfEmpty(0).Min();

                    var fireControlValueModifierCoef = fireControlValueModifiers.Select(m => m.GetFireControlValueCoef()).DefaultIfEmpty(1).Min();
                    fireControlScoreRaw = Math.Max((fireControlScoreRaw + fireControlValueModifierOffset) * fireControlValueModifierCoef, 0);

                    // var firedAtTargetBatteriesCount = targetSup.batteriesFiredAtMe.Count; // over-concentration

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

                    // TODO: Move to precalculate context?
                    var sunState = NavalGameState.Instance.scenarioState.GetSunPosition(ctx.shipLog.position);
                    var sunLevel = sunState.GetDayNightLevel();

                    // TODO: Handle Additional for dawn/dusk condition
                    // Target silhouetted by horizon: +1
                    // Target in darkness: -2
                    // None of above: +0

                    // Handle Additional for night conditions
                    // No moonlight: -4
                    // Moonlight: -2

                    if (sunLevel == DayNightLevel.Night)
                    {
                        var moonlightOffset = NavalGameState.Instance.scenarioState.hasMoonlight ? -2 : -4;
                        fireControlScore += moonlightOffset;
                    }

                    // TODO: Handle Additional for illumination (1b or 1c)
                    // Target afire or illuminated by searchlight: +2
                    // Target using searchlight OR is illuminated: +1

                    // TODO: Blind Fire
                    // Firing ship is using Blind Fire (target cannot be seen): -5

                    // TODO: Smoke (cumulative and does not apply to Blind Fire using Radar)
                    // Target obscured by battle smoke: -1
                    // Target obscured by funnel smokescreen: -3

                    // Evasive Action / Emergency Turn
                    // Target only in EA: -3
                    // Firing ship only in EA: -2
                    // Target and firing ships in EA: -8

                    var firingShipEA = ctx.shipLog.IsEvasiveManeuvering();
                    var targetShipEA = tgt.IsEvasiveManeuvering();
                    
                    if (firingShipEA && targetShipEA)
                        fireControlScore -= 8;
                    else if (targetShipEA)
                        fireControlScore -= 3;
                    else if (firingShipEA)
                        fireControlScore -= 2;

                    // Target Acquisition
                    // Firing on different ship from last turn: -2
                    // Target ship hit by firing ship last turn: +2

                    var trackingStates = ctx.batteryStatus.fireControlSystemStatusRecords.Where(
                        fcs => fcs.IsOperational() && fcs.targetObjectId == tgt.objectId
                    ).Select(fcs => fcs.trackingState).ToList();

                    if (trackingStates.Count == 0)
                    {
                        fireControlScore /= 2; // No FCS is tracking target => Local Control: /2 FCS (DoB: -5)
                        // Well, so Local Control may be better than BeginTracking according to Rulebook.
                    }
                    else
                    {
                        if (trackingStates.Contains(TrackingSystemState.Hitting))
                        {
                            fireControlScore += 2;
                        }
                        else if (trackingStates.Contains(TrackingSystemState.Tracking))
                        {
                            fireControlScore += 0;
                        }
                        else if (trackingStates.Contains(TrackingSystemState.BeginTracking))
                        {
                            fireControlScore -= 2;
                        }
                    }

                    // Firing ship under fire
                    // Under fire from 3 or more ships during this turn: -2

                    var meShipLogSup = fireCtx.shipLogSupplementaryMap[ctx.shipLog];
                    if (meShipLogSup.shipLogsFiredAtMe.Count >= 3)
                    {
                        fireControlScore -= 2;
                    }

                    // Over-Concentration & Barrage
                    // 1 ship firing at target with 1 battery: 0
                    // For each additional primary, secondary or teriary battery of any ship firing at same target: -1
                    // For every primary, secondary or tertiary battery of any ship using barrage fire at same target: -2

                    fireControlScore += Math.Min(0, -(targetSup.batteriesFiredAtMe.Count - 1));

                    // Size of target ship
                    // TS (from Ship Log of target ship)
                    fireControlScore += tgt.shipClass.targetSizeModifier;

                    // Pending: Spotter Aircraft
                    // Spotter aircraft (target visible from firing ship): +2

                    // Battle factor
                    // Sea State + Crew Rating (from Ship Log)
                    var seaStateOffset = RuleChart.ResolveSeaStateOffset(
                        ctx.shipClass.displacementTons,
                        NavalGameState.Instance.scenarioState.seaStateBeaufort,
                        out bool blocked
                    );
                    fireControlScore += seaStateOffset; // Use -100 to soft block
                    fireControlScore += ctx.shipLog.namedShip.crewRating;

                    // Fire Control Radar Modifier

                    if (!GetSubStates<IElectronicSystemModifier>().Any(m => m.IsFireControlRadarDisabled()))
                    {
                        var fireControlRadarModifier = ctx.batteryRecord.fireControlRadarModifier;
                        fireControlScore += fireControlRadarModifier;
                    }

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
                    logs.Add(logRecord); // logRecord could be modified in the following code

                    if (hit)
                    {
                        var armorLocation = RuleChart.RollArmorLocation(stats.targetPresentAspectFromObserver, penRecord.rangeBand);
                        if (armorLocation != ArmorLocation.Ineffective)
                        {
                            var armorLocationAngleType = RuleChart.armorLocationToAngleType.GetValueOrDefault(armorLocation);
                            var refPenInch = penRecord.GetValue(armorLocationAngleType);
                            var penInch = RuleChart.GetAdjustedPenetrationByType(ctx.batteryRecord.penetrationTableBaseType, refPenInch, ctx.batteryRecord.shellSizeInch, ammunitionType);

                            var armorEffInch = tgt.shipClass.armorRating.GetArmorEffectiveInch(armorLocation);

                            if (armorLocation == ArmorLocation.MainBelt)
                            {
                                var armorCoef = tgt.GetSubStates<IArmorModifier>().Select(m => m.GetMainBeltArmorCoef()).DefaultIfEmpty(1).Min();
                                armorEffInch *= armorCoef;
                            }

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
                            var tgtLog = new ShipLogBatteryHitLog()
                            {
                                time = NavalGameState.Instance.scenarioState.dateTime
                            };

                            tgtLog.hitPenDetType = logRecord.hitPenDetType = hitPenDetType;
                            tgtLog.armorLocation = logRecord.armorLocation = armorLocation;
                            logRecord.shellDamageResult = shellDamageResult;
                            tgtLog.damagePoint = shellDamageResult.damagePoint;
                            tgt.logs.Add(tgtLog);

                            // TODO: Handle damage effect and general (DP caused) damage effect.
                            // tgt.damagePoint += shellDamageResult.damagePoint;
                            tgt.AddDamagePoint(shellDamageResult.damagePoint);

                            string damageEffectId = null;
                            // Process Damage Effect
                            if (RandomUtils.NextFloat() <= shellDamageResult.damageEffectProb)
                            {
                                var damageEffectCause = armorLocation switch
                                {
                                    ArmorLocation.Deck => DamageEffectCause.Deck,
                                    ArmorLocation.TurretHorizontal => DamageEffectCause.Turret,
                                    ArmorLocation.SuperStructureHorizontal => DamageEffectCause.Superstrucure,
                                    ArmorLocation.ConningTower => DamageEffectCause.ConningTower,
                                    ArmorLocation.MainBelt => DamageEffectCause.MainBelt,
                                    ArmorLocation.BeltEnd => DamageEffectCause.BeltEnd,
                                    ArmorLocation.Barbette => DamageEffectCause.Barbette,
                                    ArmorLocation.TurretVertical => DamageEffectCause.Turret,
                                    ArmorLocation.SuperStructureVertical => DamageEffectCause.Superstrucure,
                                    _ => DamageEffectCause.MainBelt
                                };
                                var damageEffectContext = new DamageEffectContext()
                                {
                                    subject = tgt,
                                    baseDamagePoint = shellDamageResult.damagePoint,
                                    cause = damageEffectCause,
                                    hitPenDetType = hitPenDetType,
                                    ammunitionType = ammunitionType,
                                    shellDiameterInch = ctx.batteryRecord.shellSizeInch,
                                    addtionalDamageEffectProbility = shellDamageResult.damageEffectProb
                                };

                                damageEffectId = DamageEffectChart.AddNewDamageEffect(damageEffectContext);

                                tgtLog.damageEffectId = logRecord.damageEffectId = damageEffectId;
                            }

                            var logger = ServiceLocator.Get<ILoggerService>();
                            logger.Log($"{ctx.shipLog.namedShip.name.GetMergedName()} {ctx.batteryRecord.name.GetMergedName()} -> {tgt.namedShip.name.GetMergedName()} ({logRecord.Summary()}) (DE: {damageEffectId})");
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
            barrels = GetMountLocationRecordInfo().record.barrels;

            logs.Clear();
        }
    }
}