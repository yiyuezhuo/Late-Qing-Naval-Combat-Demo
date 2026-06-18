using System;
using System.Xml.Serialization;
using System.Xml;
using System.Collections.Generic;
using System.Linq;

using CoreUtils;
using YYZ;


namespace NavalCombatCore
{
    public enum StateLifeCycle
    {
        Permanent, // True permanent, child, or ended by itself
        GivenTime, // after 120 min, 240 min, ...
        SeverityBased, // DCR allocation mini game
        ShipboardFire, // Fire dedicated DCR mechnanism
        DieRollPassed, // Die rol for every clock tick, if passed the state is removed
        Dependent // If Parent is ended, the DE State is ended as well
    }

    public enum CampaignPersistence
    {
        Clear, // Removed if state is transitioned to campaign or strategic.
        Volatile, // If DCA applicable, removed, otherwise, maintained.
        Maintained, // Maintained sub state, this may be repaired in the long time (campaign or strategic) mode.
        DestinedSunk, // Though the ship is not sunk, ship would be sunk due to this DE in the the short => long transition.
    }

    public interface ISubject
    {
        void AddSubState(SubState state);
        void RemoveSubState(SubState state);
    }

    [XmlInclude(typeof(ShipboardFireState))]
    [XmlInclude(typeof(SinkingState))]
    [XmlInclude(typeof(BatteryMountStatusModifier))]
    [XmlInclude(typeof(RateOfFireModifier))]
    [XmlInclude(typeof(BatteryFireControlStatusDisabledModifier))]
    [XmlInclude(typeof(ControlSystemDisabledModifier))]
    [XmlInclude(typeof(FireControlValueModifier))]
    [XmlInclude(typeof(RiskingInMagazineExplosion))]
    [XmlInclude(typeof(EngineRoomHitModifier))]
    [XmlInclude(typeof(BoilerRoomHitModifier))]
    [XmlInclude(typeof(SteamLineDamaged))]
    [XmlInclude(typeof(DamageControlModifier))]
    [XmlInclude(typeof(DynamicModifier))]
    [XmlInclude(typeof(FeedwaterPumpDamaged))]
    [XmlInclude(typeof(RudderDamaged))]
    [XmlInclude(typeof(FuelSupplyDamaged))]
    [XmlInclude(typeof(EngineRoomCommunicationDamaged))]
    [XmlInclude(typeof(TorpedoMountDamaged))]
    [XmlInclude(typeof(TorpedoMountModifer))]
    [XmlInclude(typeof(SmokeGeneratorDamaged))]
    [XmlInclude(typeof(SectorFireState))]
    [XmlInclude(typeof(MainPowerplantOOA))]
    [XmlInclude(typeof(BatteryTargetChangeBlocker))]
    [XmlInclude(typeof(ElectronicSystemModifier))]
    [XmlInclude(typeof(ArmorModifier))]
    [XmlInclude(typeof(SevereFloodingRollModifier))]
    [XmlInclude(typeof(LossOfCommunicationToFireControlSystemState))]
    [XmlInclude(typeof(LossOfCommunicationsAndPowerToSearchLight))]
    [XmlInclude(typeof(LossOfCommunicationToEngineRoom))]
    [XmlInclude(typeof(BatteryHandlingRoomAbandoned))]
    [XmlInclude(typeof(OneShotDamageEffectHappend))]
    [XmlInclude(typeof(DE602DyanmicModifier))]
    [XmlInclude(typeof(DE607DyanmicModifier))]
    [XmlInclude(typeof(ShipSettleState))]
    [XmlInclude(typeof(DE609Effect))]
    [XmlInclude(typeof(FiringCircuitDamagedMaster))]
    [XmlInclude(typeof(FiringCircuitDamagedWorker))]
    [XmlInclude(typeof(DE806DynamicModifier))]
    [XmlInclude(typeof(BatteryDamaged))]
    [XmlInclude(typeof(PlaceholderState))]
    [XmlInclude(typeof(FireInExplodePotentialCargoHold))]
    [XmlInclude(typeof(PropulsionSystemDamaged))]
    [XmlInclude(typeof(ExcessiveFlooding))]
    [XmlInclude(typeof(UncontrolledFlooding))]
    [XmlInclude(typeof(PropulsionDamaged))]
    public partial class SubState : IObjectIdLabeled
    {
        public string objectId { get; set; }
        public IEnumerable<IObjectIdLabeled> GetSubObjects()
        {
            yield break;
        }

        // Left Cycle Parameter
        public StateLifeCycle lifeCycle;

        public float givenTimeSeconds = 120; // For GivenTime
        public float severity = 0; // For SeverityBased and ShipboardFire
        public float dieRollThreshold = 0; // For RollPassed, if die roll <=  the threshold, the state is removed.
        public string dependentObjectId;

        public string DescribeLiftCycle()
        {
            if (lifeCycle == StateLifeCycle.Permanent)
            {
                return Localize(
                    "Permanent"
                );
            }
            else if (lifeCycle == StateLifeCycle.GivenTime)
            {
                return Localize(
                    "For {0} seconds",
                    givenTimeSeconds
                );
            }
            else if (lifeCycle == StateLifeCycle.SeverityBased || lifeCycle == StateLifeCycle.ShipboardFire)
            {
                return Localize(
                    "Severity: {0}",
                    severity
                );
            }
            else if (lifeCycle == StateLifeCycle.DieRollPassed)
            {
                return Localize(
                    "DieRoll: {0}",
                    dieRollThreshold
                );
            }
            return Localize(
                "Unknown life cycle"
            );
        }

        public string cause = "";

        protected static string Localize(string key, params object[] args) => ServiceLocator.Get<ILocalizeService>().Get(key, args);

        // public bool permanent; // If it's not permanent then this can be damage controlled.
        public virtual bool damageControllable
        {
            get
            {
                if (lifeCycle == StateLifeCycle.SeverityBased || lifeCycle == StateLifeCycle.ShipboardFire)
                {
                    var shipLog = EntityManager.Instance.GetParent<ShipLog>(this);
                    if (shipLog != null)
                    {
                        var damageControlModifiers = shipLog.GetSubStates<IDamageControlModifier>().ToList();

                        if (damageControlModifiers.Any(m => m.IsDamageControlBlocked()))
                            return false;

                        if (lifeCycle == StateLifeCycle.ShipboardFire &&
                                damageControlModifiers.Any(m => m.IsFightingFireBlocked()))
                            return false;

                        if (IsBatteryRelated() && damageControlModifiers.Any(m => m.IsBatteryDamageControlBlock()))
                            return false;
                    }
                    return true;
                }
                return false;
            }
        }

        public virtual bool IsBatteryRelated() => false; // Poor man's tag

        public virtual float GetDamageControlPriorityCoef() => 1;
        public virtual float GetDamageControlPriority()
        {
            if (!damageControllable)
                return 0;
            if (lifeCycle == StateLifeCycle.ShipboardFire)
            {
                return (severity + 100) * GetDamageControlPriorityCoef();
            }
            else if (lifeCycle == StateLifeCycle.SeverityBased)
            {
                return (100 - severity) * GetDamageControlPriorityCoef();
            }
            return GetDamageControlPriorityCoef();
        }
        public bool damageControlApplied;
        public SimulationClock turnClock = new SimulationClock()
        {
            intervalSeconds = 120, // 1 SK5 Turn, 2 min
        };

        // public List<SubState> children = new();

        public virtual void Step(ISubject subject, float deltaSeconds)
        {
            DoStep(subject, deltaSeconds);

            var tick = turnClock.Step(deltaSeconds);
            for (int i = 0; i < tick; i++)
            {
                OnClockTick(subject, deltaSeconds); // SeverityBased, ShipboardFire, DieRollPassed may be removed in the running of callback
            }

            if (lifeCycle == StateLifeCycle.GivenTime && turnClock.elapsedSeconds > givenTimeSeconds)
            {
                EndAt(subject);
            }

            if (lifeCycle == StateLifeCycle.Dependent)
            {
                var dependSubState = EntityManager.Instance.Get<SubState>(dependentObjectId);
                if (dependSubState == null)
                {
                    EndAt(subject);
                }
            }
        }

        public virtual void DoStep(ISubject subject, float deltaSeconds)
        { }

        public virtual string Describe() => Localize(
            "Sub State: {0}",
            GetType().Name
        );

