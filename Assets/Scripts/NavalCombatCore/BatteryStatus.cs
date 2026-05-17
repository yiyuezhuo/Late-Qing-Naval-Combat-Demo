using System.Collections.Generic;
using System.Data;
using System.Linq;
using System;
using GeographicLib;
using System.Xml.Serialization;

using CoreUtils;

namespace NavalCombatCore
{
    public partial class BatteryStatus : UnitModule, IWTABattery
    {
        // public string objectId { get; set; }
        public BatteryAmmunitionRecord ammunition = new(); // TODO: based on mount instead of battery?
        public List<MountStatusRecord> mountStatus = new();
        // public int fireControlHits;
        public List<FireControlSystemStatusRecord> fireControlSystemStatusRecords = new();

        public bool fireControlRadarDisabled;

        public override IEnumerable<IObjectIdLabeled> GetSubObjects()
        {
            foreach (var so in base.GetSubObjects())
                yield return so;

            foreach (var mount in mountStatus)
            {
                yield return mount;
            }
            foreach (var fireControlSystemStatusRecord in fireControlSystemStatusRecords)
            {
                yield return fireControlSystemStatusRecord;
            }
        }

        public override string ToString()
        {
            return $"BatteryStatus({GetBatteryRecord()?.name?.GetMergedNamePure()})";
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

        public void ResetDamageExpenditureState(ResetDamageExpenditureStateContext ctx)
        {
            var batteryRecord = GetBatteryRecord();

            // ammunition.ArmorPiercing = batteryRecord.ammunitionCapacity / 2;
            // ammunition.common = batteryRecord.ammunitionCapacity / 2;
            // ctx.SetAmmunition(batteryRecord.shellSizeInch, batteryRecord.ammunitionCapacity, ammunition);
            ResetExpenditureState(ctx);

            var expectedLength = batteryRecord.mountLocationRecords.Sum(r => r.mounts);
            Utils.SyncListToLength(expectedLength, mountStatus, this);
            foreach (var s in mountStatus)
                s.ResetDamageExpenditureState();

            // fireControlHits = 0;
            Utils.SyncListToLength(batteryRecord.fireControlPositions, fireControlSystemStatusRecords, this);
            foreach (var s in fireControlSystemStatusRecords)
                s.ResetToIntegrityState();

            fireControlRadarDisabled = false;
        }

        public void ResetExpenditureState(ResetDamageExpenditureStateContext ctx)
        {
            ctx.SetAmmunition(batteryRecord.shellSizeInch, batteryRecord.ammunitionCapacity, ammunition);
        }

        public float GetEffectiveBarrels(IEnumerable<MountStatusRecord> considieredMounts) // modified by ROF, FC value modifier and etc
        {
            var barrels = considieredMounts.Sum(m => m.barrels *
                m.GetSubStates<IRateOfFireModifier>().Select(m => m.GetRateOfFireCoef()).DefaultIfEmpty(1).Min() *
                m.GetSubStates<IFireControlValueModifier>().Select(m => Math.Max(0, m.GetFireControlValueCoef() + m.GetFireControlValueOffset() * 0.05f)).DefaultIfEmpty(1).Min()
            );

            var localizedMod = GetSubStates<ILocalizedDirectionalFireControlValueModifier>().FirstOrDefault();
            if (localizedMod != null)
            {
                barrels *= 0.9f;
            }

            return barrels;
        }

        public float EvaluateFirepowerScore()
        {
            var batteryRecord = GetBatteryRecord();
            var firepoweScorePerBarrel = batteryRecord.EvaluateFirepowerPerBarrel();

            var availableBarrels = GetEffectiveBarrels(
                mountStatus.Where(
                    m => m.IsOperational()
                )
            );

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

            var barrels = GetEffectiveBarrels(
                mountStatus.Where(
                    m => m.IsOperational() &&
                         (m.GetMountLocationRecordInfo()?.record?.IsInArc(bearingRelativeToBowDeg) ?? false)
                )
            );

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
                if (mnt.IsOperational() &&
                    mnt.GetMountLocationRecordInfo().record.IsInArc(stats.observerToTargetBearingRelativeToBowDeg))
                {
                    // TODO: Check Range? Though effect of range shoul have been handled in the evaluation. 
                    mnt.SetFiringTarget(target);
                }
                else
                {
                    mnt.SetFiringTarget(null);
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

        public void NotifyHitOnTarget(ShipLog target)
        {
            if (target == null)
                return;

            foreach (var fcs in fireControlSystemStatusRecords)
            {
                fcs.NotifyTrackedTargetHit(target);
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

            if(ammunition.GetTotalValue() <= 0)
            {
                foreach(var fcs in fireControlSystemStatusRecords)
                    fcs.ResetTargetting();
                foreach(var mnt in mountStatus)
                    mnt.ResetTargetting();
            }
        }

        public string DescribeFireControlDetail()
        {
            var lines = new List<string>();

            lines.Add($"Battery Detail: {objectId}");

            List<IGrouping<(string targetObjectId, string signature), (int idx, MountStatusRecord.HitProbabilityBreakdown breakdown, string mountLabel)>> groupedBreakdowns;
            using (var fireCtx = GunneryFireContext.Begin())
            {
                var entries = mountStatus.Select((mount, idx) => new
                {
                    idx,
                    breakdown = mount.GetCurrentHitProbabilityBreakdown(fireCtx),
                    mountLabel = mount.GetMountLocationRecordInfo()?.Summary() ?? mount.objectId
                }).Select(e => (e.idx, e.breakdown, e.mountLabel)).ToList();

                groupedBreakdowns = entries
                    .GroupBy(e => (
                        targetObjectId: e.breakdown?.targetObjectId ?? "[NoTarget]",
                        signature: e.breakdown?.GetBreakdownSignature() ?? "NoBreakdown"
                    ))
                    .OrderBy(g => g.Min(e => e.idx))
                    .ToList();
            }

            foreach (var group in groupedBreakdowns)
            {
                lines.Add("");
                var first = group.First();
                lines.AddRange(MountStatusRecord.BuildHitProbabilityBreakdownLines(
                    first.breakdown,
                    group.Select(x => x.mountLabel)
                ));
            }

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

            return string.Join("\n", lines);
        }

        public string DescribeDetail()
        {
            var lines = new List<string>();
            var fireControlDetail = DescribeFireControlDetail();
            if (!string.IsNullOrWhiteSpace(fireControlDetail))
                lines.Add(fireControlDetail);

            var logsFlatten = mountStatus.SelectMany(mount => mount.logs).ToList();
            logsFlatten.Sort((log1, log2) => log1.firingTime.CompareTo(log2.firingTime));
            if (logsFlatten.Count > 0)
            {
                if (lines.Count > 0 && lines[^1] != "")
                    lines.Add("");
            }
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

        public bool IsChangeTargetBlocked()
        {
            var mountChangeAnyBlocked = mountStatus.Any(
                mnt => mnt.GetSubStates<IBatteryTargetChangeBlocker>()
                    .Any(m => m.IsBatteryTargetChangeBlocked())
            );

            var fcsChangeAnyBlocked = fireControlSystemStatusRecords.Any(
                fcs => fcs.GetSubStates<IFireControlSystemTargetChangeBlocker>()
                    .Any(m => m.IsFireControlSystemTargetChangeBlocked())
            );

            return mountChangeAnyBlocked || fcsChangeAnyBlocked;
        }

        public void TrimMissHitLogs()
        {
            foreach(var mnt in mountStatus)
            {
                mnt.TrimMissHitLogs();
            }
        }
    }
}
