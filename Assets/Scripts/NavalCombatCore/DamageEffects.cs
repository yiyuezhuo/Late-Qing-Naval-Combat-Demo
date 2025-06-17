using System;
using System.Xml.Serialization;
using System.Xml;
using System.Collections.Generic;
using System.Linq;

namespace NavalCombatCore
{
    public enum DamageEffectCause
    {
        Deck, // 1
        Turret, // 2/8
        Superstrucure, // 3/9
        ConningTower, // 4
        MainBelt, // 5
        BeltEnd, // 6
        Barbette, // 7
        General, // G
        Fires, // F
        Torpedo // T
    }

    // public interface IDamageEffect
    // {
    //     string id { get; }
    //     void DoEffect(DamageEffectContext ctx);
    //     string Describe();
    // }

    public class DamageEffectContext
    {
        public ShipLog subject;
        public float baseDamagePoint; // DE can be a DP multiplier
        public DamageEffectCause cause;
        public HitPenDetType hitPenDetType;
        public AmmunitionType ammunitionType;
        public float shellDiameterInch; // M2: Unspecified Damage severity = D100 + shell diameter (in inches)
        public int chainNumber; // Additional damage effect would be blocked if chainNumber > 0
    }

    // public class DamageEffect

    // public abstract class SK5DE : IDamageEffect
    // {
    //     public abstract string id { get; }
    //     public abstract void A(DamageEffectContext ctx);
    //     public abstract void B(DamageEffectContext ctx);
    //     public abstract void C(DamageEffectContext ctx);
    //     public abstract void HE(DamageEffectContext ctx);
    //     public virtual string Describe() => $"DE{id}";
    //     public void DoEffect(DamageEffectContext ctx)
    //     {
    //         if (ctx.ammunitionType == AmmunitionType.HighExplosive)
    //         {
    //             HE(ctx);
    //         }
    //         switch (ctx.hitPenDetType)
    //         {
    //             case HitPenDetType.PenetrateWithDetonate:
    //                 A(ctx);
    //                 break;
    //             case HitPenDetType.PassThrough:
    //                 B(ctx);
    //                 break;
    //             case HitPenDetType.NoPenetration:
    //                 C(ctx);
    //                 break;
    //         }
    //     }
    // }

    // public class DE100 : SK5DE
    // {
    //     public override string id => "100";
    //     public override void A(DamageEffectContext ctx)
    //     {
    //         ctx.subject.operationalState = (ShipOperationalState)Math.Max((int)ctx.subject.operationalState, (int)ShipOperationalState.FloodingObstruction);
    //         var subState = new SinkingIfRollPassed()
    //         {
    //             rollThreshold = 0.25f, // 25%
    //             casue = "Magazine explosion."
    //         };
    //         subState.BeginDamageSubState(ctx.subject);
    //     }
    //     public override void B(DamageEffectContext ctx) => A(ctx);
    //     public override void C(DamageEffectContext ctx)
    //     {

    //     }
    //     public override void HE(DamageEffectContext ctx) => C(ctx);
    // }

    // M1 Damage Effect
    public static class DamageEffectChart // This class is separated from the RuleChart, since it rely on ShipLog heavyly.
    {
        public static void AddNewDamageEffect(DamageEffectContext ctx)
        {
            var damageEffectId = RuleChart.ResolveDamageEffectId(ctx.cause);
            if (damageEffectMap.TryGetValue(damageEffectId, out var damageEffectEnforcer))
            {
                damageEffectEnforcer(ctx);
            }
            else
            {
                var logger = ServiceLocator.Get<ILoggerService>();
                logger.LogWarning($"Damage effect {damageEffectId} not found, skip");
            }
        }

        public static bool IsA(DamageEffectContext ctx) => ctx.ammunitionType != AmmunitionType.HighExplosive && ctx.hitPenDetType == HitPenDetType.PenetrateWithDetonate;
        public static bool IsB(DamageEffectContext ctx) => ctx.ammunitionType != AmmunitionType.HighExplosive && ctx.hitPenDetType == HitPenDetType.PassThrough;
        public static bool IsC(DamageEffectContext ctx) => ctx.ammunitionType != AmmunitionType.HighExplosive && ctx.hitPenDetType == HitPenDetType.NoPenetration;
        public static bool IsHE(DamageEffectContext ctx) => ctx.ammunitionType == AmmunitionType.HighExplosive;
        public static bool IsAB(DamageEffectContext ctx) => IsA(ctx) || IsB(ctx);