        public virtual void DoOnClockTick(ISubject subject, float deltaSeconds)
        { }

        public virtual void OnClockTick(ISubject subject, float deltaSeconds) // Generally, SK5 turn advancement callback (per 2min)
        {
            DoOnClockTick(subject, deltaSeconds);

            if (lifeCycle == StateLifeCycle.SeverityBased)
            {
                // M3 Damages Status Check
                var damageContrlThreshold = severity + (damageControlApplied ? 0 : 20);
                var permanentThreshold = damageControlApplied ? 2 : 7;
                var d100 = RandomUtils.D100F();

                var d100Offset = 0f;
                if (subject is ShipLog shipLog)
                {
                    d100Offset = shipLog.GetSubStates<IDamageControlModifier>().Sum(m => m.GetDamageControlDieRollOffset());
                }

                if (d100 + d100Offset > damageContrlThreshold)
                {
                    OnDamageControllSuccessed(subject, deltaSeconds);
                }
                else
                {
                    // OnDamageControllFailed();
                    if (d100 <= permanentThreshold)
                    {
                        OnDamageControllSetPermanent(subject, deltaSeconds);
                    }
                }
            }

            if (lifeCycle == StateLifeCycle.ShipboardFire)
            {
                if (subject is ShipLog shipLog) // Shipboard Fire can only be attached to ShipLog
                {
                    // shipLog.damagePoint += severity;
                    shipLog.AddDamagePoint(severity);

                    var newDamageEffectCausedByFire = RuleChart.ResolveShipboardFireDamageEffect(severity);
                    if (newDamageEffectCausedByFire)
                    {
                        var damageSchema = shipLog?.shipClass?.GetDamageSchema() ?? DamageSchema.Warship;

                        // Add DE caused by shipbpard fire
                        var damageEffectCtx = new DamageEffectContext
                        {
                            subject = shipLog,
                            damageSchema = damageSchema,
                            cause = DamageEffectCause.Fires,
                            source = this,
                        };
                        if (damageSchema == DamageSchema.LandBattery)
                        {
                            damageEffectCtx.causeLandBattery = DamageEffectCauseLandBattery.Fires;
                        }

                        DamageEffectChart.AddNewDamageEffect(damageEffectCtx);
                    }

                    var d100Offset = shipLog.GetSubStates<IDamageControlModifier>().Sum(m => m.GetFightingFireDieRollOffset());

                    severity = RuleChart.ResolveFightingShipBoardFiresDelta(severity, damageControlApplied, d100Offset);
                    if (severity == 0)
                    {
                        EndAt(subject);
                    }
                }
            }

            if (lifeCycle == StateLifeCycle.DieRollPassed && RandomUtils.D100F() <= dieRollThreshold)
            {
                EndAt(subject);
            }
        }

        ShipLog GetShipLog(ISubject subject)
        {
            if (subject is IObjectIdLabeled obj)
            {
                while (true)
                {
                    if (obj is ShipLog shipLog)
                        return shipLog;

                    var parent = EntityManager.Instance.GetParent<IObjectIdLabeled>(obj);
                    if (parent == null)
                        return null;

                    obj = parent;
                }
            }
            return null;
        }

        public virtual void BeginAt(ISubject subject)
        {
            subject.AddSubState(this);
            EntityManager.Instance.Register(this, subject);

            var shipLog = GetShipLog(subject);
            if (shipLog != null)
            {
                // shipLog.AddStringLog($"Begin: {description} {cause}");
                shipLog.AddStringLog(Localize(
                    "Begin: {0} {1}",
                    description, cause
                ));
            }

            DoBeginAt(subject);
        }

        public virtual void EndAt(ISubject subject)
        {
            subject.RemoveSubState(this);
            EntityManager.Instance.Unregister(this);

            var shipLog = GetShipLog(subject);
            if (shipLog != null)
            {
                shipLog.AddStringLog(Localize(
                    "End: {0} {1}",
                    description, cause
                ));
            }

            DoEndAt(subject);
        }

        public virtual void DoBeginAt(ISubject subject)
        { }

        public virtual void DoEndAt(ISubject subject)
        { }

        public virtual void OnDamageControllSuccessed(ISubject subject, float deltaSeconds)
        {
            EndAt(subject); // Shipboard Fire cannot be eliminated just just a Roll
        }

        public virtual void OnDamageControllSetPermanent(ISubject subject, float deltaSeconds)
        {
            lifeCycle = StateLifeCycle.Permanent; // or something like SeverityBasedPermanent?
        }

        // public void AddChild(SubState subState)
        // {
        //     subState.lifeCycle = StateLifeCycle.Permanent; // Controlled by parent
        //     children.Add(subState);
        // }

        public virtual CampaignPersistence GetCampaignPersistence() => CampaignPersistence.Clear;
    }

    public interface IBatteryMountStatusModifier // Mounts should check its platform's damage effects which implements IBatteryMountEffector to determine its effective status (the state hold by itself is the "permanent" state, while effector may override this value for a given time)
    {
        MountStatus GetBatteryMountStatus(); // E.X this may restrict a Operational mount to Damage and OOA
    }

    public interface ITorpedoMountStatusModifier
    {
        MountStatus GetTorpedoMountStatus();
    }

    public interface IRateOfFireModifier
    {
        float GetRateOfFireCoef(); // E.X: this many resctrict a mount ROF to 50% of its original value.
    }

    public interface IBarrageFireBlocker // TODO: Support Barrage Fire
    {
        bool IsBarrageFireBlocked();
    }

    public interface IFireControlValueModifier
    {
        float GetFireControlValueCoef();
        float GetFireControlValueOffset();
    }

    public interface ILocalizedDirectionalFireControlValueModifier
    {
        // float GetFireControlValueCoef(MountLocation mountLocation, float bearingRelativeToBowDeg) => 1;
        float GetFireControlValueOffset(MountLocation mountLocation, float bearingRelativeToBowDeg);//  => 0;
    }

    public interface IBatteryFireContrlStatusModifier
    {
        bool GetBatteryFireControlDisabled(); // Tracking system use different status representation for now.
    }

    public interface IEngineRoomHitModifier
    {
        int GetEngineRoomHitOffset();
    }

    public interface IBoilerRoomHitModifier
    {
        int GetBoilerRoomHitOffset();
    }

    public interface IDynamicModifier
    {
        float GetMaxSpeedKnotOffset() => 0;
        float GetMaxSpeedKnotCoef() => 1;
        float GetMaxSpeedUpperLimit() => 100_000;
        float GetStandardTurnCoef() => 1;
        float GetEmergencyTurnCoef() => 1;
        float GetStandardTurnUpperLimit() => 100_000;
        float GetEmergencyTurnUpperLimit() => 100_000;
        float GetAccelerationUpperLimit() => 100_000;

        // It's all "physic" backed resitrction, they differ from communication / command malfunction induced problem
        bool IsEvasiveManeuverBlocked() => false;
        bool IsCourseChangeBlocked() => false; // EX: steering gear is jammed
        bool IsSpeedChangeBlocked() => false; // EX: DE 145, bridge destroyed
        bool IsEmergencyTurnBlocked() => false;
        float GetDesiredHeadingOffset() => 0;
        bool IsTurnPortBlocked() => false;
        bool IsTurnStarboardBlocked() => false;
    }

    public interface IDamageControlModifier
    {
        int GetDamageControlRatingOffset();
        bool IsFightingFireBlocked();
        bool IsDamageControlBlocked();
        float GetDamageControlDieRollOffset();
        bool IsBatteryDamageControlBlock() => false;
        float GetSeverityDieRollOffset() => 0;
        float GetFightingFireDieRollOffset() => 0;
    }

    public interface IElectronicSystemModifier
    {
        bool IsSearchLightDisabled() => false;
        (bool, bool) IsSearchLightDisabledOneSide(RapidFiringBatteryLocation location) => (false, false);
        bool IsFireControlRadarDisabled() => false; // Separate Fire Control Radar and Search Radar?
        bool IsSearchRadarDisabled() => false;
        bool IsSonarDisabled() => false;
    }

    // TODO: merge it into `IDynamicModifier`?
    public interface IDesiredSpeedUpdateToBoilerRoomBlocker // DE 124
    {
        bool IsDesiredSpeedCommandBlocked();
    }

