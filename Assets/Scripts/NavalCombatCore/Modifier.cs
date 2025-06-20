using System;
using System.Xml.Serialization;
using System.Xml;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.Distributions;
using Unity.VisualScripting;


namespace NavalCombatCore
{
    public enum StateLifeCycle
    {
        Permanent,
        GivenTime,
        SeverityBased,
        ShipboardFire,
        DieRollPassed // Die rol for every clock tick, if passed the state is removed
    }

    public interface ISubject
    {
        void AddSubState(SubState state);
        void RemoveSubState(SubState state);
    }

    [XmlInclude(typeof(SinkingState))]
    [XmlInclude(typeof(BatteryMountStatusModifier))]
    [XmlInclude(typeof(RateOfFireModifier))]
    [XmlInclude(typeof(ControlSystemDisabledModifier))]
    [XmlInclude(typeof(FireControlValueModifier))]
    public class SubState
    {
        // Left Cycle Parameter
        public StateLifeCycle lifeCycle;

        public float givenTimeSeconds = 120; // For GivenTime
        public float severity = 0; // For SeverityBased and ShipboardFire
        public float dieRollThreshold = 0; // For RollPassed, if die roll <=  the threshold, the state is removed.

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
        public bool damageControllable => lifeCycle == StateLifeCycle.SeverityBased || lifeCycle == StateLifeCycle.ShipboardFire;
        public virtual float damageControlPriority => 1;
        public bool damageControlApplied;
        public SimulationClock turnClock = new SimulationClock()
        {
            intervalSeconds = 120, // 1 SK5 Turn, 2 min
        };

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
        }

        public virtual void DoStep(ISubject subject, float deltaSeconds)
        { }

        public virtual string Describe() => "General Damage Effect";
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

                shipLog.damagePoint += severity;

                var newDamageEffectCausedByFire = RuleChart.ResolveShipboardFireDamageEffect(severity);
                if (newDamageEffectCausedByFire)
                {
                    // Add DE caused by shipbpard fire
                    DamageEffectChart.AddNewDamageEffect(new DamageEffectContext
                    {
                        subject = shipLog,
                        cause = DamageEffectCause.Fires,
                        // chainNumber =1
                    });
                }

