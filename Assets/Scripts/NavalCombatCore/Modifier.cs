using System;
using System.Xml.Serialization;
using System.Xml;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.Distributions;
using Unity.VisualScripting.Dependencies.NCalc;


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

    public interface ISubject
    {
        void AddSubState(SubState state);
        void RemoveSubState(SubState state);
    }

    [XmlInclude(typeof(ShipboardFireState))]
    [XmlInclude(typeof(SinkingState))]
    [XmlInclude(typeof(BatteryMountStatusModifier))]
    [XmlInclude(typeof(RateOfFireModifier))]
    [XmlInclude(typeof(BatteryFireContrlStatusDisabledModifier))]
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
    [XmlInclude(typeof(PowerDistributionSymtemDamaged))]
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
                return "Permanent";
            }
            else if (lifeCycle == StateLifeCycle.GivenTime)
            {
                return $"For {givenTimeSeconds} seconds";
            }
            else if (lifeCycle == StateLifeCycle.SeverityBased || lifeCycle == StateLifeCycle.ShipboardFire)
            {
                return $"Severity: {severity}";
            }
            else if (lifeCycle == StateLifeCycle.DieRollPassed)
            {
                return $"DieRoll: {dieRollThreshold}";
            }
            return "Unknown life cycle";
        }

        public string cause = "";
        // public bool permanent; // If it's not permanent then this can be damage controlled.
        public virtual bool damageControllable => lifeCycle == StateLifeCycle.SeverityBased || lifeCycle == StateLifeCycle.ShipboardFire;
        public virtual float damageControlPriority => 1;
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

        public virtual string Describe() => $"Sub State: {GetType().Name}";
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
                if (d100 > damageContrlThreshold)
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
                var shipLog = subject as ShipLog;// Shipboard Fire can only be attached to ShipLog

                // shipLog.damagePoint += severity;
                shipLog.AddDamagePoint(severity);

                var newDamageEffectCausedByFire = RuleChart.ResolveShipboardFireDamageEffect(severity);
                if (newDamageEffectCausedByFire)
                {
                    // Add DE caused by shipbpard fire
                    DamageEffectChart.AddNewDamageEffect(new DamageEffectContext
                    {
                        subject = shipLog,
                        cause = DamageEffectCause.Fires,
                        source = this,
                    });
                }

                severity = Math.Min(100, severity + RuleChart.ResolveFightingShipBoardFiresDelta(severity, damageControlApplied));
                if (severity == 0)
                {
                    EndAt(subject);
                }
            }

            if (lifeCycle == StateLifeCycle.DieRollPassed && RandomUtils.D100F() <= dieRollThreshold)
            {
                EndAt(subject);
            }
        }

        public virtual void BeginAt(ISubject subject)
        {
            subject.AddSubState(this);
            EntityManager.Instance.Register(this, subject);

            DoBeginAt(subject);
        }
        public virtual void EndAt(ISubject subject)
        {
            subject.RemoveSubState(this);
            EntityManager.Instance.Unregister(this);

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
    }

    public interface IBatteryMountStatusModifier // Mounts should check its platform's damage effects which implements IBatteryMountEffector to determine its effective status (the state hold by itself is the "permanent" state, while effector may override this value for a given time)
    {
        MountStatus GetBatteryMountStatus(); // E.X this may restrict a Operational mount to Damage and OOA
    }

    public interface ILocalizedBatteryMountStatusModifier
    {
        MountStatus GetBatteryMountStatus(MountLocation mountLocation);
    }

    public interface ITorpedoMountStatusModifier
    {
        MountStatus GetTorpedoMountStatus();
    }

    public interface ILocalizedTorpedoMountStatusModifier
    {
        MountStatus GetTorpedoMountStatus(MountLocation mountLocation);
    }

    public interface IRateOfFireModifier
    {
        float GetRateOfFireCoef(); // E.X: this many resctrict a mount ROF to 50% of its original value.
    }

    public interface IBarrageFireBlocker
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
        float GetMaxSpeedUpperLimit() => 100000;
        float GetStandardTurnCoef() => 1;
        float GetEmergencyTurnCoef() => 1;
        float GetStandardTurnUpperLimit() => 100000;
        float GetEmergencyTurnUpperLimit() => 100000;
        float GetAccelerationUpperLimit() => 100000;

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
        float GetDamageControlRatingOffset();
        bool IsFightingFireBlocked();
        bool IsDamageControlBlocked();
        float GetDamageControlDieRollOffset();
        bool IsBatteryDamageControlBlock() => false;
        float GetSeveriDieRollOffset() => 0;
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

    public interface ISmokeGeneratorModifier
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
        public override string Describe() => $"Shipboard Fire Severity: {severity}";
    }

    public class SinkingState : SubState
    {
        public override void DoEndAt(ISubject subject)
        {
            var shipLog = subject as ShipLog; // This state can only be attached to a ShipLog
            if (shipLog != null)
            {
                shipLog.mapState = MapState.Destroyed; // Sunk
            }
        }
        public override string Describe() => $"Sunk When DE ended: {DescribeLiftCycle()}";
        // public override bool damageControlable => false;
    }

    public class BatteryMountStatusModifier : SubState, IBatteryMountStatusModifier
    {
        public MountStatus mountStatus = MountStatus.Disabled;

        public MountStatus GetBatteryMountStatus()
        {
            return mountStatus;
        }
        public override string Describe() => $"Battery Mount is disabled ({DescribeLiftCycle()})";
    }

    public class RateOfFireModifier : SubState, IRateOfFireModifier
    {
        public float rateOfFireCoef = 0.5f;

        public float GetRateOfFireCoef()
        {
            return rateOfFireCoef;
        }

        public override string Describe() => $"RateOfFireModifier({rateOfFireCoef}) ({DescribeLiftCycle()})";
    }

    public class BatteryFireContrlStatusDisabledModifier : SubState, IBatteryFireContrlStatusModifier
    {
        public bool GetBatteryFireControlDisabled() => true;

        public override string Describe() => $"BatteryFireContrlStatusDisabledModifier ({DescribeLiftCycle()})";
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

        public override string Describe() => $"ControlSystemDisabledModifier({batteryMountStatus}) ({DescribeLiftCycle()})";
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

        public override string Describe() => $"FireControlValueModifier(coef={fireControlValueCoef}, offset={fireControlValueOffset}) ({DescribeLiftCycle()})";
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

        public override string Describe() => $"RiskingInMagazineExplosion(explosionProbPercent={explosionProbPercent}, sinkingThreshold={sinkingThreshold}) ({DescribeLiftCycle()})";
    }

    public class EngineRoomHitModifier : SubState, IEngineRoomHitModifier
    {
        public int engineRoomHitOffset;

        public int GetEngineRoomHitOffset() => engineRoomHitOffset;

        public override string Describe() => $"EngineRoomHitModifier(offset={engineRoomHitOffset}) ({DescribeLiftCycle()})";
    }

    public class BoilerRoomHitModifier : SubState, IBoilerRoomHitModifier
    {
        public int boilerRoomHitOffset;

        public int GetBoilerRoomHitOffset() => boilerRoomHitOffset;

        public override string Describe() => $"BoilerRoomHitModifier(offset={boilerRoomHitOffset}) ({DescribeLiftCycle()})";
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

        public override string Describe() => $"SteamLineDamaged(offset={currentMaxSpeedOffset}) ({DescribeLiftCycle()})";
    }

    public class DamageControlModifier : SubState, IDamageControlModifier
    {
        public float damageControlRatingOffset = 0;
        public bool isFightingFireBlocked = false;
        public bool isDamageControlBlocked = false;
        public float damageControlDieRollOffset = 0;
        public bool isBatteryDamageControlBlock = false;
        public float severiDieRollOffset = 0;
        public float fightingFireDieRollOffset = 0;
        public float GetDamageControlRatingOffset() => damageControlRatingOffset;
        public bool IsFightingFireBlocked() => isFightingFireBlocked;
        public bool IsDamageControlBlocked() => isDamageControlBlocked;
        public float GetDamageControlDieRollOffset() => damageControlDieRollOffset;
        public bool IsBatteryDamageControlBlock() => isBatteryDamageControlBlock;
        public float GetSeveriDieRollOffset() => severiDieRollOffset;
        public float GetFightingFireDieRollOffset() => fightingFireDieRollOffset;

        public override string Describe()
        {
            var lines = new List<string>()
            {
                damageControlRatingOffset != 0 ? $"DC Rating Offset: {damageControlRatingOffset}" : null,
                isFightingFireBlocked ? "Fighting Fire Blocked" : null,
                isDamageControlBlocked ? "Damage Control Blocked" : null,
                damageControlDieRollOffset != 0 ? $"DC Die Roll Offset: {damageControlDieRollOffset}" : null,
                isBatteryDamageControlBlock ? "Battery DC Blocked" : null,
                severiDieRollOffset != 0 ? $"Severi Die Roll Offset: {severiDieRollOffset}" : null,
                fightingFireDieRollOffset != 0 ? $"Fighting Fire Die Roll Offset: {fightingFireDieRollOffset}" : null
            };
            return "DamageControlModifier:" + string.Join(";", lines.Where(line => line != null)) + " | " + DescribeLiftCycle();
        }
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
                maxSpeedKnotOffset != 0 ? $"Speed Offset: {maxSpeedKnotOffset}" : null,
                maxSpeedKnotCoef != 1 ? $"Speed Coef: {maxSpeedKnotCoef}" : null,
                maxSpeedUpperLimit != 100000 ? $"Speed Upper Limit: {maxSpeedUpperLimit}" : null,
                standardTurnCoef != 1 ? $"Std Turn Coef: {standardTurnCoef}" : null,
                emergencyTurnCoef != 1 ? $"Emer Turn Coef: {emergencyTurnCoef}" : null,
                standardTurnUpperLimit != 100000 ? $"Std Turn Upper Limit: {standardTurnUpperLimit}" : null,
                emergencyTurnUpperLimit != 100000 ? $"Emer Turn Upper Limit: {emergencyTurnUpperLimit}" : null,
                accelerationUpperLimit != 100000 ? $"Accel Upper Limit: {accelerationUpperLimit}" : null,
                isEvasiveManeuverBlocked ? "Evasive Blocked" : null,
                isCourseChangeBlocked ? "Course Change Blocked" : null,
                isSpeedChangeBlocked ? "Speed Change Blocked" : null,
                isTurnPortBlocked ? "Turn Port Blocked" : null,
                isTurnStarboardBlocked ? "Turn Starboard Blocked" : null,
                isEmergencyTurnBlocked ? "Emer Turn Blocked" : null
            };
            return "DynamicModifier:" + string.Join(";", lines.Where(line => line != null)) + " | " + DescribeLiftCycle();
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

        public override string Describe() => $"FeedwaterPumpDamaged(lostAllPropulsionPercentage={lostAllPropulsionPercentage}, hasLoseAllPropulsion={hasLoseAllPropulsion}) ({DescribeLiftCycle()})";
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

        public override string Describe() => $"RudderDamaged(currentDesiredHeadingOffset={currentDesiredHeadingOffset}, ({DescribeLiftCycle()})";
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

        public override string Describe() => $"FuelSupplyDamaged(active={active} (if active, speed coef=0.5)), ({DescribeLiftCycle()})";
    }

    public class EngineRoomCommunicationDamaged : SubState, IDesiredSpeedUpdateToBoilerRoomBlocker
    {
        public bool blocked;
        public override void DoOnClockTick(ISubject subject, float deltaSeconds)
        {
            blocked = RandomUtils.D100F() >= 75;
        }

        public bool IsDesiredSpeedCommandBlocked() => blocked;

        public override string Describe() => $"EngineRoomCommunicationDamaged(blocked={blocked}) ({DescribeLiftCycle()})";
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

        public override string Describe() => $"TorpedoMountDamaged(currentStatus={currentStatus},operationalPercentange={operationalPercentange}) ({DescribeLiftCycle()})";
    }

    public class TorpedoMountModifer : SubState, ITorpedoMountStatusModifier
    {
        public MountStatus status;
        public MountStatus GetTorpedoMountStatus() => status;

        public override string Describe() => $"TorpedoMountModifer(status={status}) ({DescribeLiftCycle()})";
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

        public override string Describe() => $"SmokeGeneratorDamaged(IsSmokeGeneratorAvailableCurrent={isSmokeGeneratorAvailableCurrent},availablePercent={availablePercent}) ({DescribeLiftCycle()})";
    }

    // DE 128
    public class SectorFireState : SubState, ILocalizedDirectionalFireControlValueModifier, ILocalizedTorpedoMountStatusModifier
    {
        public bool disableTorpedo;

        public enum SectionLocation
        {
            Front,
            Midship,
            After
        }
        public SectionLocation fireAndSmokeLocation;

        SectionLocation GetSectionLocation(MountLocation mountLocation)
        {
            if (mountLocation <= MountLocation.StarboardForward)
                return SectionLocation.Front;
            else if (mountLocation <= MountLocation.StarboardMidship)
                return SectionLocation.Midship;
            return SectionLocation.After;
        }

        public float GetFireControlValueOffset(MountLocation mountLocation, float bearingRelativeToBowDeg)
        {
            if (mountLocation == MountLocation.NotSpecified)
                return 0;

            var toFront = bearingRelativeToBowDeg <= 45 || bearingRelativeToBowDeg >= 315;
            var toAfter = bearingRelativeToBowDeg >= 135 && bearingRelativeToBowDeg <= 225;

            var mountSectionLocation = GetSectionLocation(mountLocation);

            if (mountSectionLocation == SectionLocation.Front) // Forward (though include unspecified)
            {
                if (fireAndSmokeLocation == SectionLocation.Front || toAfter)
                {
                    return -1;
                }
                return 0;
            }
            else if (mountSectionLocation == SectionLocation.Midship) // Midship
            {
                if (fireAndSmokeLocation == SectionLocation.Midship)
                    return -1;
                if (fireAndSmokeLocation == SectionLocation.Front && toFront)
                    return -1;
                if (fireAndSmokeLocation == SectionLocation.After && toAfter)
                    return -1;
                return 0;
            }
            else // after
            {
                if (mountSectionLocation == SectionLocation.After || toFront)
                {
                    return -1;
                }
                return 0;
            }
        }

        public MountStatus GetTorpedoMountStatus(MountLocation mountLocation)
        {
            // TODO: Track if torpedo is deck torpedo (or submerged etc)
            var mountSectionLocation = GetSectionLocation(mountLocation);
            return mountSectionLocation == fireAndSmokeLocation ? MountStatus.Disabled : MountStatus.Operational;
        }

        public override string Describe() => $"SectorFireState(disableTorpedo={disableTorpedo},fireAndSmokeLocation={fireAndSmokeLocation}) ({DescribeLiftCycle()})";
    }

    public class MainPowerplantOOA : SubState, IRateOfFireModifier, IDamageControlModifier, IElectronicSystemModifier
    {
        // TODO: Add command related things

        // Optional effect
        public float rateOfFireCoef = 1;
        public bool isDamageControlBlocked = false;

        public float GetRateOfFireCoef() => rateOfFireCoef;

        public float GetDamageControlRatingOffset() => 0;
        public bool IsFightingFireBlocked() => false;
        public bool IsDamageControlBlocked() => isDamageControlBlocked;
        public float GetDamageControlDieRollOffset() => 0;

        public bool IsSearchLightDisabled() => true;
        public bool IsFireControlRadarDisabled() => true; // Separate Fire Control Radar and Search Radar?
        public bool IsSearchRadarDisabled() => true;
        public bool IsSonarDisabled() => true;

        public override string Describe() => $"MainPowerplantOOA(rateOfFireCoef={rateOfFireCoef},isDamageControlBlocked={isDamageControlBlocked}) ({DescribeLiftCycle()})";
    }

    public class PowerDistributionSymtemDamaged : SubState, ILocalizedBatteryMountStatusModifier
    {
        // TODO: Add command related things
        public List<MountLocation> locations = new();

        public MountStatus GetBatteryMountStatus(MountLocation mountLocation)
        {
            return locations.Contains(mountLocation) ? MountStatus.Disabled : MountStatus.Operational;
        }

        public override string Describe() => $"PowerDistributionSymtemDamaged(locations={locations}) ({DescribeLiftCycle()})";
    }

    public class BatteryTargetChangeBlocker : SubState, IBatteryTargetChangeBlocker
    {
        public bool isBatteryTargetChangeBlocked = true;

        public bool IsBatteryTargetChangeBlocked() => isBatteryTargetChangeBlocked;

        public override string Describe() => $"BatteryTargetChangeBlocker(blocked={isBatteryTargetChangeBlocked}) ({DescribeLiftCycle()})";
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
                isSearchLightDisabled ? "Search Light Disabled" : null,
                isFireControlRadarDisabled ? "Fire Control Radar Disabled" : null,
                isSearchRadarDisabled ? "Search Radar Disabled" : null,
                isSonarDisabled ? "Sonar Disabled" : null
            };
            return "ElectronicSystemModifier:" + string.Join(";", lines.Where(line => line != null)) + " | " + DescribeLiftCycle();
        }
    }

    public class ArmorModifier : SubState, IArmorModifier
    {
        public float mainBeltArmorCoef;
        public float GetMainBeltArmorCoef() => mainBeltArmorCoef;

        public override string Describe() => $"ArmorModifier(mainBeltArmorCoef={mainBeltArmorCoef}) {DescribeLiftCycle()}";
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
                        cause = "M6: List to [PORTS/STARBOARD]",
                        fireControlValueOffset = -2
                    };
                    DE.BeginAt(shipLog.batteryStatus[0]);
                }
                if (shipLog.batteryStatus.Count > 1)
                {
                    var DE = new BatteryMountStatusModifier()
                    {
                        lifeCycle = StateLifeCycle.GivenTime,
                        cause = "M6: List to [PORTS/STARBOARD]",
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
                        cause = "M6: Heavy list to [PORT/STARBOARD]",
                        fireControlValueOffset = -3
                    };
                    DE.BeginAt(shipLog.batteryStatus[0]);
                }
                if (shipLog.batteryStatus.Count > 1)
                {
                    var DE = new BatteryMountStatusModifier()
                    {
                        lifeCycle = StateLifeCycle.GivenTime,
                        cause = "M6: Heavy list to [PORT/STARBOARD]",
                    };
                    DE.BeginAt(shipLog.batteryStatus[1]);
                }
                var DE3 = new ArmorModifier()
                {
                    lifeCycle = StateLifeCycle.GivenTime,
                    mainBeltArmorCoef = 0.5f,
                    cause = "M6: Heavy list to [PORT/STARBOARD]",
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
                var DE = new BatteryFireContrlStatusDisabledModifier()
                {
                    lifeCycle = StateLifeCycle.GivenTime,
                    cause = "M6: All [PRIMARY/SECONDARY] battery fire control systems OOA during next game turn"
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
                    cause = "M6, Damage to main feedwater pump"
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

        public override string Describe() => $"SevereFloodingState(dieRollOffset={dieRollOffset}) {DescribeLiftCycle()}";
    }

    public class SevereFloodingRollModifier : SubState, ISevereFloodingRollModifier
    {
        public float severeFloodingRollOffset;
        public float GetSevereFloodingRollOffset() => severeFloodingRollOffset;

        public override string Describe() => $"SevereFloodingRollModifier(severeFloodingRollOffset={severeFloodingRollOffset}) {DescribeLiftCycle()}";
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

        public override string Describe() => $"LossOfCommunicationToFireControlSystemState(blocked={isFireControlSystemTargetChangeBlocked},succPercentage={succPercentage}) {DescribeLiftCycle()}";
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

        public override string Describe() => $"LossOfCommunicationsAndPowerToSearchLight(location={location},succPercentage={succPercentage},isSearchLightDisabled={isSearchLightDisabled}) {DescribeLiftCycle()}";
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

        public override string Describe() => $"LossOfCommunicationToEngineRoom(succPercentage={succPercentage},isSpeedChangeBlocked={isSpeedChangeBlocked}) {DescribeLiftCycle()}";
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

        public override string Describe() => $"BatteryHandlingRoomAbandoned {DescribeLiftCycle()}";
    }

    // Asterisk labeled family, they may doesn't have many functionally, just a label that some 
    public class OneShotDamageEffectHappend : SubState
    {
        public string damageEffectCode;

        public override string Describe() => $"OneShotDamageEffectHappend: {damageEffectCode}";
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

        public override string Describe() => $"DE602DyanmicModifier: isSpeedChangeBlocked={isSpeedChangeBlocked}), Std Turn Coef: 0.5, Emer Turn Blocked, Evasive Man. Blocked";
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

        public override string Describe() => $"DE607DyanmicModifier: isCourceChangeBlocked={isCourceChangeBlocked})";
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
                maxSpeedUpperLimitApplied ? $"Speed Upper Limit: {maxSpeedUpperLimit}" : null,
                maxSpeedUpperLimitAppliedThreshold >= 0 ? $"Speed Limit Threshold: {maxSpeedUpperLimitAppliedThreshold}" : null,
                sinkingThreshold >= 0 ? $"Sinking Threshold: {sinkingThreshold}" : null,
                isCourseChangeBlockedThreshold >= 0 ? $"Course Block Threshold: {isCourseChangeBlockedThreshold}" : null,
                isCourseChangeBlocked ? "Course Change Blocked" : null
            };
            return "ShipSettleState:" + string.Join(";", lines.Where(line => line != null));
        }
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

        public override string Describe() => $"DE609Effect";
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

        public override string Describe() => $"FiringCircuitDamagedMaster(currentRateOfFireCoef={currentRateOfFireCoef}) {DescribeLiftCycle()}";
    }

    public class FiringCircuitDamagedWorker : SubState, IRateOfFireModifier, IBarrageFireBlocker // DE *615
    {
        public float GetRateOfFireCoef()
        {
            var master = EntityManager.Instance.Get<FiringCircuitDamagedMaster>(dependentObjectId);
            return master.currentRateOfFireCoef;
        }

        public bool IsBarrageFireBlocked() => true;
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

        public override string Describe() => $"DE806DynamicModifier(restored={restored}, maxSpeedKnotCoef={maxSpeedKnotCoef}) {DescribeLiftCycle()}";
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

        public override string Describe() => $"BatteryDamaged(status={status}, operationalPercentage={operationalPercentage}) {DescribeLiftCycle()}";
    }
}