    public interface ISmokeGeneratorModifier // TODO: Wait for implementation of smoke generator (though in the battle of yalu, this device is not used though)
    {
        bool IsSmokeGeneratorAvailable();
    }

    public interface IBatteryTargetChangeBlocker
    {
        bool IsBatteryTargetChangeBlocked();
    }

    public interface IFireControlSystemTargetChangeBlocker
    {
        bool IsFireControlSystemTargetChangeBlocked();
    }

    public interface IArmorModifier
    {
        float GetMainBeltArmorCoef();
    }

    public interface ISevereFloodingRollModifier
    {
        float GetSevereFloodingRollOffset();
    }

    public class ShipboardFireState : SubState
    {
        public override string Describe() => Localize(
            "Shipboard Fire Severity: {0}",
            severity
        );
    }

    public class SinkingState : SubState
    {
        public override void DoEndAt(ISubject subject)
        {
            var shipLog = subject as ShipLog; // This state can only be attached to a ShipLog
            if (shipLog != null)
            {
                shipLog.mapState = MapState.Destroyed; // Sunk
                shipLog.AddStringLog(Localize(
                    "Sunk due to sinking process finished"
                ));
            }
        }
        public override string Describe() => Localize(
            "Sunk When DE ended: {0}",
            DescribeLiftCycle()
        );
        // public override bool damageControlable => false;
        public override CampaignPersistence GetCampaignPersistence() => CampaignPersistence.DestinedSunk;
    }

    public class BatteryMountStatusModifier : SubState, IBatteryMountStatusModifier
    {
        public MountStatus mountStatus = MountStatus.Disabled;

        public MountStatus GetBatteryMountStatus()
        {
            return mountStatus;
        }
        public override string Describe() => Localize(
            "Battery Mount is disabled ({0})",
            DescribeLiftCycle()
        );

        public override bool IsBatteryRelated() => true;

        public override CampaignPersistence GetCampaignPersistence() => CampaignPersistence.Volatile;
    }

    public class RateOfFireModifier : SubState, IRateOfFireModifier
    {
        public float rateOfFireCoef = 0.5f;

        public float GetRateOfFireCoef()
        {
            return rateOfFireCoef;
        }

        public override string Describe() => Localize(
            "RateOfFireModifier({0}) ({1})",
            rateOfFireCoef, DescribeLiftCycle()
        );
        public override bool IsBatteryRelated() => true;

        public override CampaignPersistence GetCampaignPersistence() => CampaignPersistence.Volatile;
    }

    public class BatteryFireControlStatusDisabledModifier : SubState, IBatteryFireContrlStatusModifier
    {
        public bool GetBatteryFireControlDisabled() => true;

        public override string Describe() => Localize(
            "BatteryFireControlStatusDisabledModifier ({0})",
            DescribeLiftCycle()
        );
        public override bool IsBatteryRelated() => true;

        public override CampaignPersistence GetCampaignPersistence() => CampaignPersistence.Volatile;
    }

    public class ControlSystemDisabledModifier : SubState, IBatteryMountStatusModifier, IBatteryFireContrlStatusModifier
    {
        public MountStatus batteryMountStatus = MountStatus.Disabled;
        // public MountStatus batteryFireControlMountStatus = MountStatus.Disabled;

        public MountStatus GetBatteryMountStatus()
        {
            return batteryMountStatus;
        }

        public bool GetBatteryFireControlDisabled()
        {
            return true;
        }

        void ResetTrackingState(ISubject subject)
        {
            if (subject is FireControlSystemStatusRecord fcs) // Can only be attached to Fire Control System
            {
                fcs.SetTrackingTarget(null);
            }

            if (subject is BatteryStatus battery)
            {
                foreach (var _fcs in battery.fireControlSystemStatusRecords)
                {
                    _fcs.SetTrackingTarget(null);
                }
            }

            if (subject is ShipLog shipLog)
            {
                foreach (var bs in shipLog.batteryStatus)
                {
                    foreach (var _fcs in bs.fireControlSystemStatusRecords)
                    {
                        _fcs.SetTrackingTarget(null);
                    }
                }
            }
        }

        public override void DoEndAt(ISubject subject)
        {
            ResetTrackingState(subject);
        }

        public override string Describe() => Localize(
            "ControlSystemDisabledModifier({0}) ({1})",
            batteryMountStatus, DescribeLiftCycle()
        );
        public override bool IsBatteryRelated() => true;

        public override CampaignPersistence GetCampaignPersistence() => CampaignPersistence.Volatile;
    }

    public class FireControlValueModifier : SubState, IFireControlValueModifier
    {
        public float fireControlValueCoef = 0.5f;
        public float fireControlValueOffset = 0f;

        public float GetFireControlValueCoef()
        {
            return fireControlValueCoef;
        }

        public float GetFireControlValueOffset()
        {
            return fireControlValueOffset;
        }

        public override string Describe() => Localize(
            "FireControlValueModifier(coef={0}, offset={1}) ({2})",
            fireControlValueCoef, fireControlValueOffset, DescribeLiftCycle()
        );

        public override CampaignPersistence GetCampaignPersistence() => CampaignPersistence.Volatile;
    }

    public class RiskingInMagazineExplosion : SubState
    {
        public float explosionProbPercent = 10; // 10%
        public float sinkingThreshold = 25;

        public override void DoOnClockTick(ISubject subject, float deltaSeconds)
        {
            if (subject is ShipLog shipLog) // This sub state can only be attached to ShipLog
            {
                if (RandomUtils.D100F() <= explosionProbPercent)
                {
                    EndAt(shipLog);

                    shipLog.operationalState = DamageEffectChart.MaxEnum(shipLog.operationalState, ShipOperationalState.FloodingObstruction);
                    var DE = new SinkingState()
                    {
                        lifeCycle = StateLifeCycle.DieRollPassed,
                        dieRollThreshold = sinkingThreshold
                    };
                    DE.BeginAt(shipLog);
                    // TODO: Impelement "Move ship to a position equivalent to its location midway through MOVEMENT PHASE"
                }
            }
        }

        public override string Describe() => Localize(
            "RiskingInMagazineExplosion(explosionProbPercent={0}, sinkingThreshold={1}) ({2})",
            explosionProbPercent, sinkingThreshold, DescribeLiftCycle()
        );

        public override CampaignPersistence GetCampaignPersistence() => CampaignPersistence.Clear;
    }

    public class EngineRoomHitModifier : SubState, IEngineRoomHitModifier
    {
        public int engineRoomHitOffset;

        public int GetEngineRoomHitOffset() => engineRoomHitOffset;

        public override string Describe() => Localize(
            "EngineRoomHitModifier(offset={0}) ({1})",
            engineRoomHitOffset, DescribeLiftCycle()
        );

        public override CampaignPersistence GetCampaignPersistence() => CampaignPersistence.Volatile;
    }

    public class BoilerRoomHitModifier : SubState, IBoilerRoomHitModifier
    {
        public int boilerRoomHitOffset;

        public int GetBoilerRoomHitOffset() => boilerRoomHitOffset;

        public override string Describe() => Localize(
            "BoilerRoomHitModifier(offset={0}) ({1})",
            boilerRoomHitOffset, DescribeLiftCycle()
        );

        public override CampaignPersistence GetCampaignPersistence() => CampaignPersistence.Volatile;
    }

    public class SteamLineDamaged : SubState, IDynamicModifier // DE 120 (AB)
    {
        public float currentMaxSpeedOffset = 0;

        public override void DoOnClockTick(ISubject subject, float deltaSeconds)
        {
            var currentTurn = turnClock.accumulateSecond / 60;
            currentMaxSpeedOffset = Math.Max(-10, -currentTurn);
        }

        public float GetMaxSpeedKnotOffset() => currentMaxSpeedOffset;

        public override string Describe() => Localize(
            "SteamLineDamaged(speedOffset={0}) ({1})",
            currentMaxSpeedOffset, DescribeLiftCycle()
        );

        public override CampaignPersistence GetCampaignPersistence() => CampaignPersistence.Volatile;
    }

