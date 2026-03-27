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
    public partial class BatteryAmmunitionRecord
    {
        public int ArmorPiercing;
        public int semiArmorPiercing;
        public int common;
        public int highExplosive;

        public override string ToString()
        {
            return $"BatteryAmmunitionRecord({ArmorPiercing}/{semiArmorPiercing}/{common}/{highExplosive})";
        }

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

        public bool IsEmpty() => GetTotalValue() == 0;
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

        // Follows are valid only if hit == true

        [XmlAttribute]
        public DamageSchema DamageSchema;

        public bool ShouldSerializeDamageSchema() => hit;

        [XmlAttribute]
        public ArmorLocation ArmorLocation; // Valid only for DamageSchema.Warship

        public bool ShouldSerializeArmorLocation() => hit && (DamageSchema == DamageSchema.Warship || DamageSchema == DamageSchema.LandBattery);

        [XmlAttribute]
        public HitLocationMerchantVessel HitLocationMerchantVessel; // Valid only for DamageSchema.MerchantVessel

        public bool ShouldSerializeHitLocationMerchantVessel() => hit && DamageSchema == DamageSchema.MerchantVessal;

        [XmlAttribute]
        public HitPenDetType HitPenDetType;

        public bool ShouldSerializeHitPenDetType() => hit;

        public RuleChart.ShellDamageResult ShellDamageResult;

        public bool ShouldSerializeShellDamageResult() => hit;

        [XmlAttribute]
        public string DamageEffectId;

        public bool ShouldSerializeDamageEffectId() => hit;


        protected static string Localize(string key, params object[] args) => ServiceLocator.Get<ILocalizeService>().Get(key, args);
        protected static string LocalizeEnum<T>(T obj) => ServiceLocator.Get<ILocalizeService>().GetEnum(obj);

        public string Summary()
        {
            var target = GetFiringTarget();
            var targetName = target?.namedShip?.name?.GetMergedName();

            // return Localize(
            //     "(DP={0}, Prob of DE={1})",
            //     damagePoint,
            //     damageEffectProb
            // );

            var ammoType = BatteryAmmunitionRecord.ammunitionTypeAcronymMap[ammunitionType];
            // var hitDesc = hit ? $"hit {armorLocation} -> {hitPenDetType} -> {shellDamageResult}" : "miss";
            var locStr = DamageSchema switch
            {
                DamageSchema.Warship => LocalizeEnum(ArmorLocation),
                DamageSchema.LandBattery => LocalizeEnum(ArmorLocation),
                DamageSchema.MerchantVessal => LocalizeEnum(HitLocationMerchantVessel),
                _ => throw new NotImplementedException()
            };
            
            var hitDesc = hit ? Localize(
                "hit {0} -> {1} -> (DP={2}, Prob of DE={3})",
                locStr,
                LocalizeEnum(HitPenDetType),
                ShellDamageResult.damagePoint,
                ShellDamageResult.damageEffectProb
            ) : Localize("miss");

            var firingTimeStr = CoreParameter.Instance.GetReferenceTimeZoneDateTimeOffsetString(firingTime);
            // return $"{firingTimeStr} {ammoType} -> {targetName}, {distanceYards} yards, Prob of Hit={hitProb * 100}%, {hitDesc} {damageEffectId}";
            return Localize(
                "{0} {1} -> {2}, {3} yards, Prob of Hit={4}% {5} {6}",
                firingTimeStr,
                ammoType,
                targetName,
                distanceYards,
                hitProb * 100,
                hitDesc,
                DamageEffectId
            );
        }

        public override string ToString() => Summary();
    }

    // public abstract partial class AbstractMountStatusRecord : UnitModule
    public partial class AbstractMountStatusRecord : UnitModule
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

        public virtual void ResetTargetting()
        {
            firingTargetObjectId = null;
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

        // public override void ResetTargetting()
        // {
        //     base.ResetTargetting();
        // }

        public void ResetDamageExpenditureState()
        {
            ResetTargetting();

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

        public static float torpedoFiringAngleErrorDeg = 0f; // solver currently has internal error so don't introduce more error
        // public static float torpedoFiringAngleErrorDeg = 0.1f;
        // public static float torpedoFiringRangeCoef = 0.95f;

        public static bool disableTorpedoReload = false; // TODO: Add manual torpedo firing like JTS 
        // Explicitly assign aw, submerged, deck torpedo. Of course this can be done by reloading limit as well.

        public void Step(float deltaSeconds)
        {
            // reload
            var platform = EntityManager.Instance.GetParent<ShipLog>(this);
            var recordInfo = GetTorpedoMountLocationRecordInfo();

            var requested = barrels - currentLoad;
            var ammunitionCap = platform.torpedoSectorStatus.ammunition;

            // var reloadLimitCap = recordInfo.record.reloadLimit == 0 ? int.MaxValue : recordInfo.record.reloadLimit - reloadedLoad;
            int reloadLimitCap;
            if(disableTorpedoReload)
            {
                reloadLimitCap = 0;
            }
            else
            {
                reloadLimitCap = recordInfo.record.reloadLimit == 0 ? int.MaxValue : recordInfo.record.reloadLimit - reloadedLoad;
            }

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

            var classSector = platform.shipClass.torpedoSector;

            if (tgt != null && currentLoad > 0 && classSector.torpedoSettings.Count > 0)
            {
                var (distanceKm, azi1) = MeasureStats.Approximation.CalculateDistanceKmAndBearingDeg(platform.position.LatDeg, platform.position.LonDeg, tgt.position.LatDeg, tgt.position.LonDeg);
                var distYards = (float)distanceKm * 1000 * MeasureUtils.meterToYard;
                var doctrineRespected = platform.doctrine.GetMaximumFiringDistanceYardsForTorpedo().IsGreaterThanIfSpecified(distYards);
                if (doctrineRespected)
                {
                    // TODO: Replace gunnery LOS with another LOS using bigger threat volume
                    
                    // var gunneryFireCtx = GunneryFireContext.GetCurrentOrCreateTemp();
                    // var losMaskResult = gunneryFireCtx.GetOrCalcualteShipLogPairSupplementary(platform, tgt).GetOrCalcualteMaskCheckResult(); // Also used by prevent friendly collsion beforehand
                    
                    // if(!losMaskResult.isMasked)
                    // {

                    var torpedoAttackCtx = TorpedoAttackContext.GetCurrentOrCreateTemp();

                    var settingPairs = classSector.torpedoSettings.Select(setting => (
                        setting,
                        torpedoAttackCtx.GetOrCalculateFireComplexSupplementary(platform, tgt, setting.speedKnots).interceptionPointSolverResult
                    )).Where(sp => sp.Item2.success && sp.Item2.distanceYards < sp.setting.rangeYards).ToList();
                    if (settingPairs.Count > 0)
                    {
                        var minInterceptionDistYard = settingPairs.Min(sp => sp.Item2.distanceYards);
                        var bestSettingPair = settingPairs.First(sp => sp.Item2.distanceYards == minInterceptionDistYard);
                        var setting = bestSettingPair.setting;
                        var interceptionRes = bestSettingPair.Item2;

                        var bearingRelativeToBowDeg = MeasureUtils.NormalizeAngle(interceptionRes.azimuth - platform.headingDeg);
                        var firingAngle = MeasureUtils.NormalizeAngle(interceptionRes.azimuth + RandomUtils.NextFloat(-torpedoFiringAngleErrorDeg, torpedoFiringAngleErrorDeg));
                        if (recordInfo.record.IsInArc(bearingRelativeToBowDeg))
                        {
                            // Launch Torpedo!
                            var newTorpedo = new LaunchedTorpedo()
                            {
                                sourceName = classSector.name.Clone(),
                                damageClass = classSector.damageClass,
                                headingDeg = firingAngle,
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

                            platform.firingTorpedos += 1;
                        }
                    }

                    // }
                }
            }
        }
    }

    public partial class MountStatusRecord : AbstractMountStatusRecord // Battery
    {
        public float processSeconds;
        public AmmunitionType ammunitionType;

        public List<MountFiringRecord> logs = new();

        // public override IEnumerable<IObjectIdLabeled> GetSubObjects()
        // {
        //     yield break;
        // }

        // public void ClearLogs() => logs.Clear();

        public override void ResetTargetting()
        {
            base.ResetTargetting();

            processSeconds = 0;
        }

        public void TrimMissHitLogs()
        {
            logs.RemoveAll(x => !x.hit);
        }

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

        public enum BreakdownBlockedReason
        {
            None,
            MountNotOperational,
            NoTarget,
            ContextNotResolved,
            TargetSupplementaryMissing,
            OutOfRange,
            OutOfArc,
            DoctrineBlocked,
            AmmunitionUnavailable,
            FireCheckFailed,
            PenetrationTableMissing,
            FireControlTableMissing
        }

        public class HitModifierLine
        {
            public string label;
            public float value;
            public bool isMultiplier;
        }

        public class HitProbabilityBreakdown
        {
            public bool canFire;
            public BreakdownBlockedReason blockedReason;

            public string targetObjectId;
            public string targetName;

            public bool hasMeasurement;
            public float distanceYards;
            public float bearingDeg;
            public float? targetSpeedKnots;
            public RangeBand? rangeBand;
            public TargetAspect? targetAspect;

            public float baseFireControlValue;
            public float closeRangeOverrideDelta;
            public float mountSubstateOffset;
            public float mountSubstateCoef = 1f;
            public float visibilityOffset;
            public float dawnDuskOffset;
            public float nightMoonlightOffset;
            public float evasiveActionOffset;
            public float trackingOffset;
            public bool trackingLocalControl;
            public float underFireOffset;
            public float overConcentrationOffset;
            public float targetSizeOffset;
            public float seaStateOffset;
            public float crewQualityBase;
            public float leaderNavalTacticalOffset;
            public LeaderSkillLevel leaderNavalTacticalLevel;
            public float fireControlRadarOffset;

            public float finalFireControlScore;
            public float hitProbabilityTableP100;
            public float globalHitCoef;
            public float finalHitProbability;

            public string GetBreakdownSignature()
            {
                static string S(float v) => Math.Round(v, 3).ToString("0.###");
                return string.Join("|", new[]
                {
                    blockedReason.ToString(),
                    rangeBand?.ToString() ?? "N/A",
                    targetAspect?.ToString() ?? "N/A",
                    trackingLocalControl ? "LC" : "TRK",
                    S(baseFireControlValue),
                    S(closeRangeOverrideDelta),
                    S(mountSubstateOffset),
                    S(mountSubstateCoef),
                    S(visibilityOffset),
                    S(dawnDuskOffset),
                    S(nightMoonlightOffset),
                    S(evasiveActionOffset),
                    S(trackingOffset),
                    S(underFireOffset),
                    S(overConcentrationOffset),
                    S(targetSizeOffset),
                    S(seaStateOffset),
                    S(crewQualityBase),
                    S(leaderNavalTacticalOffset),
                    leaderNavalTacticalLevel.ToString(),
                    S(fireControlRadarOffset),
                    S(finalFireControlScore),
                    S(hitProbabilityTableP100),
                    S(globalHitCoef),
                    S(finalHitProbability)
                });
            }
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
                BreakdownBlockedReason.MountNotOperational => "Mount is not operational.",
                BreakdownBlockedReason.NoTarget => "No current firing target.",
                BreakdownBlockedReason.ContextNotResolved => "Mount context is not fully resolved.",
                BreakdownBlockedReason.TargetSupplementaryMissing => "Target supplementary data is unavailable.",
                BreakdownBlockedReason.OutOfRange => "Target is out of battery range.",
                BreakdownBlockedReason.OutOfArc => "Target is outside this mount's firing arc.",
                BreakdownBlockedReason.DoctrineBlocked => "Blocked by maximum firing distance doctrine.",
                BreakdownBlockedReason.AmmunitionUnavailable => "Ammunition is unavailable for current doctrine/ammo state.",
                BreakdownBlockedReason.FireCheckFailed => "Not included in current gunnery fire-check set.",
                BreakdownBlockedReason.PenetrationTableMissing => "No penetration table record for current distance.",
                BreakdownBlockedReason.FireControlTableMissing => "No fire control table record for target speed.",
                _ => "Blocked by an unknown reason."
            };
        }

        public static List<string> BuildHitProbabilityBreakdownLines(HitProbabilityBreakdown breakdown, IEnumerable<string> mountLabels = null)
        {
            var lines = new List<string>();

            var targetName = string.IsNullOrWhiteSpace(breakdown?.targetName) ? "[No Target]" : breakdown.targetName;
            lines.Add($"Hit probability of {targetName}:");

            if (mountLabels != null)
            {
                var mountLabelList = mountLabels.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
                if (mountLabelList.Count > 0)
                {
                    lines.Add($"Mounts: {string.Join(", ", mountLabelList)}");
                }
            }

            if (breakdown != null && breakdown.hasMeasurement)
            {
                var targetSpeedLabel = breakdown.targetSpeedKnots.HasValue
                    ? $"{FormatNumber(breakdown.targetSpeedKnots.Value)} knots"
                    : "N/A";
                lines.Add($"{FormatNumber(breakdown.distanceYards)} yards => {(breakdown.rangeBand?.ToString() ?? "N/A")} Range Band");
                lines.Add($"{FormatNumber(breakdown.bearingDeg)} deg => {(breakdown.targetAspect?.ToString() ?? "N/A")} Angle");
                lines.Add($"Target Speed: {targetSpeedLabel}");
            }

            if (breakdown == null || !breakdown.canFire)
            {
                lines.Add("");
                lines.Add("Final Hit Probability => 0%");
                lines.Add($"Reason: {DescribeBlockedReason(breakdown?.blockedReason ?? BreakdownBlockedReason.ContextNotResolved)}");
                return lines;
            }

            lines.Add("");
            var baseFireControlTargetSpeedLabel = breakdown.targetSpeedKnots.HasValue
                ? $"{FormatNumber(breakdown.targetSpeedKnots.Value)} knots"
                : "N/A";
            lines.Add($"Base Fire Control Value ({breakdown.rangeBand}/{breakdown.targetAspect}/{baseFireControlTargetSpeedLabel}) => {FormatNumber(breakdown.baseFireControlValue)}");
            lines.Add($"Close Range Override: {FormatSigned(breakdown.closeRangeOverrideDelta)}");
            lines.Add($"Mount/Substate FC Offset: {FormatSigned(breakdown.mountSubstateOffset)}");
            lines.Add($"Mount/Substate FC Coef: x{FormatNumber(breakdown.mountSubstateCoef)}");
            lines.Add($"Visibility ({NavalGameState.Instance.scenarioState.visibility}): {FormatSigned(breakdown.visibilityOffset)}");
            lines.Add($"Dawn/Dusk (Sun Bearing Sector): {FormatSigned(breakdown.dawnDuskOffset)}");
            lines.Add($"Night/Moonlight: {FormatSigned(breakdown.nightMoonlightOffset)}");
            lines.Add($"Evasive Action: {FormatSigned(breakdown.evasiveActionOffset)}");
            lines.Add(breakdown.trackingLocalControl
                ? "Tracking (Local Control): x0.5"
                : $"Tracking State: {FormatSigned(breakdown.trackingOffset)}");
            lines.Add($"Under Fire (3+ ships): {FormatSigned(breakdown.underFireOffset)}");
            lines.Add($"Over Concentration: {FormatSigned(breakdown.overConcentrationOffset)}");
            lines.Add($"Target Size: {FormatSigned(breakdown.targetSizeOffset)}");
            lines.Add($"Sea State: {FormatSigned(breakdown.seaStateOffset)}");
            lines.Add($"Crew Quality: {FormatSigned(breakdown.crewQualityBase)}");
            lines.Add($"Leader Naval Tactical ({breakdown.leaderNavalTacticalLevel}): {FormatSigned(breakdown.leaderNavalTacticalOffset)}");
            lines.Add($"Fire Control Radar: {FormatSigned(breakdown.fireControlRadarOffset)}");

            lines.Add("");
            lines.Add($"Final Fire Control Score => {FormatNumber(breakdown.finalFireControlScore)}");
            lines.Add($"Hit Probability Table => {FormatNumber(breakdown.hitProbabilityTableP100)}%");
            lines.Add($"Global Hit Coef => x{FormatNumber(breakdown.globalHitCoef)}");
            lines.Add($"Final Hit Probability => {FormatNumber(breakdown.finalHitProbability * 100f)}%");

            return lines;
        }

        AmmunitionType ResolvePreferredAmmunitionType(FullContext ctx, ShipLog target)
        {
            if (ctx == null || target == null)
                return ammunitionType;

            var selected = ammunitionType;
            var isAmmoSwitchAuto = ctx.shipLog?.doctrine?.GetAmmunitionSwitchAutomaticType() == AutomaticType.Automatic;
            if (isAmmoSwitchAuto)
            {
                var tgtArmorScore = target.EvaluateArmorScore();
                selected = tgtArmorScore < 0.5
                    ? ctx.batteryStatus.ChooseAmmunitionByPreferredType(AmmunitionType.HighExplosive)
                    : ctx.batteryStatus.ChooseAmmunitionByPreferredType(AmmunitionType.ArmorPiercing);
            }

            return selected;
        }

        bool IsAmmunitionFireable(FullContext ctx, AmmunitionType ammo)
        {
            var ammoFallbackable = ctx.shipLog.doctrine.GetAmmunitionFallbackable();
            var hasPreferredAmmo = ctx.batteryStatus.ammunition.GetValue(ammo) > 0;
            var hasAnyAmmo = ctx.batteryStatus.ammunition.GetTotalValue() > 0;
            return hasPreferredAmmo || (ammoFallbackable && hasAnyAmmo);
        }

        HitProbabilityBreakdown BuildResolvedHitProbabilityBreakdown(
            GunneryFireContext fireCtx,
            FullContext ctx,
            ShipLog target,
            MeasureStats stats,
            PenetrationTableRecord penRecord,
            GunneryFireContext.ShipLogSupplementary targetSup)
        {
            var breakdown = new HitProbabilityBreakdown()
            {
                canFire = false,
                blockedReason = BreakdownBlockedReason.FireControlTableMissing,
                targetObjectId = target.objectId,
                targetName = target.namedShip?.name?.GetMergedName(),
                hasMeasurement = true,
                distanceYards = stats.distanceYards,
                bearingDeg = stats.observerToTargetBearingRelativeToBowDeg,
                targetSpeedKnots = target.speedKnots,
                rangeBand = penRecord.rangeBand,
                targetAspect = stats.targetPresentAspectFromObserver,
                globalHitCoef = CoreParameter.Instance.globalHitCoef,
            };

            // Fire Control Value Resolution
            var fireControlRow = ctx.batteryRecord.fireControlTableRecords.FirstOrDefault(r => target.speedKnots <= r.speedThresholdKnot);
            if (fireControlRow == null)
                return breakdown;

            breakdown.blockedReason = BreakdownBlockedReason.None;
            breakdown.canFire = true;

            var fireControlScoreRaw = fireControlRow.GetValue(penRecord.rangeBand, stats.targetPresentAspectFromObserver);
            breakdown.baseFireControlValue = fireControlScoreRaw;

            // Positive Modifier
            if (stats.distanceYards <= 4500 && penRecord.rangeBand == RangeBand.Short)
            {
                var closeRangeFireControlScore = RuleChart.GetCloseRangeFireControlScore(stats.distanceYards, target.speedKnots, stats.targetPresentAspectFromObserver);
                var adjusted = Math.Max(fireControlScoreRaw, closeRangeFireControlScore);
                breakdown.closeRangeOverrideDelta = adjusted - fireControlScoreRaw;
                fireControlScoreRaw = adjusted;
            }

            // Negative Modifiers
            var fireControlValueModifiers = GetSubStates<IFireControlValueModifier>().ToList();
            breakdown.mountSubstateOffset = fireControlValueModifiers.Select(m => m.GetFireControlValueOffset()).Sum();
            // var mountLocation = ctx.mountLocationRecord.mountLocation;
            breakdown.mountSubstateOffset += GetSubStates<ILocalizedDirectionalFireControlValueModifier>().Select(
                m => m.GetFireControlValueOffset(ctx.mountLocationRecord.mountLocation, stats.observerToTargetBearingRelativeToBowDeg)
            ).DefaultIfEmpty(0).Min();

            breakdown.mountSubstateCoef = fireControlValueModifiers.Select(m => m.GetFireControlValueCoef()).DefaultIfEmpty(1).Min();
            fireControlScoreRaw = Math.Max((fireControlScoreRaw + breakdown.mountSubstateOffset) * breakdown.mountSubstateCoef, 0);

            // var firedAtTargetBatteriesCount = targetSup.batteriesFiredAtMe.Count; // over-concentration
            var fireControlScore = fireControlScoreRaw;

            // Visibility - apply to all conditions
            var visibility = NavalGameState.Instance.scenarioState.visibility;
            if (visibility >= VisibilityDescription.VeryClear1)
            {
                // Code 8-9 (very clear): +1
                breakdown.visibilityOffset = 1;
            }
            else if (visibility >= VisibilityDescription.LightHaze)
            {
                // Code 6-7 (normal): +0
                breakdown.visibilityOffset = 0;
            }
            else if (visibility >= VisibilityDescription.ThinFog)
            {
                // Code 4-5 (haze): -2
                breakdown.visibilityOffset = -2;
            }
            else
            {
                // Patchy fog or squalls
                breakdown.visibilityOffset = -4;
            }
            fireControlScore += breakdown.visibilityOffset;

            // TODO: Move to precalculate context?
            var shooterSunState = NavalGameState.Instance.scenarioState.GetSunPosition(ctx.shipLog.position);
            var targetSunState = NavalGameState.Instance.scenarioState.GetSunPosition(target.position);

            // Target silhouetted by horizon: +1
            // Target in darkness: -2
            // None of above: +0
            breakdown.dawnDuskOffset = NavalUtils.GetDawnDuskFireControlOffset(targetSunState, stats.observerToTargetTrueBearingRelativeToNorthDeg);
            fireControlScore += breakdown.dawnDuskOffset;

            // Handle Additional for night conditions
            // No moonlight: -4
            // Moonlight: -2
            if (targetSunState.GetDayNightLevel() == DayNightLevel.Night)
            {
                breakdown.nightMoonlightOffset = NavalGameState.Instance.scenarioState.hasMoonlight ? -2 : -4;
            }
            fireControlScore += breakdown.nightMoonlightOffset;

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
            var targetShipEA = target.IsEvasiveManeuvering();
            if (firingShipEA && targetShipEA)
                breakdown.evasiveActionOffset = -8;
            else if (targetShipEA)
                breakdown.evasiveActionOffset = -3;
            else if (firingShipEA)
                breakdown.evasiveActionOffset = -2;
            fireControlScore += breakdown.evasiveActionOffset;

            // Target Acquisition
            // Firing on different ship from last turn: -2
            // Target ship hit by firing ship last turn: +2
            var trackingStates = ctx.batteryStatus.fireControlSystemStatusRecords.Where(
                fcs => fcs.IsOperational() && fcs.targetObjectId == target.objectId
            ).Select(fcs => fcs.trackingState).ToList();

            if (trackingStates.Count == 0)
            {
                breakdown.trackingLocalControl = true;
                fireControlScore /= 2; // No FCS is tracking target => Local Control: /2 FCS (DoB: -5)
                // Well, so Local Control may be better than BeginTracking according to Rulebook.
            }
            else
            {
                if (trackingStates.Contains(TrackingSystemState.Hitting))
                    breakdown.trackingOffset = 2;
                else if (trackingStates.Contains(TrackingSystemState.BeginTracking))
                    breakdown.trackingOffset = -2;

                fireControlScore += breakdown.trackingOffset;
            }

            // Firing ship under fire
            // Under fire from 3 or more ships during this turn: -2
            if (fireCtx.shipLogSupplementaryMap.TryGetValue(ctx.shipLog, out var meShipLogSup) &&
                meShipLogSup.shipLogsFiredAtMe.Count >= 3)
            {
                breakdown.underFireOffset = -2;
            }
            fireControlScore += breakdown.underFireOffset;

            // Over-Concentration & Barrage
            // 1 ship firing at target with 1 battery: 0
            // For each additional primary, secondary or teriary battery of any ship firing at same target: -1
            // For every primary, secondary or tertiary battery of any ship using barrage fire at same target: -2
            breakdown.overConcentrationOffset = Math.Min(0, -(targetSup.batteriesFiredAtMe.Count - 1));
            fireControlScore += breakdown.overConcentrationOffset;

            // Size of target ship
            // TS (from Ship Log of target ship)
            breakdown.targetSizeOffset = target.shipClass.targetSizeModifier;
            fireControlScore += breakdown.targetSizeOffset;

            // Pending: Spotter Aircraft
            // Spotter aircraft (target visible from firing ship): +2

            // Battle factor
            // Sea State + Crew Rating (from Ship Log)
            breakdown.seaStateOffset = RuleChart.ResolveSeaStateOffset(
                ctx.shipClass.displacementTons,
                NavalGameState.Instance.scenarioState.seaStateBeaufort,
                out bool blocked
            );
            fireControlScore += breakdown.seaStateOffset; // Use -100 to soft block

            breakdown.crewQualityBase = ctx.shipLog.GetCrewQualityBaseForFloatUsageDisplay();
            breakdown.leaderNavalTacticalLevel = ctx.shipLog.GetLeaderNavalTacticalLevelForDisplay();
            breakdown.leaderNavalTacticalOffset = ctx.shipLog.GetLeaderNavalTacticalOffsetForCrewQualityDisplay();
            fireControlScore += breakdown.crewQualityBase + breakdown.leaderNavalTacticalOffset;

            // Fire Control Radar Modifier
            if (ctx.batteryRecord.hasFireControlRadar && !GetSubStates<IElectronicSystemModifier>().Any(m => m.IsFireControlRadarDisabled()))
            {
                breakdown.fireControlRadarOffset = ctx.batteryRecord.fireControlRadarModifier;
            }
            fireControlScore += breakdown.fireControlRadarOffset;

            breakdown.finalFireControlScore = fireControlScore;
            breakdown.hitProbabilityTableP100 = RuleChart.GetHitProbP100(fireControlScore);
            breakdown.finalHitProbability = breakdown.hitProbabilityTableP100 * 0.01f * breakdown.globalHitCoef;

            return breakdown;
        }

        FullContext ResolveContextFromFireContext(GunneryFireContext fireCtx)
        {
            if (fireCtx != null &&
                fireCtx.mountStatusRecordMap.TryGetValue(this, out var mntSup) &&
                mntSup?.ctx != null)
            {
                return mntSup.ctx;
            }

            return GetFullContext();
        }

        public HitProbabilityBreakdown GetCurrentHitProbabilityBreakdown(GunneryFireContext fireCtx = null)
        {
            if (fireCtx == null)
            {
                using (var tempFireCtx = GunneryFireContext.Begin())
                {
                    return GetCurrentHitProbabilityBreakdown(tempFireCtx);
                }
            }

            var target = GetFiringTarget();
            var breakdown = new HitProbabilityBreakdown()
            {
                canFire = false,
                blockedReason = BreakdownBlockedReason.NoTarget,
                targetObjectId = target?.objectId,
                targetName = target?.namedShip?.name?.GetMergedName(),
                targetSpeedKnots = target?.speedKnots,
                globalHitCoef = CoreParameter.Instance.globalHitCoef,
            };

            if (!IsOperational())
            {
                breakdown.blockedReason = BreakdownBlockedReason.MountNotOperational;
                return breakdown;
            }
            if (target == null)
                return breakdown;

            var ctx = ResolveContextFromFireContext(fireCtx);
            if (ctx == null || !ctx.fullyResolved)
            {
                breakdown.blockedReason = BreakdownBlockedReason.ContextNotResolved;
                return breakdown;
            }

            var shooter = ctx.shipLog;
            if (shooter == null || ctx.batteryRecord == null || ctx.mountLocationRecord == null)
            {
                breakdown.blockedReason = BreakdownBlockedReason.ContextNotResolved;
                return breakdown;
            }

            var shooterTargetSup = fireCtx.GetOrCalcualteShipLogPairSupplementary(shooter, target);
            var stats = shooterTargetSup.stats;
            breakdown.hasMeasurement = true;
            breakdown.distanceYards = stats.distanceYards;
            breakdown.bearingDeg = stats.observerToTargetBearingRelativeToBowDeg;
            breakdown.targetAspect = stats.targetPresentAspectFromObserver;

            if (stats.distanceYards > ctx.batteryRecord.rangeYards)
            {
                breakdown.blockedReason = BreakdownBlockedReason.OutOfRange;
                return breakdown;
            }

            if (!ctx.batteryStatus.IsMaxDistanceDoctrineRespected(stats.distanceYards))
            {
                breakdown.blockedReason = BreakdownBlockedReason.DoctrineBlocked;
                return breakdown;
            }

            if (!ctx.mountLocationRecord.IsInArc(stats.observerToTargetBearingRelativeToBowDeg))
            {
                breakdown.blockedReason = BreakdownBlockedReason.OutOfArc;
                return breakdown;
            }

            var chosenAmmoType = ResolvePreferredAmmunitionType(ctx, target);
            if (!IsAmmunitionFireable(ctx, chosenAmmoType))
            {
                breakdown.blockedReason = BreakdownBlockedReason.AmmunitionUnavailable;
                return breakdown;
            }

            if (!fireCtx.shipLogSupplementaryMap.TryGetValue(target, out var targetSup))
            {
                breakdown.blockedReason = BreakdownBlockedReason.TargetSupplementaryMissing;
                return breakdown;
            }

            var isFireChecked = targetSup.batteriesFiredAtMe.Contains(ctx.batteryStatus);
            if (!isFireChecked)
            {
                breakdown.blockedReason = BreakdownBlockedReason.FireCheckFailed;
                return breakdown;
            }

            var penRecord = ctx.batteryRecord.penetrationTableRecords.FirstOrDefault(r => stats.distanceYards <= r.distanceYards);
            if (penRecord == null)
            {
                breakdown.blockedReason = BreakdownBlockedReason.PenetrationTableMissing;
                return breakdown;
            }
            breakdown.rangeBand = penRecord.rangeBand;

            return BuildResolvedHitProbabilityBreakdown(fireCtx, ctx, target, stats, penRecord, targetSup);
        }

        public string DescribeDetail()
        {
            var lines = new List<string>() { $"Detail: {objectId}" };
            var breakdown = GetCurrentHitProbabilityBreakdown();
            lines.AddRange(BuildHitProbabilityBreakdownLines(breakdown));

            var subStateDescriptions = subStates
                .Select(s => s?.Describe())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();

            if (subStateDescriptions.Count > 0)
            {
                if (lines.Count > 0 && lines[^1] != "")
                    lines.Add("");
                lines.Add("SubStates:");
                lines.AddRange(subStateDescriptions.Select(desc => $"- {desc}"));
            }

            if (logs.Count > 0)
            {
                if (lines.Count > 0 && lines[^1] != "")
                    lines.Add("");
            }
            lines.AddRange(logs.Select(r => r.Summary()));

            return string.Join("\n", lines);
        }

        public static bool disableAmmunitionCost = false;


        public void Step(float deltaSeconds)
        {
            if (!IsOperational())
                return;

            var tgt = GetFiringTarget();
            if (tgt != null)
            {
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
                bool IsAmmoFireable()
                {
                    var hasPreferredAmmo = ctx.batteryStatus.ammunition.GetValue(ammunitionType) > 0;
                    var hasAnyAmmo = ctx.batteryStatus.ammunition.GetTotalValue() > 0;
                    return hasPreferredAmmo || (ammoFallbackable && hasAnyAmmo);
                }
                if (!IsAmmoFireable()) // Re-check will be required in the following code
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

                processSeconds += deltaSeconds;

                // skip to log ammo consumption and firing "result"
                while (processSeconds >= secondsPerShoot &&
                        IsAmmoFireable()
                )
                {
                    processSeconds -= secondsPerShoot;

                    if (ammoFallbackable)
                    {
                        ammunitionType = ctx.batteryStatus.ChooseAmmunitionByPreferredType(ammunitionType); // TODO: Use doctrine suggested value
                    }
                    if (ctx.batteryStatus.ammunition.GetValue(ammunitionType) <= 0)
                    {
                        break;
                    }

                    if(!disableAmmunitionCost)
                    {
                        ctx.batteryStatus.ammunition.CostOne(ammunitionType);
                    }

                    ctx.shipLog.firingRounds += 1;
                    var breakdown = BuildResolvedHitProbabilityBreakdown(fireCtx, ctx, tgt, stats, penRecord, targetSup);
                    if (!breakdown.canFire)
                    {
                        if (breakdown.blockedReason == BreakdownBlockedReason.FireControlTableMissing)
                            return;
                        continue;
                    }

                    var hitProb = breakdown.finalHitProbability;
                    var hit = (float)RandomUtils.rand.NextDouble() < hitProb;

                    var logRecord = new MountFiringRecord()
                    {
                        firingTargetObjectId = tgt.objectId,
                        ammunitionType = ammunitionType,
                        firingTime = NavalGameState.Instance.scenarioState.dateTime,
                        distanceYards = stats.distanceYards,
                        hitProb = hitProb,
                        hit = hit,
                        // hitPenDetType = hitPenDetType,
                        // armorLocation = armorLocation,
                        // shellDamageResult = shellDamageResult
                    };
                    logs.Add(logRecord); // logRecord could be further modified in the following code

                    if (hit)
                    {
                        ProcessHit(tgt, ctx, shooter, stats, penRecord, logRecord);
                    }
                }
            }
            else
            {
                processSeconds = 0;
            }
        }

        private void ProcessHit(ShipLog tgt, FullContext ctx, ShipLog shooter, MeasureStats stats, PenetrationTableRecord penRecord, MountFiringRecord logRecord)
        {
            var damageSchema = tgt.shipClass.GetDamageSchema();
            logRecord.DamageSchema = damageSchema;
            ctx.batteryStatus?.NotifyHitOnTarget(tgt);

            if (damageSchema == DamageSchema.Warship) // Warship
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

                    var tgtLog = new ShipLogBatteryHitLog()
                    {
                        shooterId = shooter.objectId,
                        time = NavalGameState.Instance.scenarioState.dateTime,
                        damageSchema = DamageSchema.Warship
                    };

                    tgtLog.hitPenDetType = logRecord.HitPenDetType = hitPenDetType;
                    tgtLog.ArmorLocation = logRecord.ArmorLocation = armorLocation;
                    logRecord.ShellDamageResult = shellDamageResult;
                    tgtLog.damagePoint = shellDamageResult.damagePoint;

                    tgt.AddLog(tgtLog);

                    tgt.AddDamagePoint(shellDamageResult.damagePoint);

                    string damageEffectId = null;
                    // Process Damage Effect
                    if (RandomUtils.NextFloat() <= shellDamageResult.damageEffectProb)
                    {
                        var damageEffectCause = RuleChart.GetDamageEffectCauseWarship(armorLocation);
                        var damageEffectContext = new DamageEffectContext()
                        {
                            subject = tgt,
                            baseDamagePoint = shellDamageResult.damagePoint,
                            ammunitionType = ammunitionType,
                            shellDiameterInch = ctx.batteryRecord.shellSizeInch,
                            hitPenDetType = hitPenDetType,

                            damageSchema = damageSchema,
                            cause = damageEffectCause,
                            addtionalDamageEffectProbility = shellDamageResult.damageEffectProb
                        };

                        damageEffectId = DamageEffectChart.AddNewDamageEffect(damageEffectContext);

                        tgtLog.damageEffectId = logRecord.DamageEffectId = damageEffectId;
                    }

                    var logger = ServiceLocator.Get<ILoggerService>();
                    logger.Log($"{ctx.shipLog.namedShip.name.GetMergedName()} {ctx.batteryRecord.name.GetMergedName()} -> {tgt.namedShip.name.GetMergedName()} ({logRecord.Summary()}) (DE: {damageEffectId})");
                }
            }
            else if (damageSchema == DamageSchema.MerchantVessal)
            {
                var hitLocationMerchantVessel = RuleChart.SampleHitLocationMerchantVessel();

                var hitPenDetType = RuleChart.ResolveHitPenDetType(1, 0, ammunitionType);
                var shellDamageResult = RuleChart.ResolveShellDamageResult(ctx.batteryRecord.damageRating, hitPenDetType, ammunitionType);

                var tgtLog = new ShipLogBatteryHitLog()
                {
                    shooterId = shooter.objectId,
                    time = NavalGameState.Instance.scenarioState.dateTime,
                    damageSchema = DamageSchema.MerchantVessal
                };

                tgtLog.hitPenDetType = logRecord.HitPenDetType = hitPenDetType;
                tgtLog.HitLocationMerchantVessel = logRecord.HitLocationMerchantVessel = hitLocationMerchantVessel;
                logRecord.ShellDamageResult = shellDamageResult;
                tgtLog.damagePoint = shellDamageResult.damagePoint;

                tgt.AddLog(tgtLog);

                tgt.AddDamagePoint(shellDamageResult.damagePoint);

                string damageEffectId = null;
                // Process Damage Effect
                if (RandomUtils.NextFloat() <= shellDamageResult.damageEffectProb)
                {
                    var causeMerchantVessel = RuleChart.GetDamageEffectCauseMerchantVessel(hitLocationMerchantVessel, tgt.cargoAreas);
                    var damageEffectContext = new DamageEffectContext()
                    {
                        subject = tgt,
                        baseDamagePoint = shellDamageResult.damagePoint,
                        ammunitionType = ammunitionType,
                        shellDiameterInch = ctx.batteryRecord.shellSizeInch,
                        hitPenDetType = hitPenDetType,

                        damageSchema = damageSchema,
                        causeMerchantVessel = causeMerchantVessel,
                    };

                    damageEffectId = DamageEffectChart.AddNewDamageEffect(damageEffectContext);

                    tgtLog.damageEffectId = logRecord.DamageEffectId = damageEffectId;
                }

                var logger = ServiceLocator.Get<ILoggerService>();
                logger.Log($"{ctx.shipLog.namedShip.name.GetMergedName()} {ctx.batteryRecord.name.GetMergedName()} -> {tgt.namedShip.name.GetMergedName()} ({logRecord.Summary()}) (DE: {damageEffectId})");

            }
            else if (damageSchema == DamageSchema.LandBattery)
            {
                var armorLocation = RuleChart.RollArmorLocationLandBattery(penRecord.rangeBand);
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

                    var tgtLog = new ShipLogBatteryHitLog()
                    {
                        shooterId = shooter.objectId,
                        time = NavalGameState.Instance.scenarioState.dateTime,
                        damageSchema = DamageSchema.LandBattery
                    };

                    tgtLog.hitPenDetType = logRecord.HitPenDetType = hitPenDetType;
                    tgtLog.ArmorLocation = logRecord.ArmorLocation = armorLocation;
                    logRecord.ShellDamageResult = shellDamageResult;
                    tgtLog.damagePoint = shellDamageResult.damagePoint;

                    tgt.AddLog(tgtLog);
                    tgt.AddDamagePoint(shellDamageResult.damagePoint);

                    string damageEffectId = null;
                    if (RandomUtils.NextFloat() <= shellDamageResult.damageEffectProb)
                    {
                        var damageEffectContext = new DamageEffectContext()
                        {
                            subject = tgt,
                            baseDamagePoint = shellDamageResult.damagePoint,
                            ammunitionType = ammunitionType,
                            shellDiameterInch = ctx.batteryRecord.shellSizeInch,
                            hitPenDetType = hitPenDetType,
                            damageSchema = damageSchema,
                            causeLandBattery = RuleChart.GetDamageEffectCauseLandBattery(armorLocation),
                            addtionalDamageEffectProbility = shellDamageResult.damageEffectProb
                        };

                        damageEffectId = DamageEffectChart.AddNewDamageEffect(damageEffectContext);
                        tgtLog.damageEffectId = logRecord.DamageEffectId = damageEffectId;
                    }

                    var logger = ServiceLocator.Get<ILoggerService>();
                    logger.Log($"{ctx.shipLog.namedShip.name.GetMergedName()} {ctx.batteryRecord.name.GetMergedName()} -> {tgt.namedShip.name.GetMergedName()} ({logRecord.Summary()}) (DE: {damageEffectId})");
                }
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
            
            ResetTargetting();
            barrels = GetMountLocationRecordInfo().record.barrels;

            logs.Clear();
        }
    }
}
