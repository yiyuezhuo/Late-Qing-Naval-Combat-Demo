using System;
using System.Xml.Serialization;
using System.Xml;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.Distributions;

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

    public class DamageEffectContext
    {
        public ShipLog subject;
        public float baseDamagePoint; // DE can be a DP multiplier
        public DamageEffectCause cause;
        public HitPenDetType hitPenDetType;
        public AmmunitionType ammunitionType;
        public float shellDiameterInch; // M2: Unspecified Damage severity = D100 + shell diameter (in inches)
        // public int chainNumber; // Additional damage effect would be blocked if chainNumber > 0
        public float addtionalDamageEffectProbility; // 0.0~1.0, Addtional DE will use the same probility to cause this DE. If an additional DE is not possible, prob should be set to 0.

        public DamageEffectContext Clone()
        {
            return XmlUtils.FromXML<DamageEffectContext>(XmlUtils.ToXML(this));
        }

        public DamageEffectContext CloneWithAdditionalDEProbToZero()
        {
            var clone = Clone();
            clone.addtionalDamageEffectProbility = 0;
            return clone;
        }
    }

    // M1 Damage Effect
    public static class DamageEffectChart // This class is separated from the RuleChart, since it rely on ShipLog heavyly.
    {
        public static void AddNewDamageEffect(DamageEffectContext ctx)
        {
            var damageEffectId = RuleChart.ResolveDamageEffectId(ctx.cause);
            AddNewDamageEffect(ctx, damageEffectId);
        }

        public static void AddNewDamageEffect(DamageEffectContext ctx, string damageEffectId)
        {
            if (damageEffectId == null || damageEffectId == "")
                return;

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

        public static bool TryGetPrimaryBattery(DamageEffectContext ctx, out BatteryStatus primaryBattery)
        {
            primaryBattery = ctx.subject.batteryStatus.FirstOrDefault();
            return primaryBattery != null;
        }

        public static bool TryToSampleAPrimaryBatteryMount(DamageEffectContext ctx, out MountStatusRecord mount)
        {
            if (TryGetPrimaryBattery(ctx, out var primaryBattery))
            {
                if (primaryBattery.mountStatus.Count > 0)
                {
                    mount = RandomUtils.Sample(primaryBattery.mountStatus);
                    return true;
                }
            }
            mount = null;
            return false;
        }

        public static bool TryToSampleASecondaryOrTertiaryBatteryMount(DamageEffectContext ctx, out MountStatusRecord mount)
        {
            mount = null;
            if (ctx.subject.batteryStatus.Count <= 1)
                return false;
            var mounts = ctx.subject.batteryStatus.Skip(1).SelectMany(bs => bs.mountStatus).ToList();
            if (mounts.Count == 0)
                return false;
            mount = RandomUtils.Sample(mounts);
            return true;
        }

        public static bool TryToSampleAAdjacentMount(DamageEffectContext ctx, MountStatusRecord baseMount, out MountStatusRecord adjMount)
        {
            var mountCtx = baseMount.GetFullContext();
            var potentialList = mountCtx.batteryStatus.mountStatus.Where(mnt => mnt.mountLocation == baseMount.mountLocation).ToList();
            if (potentialList.Count == 0)
            {
                adjMount = null;
                return false;
            }
            adjMount = RandomUtils.Sample(potentialList);
            return true;
        }

        // public static T EnumMax<T>(T x, T y) //  where T: Enum
        // {
        //     return (T)(object)Math.Max((int)(object)x, (int)(object)y);
        // }

        public static T MaxEnum<T>(T a, T b) where T : Enum
        {
            int aValue = Convert.ToInt32(a);
            int bValue = Convert.ToInt32(b);
            return (T)Enum.ToObject(typeof(T), Math.Max(aValue, bValue));
        }


        public static void FireInPrimaryBatteryMagazine(DamageEffectContext ctx)
        {
            if (TryGetPrimaryBattery(ctx, out var primaryBattery))
            {
                // Fire in primary battery magazine. Roll to determine section location of affected magazine.
                var gp = primaryBattery.mountStatus // Check remanent Ammunition? 
                    .GroupBy(mnt => mnt.GetMountLocationRecordInfo().record.mountLocation)
                    .ToList();

                if (gp.Count > 0)
                {
                    ctx.subject.damagePoint += ctx.baseDamagePoint * 2; // Triple total DP caused by this hit

                    // Magazines are flooded and all guns serviced by this magazine (located in affected section) may not fire for the duration of battle.
                    var g = RandomUtils.Sample(gp);
                    foreach (var bs in g)
                    {
                        // bs.status = (MountStatus)Math.Max((int)bs.status, (int)MountStatus.Disabled);
                        bs.status = MaxEnum(bs.status, MountStatus.Disabled);
                    }
                }
            }
        }

        public static void RollForAdditionalDamageEffect(DamageEffectContext ctx, float[] rightBounds, string[] damageEffectIds)
        {
            if (RandomUtils.NextFloat() < ctx.addtionalDamageEffectProbility)
            {
                // var rightBounds = new List<float>(){10, 25, 45, 70, 100};
                // var damageEffectIds = new List<string>(){"100", "147", "107", "146", ""};
                var d100 = RandomUtils.D100F();
                var idx = Enumerable.Range(0, rightBounds.Length).First(i => d100 <= rightBounds[i]);
                var damageEffectId = damageEffectIds[idx];

                AddNewDamageEffect(ctx.CloneWithAdditionalDEProbToZero(), damageEffectId);
            }
        }

        static float[] additionalDamageEffectRightBoundsDefault = new float[] { 10, 25, 45, 70, 100 };

        public static void RollForAdditionalDamageEffect(DamageEffectContext ctx, string[] damageEffectIds) =>
            RollForAdditionalDamageEffect(ctx, additionalDamageEffectRightBoundsDefault, damageEffectIds);

        public static void Lost1RandomRapidFiringBatteryBox(DamageEffectContext ctx)
        {
            // Permanent loss of 1 box in one Rapid Fire battery. Roll to determine battery (if more than one) and roll to determine location [Port/STBD]
            if (ctx.subject.rapidFiringStatus.Count > 0)
            {
                var rapidFiringStatus = RandomUtils.Sample(ctx.subject.rapidFiringStatus);
                var hitPort = RandomUtils.NextFloat() < 0.5f;
                if (hitPort)
                {
                    rapidFiringStatus.portMountHits += 1;
                }
                else
                {
                    rapidFiringStatus.starboardMountHits += 1;
                }
            }
        }

        public static void AddShipboardFire(DamageEffectContext ctx, string cause, float severity)
        {
            var shipboardFire = new SubState()
            {
                lifeCycle = StateLifeCycle.ShipboardFire,
                casue = cause,
                severity = severity
            };
            shipboardFire.BeginAt(ctx.subject);
        }

        public static Dictionary<string, Action<DamageEffectContext>> damageEffectMap = new() // DE id => Enforcer (enforcer will immdiately update some states and may append persistence DE state)
        {
            // DE 100, shell hit deck above the magazine
            { "100", ctx =>{
                if(IsAB(ctx)) // A/B
                {
                    // ctx.subject.operationalState = (ShipOperationalState)Math.Max((int)ctx.subject.operationalState, (int)ShipOperationalState.FloodingObstruction);
                    ctx.subject.operationalState = MaxEnum(ctx.subject.operationalState, ShipOperationalState.FloodingObstruction);
                    var damageEffect = new SinkingState()
                    {
                        lifeCycle = StateLifeCycle.DieRollPassed,
                        dieRollThreshold = 25,
                        casue = "DE100 (A/B): Magazine explosion."
                    };
                    damageEffect.BeginAt(ctx.subject);
                }
                else
                {
                    AddShipboardFire(ctx, "DE100 (C/HE): Shipboard fire only.", 50);

                    var d100 = RandomUtils.D100F();
                    if(d100 < 5)
                    {
                        FireInPrimaryBatteryMagazine(ctx);
                    }
                }
            }},
            
            // DE 101
            { "101", ctx=>{
                if(IsAB(ctx))
                {
                    // Fire in primary battery magazine... (Like DE 100 (C/HE))
                    FireInPrimaryBatteryMagazine(ctx);

                    // Additional Damage Effect Roll
                    RollForAdditionalDamageEffect(ctx,
                        new []{10f, 25, 45, 70, 100},
                        new []{"100", "147", "107", "146", ""});
                }
                else
                {
                    // Shipboard fire only, Severity 40. No additional DE
                    AddShipboardFire(ctx, "DE100 (C/HE): Shipboard fire only.", 40);

                    if (IsHE(ctx))
                    {
                        Lost1RandomRapidFiringBatteryBox(ctx);
                    }
                }
            } },

            // DE 102
            { "DE102", ctx=>{
                if(IsAB(ctx))
                {
                    // Flooding in primary battery barbette. Roll to determine location of mount. Mount is permanently OOA for the duration of the game.
                    if(TryToSampleAPrimaryBatteryMount(ctx, out var mountStatus))
                    {
                        mountStatus.status = MaxEnum(mountStatus.status, MountStatus.Disabled);

                        RollForAdditionalDamageEffect(ctx,
                            new []{10f, 25, 45, 70, 100},
                            new []{"119", "120", "124", "151", ""});
                    }
                }
                else
                {
                    // Damage to primary battery barbette. On addtional roll of 01-30 mount is permanently OOA for the duration of the game.
                    // Mount is OOA next game turn only on roll of 31-00. Roll to determine location of mount.
                    if(TryToSampleAPrimaryBatteryMount(ctx, out var mountStatus))
                    {
                        if(RandomUtils.D100F() <= 30)
                        {
                            mountStatus.status = MaxEnum(mountStatus.status, MountStatus.Disabled);
                        }
                        else
                        {
                            // Disable the mount in the following turn (2min)
                            var damageEffect = new BatteryMountStatusModifier()
                            {
                                lifeCycle = StateLifeCycle.GivenTime,
                            };
                            damageEffect.BeginAt(mountStatus);
                        }
                    }

                    if (IsHE(ctx))
                    {
                        AddShipboardFire(ctx, "DE102 (HE): Shipboard fire Severity 30.", 30);
                    }
                }
            } },

            // DE 103 (Hit on primary battery mount)
            {"103", ctx=>{

                if(TryToSampleAPrimaryBatteryMount(ctx, out var mountStatus))
                {
                    var severity = RandomUtils.D100F() + ctx.shellDiameterInch;
                    if(!IsAB(ctx))
                    {
                        severity /= 2;
                    }

                    var DE = new BatteryMountStatusModifier()
                    {
                        lifeCycle = StateLifeCycle.SeverityBased,
                        severity = severity,
                        casue = "DE 103: One primary battery turret or gunmount OOA."
                    };
                    DE.BeginAt(mountStatus);
                }

                Lost1RandomRapidFiringBatteryBox(ctx);

                if(IsAB(ctx))
                {
                    RollForAdditionalDamageEffect(ctx,
                        new []{10f, 25, 45, 70, 100},
                        new []{"101", "146", "147", "153", ""});
                }

                if(IsHE(ctx))
                {
                    AddShipboardFire(ctx, "DE 103 (HE): Shipboard Fire 30", 30);
                    Lost1RandomRapidFiringBatteryBox(ctx);
                }
            }},

            // DE 104 (Hit on Non-Primary Battery Mount)
            {"104", ctx=>{

                if(TryToSampleASecondaryOrTertiaryBatteryMount(ctx, out var secondaryOrTertiaryMount))
                {
                    // One secondary OR tertiary battery turret or gunmount OOA. Permanent damage.
                    secondaryOrTertiaryMount.status = MaxEnum(secondaryOrTertiaryMount.status, MountStatus.Disabled);

                    if(IsAB(ctx))
                    {
                        // One adjacent mount in same section also permanently OOA on additional roll of 01-35
                        if(RandomUtils.D100F() <= 35)
                        {
                            if(TryToSampleAAdjacentMount(ctx, secondaryOrTertiaryMount, out var adjMount))
                            {

                                adjMount.status = MaxEnum(adjMount.status, MountStatus.Disabled);
                            }
                        }

                        RollForAdditionalDamageEffect(ctx,
                            new []{10f, 25, 45, 70, 100},
                            new []{"110", "110", "118", "120", ""});
                    }
                    if(IsHE(ctx))
                    {
                        AddShipboardFire(ctx, "DE 104 (HE): shipboard fire Severity 30", 30);
                    }
                }
                else // If no secondary or tertiary battery, use DE103 on a roll of 01-65. Otherwise no effect.
                {
                    if(RandomUtils.D100F() <= 65)
                    {
                        AddNewDamageEffect(ctx, "103");
                    }
                }
            }},

            // DE 105 (Hit on Ammo Hoist or handling room)
            {"105", ctx=>{
                if(TryToSampleAPrimaryBatteryMount(ctx, out var mount))
                {
                    var ammoHoistOOAPermanent = IsAB(ctx) || (RandomUtils.D100F() <= 60);
                    if(ammoHoistOOAPermanent)
                    {
                        var DE = new RateOfFireModifier()
                        {
                            lifeCycle = StateLifeCycle.Permanent,
                            casue = "DE 105: Ammo Hoist or handling/handling room in one primary battery turret or gunmount OOA permanently"
                        };
                        DE.BeginAt(mount);
                    }
                    else
                    {
                        var DE = new RateOfFireModifier()
                        {
                            lifeCycle = StateLifeCycle.GivenTime,
                            casue = "DE 104: Ammo Hoist or handling/handling room in one primary battery turret or gunmount OOA next turn"
                        };
                        DE.BeginAt(mount);
                    }

                    if(IsAB(ctx))
                    {
                        if(IsA(ctx))
                        {
                            AddShipboardFire(ctx, "DE 104 (A)", 30);
                        }
                        RollForAdditionalDamageEffect(ctx, new[]{"109", "109", "102", "179", ""});
                    }
                    // HE: if a CLASS A hit. on a roll of 01-20, fire in primary battery magazine.
                    // Roo to determine section location of affected magazine. Triple total DP caused by this hit.
                    // Magazines are flooded and all guns serviced by this magazine (located in affected section) may not fire for the duration of the battle.
                    if (IsHE(ctx) && ctx.hitPenDetType == HitPenDetType.PenetrateWithDetonate && RandomUtils.D100F() <= 20)
                    {
                        ctx.subject.damagePoint += 2 * ctx.baseDamagePoint;
                        var affectedLocation = mount.mountLocation;
                        foreach(var affectedMount in ctx.subject.batteryStatus.SelectMany(bs => bs.mountStatus)
                            .Where(mnt => mnt.mountLocation == affectedLocation))
                        {
                            affectedMount.status = MaxEnum(affectedMount.status, MountStatus.Disabled);
                        }
                    }
                }
            }},

            // DE 106
            { "106", ctx=>{
                if(TryToSampleASecondaryOrTertiaryBatteryMount(ctx, out var secOrTerMount))
                {
                    // var affectedMounts = new List<MountStatusRecord>();
                    // var word = "";
                    var DE = new RateOfFireModifier()
                    {
                        casue = $"DE 106: Ammo Hoist or handling/handling room OOA permanently"
                    };

                    if (IsAB(ctx))
                    {
                        var mountCtx = secOrTerMount.GetFullContext();
                        DE.BeginAt(mountCtx.batteryStatus);
                    }
                    else
                    {
                        DE.BeginAt(secOrTerMount);
                    }

                    if(IsHE(ctx) && ctx.hitPenDetType == HitPenDetType.PenetrateWithDetonate && RandomUtils.D100F() <= 10)
                    {
                        ctx.subject.damagePoint += ctx.baseDamagePoint; // DOuble total DP caused by this hit
                        var affectedSector = secOrTerMount.mountLocation;
                        foreach(var affectedMount in ctx.subject.batteryStatus.SelectMany(bs => bs.mountStatus)
                            .Where(mnt => mnt.mountLocation == affectedSector))
                        {
                            affectedMount.status = MaxEnum(affectedMount.status, MountStatus.Disabled);
                        }
                    }
                }
                else
                {
                    if(RandomUtils.D100F() <= 65)
                    {
                        AddNewDamageEffect(ctx, "105");
                    }
                }
            }},

            // DE 107 (Hit on control system of primary battery)
            {"107", ctx=>{
                if(TryGetPrimaryBattery(ctx, out var primaryBattery))
                {
                    // primaryBattery.ResetFiringTarget();
                    var DE = new FireControlSystemDisabledModifier()
                    {
                        lifeCycle = StateLifeCycle.GivenTime
                    };
                    DE.BeginAt(ctx.subject);

                    if(IsAB(ctx))
                    {
                        if(RandomUtils.D100F() <= 70)
                        {
                            if(TryToSampleAPrimaryBatteryMount(ctx, out var mount))
                            {
                                mount.status = MaxEnum(mount.status, MountStatus.Disabled); // one primary battey turret or gunmount jammed
                            }
                        }
                        RollForAdditionalDamageEffect(ctx, new[]{"115", "124", "140", "141", ""});
                    }
                    if(IsC(ctx))
                    {
                        AddShipboardFire(ctx, "DE 107 (C)", 10);
                    }
                    if(IsHE(ctx))
                    {
                        AddShipboardFire(ctx, "DE 107 (HE)", 30);
                    }
                }
            }},

            // DE 108, (hit on secondary battery control system)
            { "DE 108", ctx=>{

            }}
        };
    }

}