    public class DamageControlModifier : SubState, IDamageControlModifier
    {
        public int damageControlRatingOffset = 0;
        public bool isFightingFireBlocked = false;
        public bool isDamageControlBlocked = false;
        public float damageControlDieRollOffset = 0;
        public bool isBatteryDamageControlBlock = false;
        public float severityDieRollOffset = 0;
        public float fightingFireDieRollOffset = 0;
        public int GetDamageControlRatingOffset() => damageControlRatingOffset;
        public bool IsFightingFireBlocked() => isFightingFireBlocked;
        public bool IsDamageControlBlocked() => isDamageControlBlocked;
        public float GetDamageControlDieRollOffset() => damageControlDieRollOffset;
        public bool IsBatteryDamageControlBlock() => isBatteryDamageControlBlock;
        public float GetSeverityDieRollOffset() => severityDieRollOffset;
        public float GetFightingFireDieRollOffset() => fightingFireDieRollOffset;

        public override string Describe()
        {
            var lines = new List<string>()
            {
                damageControlRatingOffset != 0 ? $"{Localize("DC Rating Offset")}: {damageControlRatingOffset}" : null,
                isFightingFireBlocked ? Localize("Fighting Fire Blocked") : null,
                isDamageControlBlocked ? Localize("Damage Control Blocked") : null,
                damageControlDieRollOffset != 0 ? $"{Localize("DC Die Roll Offset")}: {damageControlDieRollOffset}" : null,
                isBatteryDamageControlBlock ? Localize("Battery DC Blocked") : null,
                severityDieRollOffset != 0 ? $"{Localize("Severity Die Roll Offset")}: {severityDieRollOffset}" : null,
                fightingFireDieRollOffset != 0 ? $"{Localize("Fighting Fire Die Roll Offset")}: {fightingFireDieRollOffset}" : null
            };
            return Localize("DamageControlModifier:") + string.Join(";", lines.Where(line => line != null)) + " | " + DescribeLiftCycle();
        }

        public override CampaignPersistence GetCampaignPersistence() => CampaignPersistence.Volatile;
    }

    public class DynamicModifier : SubState, IDynamicModifier
    {
        public float maxSpeedKnotOffset = 0;
        public float maxSpeedKnotCoef = 1;
        public float maxSpeedUpperLimit = 100000; // -1 denotes upperLimit is disabled
        public float standardTurnCoef = 1;
        public float emergencyTurnCoef = 1;
        public float standardTurnUpperLimit = 100000;
        public float emergencyTurnUpperLimit = 100000;
        public float accelerationUpperLimit = 100000;
        public bool isEvasiveManeuverBlocked = false;
        public bool isCourseChangeBlocked = false;
        public bool isSpeedChangeBlocked = false;
        public bool isTurnPortBlocked = false;
        public bool isTurnStarboardBlocked = false;
        public bool isEmergencyTurnBlocked = false;

        public override string Describe()
        {
            var lines = new List<string>()
            {
                maxSpeedKnotOffset != 0 ? Localize("Speed Offset: {0}", maxSpeedKnotOffset) : null,
                maxSpeedKnotCoef != 1 ? Localize("Speed Coef: {0}", maxSpeedKnotCoef) : null,
                maxSpeedUpperLimit != 100000 ? Localize("Speed Upper Limit: {0}", maxSpeedUpperLimit) : null,
                standardTurnCoef != 1 ? Localize("Std Turn Coef: {0}", standardTurnCoef) : null,
                emergencyTurnCoef != 1 ? Localize("Emer Turn Coef: {0}", emergencyTurnCoef) : null,
                standardTurnUpperLimit != 100000 ? Localize("Std Turn Upper Limit: {0}", standardTurnUpperLimit) : null,
                emergencyTurnUpperLimit != 100000 ? Localize("Emer Turn Upper Limit: {0}", emergencyTurnUpperLimit) : null,
                accelerationUpperLimit != 100000 ? Localize("Accel Upper Limit: {0}", accelerationUpperLimit) : null,
                isEvasiveManeuverBlocked ? Localize("Evasive Blocked") : null,
                isCourseChangeBlocked ? Localize("Course Change Blocked") : null,
                isSpeedChangeBlocked ? Localize("Speed Change Blocked") : null,
                isTurnPortBlocked ? Localize("Turn Port Blocked") : null,
                isTurnStarboardBlocked ? Localize("Turn Starboard Blocked") : null,
                isEmergencyTurnBlocked ? Localize("Emer Turn Blocked") : null
            };
            return Localize("DynamicModifier:") + string.Join(";", lines.Where(line => line != null)) + " | " + DescribeLiftCycle();
        }

        public float GetMaxSpeedKnotOffset() => maxSpeedKnotOffset;
        public float GetMaxSpeedKnotCoef() => maxSpeedKnotCoef;
        public float GetMaxSpeedUpperLimit() => maxSpeedUpperLimit;
        public float GetStandardTurnCoef() => standardTurnCoef;
        public float GetEmergencyTurnCoef() => emergencyTurnCoef;
        public float GetStandardTurnUpperLimit() => standardTurnUpperLimit;
        public float GetEmergencyTurnUpperLimit() => emergencyTurnUpperLimit;

        public float GetAccelerationUpperLimit() => accelerationUpperLimit;
        public bool IsEvasiveManeuverBlocked() => isEvasiveManeuverBlocked;
        public bool IsCourseChangeBlocked() => isCourseChangeBlocked;
        public bool IsSpeedChangeBlocked() => isSpeedChangeBlocked;
        public bool IsTurnPortBlocked() => isTurnPortBlocked;
        public bool IsTurnStarboardBlocked() => isTurnStarboardBlocked;
        public bool IsEmergencyTurnBlocked() => isEmergencyTurnBlocked;

        public override CampaignPersistence GetCampaignPersistence() => CampaignPersistence.Volatile;
    }

    public class FeedwaterPumpDamaged : SubState, IDynamicModifier
    {
        public float lostAllPropulsionPercentage = 15;
        public bool hasLoseAllPropulsion = false;
        public override void DoOnClockTick(ISubject subject, float deltaSeconds)
        {
            if (!hasLoseAllPropulsion && RandomUtils.D100F() <= lostAllPropulsionPercentage)
            {
                hasLoseAllPropulsion = true;
            }
        }

        public float GetMaxSpeedKnotCoef() => hasLoseAllPropulsion ? 0 : 1;

        public override string Describe() => Localize(
            "FeedwaterPumpDamaged(lostAllPropulsionPercentage={0}, hasLoseAllPropulsion={1}) ({2})",
            lostAllPropulsionPercentage, hasLoseAllPropulsion, DescribeLiftCycle()
        );

        public override CampaignPersistence GetCampaignPersistence() => CampaignPersistence.Volatile;
    }

    public class RudderDamaged : SubState, IDynamicModifier
    {
        public float currentDesiredHeadingOffset;
        public override void DoOnClockTick(ISubject subject, float deltaSeconds)
        {
            if (RandomUtils.D100F() <= 40)
            {
                currentDesiredHeadingOffset = RandomUtils.D100F() <= 50 ? -15 : 15;
            }
            else
            {
                currentDesiredHeadingOffset = 0;
            }
        }
        public bool IsEvasiveManeuverBlocked() => true;
        public bool IsEmergencyTurnBlocked() => true;

        public override string Describe() => Localize(
            "RudderDamaged(currentDesiredHeadingOffset={0}, ({1})",
            currentDesiredHeadingOffset, DescribeLiftCycle()
        );

        public override CampaignPersistence GetCampaignPersistence() => CampaignPersistence.Volatile;
    }

    public class FuelSupplyDamaged : SubState, IDynamicModifier
    {
        public bool active = false;
        public override void DoOnClockTick(ISubject subject, float deltaSeconds)
        {
            if (RandomUtils.D100F() <= 10)
            {
                active = !active;
            }
        }

        public float GetMaxSpeedKnotCoef()
        {
            return active ? 0.5f : 1;
        }

        public override string Describe() => Localize(
            "FuelSupplyDamaged(active={0} (if active, speed coef=0.5)), ({1})",
            active, DescribeLiftCycle()
        );

        public override CampaignPersistence GetCampaignPersistence() => CampaignPersistence.Volatile;
    }

    public class EngineRoomCommunicationDamaged : SubState, IDesiredSpeedUpdateToBoilerRoomBlocker
    {
        public bool blocked;
        public override void DoOnClockTick(ISubject subject, float deltaSeconds)
        {
            blocked = RandomUtils.D100F() >= 75;
        }

        public bool IsDesiredSpeedCommandBlocked() => blocked;

        public override string Describe() => Localize(
            "EngineRoomCommunicationDamaged(blocked={0}) ({1})",
            blocked, DescribeLiftCycle()
        );

