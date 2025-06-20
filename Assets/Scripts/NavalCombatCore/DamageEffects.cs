using System;
using System.Xml.Serialization;
using System.Xml;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.Distributions;
using Acornima.Ast;

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

        public float RollForSeverity() => RandomUtils.D100F() + shellDiameterInch;
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

        // public static bool IsA(DamageEffectContext ctx) => ctx.ammunitionType != AmmunitionType.HighExplosive && ctx.hitPenDetType == HitPenDetType.PenetrateWithDetonate;
        // public static bool IsB(DamageEffectContext ctx) => ctx.ammunitionType != AmmunitionType.HighExplosive && ctx.hitPenDetType == HitPenDetType.PassThrough;
        // public static bool IsC(DamageEffectContext ctx) => ctx.ammunitionType != AmmunitionType.HighExplosive && ctx.hitPenDetType == HitPenDetType.NoPenetration;
        public static bool IsA(DamageEffectContext ctx) => ctx.hitPenDetType == HitPenDetType.PenetrateWithDetonate;
        public static bool IsB(DamageEffectContext ctx) => ctx.hitPenDetType == HitPenDetType.PassThrough;
        public static bool IsC(DamageEffectContext ctx) => ctx.hitPenDetType == HitPenDetType.NoPenetration;

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

        public static bool TryGetSecondaryBattery(DamageEffectContext ctx, out BatteryStatus battery)
        {
            battery = null;
            if (ctx.subject.batteryStatus.Count <= 1)
                return false;
            battery = ctx.subject.batteryStatus[1];
            return true;
        }

        public static bool TryToSampleASecondaryBatteryMount(DamageEffectContext ctx, out MountStatusRecord mount)
        {
            mount = null;
            if (TryGetSecondaryBattery(ctx, out var secondaryBattery))
            {
                if (secondaryBattery.mountStatus.Count > 0)
                {
                    mount = RandomUtils.Sample(secondaryBattery.mountStatus);
                    return true;
                }
            }
            return false;
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

        public static bool TryToSampleATorpedoMount(DamageEffectContext ctx, out TorpedoMountStatusRecord mount)
        {
            mount = null;
            if (ctx.subject.torpedoSectorStatus.mountStatus.Count == 0)
                return false;

            mount = RandomUtils.Sample(ctx.subject.torpedoSectorStatus.mountStatus);
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

        public static void LostRandomRapidFiringBatteryBoxAndFCSBox(DamageEffectContext ctx, int batteryBoxLost, int fcsBoxLost)
        {
            // Permanent loss of 1 box in one Rapid Fire battery. Roll to determine battery (if more than one) and roll to determine location [Port/STBD]
            if (ctx.subject.rapidFiringStatus.Count > 0)
            {
                var rapidFiringStatus = RandomUtils.Sample(ctx.subject.rapidFiringStatus);
                var hitPort = RandomUtils.NextFloat() < 0.5f;
                if (hitPort)
                {
                    rapidFiringStatus.portMountHits += batteryBoxLost;
                }
                else
                {
                    rapidFiringStatus.starboardMountHits += batteryBoxLost;
                }
                rapidFiringStatus.fireControlHits += fcsBoxLost;
            }
        }

        public static void Lost1RandomRapidFiringBatteryBox(DamageEffectContext ctx)
        {
            // Permanent loss of 1 box in one Rapid Fire battery. Roll to determine battery (if more than one) and roll to determine location [Port/STBD]
            LostRandomRapidFiringBatteryBoxAndFCSBox(ctx, 1, 0);
        }

        public static void Lost1RandomRapidFiringBatteryBoxAnd1FCSBox(DamageEffectContext ctx)
        {
            // Permanent loss of 1 box in one Rapid Fire battery. Roll to determine battery (if more than one) and roll to determine location [Port/STBD]
            LostRandomRapidFiringBatteryBoxAndFCSBox(ctx, 1, 1);
        }

        public static void Lost1RandomSearchlight(DamageEffectContext ctx)
        {
            if (RandomUtils.D100F() <= 50)
            {
                ctx.subject.searchLightHits.portHit += 1;
            }
            else
            {
                ctx.subject.searchLightHits.starboardHit += 1;
            }
        }

        public static void AddShipboardFire(DamageEffectContext ctx, string cause, float severity)
        {
            var shipboardFire = new SubState()
            {
                lifeCycle = StateLifeCycle.ShipboardFire,
                cause = cause,
                severity = severity
            };
            shipboardFire.BeginAt(ctx.subject);
        }

        public static void SetOOA(MountStatusRecord mount)
        {
            mount.status = MaxEnum(mount.status, MountStatus.Disabled);
        }

        public static void SetOOA(TorpedoMountStatusRecord mount)
        {
            mount.status = MaxEnum(mount.status, MountStatus.Disabled);
        }

        public static void SetOOA(FireControlSystemStatusRecord fcs)
        {
            fcs.trackingState = TrackingSystemState.Destroyed;
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
                        cause = "DE100 (A/B): Magazine explosion."
                    };
                    damageEffect.BeginAt(ctx.subject);
                }
                else
                {
                    AddShipboardFire(ctx, "DE100 (C): Shipboard fire only.", 50);

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
                    AddShipboardFire(ctx, "DE100 (C): Shipboard fire only.", 40);
                }
                if (IsHE(ctx))
                {
                    Lost1RandomRapidFiringBatteryBox(ctx);
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
                }
                if (IsHE(ctx))
                {
                    AddShipboardFire(ctx, "DE102 (HE): Shipboard fire Severity 30.", 30);
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
                        cause = "DE 103: One primary battery turret or gunmount OOA."
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
                            cause = "DE 105: Ammo Hoist or handling/handling room in one primary battery turret or gunmount OOA permanently"
                        };
                        DE.BeginAt(mount);
                    }
                    else
                    {
                        var DE = new RateOfFireModifier()
                        {
                            lifeCycle = StateLifeCycle.GivenTime,
                            cause = "DE 104: Ammo Hoist or handling/handling room in one primary battery turret or gunmount OOA next turn"
                        };
                        DE.BeginAt(mount);
                    }

                    if(IsAB(ctx))
                    {
                        if(IsA(ctx))
                        {
                            AddShipboardFire(ctx, "DE 105 (A): Hit on Ammo Hoist or handling room", 30);
                        }
                        RollForAdditionalDamageEffect(ctx, new[]{"109", "109", "102", "179", ""});
                    }
                    // HE: if a CLASS A hit. on a roll of 01-20, fire in primary battery magazine.
                    // Roo to determine section location of affected magazine. Triple total DP caused by this hit.
                    // Magazines are flooded and all guns serviced by this magazine (located in affected section) may not fire for the duration of the battle.
                    if (IsHE(ctx) && IsA(ctx) && RandomUtils.D100F() <= 20)
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
                        cause = $"DE 106: Ammo Hoist or handling/handling room OOA permanently"
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

                    if(IsHE(ctx) && IsA(ctx) && RandomUtils.D100F() <= 10)
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
                    var DE = new ControlSystemDisabledModifier()
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
                if(TryGetSecondaryBattery(ctx, out var secondaryBattery))
                {
                    var DE = new BatteryMountStatusModifier()
                    {
                        cause="DE 108: Damage to secondary battery control system",
                        lifeCycle = StateLifeCycle.GivenTime
                    };
                    DE.BeginAt(secondaryBattery);

                    AddShipboardFire(ctx, "DE 108: Shipboard fire severity 10", 10);

                    if(RandomUtils.D100F() <= 70)
                    {
                        if(secondaryBattery.fireControlSystemStatusRecords.Count > 0)
                        {
                            var fcs = RandomUtils.Sample(secondaryBattery.fireControlSystemStatusRecords);
                            fcs.trackingState = TrackingSystemState.Destroyed;
                        }
                    }

                    RollForAdditionalDamageEffect(ctx, new[]{"110", "124", "140", "143", ""});

                    if(IsHE(ctx))
                    {
                        AddShipboardFire(ctx, "DE 108 (HE): Shipboard fire severity 30", 30);
                    }
                }
                else if(RandomUtils.D100F() <= 70)
                {
                    AddNewDamageEffect(ctx, "158");
                }
            }},

            // DE 109, (hit on ready-use ammo of primary battery and possibly cause fire and more catastrophe)
            {"109", ctx=>{
                if(TryGetPrimaryBattery(ctx, out var primaryBattery))
                {
                    if(IsAB(ctx))
                    {
                        if(IsA(ctx))
                        {
                            ctx.subject.damagePoint += ctx.baseDamagePoint * 2;
                            AddShipboardFire(ctx, "DE 109 (A): Shipboard fire severity 50", 50);

                            var DE = new RiskingInMagazineExplosion()
                            {
                                cause = "DE 109 (A): Potential magazine explosion in primary battery ready-use ammo",
                                lifeCycle = StateLifeCycle.GivenTime,
                                givenTimeSeconds = 120,
                                explosionProbPercent = 10,
                                sinkingThreshold = 25
                            };
                            DE.BeginAt(ctx.subject);
                        }
                        if(IsB(ctx))
                        {
                            ctx.subject.damagePoint += ctx.baseDamagePoint * 2;
                            AddShipboardFire(ctx, "DE 109 (B): Shipboard fire severity 30", 30);
                        }

                        if(TryToSampleAPrimaryBatteryMount(ctx, out var mount))
                        {
                            var DE = new BatteryMountStatusModifier(){
                                cause="DE 109 (AB): Battery mount temporarily OOA"
                            };
                            DE.BeginAt(mount);
                        }

                        RollForAdditionalDamageEffect(ctx, new[]{"100", "147", "153", "", ""});
                    }
                    else
                    {
                        if(TryToSampleAPrimaryBatteryMount(ctx, out var mount))
                        {
                            var DE = new BatteryMountStatusModifier(){
                                lifeCycle = StateLifeCycle.SeverityBased,
                                severity=ctx.RollForSeverity()
                            };
                            DE.BeginAt(mount);
                        }
                    }
                }
            }},

            // DE 110, (hit on ready-use ammo of secondary battery and possibly cause fire and more catastrophe)
            {"110", ctx=>{
                if(IsAB(ctx))
                {
                    if(TryToSampleASecondaryBatteryMount(ctx, out var mount))
                    {
                        ctx.subject.damagePoint += ctx.baseDamagePoint;
                        mount.status = MaxEnum(mount.status, MountStatus.Disabled);

                        var DE = new RiskingInMagazineExplosion()
                        {
                            cause = "DE 110 (A/B): Potential magazine explosion in secondary battery ready-use ammo",
                            lifeCycle = StateLifeCycle.GivenTime,
                            givenTimeSeconds = 120,
                            explosionProbPercent = 5,
                            sinkingThreshold = 25
                        };
                        DE.BeginAt(ctx.subject);

                        RollForAdditionalDamageEffect(ctx, new[]{"100", "161", "132", "132", ""});
                    }
                    else
                    {
                        AddShipboardFire(ctx, "DE 110 (A/B, no secondary battery): Shipboard fire severity 40", 40);
                    }
                }
                else
                {
                    if(TryToSampleASecondaryBatteryMount(ctx, out var mount))
                    {
                        mount.status = MaxEnum(mount.status, MountStatus.Disabled);
                    }
                    else
                    {
                        AddShipboardFire(ctx, "DE 110 (C, no secondary battery): Shipboard fire severity 30", 30);
                    }
                }
            }},

            // DE 111, hit on primary battery's one barrel
            {"111", ctx=>{
                if(TryToSampleAPrimaryBatteryMount(ctx, out var primaryBatteryMount))
                {
                    primaryBatteryMount.barrels = Math.Max(0, primaryBatteryMount.barrels - 1); // One gun in primary battery turret or gunmount OOA. Permannent damage.

                    if(IsAB(ctx))
                    {
                        Lost1RandomRapidFiringBatteryBox(ctx);

                        if(RandomUtils.D100F() <= 60)
                        {
                            primaryBatteryMount.status = MaxEnum(primaryBatteryMount.status, MountStatus.Disabled);
                            AddShipboardFire(ctx, "DE 111 (A/B): ready-use ammo fire, severity 50", 50);
                        }
                    }
                    if(IsHE(ctx))
                    {
                        AddShipboardFire(ctx, "DE 111 (HE): Shipboard fire severity 30", 30);
                    }
                }
            }},

            // DE 112, Shock damage. Primary battery guns and FCS out of alignment.
            { "112", ctx=>{
                if(TryGetPrimaryBattery(ctx, out var primaryBattery))
                {
                    var offset = (IsAB(ctx) && !IsHE(ctx)) ? -2 : -1;

                    var DE = new FireControlValueModifier()
                    {
                        cause = "DE 112: Shock damage. Primary battery guns and FCS out of alignment",
                        fireControlValueCoef = 0.5f,
                        fireControlValueOffset = offset,
                    };
                    DE.BeginAt(primaryBattery);

                    if(IsAB(ctx))
                    {
                        Lost1RandomRapidFiringBatteryBoxAnd1FCSBox(ctx);
                        if(RandomUtils.D100F() <= 60)
                        {
                            var DE2 = new ControlSystemDisabledModifier()
                            {
                                cause="DE 112: Shock Damage Impact Control System",
                                lifeCycle = StateLifeCycle.GivenTime,
                            };
                            DE2.BeginAt(ctx.subject);
                        }
                    }

                    if(IsHE(ctx))
                    {
                        AddShipboardFire(ctx, "DE 112 (HE): Shipboard fire severity 20", 20);
                    }
                }
            }},

            // DE 113: hit on secondary battery, FCS and rapid RF batteries
            {"113", ctx=>{
                if(TryToSampleASecondaryBatteryMount(ctx, out var secondaryMount))
                {
                    // secondaryMount.status = MaxEnum(secondaryMount.status, MountStatus.Disabled);
                    SetOOA(secondaryMount);

                    if (IsAB(ctx))
                    {
                        var fcsRecs = ctx.subject.batteryStatus[1].fireControlSystemStatusRecords;
                        if(fcsRecs.Count > 0)
                        {
                            var fcsRec = RandomUtils.Sample(fcsRecs);
                            SetOOA(fcsRec);
                            Lost1RandomRapidFiringBatteryBoxAnd1FCSBox(ctx);
                        }

                        // Port OR Starboard searchlight battery OOA. Permanent damage.
                        // if(RandomUtils.D100F() <= 50)
                        // {
                        //     ctx.subject.searchLightHits.portHit += 1;
                        // }
                        // else
                        // {
                        //     ctx.subject.searchLightHits.starboardHit += 1;
                        // }
                        Lost1RandomSearchlight(ctx);

                        RollForAdditionalDamageEffect(ctx, new[]{"114", "113", "161", "132", ""});
                    }
                    if(IsHE(ctx))
                    {
                        AddShipboardFire(ctx, "DE 113 (HE): Shipboard fire severity 20", 20);
                    }
                }
                else
                {
                    var percent = IsAB(ctx) ? 75 : 65;
                    if(RandomUtils.D100F() <= percent)
                    {
                        AddNewDamageEffect(ctx, "111");
                    }
                }
            }},

            // DE 114, hit on secondary magazine or read-use ammo
            {"114", ctx=>{
                if(TryToSampleASecondaryBatteryMount(ctx, out var secondaryMount))
                {
                    ctx.baseDamagePoint += ctx.baseDamagePoint;

                    if(IsAB(ctx))
                    {
                        var affectedMounts = ctx.subject.batteryStatus.SelectMany(bs => bs.mountStatus).Where(mnt => mnt.mountLocation == secondaryMount.mountLocation).ToList();
                        foreach(var mnt in affectedMounts)
                        {
                            SetOOA(mnt);
                        }
                    }
                    else
                    {
                        SetOOA(secondaryMount);
                        AddShipboardFire(ctx, "DE 114 (C): Ready-use ammo fire in secondary battery turret or mount. Shipboard fire severity 40", 40);
                    }
                    if(IsHE(ctx))
                    {
                        AddShipboardFire(ctx, "DE 114 (HE): Shipboard fire severity 30", 30);
                    }
                }
                else
                {
                    AddNewDamageEffect(ctx, "101");
                }
            }},

            // DE 115, Crew casualties in primary battery turret or gunmount
            {"115", ctx=>{
                if(TryToSampleAPrimaryBatteryMount(ctx, out MountStatusRecord mount))
                {
                    SetOOA(mount);
                    var percent = IsAB(ctx) ? 60 : 30;
                    if(RandomUtils.D100F() <= percent)
                    {
                        var fcsRecs = ctx.subject.batteryStatus[0].fireControlSystemStatusRecords;
                        if(fcsRecs.Count > 0)
                        {
                            var fcsRec = RandomUtils.Sample(ctx.subject.batteryStatus[0].fireControlSystemStatusRecords);
                            SetOOA(fcsRec);
                        }
                    }
                    if(IsAB(ctx))
                    {
                        Lost1RandomRapidFiringBatteryBox(ctx);
                    }
                    var disabledSeconds = IsHE(ctx) ? 240 : 120;
                    var DE = new BatteryMountStatusModifier()
                    {
                        lifeCycle = StateLifeCycle.GivenTime,
                        givenTimeSeconds = disabledSeconds,
                    };
                    DE.BeginAt(ctx.subject.batteryStatus[0]);
                }
            }},

            // DE 116, Damage to engine spaces
            {"116", ctx=>{
                if(IsAB(ctx))
                {
                    var idx = Categorical.Sample(new[]{10.0, 20, 70});
                    var speedOffset = idx - 3;
                    ctx.subject.dynamicStatus.maxSpeedKnotsOffset += speedOffset;

                    if(RandomUtils.D100F() <= 20)
                    {
                        ctx.subject.dynamicStatus.engineRoomHits += 1;
                    }

                    Lost1RandomRapidFiringBatteryBox(ctx);
                }
                else
                {
                    ctx.subject.dynamicStatus.maxSpeedKnotsOffset += -1;
                    if(RandomUtils.D100F() <= 40)
                    {
                        ctx.subject.dynamicStatus.accelerationOffset += -1; // There will be at least 0.5 knots accleartion though.
                    }
                }
                if(IsHE(ctx))
                {
                    AddShipboardFire(ctx, "DE 116 (HE): Shipboard fire severity 40", 40);
                    Lost1RandomRapidFiringBatteryBox(ctx);
                }
            }},

            // DE 117, Damage to engine room
            {"117", ctx=>{
                var tempEngineRoomHit = 1;
                if(IsAB(ctx))
                {
                    if(RandomUtils.D100F() <= 40)
                    {
                        tempEngineRoomHit += 1;
                    }
                    if(RandomUtils.D100F() <= 30)
                    {
                        AddShipboardFire(ctx, "DE 117 (AB): Shipboard fire severity 30", 30);
                    }
                    RollForAdditionalDamageEffect(ctx, new[]{"152", "116", "153", "154", ""});
                }
                var DE = new EngineRoomHitModifier()
                {
                    cause = "DE 117: Damage to engine room",
                    lifeCycle = StateLifeCycle.SeverityBased,
                    severity = ctx.RollForSeverity(),
                    engineRoomHitOffset = tempEngineRoomHit,
                };
                DE.BeginAt(ctx.subject);
                if(IsHE(ctx))
                {
                    AddShipboardFire(ctx, "DE 117 (HE): Shipboard fire severity 30", 30);
                }
            }},

            // DE 118, Damage to engine room
            { "118", ctx=>{
                if(IsAB(ctx))
                {
                    var idx = Categorical.Sample(new[]{60.0, 30, 10});
                    var speedOffset = -(idx + 1);
                    ctx.subject.dynamicStatus.maxSpeedKnotsOffset += speedOffset;
                    ctx.subject.dynamicStatus.accelerationOffset += -1;

                    RollForAdditionalDamageEffect(ctx, new[]{"117", "119", "123", "151", ""});
                }
                else
                {
                    ctx.subject.dynamicStatus.maxSpeedKnotsOffset += -1;
                }
                AddShipboardFire(ctx, "DE 118 (HE): Shipboard fire severity 20", 20);
                if(IsHE(ctx))
                {
                    AddShipboardFire(ctx, "DE 118 (HE): Shipboard fire severity 40", 40);
                }
            }},

            // TODO: DE 119, Skip, It's for oil fueled ship so table for 1880-1905 doesn't include it, and it require more state in ship class.
            // {"119", ctx=>{

            // }},

            // DE 120, Steam Line Damaged
            {"120", ctx=>{
                if(IsAB(ctx))
                {
                    var DE = new SteamLineDamaged()
                    {
                        lifeCycle=StateLifeCycle.SeverityBased,
                        severity=ctx.RollForSeverity(),
                        cause="DE 120, Steam Line Damaged"
                    };
                    DE.BeginAt(ctx.subject);
                    var DE2 = new DamageControlModifier()
                    {
                        lifeCycle=StateLifeCycle.GivenTime,
                        fireControlRatingOffset=-1,
                        cause="DE 120, Steam Line Damaged"
                    };
                    DE2.BeginAt(ctx.subject);
                }
                else
                {
                    ctx.subject.dynamicStatus.maxSpeedKnotsOffset += -1;
                }
                if(IsHE(ctx))
                {
                    AddShipboardFire(ctx, "DE 120 (HE): Damage to Steam Line, Shipboard fire severity 20", 20);
                }
            }},

            // DE 121
            {"121", ctx=>{
                if(IsAB(ctx))
                {
                    var DE = new FeedwaterPumpDamaged()
                    {
                        lifeCycle=StateLifeCycle.SeverityBased,
                        severity=ctx.RollForSeverity(),
                        cause="DE 121 (A/B), Damage to main feedwater pump"
                    };
                    DE.BeginAt(ctx.subject);

                    RollForAdditionalDamageEffect(ctx, new[]{"123", "130", "151", "154", ""});
                }
                else
                {
                    var DE = new FeedwaterPumpDamaged()
                    {
                        lifeCycle=StateLifeCycle.GivenTime,
                        cause="DE 121 (C), Damage to main feedwater pump"
                    };
                    DE.BeginAt(ctx.subject);
                }
                if(IsHE(ctx))
                {
                    AddShipboardFire(ctx, "DE 121 (HE): Damage to main feedwater pump, Shipboard fire severity 30", 30);
                }
            }},

            // DE 122, Damage to fuel supply (this is not included in the Ironclad DE table as well)
            {"122", ctx=>{
                // Damage to fuel supply. For the duration of the game, a roll of 01-10 at the beginning of any MOVEMENT PHASE causes the ship to lose power and reduce to one-half of maxnimum capable speed. 
                // If power is lost, rolls continue and ship may not begin acceleration until turn following a roll of 01-10 during the DAMAGE PHASE.
                if(IsAB(ctx))
                {
                    var DE = new FuelSupplyDamaged()
                    {
                        lifeCycle = StateLifeCycle.Permanent,
                        cause="DE 122: Damage to fuel supply",
                    };
                    DE.BeginAt(ctx.subject);
                }
                else
                {
                    var DE = new FuelSupplyDamaged()
                    {
                        lifeCycle = StateLifeCycle.GivenTime,
                        cause="DE 122: Damage to fuel supply",
                    };
                    DE.BeginAt(ctx.subject);
                }
                if(IsHE(ctx))
                {
                    AddShipboardFire(ctx, "DE 122 (HE): Damage to fuel supply, Shipboard fire severity 40", 40);
                }
            }},

            {"123", ctx=>{
                if(IsAB(ctx))
                {
                    // Flooding in one boiler room. Boiler room is permanently OOA
                    ctx.subject.dynamicStatus.boilerRoomFloodingHits += 1;
                    if(RandomUtils.D100F() <= 25)
                    {
                        ctx.subject.damagePoint += ctx.baseDamagePoint;
                    }
                    RollForAdditionalDamageEffect(ctx, new[]{"168", "170", "180", "182", ""});
                }
                else
                {
                    AddShipboardFire(ctx, "DE 123 (C): Shipboard fire severity 20", 20);
                }
                if(IsHE(ctx))
                {
                    AddShipboardFire(ctx, "DE 123 (HE): Shipboard fire severity 30", 30);
                }
            }},

            // DE 124, Lost off communication to engine room
            { "124", ctx=>{
                var DE = new EngineRoomCommunicationDamaged()
                {
                    cause="DE 124, Loss of communication to engine room"
                };
                DE.BeginAt(ctx.subject);

                RollForAdditionalDamageEffect(ctx, new[]{"147", "147", "127", "163", ""});

                if(IsHE(ctx))
                {
                    AddShipboardFire(ctx, "DE 124 (HE): Shipboard fire severity 20", 20);
                }
            }},

            // DE 125, Damage to boiler room
            {"125", ctx=>{
                if(IsAB(ctx))
                {
                    var bolierRoomHitOffset = RandomUtils.D100F() <= 40 ? 2 : 1;
                    var DE = new BoilerRoomHitModifier
                    {
                        lifeCycle = StateLifeCycle.SeverityBased,
                        severity = ctx.RollForSeverity(),
                        boilerRoomHitOffset = bolierRoomHitOffset,
                        cause = "DE 125 (A/B), Damage to boiler room"
                    };
                    DE.BeginAt(ctx.subject);

                    RollForAdditionalDamageEffect(ctx, new[]{"126", "119", "118", "154", ""});
                }
                else
                {
                    var DE = new BoilerRoomHitModifier
                    {
                        lifeCycle = StateLifeCycle.GivenTime,
                        boilerRoomHitOffset = 1,
                        cause = "DE 125 (C), Damage to boiler room"
                    };
                    DE.BeginAt(ctx.subject);
                }
                if(IsHE(ctx))
                {
                    AddShipboardFire(ctx, "DE 125 (HE): Shipboard fire severity 30", 30);
                }
            }},

            // DE 126
            {"126", ctx=>{
                if(IsAB(ctx))
                {
                    ctx.subject.dynamicStatus.engineRoomFloodingHits += 1;
                    if(RandomUtils.D100F() <= 20)
                    {
                        ctx.subject.dynamicStatus.boilerRoomFloodingHits += 1;
                    }

                    if(TryToSampleAPrimaryBatteryMount(ctx, out var primaryMount))
                    {
                        foreach(var mnt in ctx.subject.batteryStatus[0].mountStatus.Where(mnt => mnt.mountLocation == primaryMount.mountLocation))
                        {
                            var DE = new BatteryMountStatusModifier()
                            {
                                lifeCycle = StateLifeCycle.GivenTime,
                                cause = "DE 126 (A/B)"
                            };
                            DE.BeginAt(mnt);
                        }
                    }

                    RollForAdditionalDamageEffect(ctx, new[]{"125", "126", "169", "118", ""});
                }
                else
                {
                    AddShipboardFire(ctx, "DE 126 (C): Shipboard fire severity 20", 20);
                }
                if(IsHE(ctx))
                {
                    AddShipboardFire(ctx, "DE 126 (HE): Shipboard fire severity 30", 30);
                }
            }},

            // DE 127, hit on torpedo mount and smoke generator
            {"127", ctx=>{
                if(TryToSampleATorpedoMount(ctx, out var torpedoMount))
                {
                    var DE = new TorpedoMountDamaged()
                    {
                        lifeCycle = StateLifeCycle.Permanent,
                        operationalPercent = 50,
                        cause = "DE 127, hit on torpedo mount"
                    };
                    DE.BeginAt(torpedoMount);

                    if(IsHE(ctx))
                    {
                        var DE2 = new TorpedoMountModifer()
                        {
                            lifeCycle = StateLifeCycle.GivenTime,
                            givenTimeSeconds = 240,
                            cause = "DE 127 (HE)"
                        };
                        DE2.BeginAt(torpedoMount);
                    }
                }
                var DE3 = new SmokeGeneratorDamaged()
                {
                    lifeCycle = StateLifeCycle.Permanent,
                    availablePercent=50,
                    cause = "DE 127, hit on smoke generator"
                };
                DE3.BeginAt(ctx.subject);
                AddShipboardFire(ctx, "DE 127: hit on torpedo mount, Shipboard fire severity 30", 30);
                RollForAdditionalDamageEffect(ctx, new[]{"125", "126", "169", "118", ""});
            }},

            // DE 128, shipboard fire and smoke affect firing
            {"128", ctx=>{
                var disableTorpedo = IsHE(ctx);

                var DE = new SectorFireState()
                {
                    lifeCycle = StateLifeCycle.ShipboardFire,
                    severity = 50,
                    cause = "DE 128, shipboard fire and smoke affect firing",
                    fireAndSmokeLocation = RandomUtils.Sample(new List<SectorFireState.SectionLocation>(){
                        SectorFireState.SectionLocation.Front,
                        SectorFireState.SectionLocation.Midship,
                        SectorFireState.SectionLocation.After
                    }),
                    disableTorpedo=disableTorpedo
                };
                DE.BeginAt(ctx.subject);

                if(IsAB(ctx))
                {
                    RollForAdditionalDamageEffect(ctx, new[]{"158", "159", "163", "164", ""});
                }
            }},

            // DE 129, hit on searchlight and small arms stores (marine's stuff)
            {"129", ctx=>{
                Lost1RandomSearchlight(ctx);
                AddShipboardFire(ctx, "DE 129: Fire in small arms stores. Shipboard fire severity 30", 30);
                if(RandomUtils.D100F() <= 40)
                {
                    ctx.subject.damagePoint += ctx.baseDamagePoint;
                }
                // TODO: Reduce effectiveness of boarding party by 50% when performing grapple and board operations, Dave
                // Well SK5 don't include detailed boarding rule though, though in the battle of yalu China's navy tried it,
                // the moderate QF firepower is enought to counter it.
                RollForAdditionalDamageEffect(ctx, new[]{"159", "148", "141", "161", ""});
                if(IsHE(ctx))
                {
                    AddShipboardFire(ctx, "DE 129 (HE): Shipboard fire severity 30", 30);
                }
            } },

            // DE 130, hit on torpedo tube and smoke generator
            {"130", ctx=>{
                if(TryToSampleATorpedoMount(ctx, out var torpedoMount))
                {
                    SetOOA(torpedoMount);
                }

                ctx.subject.smokeGeneratorDisabled = true;

                if(TryToSampleAPrimaryBatteryMount(ctx, out var primaryMount))
                {
                    foreach(var affectedMount in ctx.subject.batteryStatus[0].mountStatus.Where(mnt => mnt.mountLocation == primaryMount.mountLocation))
                    {
                        var DE = new BatteryMountStatusModifier()
                        {
                            lifeCycle = StateLifeCycle.GivenTime,
                            cause = "DE 130: Temporary loss of power to primary battery gun in one section"
                        };
                        DE.BeginAt(affectedMount);
                    }
                }

                RollForAdditionalDamageEffect(ctx, new[]{"132", "132", "104", "124", "142"});

                if(IsHE(ctx))
                {
                    AddShipboardFire(ctx, "DE 130 (HE): Shipboard fire severity 30", 30);
                }
            }},

            // DE 131, Damage to ASW mount, it's not listed in 1880-1905 table and require extra modeling, so skip. (In fact, it's listed only in the DE table of 1923-1945)

            // DE 132, Hit on rapid fire battery, searchlight, and possibly secondary battery
            {"132", ctx=>{
                LostRandomRapidFiringBatteryBoxAndFCSBox(ctx, 2, 0);
                Lost1RandomSearchlight(ctx);
                if(RandomUtils.D100F() <= 75)
                {
                    if(TryToSampleASecondaryBatteryMount(ctx, out var mount))
                    {
                        SetOOA(mount);
                    }
                }
                if(IsAB(ctx))
                {
                    RollForAdditionalDamageEffect(ctx, new[]{"114", "104", "106", "113", "129"});
                }
                if(IsHE(ctx))
                {
                    AddShipboardFire(ctx, "DE 132 (HE): Shipboard fire severity 30", 30);
                }
            }},

            // DE 133, damage to steering gear
            {"133", ctx=>{
                if(IsAB(ctx))
                {
                    var isCourceChangeBlocked = RandomUtils.D100F() <= 45;
                    var DE = new DynamicModifier()
                    {
                        lifeCycle = StateLifeCycle.SeverityBased,
                        severity = ctx.RollForSeverity(),
                        maxSpeedKnotCoef = 0.5f,
                        isEvasiveManeuverBlocked = true,
                        isCourseChangeBlocked = isCourceChangeBlocked,
                        cause = "DE 133 (A/B), damage to steering gear"
                    };
                    DE.BeginAt(ctx.subject);

                    RollForAdditionalDamageEffect(ctx, new[]{"153", "151", "129", "131", "142"});
                }
                else
                {
                    AddShipboardFire(ctx, "DE 133 (C): Shipboard fire severity 20", 20);

                    var DE = new DynamicModifier()
                    {
                        lifeCycle = StateLifeCycle.GivenTime,
                        maxSpeedKnotCoef = 0.5f,
                        isEvasiveManeuverBlocked = true,
                        cause = "DE 133 (C), hit on steering gear"
                    };
                    DE.BeginAt(ctx.subject);
                }
                if(IsHE(ctx))
                {
                    AddShipboardFire(ctx, "DE 133 (HE): Shipboard fire severity 30", 30);
                }
            }},

            // DE 134, damage to rudder
            {"134", ctx=>{
                var DE = new RudderDamaged()
                {
                    lifeCycle = StateLifeCycle.SeverityBased,
                    severity = ctx.RollForSeverity(),
                    cause = "DE 134, damage to rudder"
                };
                DE.BeginAt(ctx.subject);
                if(IsAB(ctx))
                {
                    RollForAdditionalDamageEffect(ctx, new[]{"169", "171", "154", "155", ""});
                }
                if(IsHE(ctx))
                {
                    AddShipboardFire(ctx, "DE 134 (HE): Shipboard fire severity 30", 30);
                }
            }},

            // DE 135, rudder jammed
            {"135", ctx=>{
                var isTurnPortBlocked = false;
                var isTurnStarboardBlocked = false;
                if(RandomUtils.D100F() <= 50)
                {
                    isTurnPortBlocked = true;
                }
                else
                {
                    isTurnStarboardBlocked = true;
                }

                var DE = new DynamicModifier()
                {
                    lifeCycle = StateLifeCycle.SeverityBased,
                    severity = ctx.RollForSeverity(),
                    isTurnPortBlocked=isTurnPortBlocked,
                    isTurnStarboardBlocked=isTurnStarboardBlocked,
                    cause = "DE 135, rudder jammed"
                };

                if(IsAB(ctx))
                {
                    RollForAdditionalDamageEffect(ctx, new[]{"171", "171", "169", "182", ""});
                }
                if(IsHE(ctx))
                {
                    AddShipboardFire(ctx, "DE 135 (HE): Shipboard fire severity 30", 30);
                }
            }},

            // DE 136 Aviation fuel storage hit (SKip since it's 1923-1945 only (for seaplane I guess))

            // DE 137 Aircraft stowage hit, (Skip, 1923-1945 only, for seaplane carried ship)

            // DE 138 Hangar fire (Skip, 1923-1945 only, for seaplane carried ship)

            // DE 139 Damage to aircraft operations facilities (skip, 1923-1945 only, for seaplane carried ship)

            // DE 140 Signal bridge destroyed
            {"140", ctx=>{
                // TODO: Represent the SK5 command system with some means?
                if(IsAB(ctx))
                {
                    if (RandomUtils.D100F() <= 75)
                    {
                        // Fire Control Radar damaged.
                        if(ctx.subject.batteryStatus.Count > 0)
                        {
                            var battery = RandomUtils.Sample(ctx.subject.batteryStatus);
                            var btyRec = battery.GetBatteryRecord();
                            if(btyRec.fireControlRadarModifier > 0)
                            {
                                battery.fireControlRadarDisabled = true;
                            }
                            else
                            {
                                var DE = new FireControlValueModifier()
                                {
                                    lifeCycle = StateLifeCycle.Permanent,
                                    fireControlValueOffset = -1
                                };
                                DE.BeginAt(battery);
                            }
                        }
                    }

                    RollForAdditionalDamageEffect(ctx, new[]{"124", "143", "142", "157", ""});
                }
                if(IsHE(ctx))
                {
                    AddShipboardFire(ctx, "DE 140 (HE): Shipboard fire severity 30", 30);
                }
            } },

            // DE 141, Disruption to communications
            {"141", ctx=>{
                // TODO: Process Flag Command Rating related things
                Lost1RandomRapidFiringBatteryBox(ctx);
                if(IsAB(ctx))
                {
                    RollForAdditionalDamageEffect(ctx, new[]{"158", "159", "156", "157", ""});
                }
                if(IsHE(ctx) && RandomUtils.D100F() <= 50)
                {
                    AddShipboardFire(ctx, "DE 141 (HE): Shipboard fire severity 50", 50);
                }
            } },

            // DE 142, Temporary disruption to shipboard communications
            {"142", ctx=>{
                // TODO: Processcommunication related things
                Lost1RandomSearchlight(ctx);
                Lost1RandomRapidFiringBatteryBoxAnd1FCSBox(ctx);
                if(IsAB(ctx))
                {
                    RollForAdditionalDamageEffect(ctx, new[]{"124", "129", "140", "144", ""});
                }
                else
                {
                    if(RandomUtils.D100F() <= 35)
                    {
                        if(TryGetPrimaryBattery(ctx, out var primaryBattery))
                        {
                            if(primaryBattery.fireControlSystemStatusRecords.Count > 0)
                            {
                                var fcsRec = RandomUtils.Sample(primaryBattery.fireControlSystemStatusRecords);
                                SetOOA(fcsRec);
                            }
                        }
                    }
                }
                if(IsHE(ctx))
                {
                    AddShipboardFire(ctx, "DE 142 (HE): Shipboard fire severity 20", 20);
                }
            } },

            // DE 143, Bridge hit
            {"143", ctx=>{
                // TODO: Process Bridge Command Rating * Flag Command Rating
                // TODO: Process kill and replacement of captain
                var DE = new DynamicModifier()
                {
                    lifeCycle = StateLifeCycle.GivenTime,
                    isCourseChangeBlocked=true,
                    cause="DE 143, Bridge hit"
                };
                DE.BeginAt(ctx.subject);
                if(IsAB(ctx))
                {
                    RollForAdditionalDamageEffect(ctx, new[]{"144", "160", "161", "158", "159"});
                }
                else
                {
                    AddShipboardFire(ctx, "DE 143 (C): Shipboard fire severity 20", 20);
                }
                if(IsHE(ctx))
                {
                    AddShipboardFire(ctx, "DE 143 (HE): Shipboard fire severity 20", 30);
                }
            }},

            // DE 144, Flag Bridge hit
            {"144", ctx=>{
                // TODO: Process reduce of command
                if(RandomUtils.D100F() <= 30)
                {
                    AddShipboardFire(ctx, "DE 144 (A/B/C): Shipboard fire severity 30", 30);
                }
                if(IsHE(ctx) && RandomUtils.D100F() <= 50)
                {
                    AddShipboardFire(ctx, "DE 144 (HE): Shipboard fire severity 30", 30);
                }
            } },

            // DE 145, Bridge hit (destoyed or shock only)
            {"145", ctx=>{
                if(IsAB(ctx))
                {
                    // TODO: Process command related things
                    // For duration of this damage, ship must continue on same course at same speed.
                    var DE = new DynamicModifier()
                    {
                        lifeCycle = StateLifeCycle.SeverityBased,
                        severity = ctx.RollForSeverity(),
                        isCourseChangeBlocked = true,
                        isSpeedChangeBlocked = true,
                        cause = "DE 145, Bridge Destroyed"
                    };
                    DE.BeginAt(ctx.subject);

                    RollForAdditionalDamageEffect(ctx, new[]{"124", "160", "158", "159", ""});
                }
                else
                {
                    var DE = new DynamicModifier()
                    {
                        lifeCycle = StateLifeCycle.GivenTime,
                        isCourseChangeBlocked = true,
                        isSpeedChangeBlocked = true,
                        cause = "DE 145, Bridge hit"
                    };
                    DE.BeginAt(ctx.subject);
                }
                if(IsHE(ctx))
                {
                    AddShipboardFire(ctx, "DE 145 (HE): Shipboard fire severity 30", 30);
                }
            }},

            // DE 146, Shock and structural damage
            {"146", ctx=>{
                // TODO: Process command related things
                if(IsAB(ctx))
                {
                    RollForAdditionalDamageEffect(ctx, new[]{"109", "110", "176", "112", ""});
                    // No rolls for fighting shipboard fires (CHART N2) can be made during the DAMAGE PHASE of next turn.
                    var DE = new DamageControlModifier()
                    {
                        lifeCycle = StateLifeCycle.GivenTime,
                        isFightingFireBlocked = true,
                        cause = "DE 146, Shock and structural damage"
                    };
                    DE.BeginAt(ctx.subject);
                }
                if(IsHE(ctx))
                {
                    AddShipboardFire(ctx, "DE 146 (HE): Shipboard fire severity 30", 30);

                    var DE = new DamageControlModifier()
                    {
                        lifeCycle = StateLifeCycle.GivenTime,
                        fireControlRatingOffset = -1,
                        cause = "DE 146 (HE), Shock and structural damage"
                    };
                    DE.BeginAt(ctx.subject);
                }
            } },

            // DE 147, Heavy personnel casualties
            {"147", ctx=>{
                var DE = new DamageControlModifier
                {
                    lifeCycle = StateLifeCycle.GivenTime,
                    cause = "DE 147 (HP), Heavy personnel casualties",
                    isDamageControlBlocked = true
                };
                DE.BeginAt(ctx.subject);

                if(IsHE(ctx))
                {
                    AddShipboardFire(ctx, "DE 147 (HE): Shipboard fire severity 30", 30);
                }
            }},

            // DE 148, funnel damage
            {"148", ctx=>{
                // Adjust the total from CHART H by -1 for all guns and reduce maximum speed speed by 1 knot permanently.
                var DE = new FireControlValueModifier()
                {
                    lifeCycle = StateLifeCycle.Permanent,
                    fireControlValueOffset = -1,
                    cause = "DE 148, Funnel damage"
                };
                DE.BeginAt(ctx.subject);

                ctx.subject.dynamicStatus.maxSpeedKnotsOffset += -1;

                Lost1RandomRapidFiringBatteryBox(ctx);
                Lost1RandomSearchlight(ctx);
                RollForAdditionalDamageEffect(ctx, new[]{"132", "151", "161", "", ""});
                if(IsHE(ctx))
                {
                    AddShipboardFire(ctx, "DE 148 (HE): Shipboard fire severity 20", 20);
                }
            }},

            // DE 149, Damage to crew spaces
            {"149", ctx=>{
                Lost1RandomRapidFiringBatteryBox(ctx);
                if(IsAB(ctx))
                {
                    RollForAdditionalDamageEffect(ctx, new[]{"122", "147", "153", "154", "125"});
                }
                if(IsHE(ctx))
                {
                    AddShipboardFire(ctx, "DE 149 (HE): Damage to crew spaces, Shipboard fire severity 20", 20);
                }
            }},

            // DE 150, Damage to galley
            {"150", ctx=>{
                Lost1RandomRapidFiringBatteryBoxAnd1FCSBox(ctx);
                if(IsAB(ctx))
                {
                    RollForAdditionalDamageEffect(ctx, new[]{"162", "151", "169", "180", "125"});
                }
                if(IsHE(ctx))
                {
                    Lost1RandomRapidFiringBatteryBox(ctx);
                    AddShipboardFire(ctx, "DE 150 (HE): Damage to galley, Shipboard fire severity 30", 30);
                }
            }},

            // DE 151, Auxiliary powerplant OOA
            {"151", ctx=>{
                // TODO: Implement ship-level communication
                Lost1RandomRapidFiringBatteryBox(ctx);

                if(IsAB(ctx))
                {
                    RollForAdditionalDamageEffect(ctx, new[]{"126", "183", "125", "123", "180"});
                    ctx.subject.damageControlRatingHits += 1;
                }
                if(IsHE(ctx))
                {
                    AddShipboardFire(ctx, "DE 151 (HE): Auxiliary powerplant OOA, Shipboard fire severity 20", 20);
                }
            } },

            // DE 152, Main powerplant OOA
            {"152", ctx=>{
                ctx.subject.damageControlRatingHits += 1;
                if(IsAB(ctx))
                {
                    var d100 = RandomUtils.D100F();

                    var DE = new MainPowerplantOOA()
                    {
                        lifeCycle = StateLifeCycle.SeverityBased,
                        severity = ctx.RollForSeverity(),
                        cause = "DE 152, Main powerplant OOA",

                        rateOfFireCoef = d100 <= 70 ? 0.5f : 1,
                        isDamageControlBlocked = d100 <= 40
                    };
                    DE.BeginAt(ctx.subject);
                }
            }},

            // DE 153, damage to power distribution system
            {"153", ctx=>{
                ctx.subject.damageControlRatingHits += 1;

                if(IsAB(ctx))
                {
                    var locations = ctx.subject.batteryStatus.SelectMany(bs => bs.mountStatus).Select(mnt => mnt.mountLocation).ToHashSet();
                    var location =  RandomUtils.Sample(locations.ToList());

                    var DE = new PowerDistributionSymtemDamaged()
                    {
                        lifeCycle = StateLifeCycle.SeverityBased,
                        severity = ctx.RollForSeverity(),
                        cause = "DE 153, damage to power distribution system",
                        locations = new(){location}
                    };

                    RollForAdditionalDamageEffect(ctx, new[]{"110", "116", "133", "124", ""});
                }
            }},

            // DE 154, Fuel bunker hit (coal or oil)
            {"154", ctx=>{
                // TODO: Process cruise range effect
                // TODO: Process oil specific things
                if(IsAB(ctx))
                {
                    AddShipboardFire(ctx, "DE 154 (A/C): fuel bunker hit, Shipboard fire severity 20", 20);

                    if(RandomUtils.D100F() <= 20)
                    {
                        ctx.subject.dynamicStatus.engineRoomFloodingHits += 1;
                    }

                    RollForAdditionalDamageEffect(ctx, new[]{"174", "183", "167", "169", ""});
                }
                if(IsHE(ctx))
                {
                    AddShipboardFire(ctx, "DE 154 (HE): fuel bunker hit, Shipboard fire severity 40", 40);
                }
            } },

            // DE 155, Severe fire in flammables storage
            {"155", ctx=>{
                AddShipboardFire(ctx, "DE 155 (A/B/C): Severe fire in flammables storage, Shipboard fire severity 50", 50);
                if(IsHE(ctx))
                {
                    Lost1RandomRapidFiringBatteryBox(ctx);
                    if(RandomUtils.D100F() <= 60)
                    {
                        AddShipboardFire(ctx, "DE 155 (HE): Severe fire in flammables storage, Shipboard fire severity 30", 30);
                    }
                }
            }},
            
            // DE 156, hit on FCS in primary battery
            {"156", ctx=>{
                if(IsAB(ctx))
                {
                    if(TryGetPrimaryBattery(ctx, out var primaryBattery))
                    {
                        var DE = new BatteryFireContrlStatusDisabledModifier()
                        {
                            lifeCycle=StateLifeCycle.GivenTime,
                            cause = "DE 156 (A/B), hit on FCS in primary battery"
                        };
                        DE.BeginAt(primaryBattery);
                    }

                    Lost1RandomRapidFiringBatteryBox(ctx);

                    RollForAdditionalDamageEffect(ctx, new[]{"159", "130", "174", "", ""});
                }
                else
                {
                    if(TryGetPrimaryBattery(ctx, out var primaryBattery))
                    {
                        if(primaryBattery.fireControlSystemStatusRecords.Count > 0)
                        {
                            var fcsRec = RandomUtils.Sample(primaryBattery.fireControlSystemStatusRecords);
                            var DE = new BatteryFireContrlStatusDisabledModifier()
                            {
                                lifeCycle=StateLifeCycle.GivenTime,
                                cause = "DE 156 (C), hit on FCS in primary battery"
                            };
                            DE.BeginAt(fcsRec);
                        }
                    }
                }
                if(IsHE(ctx))
                {
                    ctx.subject.damageControlRatingHits += 1;
                    AddShipboardFire(ctx, "DE 156 (HE): Shipboard fire severity 20", 20);
                }
            }},

            // DE 157, hit on secondary battery FCS
            {"157", ctx=>{
                if(TryGetSecondaryBattery(ctx, out var battery))
                {
                    AddShipboardFire(ctx, "DE 157 (C): Shipboard fire severity 20", 20);

                    if(IsAB(ctx))
                    {
                        var DE = new BatteryFireContrlStatusDisabledModifier()
                        {
                            lifeCycle=StateLifeCycle.GivenTime,
                            cause = "DE 157 (A/B), hit on FCS in secondary battery"
                        };
                        DE.BeginAt(battery);
                    }
                    else
                    {
                        if(battery.fireControlSystemStatusRecords.Count > 0)
                        {
                            var fcsRec = RandomUtils.Sample(battery.fireControlSystemStatusRecords);
                            var DE = new BatteryFireContrlStatusDisabledModifier()
                            {
                                lifeCycle=StateLifeCycle.GivenTime,
                                cause = "DE 157 (A/B), hit on FCS in secondary battery"
                            };
                            DE.BeginAt(fcsRec);
                        }
                    }

                    if(IsHE(ctx))
                    {
                        ctx.subject.damageControlRatingHits += 1;
                        Lost1RandomRapidFiringBatteryBox(ctx);
                    }
                }
                else
                {
                    if(RandomUtils.D100F() <= 75)
                    {
                        AddNewDamageEffect(ctx, "156");
                    }
                }
            }},

            // DE 158, hit on FCS of primary battery
            {"158", ctx=>{
                if(TryGetPrimaryBattery(ctx, out var battery) && battery.fireControlSystemStatusRecords.Count > 0)
                {
                    var severity = ctx.RollForSeverity();
                    if(IsC(ctx))
                    {
                        severity /= 2;
                    }
                    var fcsRec = RandomUtils.Sample(battery.fireControlSystemStatusRecords);
                    var DE = new BatteryFireContrlStatusDisabledModifier()
                    {
                        lifeCycle=StateLifeCycle.SeverityBased,
                        severity = severity,
                        cause = "DE 158, hit on FCS in primary battery"
                    };
                    DE.BeginAt(fcsRec);

                    if(IsAB(ctx))
                    {
                        Lost1RandomRapidFiringBatteryBoxAnd1FCSBox(ctx);

                        RollForAdditionalDamageEffect(ctx, new[]{"130", "132", "142", "141", ""});
                    }

                    if(IsHE(ctx))
                    {
                        Lost1RandomRapidFiringBatteryBox(ctx);
                    }
                }
            }},

            // DE 159, Damage to one secondary battery fire control system.
            {"159", ctx=>{
                if(TryGetSecondaryBattery(ctx, out var battery) && battery.fireControlSystemStatusRecords.Count > 0)
                {
                    var fcsRec = RandomUtils.Sample(battery.fireControlSystemStatusRecords);
                    var DE = new BatteryFireContrlStatusDisabledModifier()
                    {
                        lifeCycle=StateLifeCycle.SeverityBased,
                        severity = ctx.RollForSeverity(),
                        cause = "DE 158, hit on FCS in primary battery"
                    };
                    DE.BeginAt(fcsRec);
                    if(IsAB(ctx))
                    {
                        RollForAdditionalDamageEffect(ctx, new[]{"158", "141", "163", "", ""});
                    }
                }
                else
                {
                    if(RandomUtils.D100F() <= 75)
                    {
                        AddNewDamageEffect(ctx, "158");
                    }
                }
            }},

            // DE 160, hit on FCS of primary battery
            {"160", ctx=>{
                
            }},
        };
    }

}