                severity = RuleChart.ResolveFightingShipBoardFires(severity, damageControlApplied);
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
            DoBeginAt(subject);
        }
        public virtual void EndAt(ISubject subject)
        {
            subject.RemoveSubState(this);
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
        public override string Describe() => $"Ship destroyed and will remain an obstruction for all following turns until a roll <= {dieRollThreshold}";
        // public override bool damageControlable => false;
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
    }

    public interface IElectronicSystemModifier
    {
        bool IsSearchLightBlocked();
        bool IsRadarBlocked(); // Separate Fire Control Radar and Search Radar?
        bool IsSonarBlock();
    }

    public interface IDesiredSpeedUpdateToBoilerRoomBlocker // DE 124
    {
        bool IsDesiredSpeedCommandBlocked();
    }

    public interface ISmokeGeneratorModifier
    {
        bool IsSmokeGeneratorAvailable();
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
    }

    public class BatteryFireContrlStatusDisabledModifier : SubState, IBatteryFireContrlStatusModifier
    {
        public bool GetBatteryFireControlDisabled() => true;
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
    }

    public class EngineRoomHitModifier : SubState, IEngineRoomHitModifier
    {
        public int engineRoomHitOffset;

        public int GetEngineRoomHitOffset() => engineRoomHitOffset;
    }

    public class BoilerRoomHitModifier : SubState, IBoilerRoomHitModifier
    {
        public int boilerRoomHitOffset;

        public int GetBoilerRoomHitOffset() => boilerRoomHitOffset;
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
    }

    public class DamageControlModifier : SubState, IDamageControlModifier
    {
        public float fireControlRatingOffset = 0;
        public bool isFightingFireBlocked = false;
        public bool isDamageControlBlocked = false;
        public float GetDamageControlRatingOffset() => fireControlRatingOffset;
        public bool IsFightingFireBlocked() => isFightingFireBlocked;
        public bool IsDamageControlBlocked() => isDamageControlBlocked;
    }

    public class DynamicModifier : SubState, IDynamicModifier
    {
        public float maxSpeedKnotOffset = 0;
        public float maxSpeedKnotCoef = 1;
        public bool isEvasiveManeuverBlocked = false;
        public bool isCourseChangeBlocked = false;
        public bool isSpeedChangeBlocked = false;
        public bool isTurnPortBlocked = false;
        public bool isTurnStarboardBlocked = false;

        public float GetMaxSpeedKnotOffset() => maxSpeedKnotOffset;
        public float GetMaxSpeedKnotCoef() => maxSpeedKnotCoef;
        public bool IsEvasiveManeuverBlocked() => isEvasiveManeuverBlocked;
        public bool IsCourseChangeBlocked() => isCourseChangeBlocked;
        public bool IsSpeedChangeBlocked() => isSpeedChangeBlocked;
        public bool IsTurnPortBlocked() => isTurnPortBlocked;
        public bool IsTurnStarboardBlocked() => isTurnStarboardBlocked;
    }

    public class FeedwaterPumpDamaged : SubState, IDynamicModifier
    {
        public bool hasLoseAllPropulsion = false;
        public override void DoOnClockTick(ISubject subject, float deltaSeconds)
        {
            if (!hasLoseAllPropulsion && RandomUtils.D100F() <= 15)
            {
                hasLoseAllPropulsion = true;
            }
        }

        public float GetMaxSpeedKnotCoef() => hasLoseAllPropulsion ? 0 : 1;
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
    }

    public class EngineRoomCommunicationDamaged : SubState, IDesiredSpeedUpdateToBoilerRoomBlocker
    {
        public bool blocked;
        public override void DoOnClockTick(ISubject subject, float deltaSeconds)
        {
            blocked = RandomUtils.D100F() >= 75;
        }

        public bool IsDesiredSpeedCommandBlocked() => blocked;
    }

    public class TorpedoMountDamaged : SubState, ITorpedoMountStatusModifier
    {
        public MountStatus currentStatus;
        public float operationalPercent = 50;
        public override void DoOnClockTick(ISubject subject, float deltaSeconds)
        {
            currentStatus = RandomUtils.D100F() <= operationalPercent ? MountStatus.Operational : MountStatus.Disabled;
        }

        public MountStatus GetTorpedoMountStatus() => currentStatus;
    }

    public class TorpedoMountModifer : SubState, ITorpedoMountStatusModifier
    {
        public MountStatus status;
        public MountStatus GetTorpedoMountStatus() => status;
    }

    public class SmokeGeneratorDamaged : SubState, ISmokeGeneratorModifier
    {
        public bool IsSmokeGeneratorAvailableCurrent;
        public float availablePercent = 50;
        public override void DoOnClockTick(ISubject subject, float deltaSeconds)
        {
            IsSmokeGeneratorAvailableCurrent = RandomUtils.D100F() <= availablePercent;
        }

        public bool IsSmokeGeneratorAvailable() => IsSmokeGeneratorAvailableCurrent;
    }

    // public class LabeledSubState : SubState
    // {
    //     public string objectId;
    // }

    // public class BatteryParentSubState : SubState
    // {
    //     public List<string> childrenObjectIds = new();
    //     public override void DoEndAt(ISubject subject)
    //     {
    //         // base.DoEndAt(subject);
    //     }
    // }

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

        public bool IsSearchLightBlocked() => true;
        public bool IsRadarBlocked() => true; // Separate Fire Control Radar and Search Radar?
        public bool IsSonarBlock() => true;
    }

    public class PowerDistributionSymtemDamaged : SubState, ILocalizedBatteryMountStatusModifier
    {
        // TODO: Add command related things
        public List<MountLocation> locations = new();

        public MountStatus GetBatteryMountStatus(MountLocation mountLocation)
        {
            return locations.Contains(mountLocation)? MountStatus.Disabled : MountStatus.Operational;
        }
    }

}