        public override CampaignPersistence GetCampaignPersistence() => CampaignPersistence.Volatile;
    }

    public class TorpedoMountDamaged : SubState, ITorpedoMountStatusModifier
    {
        public MountStatus currentStatus;
        public float operationalPercentange = 50;
        public override void DoOnClockTick(ISubject subject, float deltaSeconds)
        {
            currentStatus = RandomUtils.D100F() <= operationalPercentange ? MountStatus.Operational : MountStatus.Disabled;
        }

        public MountStatus GetTorpedoMountStatus() => currentStatus;

        public override string Describe() => Localize(
            "TorpedoMountDamaged(currentStatus={0},operationalPercentange={1}) ({2})",
            currentStatus, operationalPercentange, DescribeLiftCycle()
        );

        public override CampaignPersistence GetCampaignPersistence() => CampaignPersistence.Volatile;
    }

    public class TorpedoMountModifer : SubState, ITorpedoMountStatusModifier
    {
        public MountStatus status;
        public MountStatus GetTorpedoMountStatus() => status;

        public override string Describe() => Localize(
            "TorpedoMountModifer(status={0}) ({1})",
            status, DescribeLiftCycle()
        );

        public override CampaignPersistence GetCampaignPersistence() => CampaignPersistence.Volatile;
    }

    public class SmokeGeneratorDamaged : SubState, ISmokeGeneratorModifier
    {
        public bool isSmokeGeneratorAvailableCurrent;
        public float availablePercent = 50;
        public override void DoOnClockTick(ISubject subject, float deltaSeconds)
        {
            isSmokeGeneratorAvailableCurrent = RandomUtils.D100F() <= availablePercent;
        }

        public bool IsSmokeGeneratorAvailable() => isSmokeGeneratorAvailableCurrent;

        public override string Describe() => Localize(
            "SmokeGeneratorDamaged(IsSmokeGeneratorAvailableCurrent={0},availablePercent={1}) ({2})",
            isSmokeGeneratorAvailableCurrent, availablePercent, DescribeLiftCycle()
        );

        public override CampaignPersistence GetCampaignPersistence() => CampaignPersistence.Volatile;
    }

    // DE 128
    public class SectorFireState : SubState, ILocalizedDirectionalFireControlValueModifier // , ILocalizedTorpedoMountStatusModifier
    {
        // public bool disableTorpedo;

        public enum SectionVLocation
        {
            Front,
            Midship,
            After
        }
        public SectionVLocation fireAndSmokeVLocation;

        public static SectionVLocation GetSectionLocation(MountLocation mountLocation)
        {
            if (mountLocation <= MountLocation.StarboardForward)
                return SectionVLocation.Front;
            else if (mountLocation <= MountLocation.StarboardMidship)
                return SectionVLocation.Midship;
            return SectionVLocation.After;
        }

        public float GetFireControlValueOffset(MountLocation mountLocation, float bearingRelativeToBowDeg)
        {
            if (mountLocation == MountLocation.NotSpecified)
                return 0;

            var toFront = bearingRelativeToBowDeg <= 45 || bearingRelativeToBowDeg >= 315;
            var toAfter = bearingRelativeToBowDeg >= 135 && bearingRelativeToBowDeg <= 225;

            var mountSectionLocation = GetSectionLocation(mountLocation);

            if (mountSectionLocation == SectionVLocation.Front) // Forward (though include unspecified)
            {
                if (fireAndSmokeVLocation == SectionVLocation.Front || toAfter)
                {
                    return -1;
                }
                return 0;
            }
            else if (mountSectionLocation == SectionVLocation.Midship) // Midship
            {
                if (fireAndSmokeVLocation == SectionVLocation.Midship)
                    return -1;
                if (fireAndSmokeVLocation == SectionVLocation.Front && toFront)
                    return -1;
                if (fireAndSmokeVLocation == SectionVLocation.After && toAfter)
                    return -1;
                return 0;
            }
            else // after
            {
                if (mountSectionLocation == SectionVLocation.After || toFront)
                {
                    return -1;
                }
                return 0;
            }
        }

        // public MountStatus GetTorpedoMountStatus(MountLocation mountLocation)
        // {
        //     // TODO: Track if torpedo is deck torpedo (or submerged etc)
        //     var mountSectionLocation = GetSectionLocation(mountLocation);
        //     return mountSectionLocation == fireAndSmokeVLocation ? MountStatus.Disabled : MountStatus.Operational;
        // }

        public override string Describe() => Localize(
            "SectorFireState(fireAndSmokeLocation={0}) ({1})",
            fireAndSmokeVLocation, DescribeLiftCycle()
        );

        public override CampaignPersistence GetCampaignPersistence() => CampaignPersistence.Clear;
    }

    public class MainPowerplantOOA : SubState, IRateOfFireModifier, IDamageControlModifier, IElectronicSystemModifier
    {
        // TODO: Add command related things

        // Optional effect
        public float rateOfFireCoef = 1;
        public bool isDamageControlBlocked = false;

        public float GetRateOfFireCoef() => rateOfFireCoef;

        public int GetDamageControlRatingOffset() => 0;
        public bool IsFightingFireBlocked() => false;
        public bool IsDamageControlBlocked() => isDamageControlBlocked;
        public float GetDamageControlDieRollOffset() => 0;

        public bool IsSearchLightDisabled() => true;
        public bool IsFireControlRadarDisabled() => true; // Separate Fire Control Radar and Search Radar?
        public bool IsSearchRadarDisabled() => true;
        public bool IsSonarDisabled() => true;

        public override string Describe() => Localize(
            "MainPowerplantOOA(rateOfFireCoef={0},isDamageControlBlocked={1}) ({2})",
            rateOfFireCoef, isDamageControlBlocked, DescribeLiftCycle()
        );

        public override CampaignPersistence GetCampaignPersistence() => CampaignPersistence.Volatile;
    }

    public class BatteryTargetChangeBlocker : SubState, IBatteryTargetChangeBlocker
    {
        public bool isBatteryTargetChangeBlocked = true;

        public bool IsBatteryTargetChangeBlocked() => isBatteryTargetChangeBlocked;

        public override string Describe() => Localize(
            "BatteryTargetChangeBlocker(blocked={0}) ({1})",
            isBatteryTargetChangeBlocked, DescribeLiftCycle()
        );

        public override CampaignPersistence GetCampaignPersistence() => CampaignPersistence.Volatile;
    }

    public class ElectronicSystemModifier : SubState, IElectronicSystemModifier
    {
        public bool isSearchLightDisabled = false;
        public bool isFireControlRadarDisabled = false;
        public bool isSearchRadarDisabled = false;
        public bool isSonarDisabled = false;

        public bool IsSearchLightDisabled() => isSearchLightDisabled;
        public bool IsFireControlRadarDisabled() => isFireControlRadarDisabled; // Separate Fire Control Radar and Search Radar?
        public bool IsSearchRadarDisabled() => isSearchRadarDisabled;
        public bool IsSonarDisabled() => isSonarDisabled;

        public override string Describe()
        {
            var lines = new List<string>()
            {
                isSearchLightDisabled ? Localize("Search Light Disabled") : null,
                isFireControlRadarDisabled ? Localize("Fire Control Radar Disabled") : null,
                isSearchRadarDisabled ? Localize("Search Radar Disabled") : null,
                isSonarDisabled ? Localize("Sonar Disabled") : null
            };
            return Localize("ElectronicSystemModifier:") + string.Join(";", lines.Where(line => line != null)) + " | " + DescribeLiftCycle();
        }

        public override CampaignPersistence GetCampaignPersistence() => CampaignPersistence.Volatile;
    }

    public class ArmorModifier : SubState, IArmorModifier
    {
        public float mainBeltArmorCoef;
        public float GetMainBeltArmorCoef() => mainBeltArmorCoef;

        public override string Describe() => Localize(
            "ArmorModifier(mainBeltArmorCoef={0}) {1}",
            mainBeltArmorCoef, DescribeLiftCycle()
        );

        public override CampaignPersistence GetCampaignPersistence() => CampaignPersistence.Volatile;
    }


    // M6 - Flooding Damage Determination
    public class SevereFloodingState : SubState
    {
        public float dieRollOffset = 0;

        public override bool damageControllable => true;

