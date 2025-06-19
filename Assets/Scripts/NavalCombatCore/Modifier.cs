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
    [XmlInclude(typeof(FireControlSystemDisabledModifier))]
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

        public string casue = "";
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

    public interface IRateOfFireModifier
    {
        float GetRateOfFireCoef(); // E.X: this many resctrict a mount ROF to 50% of its original value.
    }

    public interface IFireControlValueModifier
    {
        float GetFireControlValueCoef();
    }

    public interface IBatteryFireContrlStatusModifier
    {
        MountStatus GetBatteryFireControlMountStatus();
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

    public class FireControlSystemDisabledModifier : SubState, IBatteryMountStatusModifier, IBatteryFireContrlStatusModifier
    {
        public MountStatus batteryMountStatus = MountStatus.Disabled;
        public MountStatus batteryFireControlMountStatus = MountStatus.Disabled;

        public MountStatus GetBatteryMountStatus()
        {
            return batteryMountStatus;
        }

        public MountStatus GetBatteryFireControlMountStatus()
        {
            return batteryFireControlMountStatus;
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

        public float GetFireControlValueCoef()
        {
            return fireControlValueCoef;
        }
    }
}