        public static Dictionary<string, Action<DamageEffectContext>> damageEffectMap = new() // DE id => Enforcer (enforcer will immdiately update some states and may append persistence DE state)
        {
            // DE 100
            { "100", ctx =>{
                if(IsAB(ctx)) // A/B
                {
                    ctx.subject.operationalState = (ShipOperationalState)Math.Max((int)ctx.subject.operationalState, (int)ShipOperationalState.FloodingObstruction);
                    var damageEffect = new SinkingIfRollPassed()
                    {
                        rollThresholdPercent = 25,
                        casue = "DE100 (A/B): Magazine explosion."
                    };
                    damageEffect.BeginAt(ctx.subject);
                }
                else
                {
                    var shipboardFire = new ShipboardFire()
                    {
                        casue = "DE100 (C/HE): Shipboard fire only.",
                        severity = 50
                    };
                    shipboardFire.BeginAt(ctx.subject);

                    var d100 = RandomUtils.D100F();
                    if(d100 < 5)
                    {
                        var primaryBattery = ctx.subject.batteryStatus.FirstOrDefault();
                        if(primaryBattery != null)
                        {
                            // Fire in primary battery magazine. Roll to determine section location of affected magazine.
                            var gp = primaryBattery.mountStatus // Check remanent Ammunition? 
                                .GroupBy(mnt => mnt.GetMountLocationRecordInfo().record.mountLocation)
                                .ToList();

                            if(gp.Count > 0)
                            {
                                ctx.subject.damagePoint += ctx.baseDamagePoint * 2; // Triple total DP caused by this hit

                                // Magazines are flooded and all guns serviced by this magazine (located in affected section) may not fire for the duration of battle.
                                var g = RandomUtils.Sample(gp);
                                foreach(var bs in g)
                                {
                                    bs.status = (MountStatus)Math.Max((int)bs.status, (int)MountStatus.Disabled);
                                }
                            }
                        }
                    }
                }
            }},
            
            // DE 101
            { "101", ctx=>{
                if(IsAB(ctx))
                {
                    // Fire in primary battery magazine... (Like DE 100 (C/HE))
                    
                }
                else
                {
                    // Shipboard fire only, Severity 40. No additional DE
                    if(IsHE(ctx))
                    {
                        // Permanent loss of 1 box in one Rapid Fire battery. Roll to determine battery (if more than one) and roll to determine location [Port/STBD]

                    }
                }
            } }
        };
    }

    [XmlInclude(typeof(SinkingIfRollPassed))]
    [XmlInclude(typeof(ShipboardFire))]
    public abstract class DamageEffect
    {
        public string casue = "";
        public bool permanent; // If it's not permanent then this can be damage controlled.
        public virtual float damageControlPriority => 1;
        public bool damageControlApplied;
        public SimulationClock turnClock = new SimulationClock()
        {
            intervalSeconds = 120, // 1 SK5 Turn
        };

        public virtual void Step(ShipLog subject, float deltaSeconds)
        {
            var tick = turnClock.Step(deltaSeconds);
            for (int i = 0; i < tick; i++)
            {
                OnTurnClockTick(subject, deltaSeconds);
            }
        }
        public virtual string Describe() => "General Damage Effect";
        public virtual void OnTurnClockTick(ShipLog subject, float deltaSeconds) // Generally, SK5 turn advancement callback (per 2min)
        { }

        public virtual void BeginAt(ShipLog subject)
        {
            subject.damageEffectSubStates.Add(this);
        }
        public virtual void EndAt(ShipLog subject)
        {
            subject.damageEffectSubStates.Remove(this);
        }
    }

    public class PermanentSubState : DamageEffect
    {
        public PermanentSubState()
        {
            permanent = true;
        }
    }

    public class SinkingIfRollPassed : PermanentSubState
    {
        public float rollThresholdPercent;

        public override void OnTurnClockTick(ShipLog subject, float deltaSeconds)
        {
            if (RandomUtils.D100F() < rollThresholdPercent)
            {
                subject.mapState = MapState.Destroyed; // Sunk
            }
        }
        public override string Describe() => $"Ship destroyed and will remain an obstruction for all following turns until a roll of {rollThresholdPercent}";
        // public override bool damageControlable => false;
    }

    public class SeverityMeasured : DamageEffect
    {
        public float severity; // 0 ~ 100 (not 0% ~ 100%)
        public override void OnTurnClockTick(ShipLog subject, float deltaSeconds)
        {
            if (!permanent)
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
        }

        public virtual void OnDamageControllSuccessed(ShipLog subject, float deltaSeconds)
        {
            EndAt(subject); // Shipboard Fire cannot be eliminated just just a Roll
        }
        
        public virtual void OnDamageControllSetPermanent(ShipLog subject, float deltaSeconds)
        {
            permanent = true;
        }
        // public virtual void OnDamageControllFailed()
    }

    public class ShipboardFire : SeverityMeasured
    {
        public override void OnTurnClockTick(ShipLog subject, float deltaSeconds)
        {
            var newDamageEffectCausedByFire = RuleChart.ResolveShipboardFireDamageEffect(severity);
            if (newDamageEffectCausedByFire)
            {
                // Add DE caused by shipbpard fire
                DamageEffectChart.AddNewDamageEffect(new DamageEffectContext
                {
                    subject = subject,
                    cause = DamageEffectCause.Fires,
                    chainNumber =1
                });
            }

            severity = RuleChart.ResolveFightingShipBoardFires(severity, damageControlApplied);
            if (severity == 0)
            {
                EndAt(subject);
            }
        }

    }
}