        public override void DoOnClockTick(ISubject subject, float deltaSeconds)
        {
            var shipLog = subject as ShipLog;
            if (shipLog != null)
                return;

            var persistentOffset = shipLog.GetSubStates<ISevereFloodingRollModifier>().Select(mod => mod.GetSevereFloodingRollOffset()).DefaultIfEmpty(0).Max();

            var d = RandomUtils.D100F() + dieRollOffset + persistentOffset;
            var c = damageControlApplied;

            if (d <= (c ? 15 : 4))
            {
                // Counter-flooding temporarily succesful. Ship on even keel.
            }
            else if (d <= (c ? 18 : 13))
            {
                // Permanent loss of half the remaining ammunition supply for all [PRIMARY/SECONDARY] battery mounts in one section.
                var locations = shipLog.batteryStatus.SelectMany(bs => bs.mountStatus).Select(mnt => mnt.mountLocation).ToList();
                if (locations.Count > 0)
                {
                    var location = RandomUtils.Sample(locations);
                    foreach (var battery in shipLog.batteryStatus)
                    {
                        var p = ((float)battery.mountStatus.Count(m => m.mountLocation == location)) / battery.mountStatus.Count;
                        battery.ammunition.CostPercent(p);
                    }
                }
            }
            else if (d <= (c ? 24 : 26))
            {
                // Permanent loss of one primary battery mount due to flooding in barbette.
                if (shipLog.batteryStatus.Count > 0 && shipLog.batteryStatus[0].mountStatus.Count > 0)
                {
                    var mount = RandomUtils.Sample(shipLog.batteryStatus[0].mountStatus);
                    DamageEffectChart.SetOOA(mount);
                }
            }
            else if (d <= (c ? 36 : 34))
            {
                // List to [PORTS/STARBOARD]. Secondary battery guns are unable to fire. Adjust the total from CHART H by -2 for primary battery guns.
                if (shipLog.batteryStatus.Count > 0)
                {
                    var DE = new FireControlValueModifier()
                    {
                        lifeCycle = StateLifeCycle.GivenTime,
                        cause = Localize("M6: List to [PORTS/STARBOARD]"),
                        fireControlValueOffset = -2
                    };
                    DE.BeginAt(shipLog.batteryStatus[0]);
                }
                if (shipLog.batteryStatus.Count > 1)
                {
                    var DE = new BatteryMountStatusModifier()
                    {
                        lifeCycle = StateLifeCycle.GivenTime,
                        cause = Localize("M6: List to [PORTS/STARBOARD]"),
                    };
                    DE.BeginAt(shipLog.batteryStatus[1]);
                }
            }
            else if (d <= 41)
            {
                // Heavy list to [PORT/STARBOARD]. Secondary battery guns are unable to fire.
                // Adjust the total from CHART H by -3 for primary battery guns.
                // If hit on location 5V (Main Belt) during next game turn, 
                // use 1/2 of 5V armor as listed on the Ship Log when checking for shell penetration or torpedo damage.
                // No Luanch or recovery of aircraft possible.
                if (shipLog.batteryStatus.Count > 0)
                {
                    var DE = new FireControlValueModifier()
                    {
                        lifeCycle = StateLifeCycle.GivenTime,
                        cause = Localize("M6: Heavy list to [PORT/STARBOARD]"),
                        fireControlValueOffset = -3
                    };
                    DE.BeginAt(shipLog.batteryStatus[0]);
                }
                if (shipLog.batteryStatus.Count > 1)
                {
                    var DE = new BatteryMountStatusModifier()
                    {
                        lifeCycle = StateLifeCycle.GivenTime,
                        cause = Localize("M6: Heavy list to [PORT/STARBOARD]"),
                    };
                    DE.BeginAt(shipLog.batteryStatus[1]);
                }
                var DE3 = new ArmorModifier()
                {
                    lifeCycle = StateLifeCycle.GivenTime,
                    mainBeltArmorCoef = 0.5f,
                    cause = Localize("M6: Heavy list to [PORT/STARBOARD]"),
                };
                DE3.BeginAt(shipLog);
            }
            else if (d <= 46)
            {
                // Additional flooding - permanent loss 1 knot speed. Loss of power to all searchlights.
                // No launch or recovery of aircraft possible.
                shipLog.dynamicStatus.maxSpeedKnotsOffset += -1;

                var DE = new ElectronicSystemModifier()
                {
                    lifeCycle = StateLifeCycle.GivenTime,
                    isSearchLightDisabled = true
                };
                DE.BeginAt(shipLog);
                // TODO: Process aircraft relaled stuff
            }
            else if (d <= 51)
            {
                // Additional flooding - permannent loss of 1 knot speed. No radio communication to ships or aircraft. 
                // Reduce Flag Command Rating by 1.
                shipLog.dynamicStatus.maxSpeedKnotsOffset += -1;
                // TODO: Command & Comm
            }
            else if (d <= 56)
            {
                // Permanent loss of all secondary battery guns in one section due to flooding. 
                // No launch or recovery of aircraft possible
                if (shipLog.batteryStatus.Count > 1 && shipLog.batteryStatus[1].mountStatus.Count > 0)
                {
                    var location = RandomUtils.Sample(shipLog.batteryStatus[1].mountStatus.Select(mnt => mnt.mountLocation).ToList());
                    foreach (var mount in shipLog.batteryStatus[1].mountStatus.Where(mnt => mnt.mountLocation == location))
                    {
                        DamageEffectChart.SetOOA(mount);
                    }
                }
                // TOOD: Aircraft
            }
            else if (d <= 61)
            {
                // Additional flooding - add DP equal to 1x a roll of percentile dice. Permannent loss of 1 DCR
                // shipLog.damagePoint += RandomUtils.D100F();
                shipLog.AddDamagePoint(RandomUtils.D100F());
                shipLog.damageControlRatingHits += 1;
            }
            else if (d <= 66)
            {
                // Additional flooding - add DP equal to 2x a roll of percentile dice. Reduce Bridge Command Rating by 1
                // shipLog.damagePoint += RandomUtils.D100F() * 2;
                shipLog.AddDamagePoint(RandomUtils.D100F() * 2);
                // TODO: Command
            }
            else if (d <= (c ? 76 : 71))
            {
                // All [PRIMARY/SECONDARY] battery fire control systems OOA during next game turn.
                // B1L or B2L order must be given during the next Command Phase for local control (LCS) of battey.
                var DE = new BatteryFireControlStatusDisabledModifier()
                {
                    lifeCycle = StateLifeCycle.GivenTime,
                    cause = Localize(
                        "M6: All [PRIMARY/SECONDARY] battery fire control systems OOA during next game turn"
                    )
                };
                DE.BeginAt(shipLog);
            }
            else if (d <= (c ? 83 : 81))
            {
                // Flooding in shaft tunnel. One prop/shaft is OOA
                shipLog.dynamicStatus.propulsionShaftHits += 1;
            }
            else if (d <= 94)
            {
                // One [ENGINE ROOM/BOILER ROOM] is OOA due to flooding
                if (RandomUtils.NextFloat() < 0.5f)
                {
                    shipLog.dynamicStatus.engineRoomHits += 1;
                }
                else
                {
                    shipLog.dynamicStatus.boilerRoomHits += 1;
                }
            }
            else if (d <= 98)
            {
                // Damage to main feedwater pumps.
                // A roll of 01-20 (01-30) at the beginning of any MOVEMENT PHASE causes the ship to lose all propulsion (as if Bridge Command SS were ordered).
                // Momentum rules apply. If all propulsion is lost, rolls continue and ship may not begin acceleration until turn following a roll of 01-20 (01-15)
                var DE = new FeedwaterPumpDamaged()
                {
                    lifeCycle = StateLifeCycle.DieRollPassed,
                    lostAllPropulsionPercentage = c ? 20 : 30, // "Active"
                    dieRollThreshold = c ? 20 : 15, // Restore
                    cause = Localize("M6, Damage to main feedwater pump")
                };
                DE.BeginAt(shipLog);
            }
            else
            {
                if (!c)
                {
                    // Ship capsizes and begins to sink. Ship will remain an obstruction for all following turns until a roll of 01-25
                    shipLog.operationalState = ShipOperationalState.FloodingObstruction;
                    var state = new SinkingState();
                    state.BeginAt(shipLog);
                }
            }
        }

        public override string Describe() => Localize(
            "SevereFloodingState(dieRollOffset={0}) {1}",
            dieRollOffset, DescribeLiftCycle()
        );

        public override CampaignPersistence GetCampaignPersistence() => CampaignPersistence.Clear;
    }

    public class SevereFloodingRollModifier : SubState, ISevereFloodingRollModifier
    {
        public float severeFloodingRollOffset;
        public float GetSevereFloodingRollOffset() => severeFloodingRollOffset;

        public override string Describe() => Localize(
            "SevereFloodingRollModifier(severeFloodingRollOffset={0}) {1}",
            severeFloodingRollOffset, DescribeLiftCycle()
        );

        public override CampaignPersistence GetCampaignPersistence() => CampaignPersistence.Volatile;
    }

    public class LossOfCommunicationToFireControlSystemState : SubState, IFireControlSystemTargetChangeBlocker
    {
        public bool isFireControlSystemTargetChangeBlocked;
        public float succPercentage = 40f;

        public override void DoOnClockTick(ISubject subject, float deltaSeconds)
        {
            isFireControlSystemTargetChangeBlocked = RandomUtils.D100F() >= succPercentage;
        }

        public bool IsFireControlSystemTargetChangeBlocked() => isFireControlSystemTargetChangeBlocked;

        public override string Describe() => Localize(
            "LossOfCommunicationToFireControlSystemState(blocked={0},succPercentage={1}) {2}",
            isFireControlSystemTargetChangeBlocked, succPercentage, DescribeLiftCycle()
        );

        public override CampaignPersistence GetCampaignPersistence() => CampaignPersistence.Volatile;
    }

    public class LossOfCommunicationsAndPowerToSearchLight : SubState, IElectronicSystemModifier
    {
        public RapidFiringBatteryLocation location;
        public float succPercentage = 30;
        public bool isSearchLightDisabled;

        public (bool, bool) IsSearchLightDisabled(RapidFiringBatteryLocation checkLocation) // (matched, value)
        {
            if (checkLocation == location)
            {
                return (true, isSearchLightDisabled);
            }
            return (false, false);
        }

        public override string Describe() => Localize(
            "LossOfCommunicationsAndPowerToSearchLight(location={0},succPercentage={1},isSearchLightDisabled={2}) {3}",
            location, succPercentage, isSearchLightDisabled, DescribeLiftCycle()
        );

        public override CampaignPersistence GetCampaignPersistence() => CampaignPersistence.Volatile;
    }

    public class LossOfCommunicationToEngineRoom : SubState, IDynamicModifier
    {
        public float succPercentage = 50;
        public bool isSpeedChangeBlocked;

        public override void DoOnClockTick(ISubject subject, float deltaSeconds)
        {
            isSpeedChangeBlocked = RandomUtils.D100F() >= succPercentage;
        }

        public bool IsSpeedChangeBlocked() => isSpeedChangeBlocked;

        public override string Describe() => Localize(
            "LossOfCommunicationToEngineRoom(succPercentage={0},isSpeedChangeBlocked={1}) {2}",
            succPercentage, isSpeedChangeBlocked, DescribeLiftCycle()
        );

        public override CampaignPersistence GetCampaignPersistence() => CampaignPersistence.Volatile;
    }

    public class BatteryHandlingRoomAbandoned : SubState // It works as a "countdown" to trigger OOA of a mount 
    {
        public override void DoEndAt(ISubject subject)
        {
            if (subject is MountStatusRecord mountStatus)
            {
                DamageEffectChart.SetOOA(mountStatus);
            }
        }

        public override string Describe() => Localize(
            "BatteryHandlingRoomAbandoned {0}",
            DescribeLiftCycle()
        );

        public override CampaignPersistence GetCampaignPersistence() => CampaignPersistence.Volatile;
    }

    // Asterisk labeled family, they may doesn't have many functionally, just a label that some 
    public class OneShotDamageEffectHappend : SubState
    {
        public string damageEffectCode;

        public override string Describe() => Localize(
            "OneShotDamageEffectHappend: {0}",
            damageEffectCode
        );

        public override CampaignPersistence GetCampaignPersistence() => CampaignPersistence.Clear;
    }

    public class DE602DyanmicModifier : SubState, IDynamicModifier
    {
        public bool isSpeedChangeBlocked;
        public override void DoOnClockTick(ISubject subject, float deltaSeconds)
        {
            if (subject is ShipLog shipLog)
            {
                var dt = shipLog.GetDamageTier();
                float threshold;
                if (dt <= 4)
                    threshold = 50;
                else if (dt <= 7)
                    threshold = 30;
                else
                    threshold = 10;
                isSpeedChangeBlocked = RandomUtils.D100F() >= threshold;
            }
        }

        public bool IsEmergencyTurnBlocked() => true;
        public bool IsEvasiveManeuverBlocked() => true;
        public float GetStandardTurnCoef() => 0.5f;
        public bool IsSpeedChangeBlocked() => isSpeedChangeBlocked;

        public override string Describe() => Localize(
            "DE602-DyanmicModifier: isSpeedChangeBlocked={0}), Std Turn Coef: 0.5, Emer Turn Blocked, Evasive Man. Blocked",
            isSpeedChangeBlocked
        );

        public override CampaignPersistence GetCampaignPersistence() => CampaignPersistence.Volatile;
    }

    public class DE607DyanmicModifier : SubState, IDynamicModifier
    {
        public bool isCourceChangeBlocked;

        public override void DoOnClockTick(ISubject subject, float deltaSeconds)
        {
            if (subject is ShipLog shipLog)
            {
                var dt = shipLog.GetDamageTier();

                float threshold;
                if (dt <= 4)
                    threshold = 40;
                else if (dt <= 8)
                    threshold = 20;
                else
                    threshold = 10;

                isCourceChangeBlocked = RandomUtils.D100F() >= threshold;
            }
        }

        public override string Describe() => Localize(
            "DE607DyanmicModifier: isCourceChangeBlocked={0}",
            isCourceChangeBlocked
        );

        public override CampaignPersistence GetCampaignPersistence() => CampaignPersistence.Volatile;
    }

    public class ShipSettleState : SubState, IDynamicModifier
    {
        public float maxSpeedUpperLimit;
        public bool maxSpeedUpperLimitApplied;
        public float maxSpeedUpperLimitAppliedThreshold = -1;
        public float sinkingThreshold = -1;
        public float isCourseChangeBlockedThreshold = -1;
        public bool isCourseChangeBlocked;

        public override void DoOnClockTick(ISubject subject, float deltaSeconds)
        {
            var d100 = RandomUtils.D100F();

            if (d100 <= sinkingThreshold)
            {
                if (subject is ShipLog shipLog)
                {
                    shipLog.mapState = MapState.Destroyed;
                    shipLog.AddStringLog(Localize(
                        "Sunk due to settle's sinking roll"
                    ));
                }
            }
            if (d100 <= maxSpeedUpperLimitAppliedThreshold)
            {
                maxSpeedUpperLimitApplied = true;
            }
            if (d100 <= isCourseChangeBlockedThreshold)
            {
                isCourseChangeBlocked = true;
            }
        }

        public float GetMaxSpeedUpperLimit() => maxSpeedUpperLimitApplied ? maxSpeedUpperLimit : 100_000;
        public bool IsCourseChangeBlocked() => isCourseChangeBlocked;

        public override string Describe()
        {
            var lines = new List<string>()
            {
                maxSpeedUpperLimitApplied ? Localize("Speed Upper Limit: {0}", maxSpeedUpperLimit) : null,
                maxSpeedUpperLimitAppliedThreshold >= 0 ? Localize("Speed Limit Threshold: {0}", maxSpeedUpperLimitAppliedThreshold) : null,
                sinkingThreshold >= 0 ? Localize("Sinking Threshold: {0}", sinkingThreshold) : null,
                isCourseChangeBlockedThreshold >= 0 ? Localize("Course Block Threshold: {0}", isCourseChangeBlockedThreshold) : null,
                isCourseChangeBlocked ? Localize("Course Change Blocked") : null
            };
            return Localize("ShipSettleState:") + string.Join(";", lines.Where(line => line != null));
        }

        public override CampaignPersistence GetCampaignPersistence() => CampaignPersistence.DestinedSunk;
    }

    // DE 609, Flooding due to splinter and shell damage near waterline
    public class DE609Effect : SevereFloodingState
    {
        public override void DoOnClockTick(ISubject subject, float deltaSeconds)
        {
            if (subject is ShipLog shipLog)
            {
                var seaState = NavalGameState.Instance.scenarioState.seaStateBeaufort;
                var offset = 10;
                if (seaState <= 3)
                { }
                else if (seaState <= 5)
                    offset = 10;
                else if (seaState <= 6)
                    offset = 20;
                else if (seaState <= 7)
                    offset = 30;
                else
                    offset = 40;

                var damageTier = shipLog.GetDamageTier();
                var damageTierRollOffset = damageTier >= 6 ? 30 : 0;

                if (RandomUtils.D100F() + offset + damageTierRollOffset >= 70)
                {
                    base.DoOnClockTick(subject, deltaSeconds);
                }
            }
        }

        public override string Describe() => Localize("DE609Effect");

        public override CampaignPersistence GetCampaignPersistence() => CampaignPersistence.Volatile;
    }

    public class FiringCircuitDamagedMaster : SubState
    {
        public float currentRateOfFireCoef;
        public override void DoOnClockTick(ISubject subject, float deltaSeconds)
        {
            var d100 = RandomUtils.D100F();
            if (d100 <= 25)
            {
                currentRateOfFireCoef = 1;
            }
            else if (d100 <= 75)
            {
                currentRateOfFireCoef = 0.5f;
            }
            else
            {
                currentRateOfFireCoef = 0;
            }
        }

        public override string Describe() => Localize(
            "FiringCircuitDamagedMaster(currentRateOfFireCoef={0}) {1}",
            currentRateOfFireCoef, DescribeLiftCycle()
        );

        public override CampaignPersistence GetCampaignPersistence() => CampaignPersistence.Clear;
    }

    public class FiringCircuitDamagedWorker : SubState, IRateOfFireModifier, IBarrageFireBlocker // DE *615
    {
        public float GetRateOfFireCoef()
        {
            var master = EntityManager.Instance.Get<FiringCircuitDamagedMaster>(dependentObjectId);
            return master.currentRateOfFireCoef;
        }

        public bool IsBarrageFireBlocked() => true;

        public override string Describe() => Localize(
            "FiringCircuitDamagedWorker"
        );

        public override CampaignPersistence GetCampaignPersistence() => CampaignPersistence.Clear;
    }

    public class DE806DynamicModifier : SubState, IDynamicModifier
    {
        public bool restored;
        public float maxSpeedKnotCoef = 0;

        public override void DoOnClockTick(ISubject subject, float deltaSeconds)
        {
            if (!restored)
            {
                if (RandomUtils.D100F() <= 30)
                {
                    restored = true;
                    maxSpeedKnotCoef = 0.5f;
                    if (RandomUtils.D100F() <= 60)
                    {
                        EndAt(subject);
                    }
                }
            }
        }

        public override string Describe() => Localize(
            "DE806DynamicModifier(restored={0}, maxSpeedKnotCoef={1}) {2}",
            restored, maxSpeedKnotCoef, DescribeLiftCycle()
        );

        public override CampaignPersistence GetCampaignPersistence() => CampaignPersistence.Volatile;
    }

    public class BatteryDamaged : SubState, IBatteryMountStatusModifier
    {
        public MountStatus status = MountStatus.Disabled;
        public float operationalPercentage;

        public MountStatus GetBatteryMountStatus() => status;
        public override void DoOnClockTick(ISubject subject, float deltaSeconds)
        {
            if (RandomUtils.D100F() <= operationalPercentage)
            {
                status = MountStatus.Operational;
            }
            else
            {
                status = MountStatus.Disabled;
            }
        }

        public override string Describe() => Localize(
            "BatteryDamaged(status={0}, operationalPercentage={1}) {2}",
            status, operationalPercentage, DescribeLiftCycle()
        );
        public override bool IsBatteryRelated() => true;

        public override CampaignPersistence GetCampaignPersistence() => CampaignPersistence.Volatile;
    }

    public class PlaceholderState : SubState // Can used to as a parent for some dependent sub-states to apply lifecycle constrait
    {
        public override string Describe() => Localize(
            "PlaceholderState({0})",
            DescribeLiftCycle()
        );
    }

    public class FireInExplodePotentialCargoHold : SubState // DE 901 for ship with a cargo in any hold of AM, FS, FO or FA.
    {
        public override string Describe() => Localize(
            "FireInExplodePotentialCargoHold({0})",
            DescribeLiftCycle()
        );

        public override void DoOnClockTick(ISubject subject, float deltaSeconds)
        {
            if(RandomUtils.D100F() <= 80) // 60 or 80? The rulebook don't give a clear description
            {
                // Successful. No additional damage
            }
            else
            {
                // Ship destroyed by explosion and will remain an obstruction for all following turns until a roll of 01-40
                if(subject is ShipLog subjectShip)
                {
                    subjectShip.operationalState = DamageEffectChart.MaxEnum(subjectShip.operationalState, ShipOperationalState.FloodingObstruction);
                    var damageEffect = new SinkingState()
                    {
                        lifeCycle = StateLifeCycle.DieRollPassed,
                        dieRollThreshold = 40,
                        cause = Localize(
                            "DE 901: Severe fire in hold containing munitions, flammable stores or fuel."
                        )
                    };
                    damageEffect.BeginAt(subject);
                }
            }
        }

        public override CampaignPersistence GetCampaignPersistence() => CampaignPersistence.Clear;
    }

    public class PropulsionSystemDamaged : SubState, IDynamicModifier // DE 902
    {
        public float currentMaxSpeedOffset = 0;

        public override void DoOnClockTick(ISubject subject, float deltaSeconds)
        {
            var currentTurn = turnClock.accumulateSecond / 120;
            currentMaxSpeedOffset = -currentTurn;
        }

        public float GetMaxSpeedKnotOffset() => currentMaxSpeedOffset;

        public override string Describe() => Localize(
            "PropulsionSystemDamaged(speedOffset={0}) ({1})",
            currentMaxSpeedOffset, DescribeLiftCycle()
        );

        public override CampaignPersistence GetCampaignPersistence() => CampaignPersistence.Volatile;
    }

    public class ExcessiveFlooding : SubState, IDynamicModifier // DE 909
    {
        public float GetMaxSpeedKnotOffset() => -6;

        public override string Describe() => Localize(
            "ExcessiveFlooding({0})",
            DescribeLiftCycle()
        );

        public override CampaignPersistence GetCampaignPersistence() => CampaignPersistence.Maintained;
    }

    public class UncontrolledFlooding : SubState // DE 910
    {
        public override void DoOnClockTick(ISubject subject, float deltaSeconds)
        {
            var d100 = RandomUtils.D100F();
            if(d100 <= 40)
            {
                // Flooding under control
            }
            else
            {
                // Ship sinks
                var shipLog = subject as ShipLog; // This state can only be attached to a ShipLog
                if (shipLog != null)
                {
                    shipLog.mapState = MapState.Destroyed; // Sunk
                    shipLog.AddStringLog(Localize(
                        "Sunk due to sinking process finished (Uncontrolled Flooding)"
                    ));
                }
            }
        }

        public override string Describe() => Localize(
            "UncontrolledFlooding({0})",
            DescribeLiftCycle()
        );

        public override CampaignPersistence GetCampaignPersistence() => CampaignPersistence.Clear;
    }

    public class PropulsionDamaged : SubState, IDynamicModifier // DE 912, Similar to FeedwaterPumpDamaged, but it need a distinct description though. And description of DE 912 is ambiguous though. 
    {
        public float lostAllPropulsionPercentage = 25;
        public bool hasLoseAllPropulsion = false;
        public override void DoOnClockTick(ISubject subject, float deltaSeconds)
        {
            if (!hasLoseAllPropulsion && RandomUtils.D100F() <= lostAllPropulsionPercentage)
            {
                hasLoseAllPropulsion = true;
            }
        }

        public float GetMaxSpeedKnotCoef() => hasLoseAllPropulsion ? 0 : 1;

        public override string Describe() => Localize(
            "PropulsionDamaged(lostAllPropulsionPercentage={0}, hasLoseAllPropulsion={1}) ({2})",
            lostAllPropulsionPercentage, hasLoseAllPropulsion, DescribeLiftCycle()
        );

        public override CampaignPersistence GetCampaignPersistence() => CampaignPersistence.Maintained;
    }
}
