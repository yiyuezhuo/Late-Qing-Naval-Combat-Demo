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

    public class DamageEffectContext // Which is not serialized and just to be used in-demand so we can reference object relative freely.
    {
        public ShipLog subject;
        public float baseDamagePoint; // DE can be a DP multiplier
        public DamageEffectCause cause;
        public HitPenDetType hitPenDetType;
        public AmmunitionType ammunitionType;
        public float shellDiameterInch; // M2: Unspecified Damage severity = D100 + shell diameter (in inches)
        // public int chainNumber; // Additional damage effect would be blocked if chainNumber > 0
        public float addtionalDamageEffectProbility; // 0.0~1.0, Addtional DE will use the same probility to cause this DE. If an additional DE is not possible, prob should be set to 0.
        public object source;

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

        public float RollForSeverity()
        {
            var severity = RandomUtils.D100F() + shellDiameterInch;
            var severityMod = subject.GetSubStates<IDamageControlModifier>().Select(m => m.GetSeverityDieRollOffset()).DefaultIfEmpty(0).Sum();
            return severity + severityMod;
        }
    }

    // M1 Damage Effect
    public static class DamageEffectChart // This class is separated from the RuleChart, since it rely on ShipLog heavyly.
    {
        public static string AddNewDamageEffect(DamageEffectContext ctx)
        {
            var damageEffectId = RuleChart.ResolveDamageEffectId(ctx.cause);
            AddNewDamageEffect(ctx, damageEffectId);
            return damageEffectId;
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

        public static bool TryToSampleAPrimaryFireControlSystem(DamageEffectContext ctx, out FireControlSystemStatusRecord fcsRec)
        {
            if (TryGetPrimaryBattery(ctx, out var battery))
            {
                if (battery.fireControlSystemStatusRecords.Count > 0)
                {
                    fcsRec = RandomUtils.Sample(battery.fireControlSystemStatusRecords);
                    return true;
                }
            }
            fcsRec = null;
            return false;
        }

        public static bool TryToSampleASecondaryFireControlSystem(DamageEffectContext ctx, out FireControlSystemStatusRecord fcsRec)
        {
            if (TryGetSecondaryBattery(ctx, out var battery))
            {
                if (battery.fireControlSystemStatusRecords.Count > 0)
                {
                    fcsRec = RandomUtils.Sample(battery.fireControlSystemStatusRecords);
                    return true;
                }
            }
            fcsRec = null;
            return false;
        }

        public static bool TryToSampleAPrimaryOrSecondaryFireControlSystem(DamageEffectContext ctx, out FireControlSystemStatusRecord fcsRec)
        {
            var fcsRecs = ctx.subject.batteryStatus.SelectMany(bty => bty.fireControlSystemStatusRecords).ToList();
            if (fcsRecs.Count > 0)
            {
                fcsRec = RandomUtils.Sample(fcsRecs);
                return true;
            }
            fcsRec = null;
            return false;
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
                    // ctx.subject.damagePoint += ctx.baseDamagePoint * 2; // Triple total DP caused by this hit
                    ctx.subject.AddDamagePoint(ctx.baseDamagePoint * 2);

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
            var shipboardFire = new ShipboardFireState()
            {
                lifeCycle = StateLifeCycle.ShipboardFire,
                cause = cause,
                severity = severity
            };
            shipboardFire.BeginAt(ctx.subject);
        }

        public static bool CheckAndEnsureOneShotHappendState(DamageEffectContext ctx, string deCode)
        {
            if (ctx.subject.GetSubStates<OneShotDamageEffectHappend>().FirstOrDefault(ss => ss.damageEffectCode == deCode) == null)
            {
                var DE = new OneShotDamageEffectHappend()
                {
                    damageEffectCode = deCode
                };
                DE.BeginAt(ctx.subject);
                return true;
            }
            return false;
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

        public static void AddLogToSubject(DamageEffectContext ctx, string log)
        {
            ctx.subject.AddStringLog(log);
        }

        public static void AddDescription(DamageEffectContext ctx, string log)
        {
            AddLogToSubject(ctx, log);
        }
        
        // public enum VSectionLocation
        // {
        //     Front,
        //     Midship,
        //     After
        // }

        // public static VSectionLocation GetVSectionLocation(MountLocation mountLocation)
        // {
        //     if (mountLocation <= MountLocation.StarboardForward)
        //         return VSectionLocation.Front;
        //     else if (mountLocation <= MountLocation.StarboardMidship)
        //         return VSectionLocation.Midship;
        //     return VSectionLocation.After;
        // }

        public static Dictionary<string, Action<DamageEffectContext>> damageEffectMap = new() // DE id => Enforcer (enforcer will immdiately update some states and may append persistence DE state)
        {
            // DE 100, shell hit deck above the magazine
            { "100", ctx =>{
                if(IsAB(ctx)) // A/B
                {
                    AddDescription(ctx, "DE 100, Magazine explosion");

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
                    AddDescription(ctx, "DE 100, Shipboard fire only");

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
                    AddDescription(ctx, "DE 101, Fire in primary battery magazine");

                    // Fire in primary battery magazine... (Like DE 100 (C/HE))
                    FireInPrimaryBatteryMagazine(ctx);

                    // Additional Damage Effect Roll
                    RollForAdditionalDamageEffect(ctx,
                        new []{10f, 25, 45, 70, 100},
                        new []{"100", "147", "107", "146", ""});
                }
                else
                {
                    AddDescription(ctx, "DE 101, shipboard fire only");

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
                    AddDescription(ctx, "DE 102, Flooding in primary battery barbette.");

                    if (TryToSampleAPrimaryBatteryMount(ctx, out var mountStatus))
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

                    AddDescription(ctx, "DE 102, Damage to primary battery barbette.");

                    if (TryToSampleAPrimaryBatteryMount(ctx, out var mountStatus))
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
                AddDescription(ctx, "DE 103, Hit on a primary battery mount.");

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
                AddDescription(ctx, "DE 104, Hit on Non-Primary Battery Mount");

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
                AddDescription(ctx, "DE 105, Hit on Ammo Hoist or handling room");

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
                        // ctx.subject.damagePoint += 2 * ctx.baseDamagePoint;
                        ctx.subject.AddDamagePoint(2 * ctx.baseDamagePoint);

                        var affectedLocation = mount.mountLocation;
                        foreach(var affectedMount in ctx.subject.batteryStatus.SelectMany(bs => bs.mountStatus)
                            .Where(mnt => mnt.mountLocation == affectedLocation))
                        {
                            affectedMount.status = MaxEnum(affectedMount.status, MountStatus.Disabled);
                        }
                    }
                }
            }},

            // DE 106, Ammo Hoist or handling/handling room OOA permanently
            { "106", ctx=>{
                AddDescription(ctx, "DE 106, Ammo Hoist or handling/handling room OOA permanently");

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
                        // ctx.subject.damagePoint += ctx.baseDamagePoint; // DOuble total DP caused by this hit
                        ctx.subject.AddDamagePoint(ctx.baseDamagePoint);

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
                AddDescription(ctx, "DE 107, Hit on control system of primary battery");

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
                AddDescription(ctx, "DE 108, hit on secondary battery control system");

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
                AddDescription(ctx, "DE 108, hit on ready-use ammo of primary battery and possibly cause fire and more catastrophe");

                if(TryGetPrimaryBattery(ctx, out var primaryBattery))
                {
                    if(IsAB(ctx))
                    {
                        if(IsA(ctx))
                        {
                            // ctx.subject.damagePoint += ctx.baseDamagePoint * 2;
                            ctx.subject.AddDamagePoint(ctx.baseDamagePoint * 2);

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
                            // ctx.subject.damagePoint += ctx.baseDamagePoint * 2;
                            ctx.subject.AddDamagePoint(ctx.baseDamagePoint * 2);

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
                AddDescription(ctx, "DE 110, hit on ready-use ammo of secondary battery and possibly cause fire and more catastrophe");

                if(IsAB(ctx))
                {
                    if(TryToSampleASecondaryBatteryMount(ctx, out var mount))
                    {
                        // ctx.subject.damagePoint += ctx.baseDamagePoint;
                        ctx.subject.AddDamagePoint(ctx.baseDamagePoint);

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
                AddDescription(ctx, "DE 111, hit on primary battery's one barrel");

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
                AddDescription(ctx, "DE 112, Shock damage. Primary battery guns and FCS out of alignment.");

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
                AddDescription(ctx, "DE 113: hit on secondary battery, FCS and rapid RF batteries");

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
                AddDescription(ctx, "DE 114, hit on secondary magazine or read-use ammo");

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
                AddDescription(ctx, "DE 115, Crew casualties in primary battery turret or gunmount");

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
                AddDescription(ctx, "DE 116, Damage to engine spaces");

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
                AddDescription(ctx, "DE 117, Damage to engine room");

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
                AddDescription(ctx, "DE 118, Damage to engine room");

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

            // TODO: DE 119, Skip, It's for oil fueled ship, so table for 1880-1905 doesn't include it, addtitionally it require more state modeling in ship class.
            // {"119", ctx=>{

            // }},

            // DE 120, Steam Line Damaged
            {"120", ctx=>{
                AddDescription(ctx, "DE 120, Steam Line Damaged");

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
                        damageControlRatingOffset=-1,
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

            // DE 121, Damage to main feedwater pump
            {"121", ctx=>{
                AddDescription(ctx, "DE 121, Damage to main feedwater pump");

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

                AddDescription(ctx, "DE 122, Damage to fuel supply");

                if (IsAB(ctx))
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

            // DE 123, Flooding in one boiler room
            { "123", ctx=>{
                AddDescription(ctx, "DE 123, Flooding in one boiler room");

                if(IsAB(ctx))
                {
                    // Flooding in one boiler room. Boiler room is permanently OOA
                    ctx.subject.dynamicStatus.boilerRoomFloodingHits += 1;
                    if(RandomUtils.D100F() <= 25)
                    {
                        ctx.subject.AddDamagePoint(ctx.baseDamagePoint);
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
                AddDescription(ctx, "DE 124, Lost off communication to engine room");

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
                AddDescription(ctx, "DE 125, Damage to boiler room");

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

            // DE 126, Heavy flooding in one engine room
            {"126", ctx=>{
                AddDescription(ctx, "DE 126, Heavy flooding in one engine room");

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
                AddDescription(ctx, "DE 127, hit on torpedo mount and smoke generator");

                if(TryToSampleATorpedoMount(ctx, out var torpedoMount))
                {
                    var DE = new TorpedoMountDamaged()
                    {
                        lifeCycle = StateLifeCycle.Permanent,
                        operationalPercentange = 50,
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
                AddDescription(ctx, "DE 128, shipboard fire and smoke affect firing");

                // var disableTorpedo = IsHE(ctx);

                var fireAndSmokeVLocation = RandomUtils.Sample(new List<SectorFireState.SectionVLocation>(){
                    SectorFireState.SectionVLocation.Front,
                    SectorFireState.SectionVLocation.Midship,
                    SectorFireState.SectionVLocation.After
                });

                var DE = new SectorFireState()
                {
                    lifeCycle = StateLifeCycle.ShipboardFire,
                    severity = 50,
                    cause = "DE 128, shipboard fire and smoke affect firing",
                    fireAndSmokeVLocation = fireAndSmokeVLocation,
                    // disableTorpedo=disableTorpedo
                };
                DE.BeginAt(ctx.subject);

                if(IsHE(ctx))
                {
                    foreach(var torpedoMount in ctx.subject.torpedoSectorStatus.mountStatus)
                    {
                        var vLocation = SectorFireState.GetSectionLocation(torpedoMount.GetTorpedoMountLocationRecordInfo().record.mountLocation);
                        if(vLocation == fireAndSmokeVLocation)
                        {
                            var DESub = new TorpedoMountModifer()
                            {
                                lifeCycle=StateLifeCycle.Dependent,
                                dependentObjectId=DE.objectId,
                                cause = "DE 128, shipboard fire and smoke affect firing (sub)",
                            };
                            DESub.BeginAt(torpedoMount);
                        }
                    }
                }

                // var fireAndSmokeLocation = RandomUtils.Sample(new List<SectorFireState.SectionLocation>(){
                //     SectorFireState.SectionLocation.Front,
                //     SectorFireState.SectionLocation.Midship,
                //     SectorFireState.SectionLocation.After
                // });

                // var DEMaster = new PlaceholderState()
                // {
                //     lifeCycle = StateLifeCycle.ShipboardFire,
                //     severity = 50,
                //     cause = $"DE 128, shipboard fire and smoke affect firing ({fireAndSmokeLocation}, master)",
                // };
                // DEMaster.BeginAt(ctx.subject);

                // foreach(var btyMount in ctx.subject.batteryStatus.SelectMany(bty => bty.mountStatus))
                // {
                //     var mountLocation = btyMount.mountLocation;

                // }

                    if (IsAB(ctx))
                {
                    RollForAdditionalDamageEffect(ctx, new[]{"158", "159", "163", "164", ""});
                }
            }},

            // DE 129, hit on searchlight and small arms stores (marine's stuff)
            {"129", ctx=>{
                AddDescription(ctx, "DE 129, hit on searchlight and small arms stores");

                Lost1RandomSearchlight(ctx);
                AddShipboardFire(ctx, "DE 129: Fire in small arms stores. Shipboard fire severity 30", 30);
                if(RandomUtils.D100F() <= 40)
                {
                    // ctx.subject.damagePoint += ctx.baseDamagePoint;
                    ctx.subject.AddDamagePoint(ctx.baseDamagePoint);
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
                AddDescription(ctx, "DE 130, hit on torpedo tube and smoke generator");

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
                AddDescription(ctx, "DE 132, Hit on rapid fire battery, searchlight, and possibly secondary battery");

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
                AddDescription(ctx, "DE 133, damage to steering gear");

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
                AddDescription(ctx, "DE 134, damage to rudder");

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
                AddDescription(ctx, "DE 135, rudder jammed");

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
                AddDescription(ctx, "DE 140 Signal bridge destroyed");

                // TODO: Represent the SK5 command system with some means?
                if (IsAB(ctx))
                {
                    if (RandomUtils.D100F() <= 75)
                    {
                        // Fire Control Radar damaged.
                        if(ctx.subject.batteryStatus.Count > 0)
                        {
                            var battery = RandomUtils.Sample(ctx.subject.batteryStatus);
                            var btyRec = battery.GetBatteryRecord();
                            if(btyRec != null)
                            {
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
                    }

                    RollForAdditionalDamageEffect(ctx, new[]{"124", "143", "142", "157", ""});
                }
                if(IsHE(ctx))
                {
                    AddShipboardFire(ctx, "DE 140 (HE): Shipboard fire severity 30", 30);
                }
            }},

            // DE 141, Disruption to communications
            {"141", ctx=>{
                AddDescription(ctx, "DE 141, Disruption to communications");

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
            }},

            // DE 142, Temporary disruption to shipboard communications
            {"142", ctx=>{
                AddDescription(ctx, "DE 142, Temporary disruption to shipboard communications");

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
            }},

            // DE 143, Bridge hit
            {"143", ctx=>{
                AddDescription(ctx, "Bridge hit");

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
                AddDescription(ctx, "DE 144, Flag Bridge hit");

                // TODO: Process reduce of command
                if (RandomUtils.D100F() <= 30)
                {
                    AddShipboardFire(ctx, "DE 144 (A/B/C): Shipboard fire severity 30", 30);
                }
                if(IsHE(ctx) && RandomUtils.D100F() <= 50)
                {
                    AddShipboardFire(ctx, "DE 144 (HE): Shipboard fire severity 30", 30);
                }
            }},

            // DE 145, Bridge hit (destoyed or shock only)
            {"145", ctx=>{
                AddDescription(ctx, "DE 145, Bridge hit");

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
                AddDescription(ctx, "DE 146, Shock and structural damage");

                // TODO: Process command related things
                if (IsAB(ctx))
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
                        damageControlRatingOffset = -1,
                        cause = "DE 146 (HE), Shock and structural damage"
                    };
                    DE.BeginAt(ctx.subject);
                }
            } },

            // DE 147, Heavy personnel casualties
            {"147", ctx=>{
                AddDescription(ctx, "DE 147, Heavy personnel casualties");

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
                AddDescription(ctx, "DE 148, funnel damage");

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
                AddDescription(ctx, "DE 149, Damage to crew spaces");

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
                AddDescription(ctx, "DE 150, Damage to galley");

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
                AddDescription(ctx, "DE 151, Auxiliary powerplant OOA");

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
                AddDescription(ctx, "DE 152, Main powerplant OOA");

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
                AddDescription(ctx, "DE 153, damage to power distribution system");

                ctx.subject.damageControlRatingHits += 1;

                if(IsAB(ctx))
                {
                    var locations = ctx.subject.batteryStatus.SelectMany(bs => bs.mountStatus).Select(mnt => mnt.mountLocation).ToHashSet();
                    var location =  RandomUtils.Sample(locations.ToList());

                    // var DE = new PowerDistributionSymtemDamaged()
                    // {
                    //     lifeCycle = StateLifeCycle.SeverityBased,
                    //     severity = ctx.RollForSeverity(),
                    //     cause = "DE 153, damage to power distribution system",
                    //     locations = new(){location}
                    // };

                    var DEMaster = new PlaceholderState()
                    {
                        lifeCycle = StateLifeCycle.SeverityBased,
                        severity = ctx.RollForSeverity(),
                        cause = "DE 153, damage to power distribution system (master)",
                    };
                    DEMaster.BeginAt(ctx.subject);

                    foreach(var mount in ctx.subject.batteryStatus.SelectMany(bs => bs.mountStatus).Where(mnt => mnt.mountLocation == location))
                    {
                        var DESub = new BatteryMountStatusModifier()
                        {
                            lifeCycle=StateLifeCycle.Dependent,
                            dependentObjectId = DEMaster.objectId,
                            cause = "DE 153, damage to power distribution system (sub)",
                        };
                        DESub.BeginAt(mount);
                    }

                    RollForAdditionalDamageEffect(ctx, new[]{"110", "116", "133", "124", ""});
                }
            }},

            // DE 154, Fuel bunker hit (coal or oil)
            {"154", ctx=>{
                AddDescription(ctx, "DE 154, Fuel bunker hit (coal or oil)");

                // TODO: Process cruise range effect
                // TODO: Process oil specific things
                if (IsAB(ctx))
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
                AddDescription(ctx, "DE 155, Severe fire in flammables storage");

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
                AddDescription(ctx, "DE 156, hit on FCS in primary battery");

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
                AddDescription(ctx, "DE 157, hit on secondary battery FCS");

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
                AddDescription(ctx, "DE 158, hit on FCS of primary battery");

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
                AddDescription(ctx, "DE 159, Damage to one secondary battery fire control system.");

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
                AddDescription(ctx, "DE 160, hit on FCS of primary battery");

                if(TryToSampleAPrimaryFireControlSystem(ctx, out var fcsRec))
                {
                    SetOOA(fcsRec);
                    if(IsAB(ctx))
                    {
                        RollForAdditionalDamageEffect(ctx, new[]{"159", "141", "163", "", ""});
                    }
                    if(IsHE(ctx))
                    {
                        AddShipboardFire(ctx, "DE 160 (HE): Shipboard fire severity 20", 20);
                    }
                }
            }},

            // DE 161, hit on FCS of secondary battery
            {"161", ctx=>{
                AddDescription(ctx, "DE 161, hit on FCS of secondary battery");

                if(TryToSampleASecondaryFireControlSystem(ctx, out var fcsRec))
                {
                    SetOOA(fcsRec);
                    if(IsAB(ctx))
                    {
                        RollForAdditionalDamageEffect(ctx, new[]{"158", "110", "113", "", ""});
                    }
                    if(IsHE(ctx))
                    {
                        AddShipboardFire(ctx, "DE 161 (HE): Shipboard fire severity 20", 20);
                    }
                }
                else
                {
                    if(RandomUtils.D100F() <= 75)
                    {
                        AddNewDamageEffect(ctx, "160");
                    }
                }
            }},

            // DE 162, Damage to primary or secondary battery plotting room or transmitting station. (Start from 1906 table)
            {"162", ctx=>{
                AddDescription(ctx, "DE 162, Damage to primary or secondary battery plotting room or transmitting station.");

                if(IsAB(ctx))
                {
                    if(ctx.subject.batteryStatus.Count > 0)
                    {
                        var battery = RandomUtils.Sample(ctx.subject.batteryStatus);
                        var DE = new BatteryFireContrlStatusDisabledModifier()
                        {
                            lifeCycle=StateLifeCycle.SeverityBased,
                            severity = ctx.RollForSeverity(),
                            cause = "DE 162, damage to battery plotting room or transmitting station"
                        };
                        DE.BeginAt(battery);

                        RollForAdditionalDamageEffect(ctx, new[]{"116", "123", "160", "141", "126"});
                    }
                }
                else
                {
                    var fcsRecs = ctx.subject.batteryStatus.SelectMany(bs => bs.fireControlSystemStatusRecords).ToList();
                    if(fcsRecs.Count > 0)
                    {
                        var fcsRec = RandomUtils.Sample(fcsRecs);
                        SetOOA(fcsRec);
                    }
                }
                if(IsHE(ctx))
                {
                    ctx.subject.damageControlRatingHits += 1;
                }
            }},

            // DE 163, gunnery officer killed
            {"163", ctx=>{
                AddDescription(ctx, "DE 163, gunnery officer killed");

                if(TryGetPrimaryBattery(ctx, out var battery))
                {
                    var DE = new BatteryTargetChangeBlocker()
                    {
                        lifeCycle=StateLifeCycle.GivenTime,
                        cause="DE 163, Gunnery officer killed"
                    };
                    DE.BeginAt(battery);

                    var DE2 = new ElectronicSystemModifier()
                    {
                        lifeCycle=StateLifeCycle.GivenTime,
                        cause="DE 163, Gunnery officer killed",
                        isFireControlRadarDisabled = true,
                    };
                    DE.BeginAt(battery);
                }

                AddShipboardFire(ctx, "DE 163 (A/B/C): Shipboard fire severity 20", 20);
                Lost1RandomSearchlight(ctx);
                Lost1RandomRapidFiringBatteryBox(ctx);

                RollForAdditionalDamageEffect(ctx, new[]{"160", "161", "158", "159", ""});
            }},

            // DE 164, hit on Fire Control Radar (1923+ only)
            {"164", ctx=>{
                AddDescription(ctx, "DE 164, hit on Fire Control Radar");

                if(ctx.subject.batteryStatus.Count > 0)
                {
                    var battery = RandomUtils.Sample(ctx.subject.batteryStatus);

                    if(IsAB(ctx) && RandomUtils.D100F() <= 65)
                    {
                        var btyRec = battery.GetBatteryRecord();
                        if(btyRec.fireControlRadarModifier > 0)
                        {
                            battery.fireControlRadarDisabled = true;
                        }
                        else
                        {
                            var DE = new FireControlValueModifier()
                            {
                                cause="DE 164, hit on FCR",
                                fireControlValueOffset = -1
                            };
                            DE.BeginAt(battery);
                        }

                        AddShipboardFire(ctx, "DE 163 (A/B): Shipboard fire severity 20", 20);
                        Lost1RandomRapidFiringBatteryBox(ctx);
                    }
                    else
                    {
                        var btyRec = battery.GetBatteryRecord();
                        if(btyRec.fireControlRadarModifier > 0)
                        {
                            var DE = new ElectronicSystemModifier()
                            {
                                lifeCycle=StateLifeCycle.SeverityBased,
                                severity=ctx.RollForSeverity(),
                                isFireControlRadarDisabled = true,
                                cause="DE 164, hit on FCR",
                            };
                            DE.BeginAt(battery);
                        }
                        else
                        {
                            var DE = new FireControlValueModifier()
                            {
                                lifeCycle=StateLifeCycle.SeverityBased,
                                severity=ctx.RollForSeverity(),
                                fireControlValueOffset = -1,
                            };
                            DE.BeginAt(battery);
                        }
                    }
                }
            }},

            // DE 165, Surface OR air search radar damaged, skip

            // DE 166, Damage to officer's accommodations
            {"166", ctx=>{
                AddDescription(ctx, "DE 166, Damage to officer's accommodations");

                AddShipboardFire(ctx, "DE 166: Shipboard fire severity 20", 20);
                Lost1RandomRapidFiringBatteryBox(ctx);
                if(IsAB(ctx))
                {
                    RollForAdditionalDamageEffect(ctx, new[]{"136", "129", "130", "144", ""});
                }
                if(IsHE(ctx))
                {
                    AddShipboardFire(ctx, "DE 166 (HE): Shipboard fire severity 30", 30);
                }
            }},

            // DE 167, Heavy flooding causes list to [PORT/STARBOARD]
            {"167", ctx=>{
                AddDescription(ctx, "DE 167, Heavy flooding causes list to [PORT/STARBOARD]");

                if(IsAB(ctx))
                {
                    var DE = new BatteryMountStatusModifier()
                    {
                        lifeCycle=StateLifeCycle.GivenTime,
                        cause = "DE 167, Heavy flooding causes list to [PORT/STARBOARD]"
                    };
                    DE.BeginAt(ctx.subject);

                    var damageTier = ctx.subject.GetDamageTier();
                    if(damageTier >= 8)
                    {
                        ctx.subject.mapState = MapState.Destroyed; // capsize
                    }
                    else if(damageTier >= 4)
                    {
                        ctx.subject.damageControlRatingHits += 1;
                    }

                    if(RandomUtils.D100F() <= 50)
                    {
                        ctx.subject.dynamicStatus.boilerRoomFloodingHits += 1;
                    }
                    else
                    {
                        ctx.subject.dynamicStatus.engineRoomFloodingHits += 1;
                    }
                }
                else
                {
                    ctx.subject.damageControlRatingHits += 1;
                }
            }},

            // DE 168, severe flooding
            {"168", ctx=>{
                AddDescription(ctx, "DE 168, severe flooding");

                if(IsAB(ctx))
                {
                    var speedKnots = ctx.subject.speedKnots;
                    if(speedKnots >= 20)
                    {
                        // ctx.subject.damagePoint += 2 * ctx.baseDamagePoint;
                        ctx.subject.AddDamagePoint(2 * ctx.baseDamagePoint);
                    }
                    else if(speedKnots >= 14)
                    {
                        // ctx.subject.damagePoint += 1 * ctx.baseDamagePoint;
                        ctx.subject.AddDamagePoint(1 * ctx.baseDamagePoint);
                    }

                    var DE = new SevereFloodingState()
                    {
                        lifeCycle = StateLifeCycle.GivenTime,
                        givenTimeSeconds = 360,
                        cause = "DE 168 (A/B), Severe Flooding",
                    };
                    DE.BeginAt(ctx.subject);

                    RollForAdditionalDamageEffect(ctx, new[]{"102", "123", "154", "173", "180"});
                }
                else
                {
                    ctx.subject.damageControlRatingHits += 1;
                }
            }},

            // DE 169, Compartment flooding
            {"169", ctx=>{
                AddDescription(ctx, "DE 169, Compartment flooding");

                if(IsAB(ctx))
                {
                    Lost1RandomRapidFiringBatteryBox(ctx);

                    var DE = new SevereFloodingState()
                    {
                        lifeCycle = StateLifeCycle.GivenTime,
                        givenTimeSeconds = 240,
                        cause = "DE 169, Compartment Flooding",
                    };
                    DE.BeginAt(ctx.subject);

                    RollForAdditionalDamageEffect(ctx, new[]{"180", "123", "182", "183", "180"});
                }
                else
                {
                    AddShipboardFire(ctx, "DE 169 (C): Shipboard fire severity 20", 20);
                }
            }},

            // DE 170, Main waterline belt is submerged due to flooding and list.
            {"170", ctx=>{
                AddDescription(ctx, "DE 170, Main waterline belt is submerged due to flooding and list.");

                if(IsAB(ctx))
                {
                    var DE = new ArmorModifier()
                    {
                        lifeCycle=StateLifeCycle.Permanent,
                        mainBeltArmorCoef = 0.5f,
                        cause= "DE 170 (A/B): Main waterline belt is submerged due to flooding and list."
                    };
                    DE.BeginAt(ctx.subject);

                    if(RandomUtils.D100F() <= 60)
                    {
                        if(RandomUtils.D100F() <= 50)
                        {
                            ctx.subject.dynamicStatus.boilerRoomFloodingHits += 1;
                        }
                        else
                        {
                            ctx.subject.dynamicStatus.engineRoomFloodingHits += 1;
                        }
                    }

                    RollForAdditionalDamageEffect(ctx, new[]{"172", "156", "157", "123", ""});
                }
                else
                {
                    AddShipboardFire(ctx, "DE 170 (C): Shipboard fire severity 20", 20);
                }
            }},

            // DE 171, Damage to prop/shaft
            {"171", ctx=>{
                AddDescription(ctx, "DE 171, Damage to prop/shaft");

                ctx.subject.dynamicStatus.propulsionShaftHits += 1;
                if(IsAB(ctx))
                {
                    RollForAdditionalDamageEffect(ctx, new[]{"173", "169", "153", "154", ""});
                }
                if(IsHE(ctx))
                {
                    AddShipboardFire(ctx, "DE 171 (HE): Shipboard fire severity 20", 20);
                }
            }},

            // DE 172, Uncontrolled flooding possible.
            {"172", ctx=>{
                AddDescription(ctx, "DE 172, Uncontrolled flooding possible.");

                if(IsAB(ctx))
                {
                    var DE = new SevereFloodingState()
                    {
                        lifeCycle = StateLifeCycle.GivenTime,
                        givenTimeSeconds = 360,
                        dieRollOffset = 10,
                        cause = "DE 172 (A/B): Uncontrolled flooding possible",
                    };
                    DE.BeginAt(ctx.subject);

                    RollForAdditionalDamageEffect(ctx, new[]{"180", "182", "183", "123", ""});
                }
                else
                {
                    ctx.subject.damageControlRatingHits += 1;
                }
            }},

            // DE 173, Possible severe damage to watertight bulkhead for ships with damaged machinery spaces.
            {"173", ctx=>{
                AddDescription(ctx, "DE 173, Possible severe damage to watertight bulkhead for ships with damaged machinery spaces.");

                if(IsAB(ctx))
                {
                    if(ctx.subject.dynamicStatus.engineRoomFloodingHits > 0 || ctx.subject.dynamicStatus.engineRoomHits > 0 ||
                        ctx.subject.dynamicStatus.boilerRoomFloodingHits > 0 || ctx.subject.dynamicStatus.boilerRoomHits > 0)
                    {
                        var DE = new SevereFloodingState()
                        {
                            lifeCycle = StateLifeCycle.GivenTime,
                            givenTimeSeconds = 360,
                            cause = "DE 173 (A/B): Possible severe damage to watertight bulkhead for ships with damaged machinery spaces",
                        };
                        DE.BeginAt(ctx.subject);
                    }
                    else
                    {
                        AddNewDamageEffect(ctx, "169");
                    }
                }
                else
                {
                    if(RandomUtils.D100F() <= 80)
                    {
                        ctx.subject.dynamicStatus.maxSpeedKnotsOffset += -1;
                    }
                }
            }},

            // DE 174, Damage to crew's mess
            {"174", ctx=>{
                AddDescription(ctx, "DE 174, Damage to crew's mess");

                if(IsHE(ctx))
                {
                    AddShipboardFire(ctx, "DE 174 (HE): Damage to crew's mess, Shipboard fire severity 30", 30);
                    LostRandomRapidFiringBatteryBoxAndFCSBox(ctx, 2, 0);
                }
                else
                {
                    AddShipboardFire(ctx, "DE 174 (A/B/C): Damage to crew's mess, Shipboard fire severity 30", 30);
                    Lost1RandomRapidFiringBatteryBoxAnd1FCSBox(ctx);
                    if(IsAB(ctx))
                    {
                        RollForAdditionalDamageEffect(ctx, new[]{"160", "125", "161", "151", "142"});
                    }
                }
            }},

            // DE 175, Damage to junior officer's quarters.
            {"175", ctx=>{
                AddDescription(ctx, "DE 175, Damage to junior officer's quarters.");

                AddShipboardFire(ctx, "DE 175: Damage to junior officer's quarters, Shipboard fire severity 20", 20);
                if(IsAB(ctx))
                {
                    RollForAdditionalDamageEffect(ctx, new[]{"120", "121", "122", "123", "120"});
                }
                if(IsHE(ctx))
                {
                    AddShipboardFire(ctx, "DE 175 (HE): Damage to junior officer's quarters, Shipboard fire severity 20", 20);
                }
            }},

            // DE 176, Additional structural damage
            {"176", ctx=>{
                AddDescription(ctx, "DE 176, Additional structural damage");

                AddShipboardFire(ctx, "DE 176: Additional structural damage, Shipboard fire severity 20", 20);

                // ctx.subject.damagePoint += ctx.baseDamagePoint;
                ctx.subject.AddDamagePoint(ctx.baseDamagePoint);

                if (IsAB(ctx))
                {
                    RollForAdditionalDamageEffect(ctx, new[]{"122", "105", "103", "118", "120"});
                }
            }},

            // DE 177, Damage to sick bay
            {"177", ctx=>{
                AddDescription(ctx, "DE 177, Damage to sick bay");

                if(IsAB(ctx))
                {
                    RollForAdditionalDamageEffect(ctx, new[]{"162", "110", "118", "120", "123"});
                    if(IsA(ctx))
                    {
                        AddShipboardFire(ctx, "DE 177(A): Damage to sick bay, Shipboard fire severity 30", 30);
                    }
                }
                else
                {
                    AddShipboardFire(ctx, "DE 177(C): Damage to sick bay, Shipboard fire severity 20", 20);
                }
            }},

            // DE 178, Damage to senior officer's quaters
            {"178", ctx=>{
                AddDescription(ctx, "DE 178, Damage to senior officer's quaters");

                if(IsAB(ctx))
                {
                    Lost1RandomSearchlight(ctx);
                    RollForAdditionalDamageEffect(ctx, new[]{"124", "128", "130", "132", "120"});
                }
                else
                {
                    AddShipboardFire(ctx, "DE 178(C): Damage to senior officer's quaters, Shipboard fire severity 20", 20);
                }
                if(IsHE(ctx))
                {
                    AddShipboardFire(ctx, "DE 178(HE): Damage to senior officer's quaters, Shipboard fire severity 30", 30);
                }
            }},

            // DE 179, Loss of belt armor plate.
            {"179", ctx=>{
                AddDescription(ctx, "DE 179, Loss of belt armor plate.");

                if(!IsHE(ctx) || IsA(ctx))
                {
                    if(ctx.subject.shipClass.armorRating.mainBelt.effectInch > 0)
                    {
                        var DE = new ArmorModifier()
                        {
                            lifeCycle = StateLifeCycle.Permanent,
                            mainBeltArmorCoef = 0.5f
                        };
                        DE.BeginAt(ctx.subject);
                    }
                    if(IsAB(ctx))
                    {
                        RollForAdditionalDamageEffect(ctx, new[]{"116", "121", "120", "119", "123"});
                    }
                }
            }},

            // DE 180, Loss of systems due to flooding
            {"180", ctx=>{
                AddDescription(ctx, "DE 180, Loss of systems due to flooding");

                if(IsAB(ctx))
                {
                    var DE = new SevereFloodingState()
                    {
                        lifeCycle = StateLifeCycle.GivenTime,
                        givenTimeSeconds = 180,
                        cause = "DE 180 (A/B): Loss of systems due to flooding",
                    };
                    DE.BeginAt(ctx.subject);
                }
                else
                {
                    if(RandomUtils.D100F() <= 80)
                    {
                        ctx.subject.dynamicStatus.maxSpeedKnotsOffset += -1;
                    }
                }
                if(IsHE(ctx))
                {
                    AddShipboardFire(ctx, "DE 180 (HE): Loss of systems due to flooding, Shipboard fire severity 30", 30);
                }
            }},

            // DE 181, Bow of ship breaks off.
            {"181", ctx=>{
                AddDescription(ctx, "DE 181, Bow of ship breaks off.");

                if(IsAB(ctx))
                {
                    var DE = new DynamicModifier()
                    {
                        lifeCycle = StateLifeCycle.SeverityBased,
                        severity = ctx.RollForSeverity(),
                        maxSpeedUpperLimit = 6
                    };
                    DE.BeginAt(ctx.subject);

                    if(ctx.subject.speedKnots >= 18)
                    {
                        // ctx.subject.damagePoint += ctx.baseDamagePoint * 2;
                        ctx.subject.AddDamagePoint(ctx.baseDamagePoint * 2);
                    }
                    else
                    {
                        // ctx.subject.damagePoint += ctx.baseDamagePoint;
                        ctx.subject.AddDamagePoint(ctx.baseDamagePoint);
                    }

                    var DE2 = new SevereFloodingState()
                    {
                        lifeCycle = StateLifeCycle.GivenTime,
                        givenTimeSeconds = 480,
                        cause = "DE 181: Bow of ship breaks off",
                    };
                    DE2.BeginAt(ctx.subject);
                }
            }},

            // DE 182, Progressive loss of power systems due to flooding
            {"182", ctx=>{
                AddDescription(ctx, "DE 182, Progressive loss of power systems due to flooding");

                if(IsAB(ctx))
                {
                    var DE = new SevereFloodingState()
                    {
                        lifeCycle = StateLifeCycle.GivenTime,
                        givenTimeSeconds = 360,
                        cause = "DE 182: Progressive loss of power systems due to flooding",
                    };
                    DE.BeginAt(ctx.subject);
                }
                else
                {
                    if(RandomUtils.D100F() <= 80)
                    {
                        ctx.subject.dynamicStatus.maxSpeedKnotsOffset += -1;
                    }
                }
                if(IsHE(ctx))
                {
                    Lost1RandomRapidFiringBatteryBox(ctx);
                }
            }},

            // DE 183, Damage to waterline bulkheads causes additional flooding.
            {"183", ctx=>{
                AddDescription(ctx, "DE 183, Damage to waterline bulkheads causes additional flooding.");

                if(IsAB(ctx))
                {
                    var DE = new SevereFloodingState()
                    {
                        lifeCycle = StateLifeCycle.GivenTime,
                        givenTimeSeconds = 240,
                        cause = "DE 183: Damage to waterline bulkheads causes additional flooding",
                    };
                    DE.BeginAt(ctx.subject);

                    Lost1RandomRapidFiringBatteryBox(ctx);
                }
                else
                {
                    AddShipboardFire(ctx, "DE 183 (C): Damage to waterline bulkheads causes additional flooding, Shipboard fire severity 20", 20);
                }
                if(IsHE(ctx) && RandomUtils.D100F() <= 20 && IsA(ctx))
                {
                    AddNewDamageEffect(ctx, "154");
                }
            }},

            // DE 184, Damage to flight Deck, Skip
            // DE 185, Damage to elevator, skip
            // DE 186, Damage to hangar, skip
            // DE 187, Hangar fire, skip

            // DE 501, Permanent damage to flooding valves or circuits.
            {"501", ctx=>{
                AddDescription(ctx, "DE 501, Permanent damage to flooding valves or circuits.");

                // Ref: https://groups.io/g/SEEKRIEG/topic/67567107#msg3706
                // The reference to DE101/114 is an artifact from an earlier playtest
                // version prior to crafting CHART M6.  The +10 should be applied to
                // rolls against CHART M6.
                var DE = new SevereFloodingRollModifier()
                {
                    cause = "Permanent damage to flooding valves or circuits.",
                    severeFloodingRollOffset=10
                };
                DE.BeginAt(ctx.subject); // M6
            }},

            // DE 502, Damage Control party trapped by fires
            {"502", ctx=>{
                AddDescription(ctx, "DE 502, Damage Control party trapped by fires");

                ctx.subject.damageControlRatingHits += 1;
            }},

            // DE 503, Damage control system OOA.
            {"503", ctx=>{
                AddDescription(ctx, "DE 503, Damage control system OOA.");

                var DE = new DamageControlModifier()
                {
                    cause="DE 503: Damage control system OOA",
                    damageControlDieRollOffset=10 // For M3 Die Roll
                };
                DE.BeginAt(ctx.subject);
            }},

            // DE 504, Loss of power to fire control radar.
            {"504", ctx=>{
                AddDescription(ctx, "DE 504, Loss of power to fire control radar.");

                foreach(var battery in ctx.subject.batteryStatus)
                {
                    battery.fireControlRadarDisabled = true;
                }
                ctx.subject.searchLightHits.portHit += 1;
                ctx.subject.searchLightHits.starboardHit += 1;
                foreach(var rfBty in ctx.subject.rapidFiringStatus)
                {
                    var rfRec = rfBty.GetRapidFireBatteryRecord();
                    rfBty.fireControlHits += rfRec.fireControlRecords.Count;
                }
            }},

            // DE 505, Loss of power to surface OR air seartch radar.
            {"505", ctx=>{
                AddDescription(ctx, "DE 505, Loss of power to surface OR air seartch radar.");

                // TODO: Radar
                ctx.subject.searchLightHits.portHit += 1;
                ctx.subject.searchLightHits.starboardHit += 1;
            } },

            // DE 506, Gunnery damage control party trapped by fires.
            { "506", ctx=>{
                AddDescription(ctx, "DE 506, Gunnery damage control party trapped by fires.");

                var DE = new DamageControlModifier()
                {
                    cause = "DE 506: Gunnery damage control party trapped by fires",
                    isBatteryDamageControlBlock = true,
                };
                DE.BeginAt(ctx.subject);
            }},

            // DE 507, Localized damage to fire-fighting systems.
            {"507", ctx=>{
                AddDescription(ctx, "DE 507, Localized damage to fire-fighting systems.");

                if(ctx.source is SubState subState)
                {
                    subState.severity *= 1.5f;
                }
            }},

            // DE 508, Damage to circuit in one primary OR secondary battery fire control system.
            {"508", ctx=>{
                AddDescription(ctx, "DE 508, Damage to circuit in one primary OR secondary battery fire control system.");

                if(TryToSampleAPrimaryOrSecondaryFireControlSystem(ctx, out var fcsRec))
                {
                    SetOOA(fcsRec);
                }
            }},

            // DE 509, Loss communications to one primary OR secondary battery fire control system.
            {"509", ctx=>{
                AddDescription(ctx, "DE 509, Loss communications to one primary OR secondary battery fire control system.");

                if(TryToSampleAPrimaryOrSecondaryFireControlSystem(ctx, out var fcsRec))
                {
                    var DE = new LossOfCommunicationToFireControlSystemState()
                    {
                        cause = "DE 509, Loss communications to one primary OR secondary battery fire control system."
                    };
                    DE.BeginAt(ctx.subject);
                }
            }},

            // DE 510, Damage to power distribution system.
            {"510", ctx=>{
                AddDescription(ctx, "DE 510, Damage to power distribution system.");

                // TODO: Command
                var locations = ctx.subject.batteryStatus.SelectMany(bty => bty.mountStatus).Select(mnt => mnt.GetMountLocationRecordInfo().record.mountLocation).ToList();
                if(locations.Count > 0)
                {
                    var location = RandomUtils.Sample(locations);
                    foreach(var bty in ctx.subject.batteryStatus)
                    {
                        foreach(var mnt in bty.mountStatus)
                        {
                            if(mnt.GetMountLocationRecordInfo().record.mountLocation == location)
                            {
                                SetOOA(mnt);
                            }
                        }
                    }
                }

                Lost1RandomRapidFiringBatteryBox(ctx);
            } },

            // DE 511, Loss of communications and power to one searchlight battery.
            {"511", ctx=>{
                AddDescription(ctx, "DE 511, Loss of communications and power to one searchlight battery.");

                var DE = new LossOfCommunicationsAndPowerToSearchLight()
                {
                    location = RandomUtils.D100F() <= 50 ? RapidFiringBatteryLocation.Port : RapidFiringBatteryLocation.Starboard,
                    succPercentage = 30,
                    cause="DE 511, Loss of communications and power to one searchlight battery."
                };
                DE.BeginAt(ctx.subject);

                Lost1RandomRapidFiringBatteryBox(ctx);
            }},

            // DE 512, Communication curcuits destroyed - no radio communications possible
            {"512", ctx=>{
                AddDescription(ctx, "DE 512, Communication curcuits destroyed");

                // TODO: Handle Command
            } },

            // DE 513, Disruption to communications circuits - no radio communications possible.
            { "513", ctx=>{
                AddDescription(ctx, "DE 513, Disruption to communications circuits");

                // TODO: Command
                Lost1RandomSearchlight(ctx);
            } },

            // DE 514, Loss of communication to engine room
            { "514", ctx=>{
                AddDescription(ctx, "DE 514, Loss of communication to engine room");

                // TODO: Command
                var DE = new LossOfCommunicationToEngineRoom()
                {
                    cause = "DE 514, Loss of communication to engine room",
                    succPercentage = 50,
                };
                DE.BeginAt(ctx.subject);
            } },

            // DE 515, Primary battery handling or handling room abandoned.
            {"515", ctx=>{
                AddDescription(ctx, "DE 515, Primary battery handling or handling room abandoned.");

                if(TryToSampleAPrimaryBatteryMount(ctx, out var mount))
                {
                    var DE = new BatteryHandlingRoomAbandoned()
                    {
                        lifeCycle = StateLifeCycle.GivenTime,
                        givenTimeSeconds = 240,
                        cause = "DE 515, Primary battery handling or handling room abandoned."
                    };
                    DE.BeginAt(mount);

                    Lost1RandomRapidFiringBatteryBox(ctx);
                }
            }},

            // DE 516, One primary battery turret or gunmount abandoned.
            {"516", ctx=>{
                AddDescription(ctx, "DE 516, One primary battery turret or gunmount abandoned.");

                if(ctx.source is SubState subState)
                {
                    if(TryToSampleAPrimaryBatteryMount(ctx, out var mount))
                    {
                        var DE = new BatteryMountStatusModifier()
                        {
                            lifeCycle = StateLifeCycle.Dependent,
                            dependentObjectId = subState.objectId,
                            cause="DE 516, One primary battery turret or gunmount abandoned"
                        };
                        DE.BeginAt(mount);
                    }

                    ctx.subject.damageControlRatingHits += 1;
                }
            }},

            // DE 517, One secondary battery turret or gunmount adandoned
            {"517", ctx=>{
                AddDescription(ctx, "DE 517, One secondary battery turret or gunmount adandoned");

                if(TryToSampleASecondaryBatteryMount(ctx, out var mount))
                {
                    SetOOA(mount);
                }
                Lost1RandomRapidFiringBatteryBox(ctx);
            }},

            // DE *601, Primary battery guns and FCS out of alignment and damage to torpedo tube mounts.
            {"*601", ctx=>{
                AddDescription(ctx, "DE *601, Primary battery guns and FCS out of alignment and damage to torpedo tube mounts.");

                if(CheckAndEnsureOneShotHappendState(ctx, "*601"))
                {
                    var damageTier = ctx.subject.GetDamageTier();
                    var table = new float[,]{
                        {4, -1},
                        {7, -2},
                        {10, -3}
                    };
                    var row = Enumerable.Range(0, table.GetLength(0)).FirstOrDefault(r => damageTier <= table[r, 0]);
                    var fireControlValueOffset = table[row, 1];
                    if(TryGetPrimaryBattery(ctx, out var bty))
                    {
                        var DE = new FireControlValueModifier()
                        {
                            fireControlValueOffset=fireControlValueOffset,
                            cause="DE *601, Primary battery guns and FCS out of alignment"
                        };
                        DE.BeginAt(bty);
                    }

                    var DE2 = new TorpedoMountDamaged()
                    {
                        operationalPercentange = 20,
                        cause="DE *601, damage to torpedo tube mounts"
                    };
                    DE2.BeginAt(ctx.subject);

                    Lost1RandomRapidFiringBatteryBox(ctx);

                    if(RandomUtils.D100F() <= 70) // steam leaks due to structural damage
                    {
                        var table2 = new float[,,]
                        {
                            {
                                {50, -1},
                                {80, -2},
                                {100, -3}
                            },
                            {
                                {10, -2},
                                {60, -3},
                                {100, -4}
                            }
                        };
                        var i = damageTier <= 4 ? 0 : 1;
                        var d100 = RandomUtils.D100F();
                        var j = Enumerable.Range(0, table2.GetLength(1)).FirstOrDefault(j => d100 <= table2[i, j, 0]);
                        var maxSpeedKnotsOffset = table2[i, j, 1];

                        ctx.subject.dynamicStatus.maxSpeedKnotsOffset += maxSpeedKnotsOffset;

                        ctx.subject.damageControlRatingHits += 1;

                        // Mast Collapses
                        // TODO: Command
                    }
                }
            }},

            // DE *602, Loss of communication to engine rooms and control of helm.
            {"*602", ctx=>{
                AddDescription(ctx, "DE *602, Loss of communication to engine rooms and control of helm.");

                if(CheckAndEnsureOneShotHappendState(ctx, "*602"))
                {
                    var DE = new DE602DyanmicModifier()
                    {
                        cause="DE *602, Loss of communication to engine rooms and control of helm."
                    };
                    DE.BeginAt(ctx.subject);

                    if(RandomUtils.D100F() <= 75) // damage control system OOA due to casualties and structural damage.
                    {
                        var dts = new float[]{3,6,8,10};
                        var damageTier = ctx.subject.GetDamageTier();
                        var idx = Enumerable.Range(0, dts.Length).FirstOrDefault(idx => damageTier <= dts[idx]);
                        ctx.subject.damageControlRatingHits += (-idx-1);
                    }

                    if(RandomUtils.D100F() <= 60)
                    {
                        // All radar system (fire control and search) OOA. Permanent damage
                        foreach(var bty in ctx.subject.batteryStatus)
                        {
                            bty.fireControlRadarDisabled = true;
                        }
                    }
                }
            }},

            // DE 603, Shipboard fire
            {"603", ctx=>{
                AddDescription(ctx, "DE 603, Shipboard fire");

                var severity = (float)(Math.Round(RandomUtils.NextFloat() * 10) * 10);
                var damageTier = ctx.subject.GetDamageTier();
                if(damageTier >= 7)
                    severity += 20;

                var DE = new RiskingInMagazineExplosion()
                {
                    lifeCycle=StateLifeCycle.ShipboardFire,
                    severity=severity,
                    explosionProbPercent = 4,
                    cause="DE 603, Shipboard fire"
                };
                DE.BeginAt(ctx.subject);

                if(RandomUtils.D100F() <= 65)
                {
                    HashSet<MountLocation> affectedLocations = RandomUtils.D100F() <= 50 ?
                        new(){MountLocation.Forward, MountLocation.PortForward, MountLocation.StarboardForward} :
                        new(){MountLocation.After, MountLocation.PortAfter, MountLocation.StarboardAfter};

                    foreach(var mount in ctx.subject.batteryStatus.SelectMany(bty => bty.mountStatus)
                        .Where(mnt => affectedLocations.Contains(mnt.GetMountLocationRecordInfo().record.mountLocation)))
                    {
                        var DE2 = new BatteryMountStatusModifier()
                        {
                            lifeCycle=StateLifeCycle.GivenTime,
                            cause = "DE 603: Loss of power to all weapons"
                        };
                        DE2.BeginAt(mount);
                    }

                    if(damageTier <= 3)
                    {
                        // ctx.subject.damagePoint += RandomUtils.NextFloat() * 25;
                        ctx.subject.AddDamagePoint(RandomUtils.NextFloat() * 25);
                    }
                    else if(damageTier <= 6)
                    {
                        // ctx.subject.damagePoint += RandomUtils.NextFloat() * 45;
                        ctx.subject.AddDamagePoint(RandomUtils.NextFloat() * 45);
                    }
                    else
                    {
                        // ctx.subject.damagePoint += RandomUtils.NextFloat() * 80;
                        ctx.subject.AddDamagePoint(RandomUtils.NextFloat() * 80);
                    }
                }
            }},

            // DE *604, Compartment flooding causes list to [PORT/STARBOARD].
            {"*604", ctx=>{
                AddDescription(ctx, "DE *604, Compartment flooding causes list to [PORT/STARBOARD].");

                if(CheckAndEnsureOneShotHappendState(ctx, "*604"))
                {
                    // Secondary battery guns on low side are unable to fire.
                    if (TryGetSecondaryBattery(ctx, out var battey))
                    {
                        HashSet<MountLocation> affectedLocations = RandomUtils.D100F() <= 50 ?
                            new(){MountLocation.PortMidship, MountLocation.PortForward, MountLocation.PortAfter} :
                            new(){MountLocation.StarboardMidship, MountLocation.StarboardForward, MountLocation.StarboardAfter};

                        foreach(var mount in battey.mountStatus)
                        {
                            var mntLoc = mount.GetMountLocationRecordInfo().record.mountLocation;
                            if(affectedLocations.Contains(mntLoc))
                            {
                                SetOOA(mount);
                            }
                        }
                    }

                    ctx.subject.damageControlRatingHits += 1;
                    ctx.subject.dynamicStatus.maxSpeedKnotsOffset += -2;
                    if(TryToSampleAPrimaryOrSecondaryFireControlSystem(ctx, out var fcsRec))
                    {
                        fcsRec.trackingState = TrackingSystemState.Destroyed;
                    }
                    Lost1RandomSearchlight(ctx);
                    Lost1RandomRapidFiringBatteryBox(ctx);

                    if(ctx.subject.rapidFiringStatus.Count > 0)
                    {
                        var rfRec = RandomUtils.Sample(ctx.subject.rapidFiringStatus);
                        var rfRecClass = rfRec.GetRapidFireBatteryRecord();
                        rfRec.fireControlHits += rfRecClass.fireControlRecords.Count();
                    }

                    if(ctx.subject.GetDamageTier() >= 6 && RandomUtils.D100F() <= 65)
                    {
                        AddNewDamageEffect(ctx, "610");
                    }
                }
            }},

            // DE 605, Structural and power distribution damage.
            {"605", ctx=>{
                AddDescription(ctx, "DE 605, Structural and power distribution damage.");

                foreach(var bty in ctx.subject.batteryStatus)
                {
                    bty.fireControlRadarDisabled = true;
                }

                var DE = new DamageControlModifier()
                {
                    cause = "DE 605, Structural and power distribution damage",
                    severityDieRollOffset = 20,
                    fightingFireDieRollOffset = 20
                };
                DE.BeginAt(ctx.subject);

                Lost1RandomSearchlight(ctx);

                var p = 60;
                var damageTier = ctx.subject.GetDamageTier();
                if(damageTier >= 7)
                    p = 95;
                else if(damageTier >= 4)
                    p = 80;
                if(RandomUtils.D100F() <= p)
                {
                    if(TryToSampleAPrimaryBatteryMount(ctx, out var mount))
                    {
                        SetOOA(mount);
                    }
                }
            }},

            // DE *606, Flooding andn structural damage.
            {"*606", ctx=>{
                AddDescription(ctx, "DE *606, Flooding andn structural damage.");

                if(CheckAndEnsureOneShotHappendState(ctx, "*606"))
                {
                    var DE = new DynamicModifier()
                    {
                        standardTurnCoef = 0.5f,
                        emergencyTurnCoef = 0.5f,
                        isEvasiveManeuverBlocked = true
                    };
                    DE.BeginAt(ctx.subject);

                    ctx.subject.damageControlRatingHits += 1;
                    // TODO: Command
                    ctx.subject.dynamicStatus.maxSpeedKnotsOffset += -2;
                    if(TryToSampleAPrimaryOrSecondaryFireControlSystem(ctx, out var fcsRec))
                    {
                        SetOOA(fcsRec);
                    }

                    if(TryToSampleAPrimaryBatteryMount(ctx, out var mount))
                    {
                        SetOOA(mount);
                    }
                    var damageTier = ctx.subject.GetDamageTier();
                    if(damageTier <= 4 && RandomUtils.D100F() > 60)
                    {
                        for(int i=0; i<2; i++)
                        {
                            if(TryToSampleASecondaryBatteryMount(ctx, out var mount2))
                            {
                                SetOOA(mount2);
                            }
                        }
                    }
                    if(damageTier > 4 && RandomUtils.D100F() > 60)
                    {
                        ctx.subject.dynamicStatus.engineRoomHits += 1;
                    }
                }
            }},

            // DE 607, All primary, secondary AND tertiary battery mounts in one or more sections OOA due to structural damage.
            {"607", ctx=>{
                AddDescription(ctx, "DE 607, All primary, secondary AND tertiary battery mounts in one or more sections OOA due to structural damage.");

                var damageTier = ctx.subject.GetDamageTier();

                var locations = ctx.subject.batteryStatus.SelectMany(bs => bs.mountStatus).Select(mnt => mnt.GetMountLocationRecordInfo().record.mountLocation).ToList();

                if(locations.Count > 0)
                {
                    int affectedSections = 0;
                    if(damageTier <= 3)
                        if(RandomUtils.D100F() <= 75)
                            affectedSections = 1;
                    else if(damageTier <= 6)
                        if(RandomUtils.D100F() <= 90)
                            if(RandomUtils.D100F() <= 30)
                                affectedSections = 2;
                            else
                                affectedSections = 1;
                    else if(damageTier <= 8)
                        if(RandomUtils.D100F() <= 60)
                            affectedSections = 2;
                        else
                            affectedSections = 1;
                    else
                        if(RandomUtils.D100F() <= 90)
                            affectedSections = 2;
                        else
                            affectedSections = 1;

                    for(var i=0; i<affectedSections; i++)
                    {
                        var location = RandomUtils.Sample(locations);
                        foreach(var mount in ctx.subject.batteryStatus.SelectMany(bs => bs.mountStatus))
                        {
                            if(mount.GetMountLocationRecordInfo().record.mountLocation == location)
                            {
                                SetOOA(mount);
                            }
                        }
                    }
                }

                if(RandomUtils.D100F() <= 40)
                {
                    // Visual communications with other ships in company limited to semaphore only
                    // TODO: Command
                    var DE = new DE607DyanmicModifier()
                    {
                        cause="DE 607, Visual communications with other ships in company limited to semaphore only"
                    };
                    DE.BeginAt(ctx.subject);
                }
            }},

            // DE 608, Compartment flooding due to splinter and structural damage.
            {"608", ctx=>{
                AddDescription(ctx, "DE 608, Compartment flooding due to splinter and structural damage.");

                var DE = new DamageControlModifier()
                {
                    lifeCycle=StateLifeCycle.GivenTime,
                    cause="DE 608, Compartment flooding due to splinter and structural damage.",
                    isDamageControlBlocked = true
                };
                DE.BeginAt(ctx.subject);

                // TODO: Command
                
                var damageTier = ctx.subject.GetDamageTier();
                if(damageTier <= 3)
                {
                    ctx.subject.dynamicStatus.maxSpeedKnotsOffset += -2;
                }
                else if(damageTier <= 7)
                {
                    ctx.subject.dynamicStatus.maxSpeedKnotsOffset += -2;
                    var DE2 = new DynamicModifier()
                    {
                        cause="DE 608, Loss of stability and maneuver due to flooding",
                        isEvasiveManeuverBlocked=true,
                        standardTurnCoef=0.5f,
                        emergencyTurnCoef=0.5f,
                    };
                    DE2.BeginAt(ctx.subject);
                }
                else
                {
                    ctx.subject.dynamicStatus.maxSpeedKnotsOffset += -3;

                    var DE2 = new ArmorModifier()
                    {
                        cause="DE 608: Main waterline belt is submerged due to flooding",
                        mainBeltArmorCoef=0.5f,
                    };
                    DE2.BeginAt(ctx.subject);

                    // var DE3 = new DE608DynamicModifier()
                    // {
                    //     cause="DE 608, Loss of stability and maneuver due to flooding."
                    // };
                    // DE3.BeginAt(ctx.subject);

                    var DE3 = new DynamicModifier()
                    {
                        cause="DE 608, Loss of stability and maneuver due to flooding.",
                        isEvasiveManeuverBlocked = true,
                        standardTurnCoef=0.5f,
                        emergencyTurnCoef=0.5f,
                    };
                    DE3.BeginAt(ctx.subject);

                    var DE4 = new ShipSettleState()
                    {
                        cause="DE 608, Loss of stability and maneuver due to flooding.",
                        maxSpeedUpperLimit=6,
                        maxSpeedUpperLimitAppliedThreshold=30,
                        sinkingThreshold=10
                    };
                    DE4.BeginAt(ctx.subject);
                }
            }},

            // DE *609, Flooding due to splinter and shell damage near waterline.
            {"*609", ctx=>{
                AddDescription(ctx, "DE *609, Flooding due to splinter and shell damage near waterline.");

                if(CheckAndEnsureOneShotHappendState(ctx, "*609"))
                {
                    var DE = new DE609Effect()
                    {
                        lifeCycle=StateLifeCycle.GivenTime,
                        givenTimeSeconds=480,
                        cause="DE *609, Flooding due to splinter and shell damage near waterline."
                    };
                    DE.BeginAt(ctx.subject);
                }
            }},

            // DE 610, Collapse of watertight bulkheads causes flooding for ships.
            {"610", ctx=>{
                AddDescription(ctx, "DE 610, Collapse of watertight bulkheads causes flooding for ships.");

                var damageTier = ctx.subject.GetDamageTier();
                if(damageTier < 4)
                {
                    ctx.subject.damageControlRatingHits += 1;
                    ctx.subject.dynamicStatus.maxSpeedKnotsOffset += -2;
                }
                else
                {
                    var d100 = RandomUtils.D100F();
                    if(d100 <= 45)
                    {
                        ctx.subject.damageControlRatingHits += 2;
                        ctx.subject.dynamicStatus.maxSpeedKnotsOffset += -2;
                        var DE = new DynamicModifier()
                        {
                            isEvasiveManeuverBlocked = true,
                            standardTurnCoef = 0.5f,
                            emergencyTurnCoef = 0.5f
                        };
                        DE.BeginAt(ctx.subject);
                    }
                    else if(d100 <= 70)
                    {
                        ctx.subject.damageControlRatingHits += 1;

                        // TODO: Command
                        
                        for (int i=0; i<2; i++)
                        {
                            if(TryToSampleASecondaryBatteryMount(ctx, out var mount))
                            {
                                var location = mount.GetMountLocationRecordInfo().record.mountLocation;
                                foreach(var mount2 in ctx.subject.batteryStatus[1].mountStatus)
                                {
                                    if(mount2.GetMountLocationRecordInfo().record.mountLocation == location)
                                    {
                                        SetOOA(mount2);
                                    }
                                }
                            }
                        }
                    }
                    else if(d100 <= 85)
                    {
                        var boilerFloodingHit = RandomUtils.D100F() <= 35 ? 2 : 1;
                        ctx.subject.dynamicStatus.boilerRoomFloodingHits += boilerFloodingHit;
                    }
                    else
                    {
                        ctx.subject.dynamicStatus.engineRoomFloodingHits += 1;
                        ctx.subject.damageControlRatingHits += 1;
                    }
                }
            }},

            // DE 611, Steam leaks due to structural damage and flooding.
            { "611", ctx=>{
                AddDescription(ctx, "DE 611, Steam leaks due to structural damage and flooding.");

                ctx.subject.dynamicStatus.maxSpeedKnotsOffset += -2;
                ctx.subject.damageControlRatingHits += 1;
                Lost1RandomSearchlight(ctx);
                if(RandomUtils.D100F() <= 50)
                {
                    foreach(var bty in ctx.subject.batteryStatus)
                        bty.fireControlRadarDisabled = true;
                }
                var damageTier = ctx.subject.GetDamageTier();
                if(damageTier <= 5 && RandomUtils.D100F() >= 7)
                {
                    ctx.subject.dynamicStatus.maxSpeedKnotsOffset += -1;
                }
                if(damageTier > 5 && RandomUtils.D100F() >= 30)
                {
                    AddNewDamageEffect(ctx, "608");
                }
            }},

            // DE 612, Uncontrolled flooding
            {"612", ctx=>{
                AddDescription(ctx, "DE 612, Uncontrolled flooding");

                var damageTier = ctx.subject.GetDamageTier();
                if(damageTier < 4)
                {
                    ctx.subject.dynamicStatus.maxSpeedKnotsOffset += -1;
                    ctx.subject.damageControlRatingHits += 1;
                }
                else if(damageTier < 6)
                {
                    if(RandomUtils.D100F() <= 50)
                    {
                        ctx.subject.dynamicStatus.boilerRoomFloodingHits += 1;
                    }
                    else
                    {
                        ctx.subject.dynamicStatus.engineRoomFloodingHits += 1;
                    }
                }
                else if(damageTier < 8)
                {
                    var hit = RandomUtils.D100F() <= 55 ? 2 : 1;
                    if(RandomUtils.D100F() <= 50)
                    {
                        ctx.subject.dynamicStatus.boilerRoomFloodingHits += hit;
                    }
                    else
                    {
                        ctx.subject.dynamicStatus.engineRoomFloodingHits += hit;
                    }
                }
                else if(damageTier < 10)
                {
                    var DE = new ShipSettleState()
                    {
                        cause="DE 612, Uncontrolled flooding",
                        maxSpeedUpperLimit=6,
                        maxSpeedUpperLimitAppliedThreshold=35,
                        sinkingThreshold=10
                    };
                    DE.BeginAt(ctx.subject);
                }
                else
                {
                    var DE = new ShipSettleState()
                    {
                        cause="DE 612, Uncontrolled flooding",
                        maxSpeedUpperLimit=2,
                        maxSpeedUpperLimitAppliedThreshold=65,
                        sinkingThreshold=25
                    };
                    DE.BeginAt(ctx.subject);
                }
            }},

            // DE *613, Flooding in shaft tunnel.
            {"*613", ctx=>{
                AddDescription(ctx, "DE *613, Flooding in shaft tunnel.");

                if(CheckAndEnsureOneShotHappendState(ctx, "*613"))
                {
                    var damageTier = ctx.subject.GetDamageTier();
                    var propHit = 1;
                    if(damageTier >= 9 && RandomUtils.D100F() <= 75)
                    {
                        propHit += 1;
                    }
                    if(damageTier >= 6 && damageTier < 9 && RandomUtils.D100F() <= 40)
                    {
                        propHit += 1;
                    }
                    ctx.subject.dynamicStatus.propulsionShaftHits += propHit;
                }
            }},

            // DE 614, Damage to machinery spaces.
            {"614", ctx=>{
                AddDescription(ctx, "DE 614, Damage to machinery spaces.");

                var DE = new DynamicModifier()
                {
                    cause="DE 614, Damage to machinery spaces.",
                    accelerationUpperLimit = 1,
                };
                DE.BeginAt(ctx.subject);

                var DE2 = new DamageControlModifier()
                {
                    lifeCycle=StateLifeCycle.GivenTime,
                    cause = "DE 614, Temporary interruption to power distribution",
                    isDamageControlBlocked = true,
                };
                DE2.BeginAt(ctx.subject);

                Lost1RandomRapidFiringBatteryBoxAnd1FCSBox(ctx);

                ctx.subject.dynamicStatus.maxSpeedKnotsOffset += -1;
                var damageTier = ctx.subject.GetDamageTier();
                if(damageTier < 4)
                {
                    if(RandomUtils.D100F() <= 30)
                    {
                        ctx.subject.dynamicStatus.engineRoomHits += 1;
                    }
                }
                else if(damageTier < 8)
                {
                    if(RandomUtils.D100F() <= 60)
                    {
                        ctx.subject.dynamicStatus.engineRoomHits += 1;
                    }
                }
                else
                {
                    ctx.subject.dynamicStatus.maxSpeedKnotsOffset += -1;
                    if(RandomUtils.D100F() <= 85)
                    {
                        ctx.subject.dynamicStatus.engineRoomHits += 1;
                    }
                }
            }},

            // DE *615, Damage to firing circuits of primary battery in one section.
            {"*615", ctx=>{
                AddDescription(ctx, "DE *615, Damage to firing circuits of primary battery in one section.");

                if(CheckAndEnsureOneShotHappendState(ctx, "*615"))
                {
                    if(TryToSampleAPrimaryBatteryMount(ctx, out var mount))
                    {
                        var location = mount.GetMountLocationRecordInfo().record.mountLocation;

                        var DE = new FiringCircuitDamagedMaster()
                        {
                            cause=$"DE *615, Damage to firing circuits of primary battery in {location}"
                        };
                        DE.BeginAt(ctx.subject);

                        foreach(var mount2 in ctx.subject.batteryStatus[0].mountStatus)
                        {
                            if(mount2.GetMountLocationRecordInfo().record.mountLocation == location)
                            {
                                var DE2 = new FiringCircuitDamagedWorker()
                                {
                                    lifeCycle=StateLifeCycle.Dependent,
                                    dependentObjectId=DE.objectId,
                                    cause=$"DE *615, Damage to firing circuits of primary battery in {location}"
                                };
                                DE2.BeginAt(mount2);
                            }
                        }
                    }

                    Lost1RandomRapidFiringBatteryBox(ctx);
                    if(RandomUtils.D100F() <= 70)
                    {
                        foreach(var bty in ctx.subject.batteryStatus)
                        {
                            bty.fireControlRadarDisabled = true;
                        }
                    }
                }
            }},

            // DE 616, Severe structural damage for ships at damage Tier 8 or above.
            {"616", ctx=>{
                AddDescription(ctx, "DE 616, Severe structural damage for ships at damage Tier 8 or above.");

                var damageTier = ctx.subject.GetDamageTier();
                if(damageTier < 7)
                {
                    if(RandomUtils.D100F() < 50)
                    {
                        ctx.subject.dynamicStatus.boilerRoomFloodingHits += 1;
                    }
                    else
                    {
                        ctx.subject.dynamicStatus.engineRoomFloodingHits += 1;
                    }
                }
                else
                {
                    ctx.subject.operationalState = ShipOperationalState.FloodingObstruction;

                    var DE = new SinkingState()
                    {
                        cause="DE 616: Severe structural damage for ships"
                    };
                    DE.BeginAt(ctx.subject);
                }
            }},

            // DE 617, Battery in a section is OOA
            { "617", ctx=>{
                AddDescription(ctx, "DE 617, Battery in a section is OOA");

                var damageTier = ctx.subject.GetDamageTier();

                float threshold;
                if(damageTier < 3)
                    threshold = 65;
                else if(damageTier < 5)
                    threshold = 75;
                else if(damageTier < 7)
                    threshold = 85;
                else if(damageTier < 9)
                    threshold = 95;
                else
                    threshold = 99;

                if(RandomUtils.D100F() <= threshold)
                {
                    var mounts = ctx.subject.batteryStatus.SelectMany(bty => bty.mountStatus).ToList();
                    if(mounts.Count > 0)
                    {
                        var mount = RandomUtils.Sample(mounts);
                        var location = mount.GetMountLocationRecordInfo().record.mountLocation;
                        foreach(var mnt in ctx.subject.batteryStatus.SelectMany(bty => bty.mountStatus)
                            .Where(mnt => mnt.GetMountLocationRecordInfo().record.mountLocation == location))
                        {
                            SetOOA(mnt);
                        }
                    }
                }

                ctx.subject.dynamicStatus.maxSpeedKnotsOffset += -2;
                ctx.subject.damageControlRatingHits += 1;
                Lost1RandomSearchlight(ctx);
                Lost1RandomRapidFiringBatteryBoxAnd1FCSBox(ctx);
                if(RandomUtils.D100F() <= 65)
                {
                    foreach(var bty in ctx.subject.batteryStatus)
                    {
                        bty.fireControlRadarDisabled = true;
                    }
                }

                if(damageTier >= 9)
                {
                    var DE = new ShipSettleState()
                    {
                        cause=" DE 617 (Tier 9-10): Ship Begin to settle",
                        sinkingThreshold=25,
                        maxSpeedUpperLimitAppliedThreshold=65,
                        maxSpeedUpperLimit=2
                    };
                    DE.BeginAt(ctx.subject);
                }
                else if(damageTier >= 7)
                {
                    if(RandomUtils.D100F() <= 50)
                    {
                        ctx.subject.dynamicStatus.engineRoomHits += 1;
                    }
                    else
                    {
                        ctx.subject.dynamicStatus.boilerRoomHits += 1;
                    }

                    if(RandomUtils.D100F() >= 55)
                    {
                        if(RandomUtils.D100F() <= 50)
                        {
                            ctx.subject.dynamicStatus.engineRoomFloodingHits += 1;
                        }
                        else
                        {
                            ctx.subject.dynamicStatus.boilerRoomFloodingHits += 1;
                        }
                    }
                }
            }},

            // DE 700-710, for submarine, skip

            // MTB / Small Craft
            
            // DE 800, ship destroyed
            { "800", ctx=>{
                AddDescription(ctx, "DE 800, ship destroyed");

                ctx.subject.mapState = MapState.Destroyed;
            }},

            // DE 801, Damage to rudder
            { "801", ctx=>{
                AddDescription(ctx, "DE 801, Damage to rudder");

                var DE = new DynamicModifier()
                {
                    lifeCycle=StateLifeCycle.GivenTime,
                    cause="DE 801, Damage to rudder"
                };
                DE.BeginAt(ctx.subject);
            }},

            // DE 802, Fuel fire.
            {"802", ctx=>{
                AddDescription(ctx, "DE 802, Fuel fire.");

                AddShipboardFire(ctx, "DE 802: Shipboard fire severity 30", 30);
                // TODO: Handle Gasoline-powered ships and diesel-powered ship destroyed roll
            }},

            // DE 803, Damage to torpedo tubes
            {"803", ctx=>{
                AddDescription(ctx, "DE 803, Damage to torpedo tubes");

                if(TryToSampleATorpedoMount(ctx, out var mount))
                {
                    var DE = new TorpedoMountDamaged()
                    {
                        cause="DE 803, Damage to torpedo tubes",
                        operationalPercentange=40
                    };
                    DE.BeginAt(mount);
                }
            }},

            // DE 804, Damage to engine
            {"804", ctx=>{
                AddDescription(ctx, "DE 804, Damage to engine");

                var DE = new DynamicModifier()
                {
                    cause="DE 804: Damage to engine",
                    maxSpeedUpperLimit=4,
                };
                DE.BeginAt(ctx.subject);

                if(RandomUtils.D100F() <= 30)
                {
                    var DE2 = new DynamicModifier()
                    {
                        lifeCycle=StateLifeCycle.GivenTime,
                        cause="DE 804: Rudder is jammed"
                    };
                    DE2.BeginAt(ctx.subject);
                }
            }},

            // DE 805, Damage to engines. Maximum speed reduced by 50%
            {"805", ctx=>{
                AddDescription(ctx, "DE 805, Damage to engines. Maximum speed reduced by 50%");

                var DE = new DynamicModifier()
                {
                    cause="DE 805: Damage to engines",
                    maxSpeedKnotCoef=0.5f
                };
                DE.BeginAt(ctx.subject);
            }},

            // DE 806, Damage to engine. Ship is dead in the water (DIW).
            {"806", ctx=>{
                AddDescription(ctx, "DE 806, Damage to engine. Ship is dead in the water (DIW).");

                var DE = new DE806DynamicModifier()
                {
                    cause="DE 806: Damage to engine. Ship is dead in the water (DIW)"
                };
                DE.BeginAt(ctx.subject);
            }},

            // DE 807, Damage to hull
            {"807", ctx=>{
                AddDescription(ctx, "DE 807, Damage to hull");

                ctx.subject.dynamicStatus.maxSpeedKnotsOffset += -8;

                if(RandomUtils.D100F() <= 25)
                {
                    var DE = new DynamicModifier()
                    {
                        cause="DE 807: Engine room is flooded and ship is dead in the water (DIW)",
                        maxSpeedKnotCoef=0
                    };
                    DE.BeginAt(ctx.subject);
                }
            }},

            // DE 808: One torpedo tube damaged and OOA.
            {"808", ctx=>{
                AddDescription(ctx, "DE 808: One torpedo tube damaged and OOA.");

                if(TryToSampleATorpedoMount(ctx, out var mount))
                {
                    SetOOA(mount);
                }
            }},

            // DE 809: Damage to one deck gun.
            {"809", ctx=>{
                AddDescription(ctx, "DE 809: Damage to one deck gun.");

                // TODO: Here original text said "one deck gun" so is it proper to use a "mount", though it doesn't seem to small craft will have a mount with multiple barrels.
                if (RandomUtils.D100F() < 25)
                {
                    var mounts = ctx.subject.batteryStatus.SelectMany(bty => bty.mountStatus).ToList();
                    if(mounts.Count > 0)
                    {
                        var mount = RandomUtils.Sample(mounts);
                        SetOOA(mount);
                    }
                    else
                    {
                        var DE = new BatteryDamaged(){operationalPercentage=50};
                        DE.BeginAt(ctx.subject);
                    }
                }
            }},

            // DE 810, lost some rapid firing batteries
            {"810", ctx=>{
                AddDescription(ctx, "DE 810, damage to rapid firing batteries");

                Lost1RandomRapidFiringBatteryBox(ctx);
            }},

            // DE 811, damage to torpedo mount
            {"811", ctx=>{
                AddDescription(ctx, "DE 811, damage to torpedo mount");

                if(TryToSampleATorpedoMount(ctx, out var mount))
                {
                    SetOOA(mount);
                }
            }},

            // DE 812: One Deck Gun OOA
            {"812", ctx=>{
                AddDescription(ctx, "DE 812: One Deck Gun OOA");

                var mounts = ctx.subject.batteryStatus.SelectMany(bty => bty.mountStatus).ToList();
                if(mounts.Count > 0)
                {
                    var mount = RandomUtils.Sample(mounts);
                    SetOOA(mount);
                }
            }},

            // DE 813: Crew casualties
            {"813", ctx=>{
                AddDescription(ctx, "DE 813: Crew casualties");

                var givenTimeSeconds = RandomUtils.D100F() <= 20 ? 240 : 120;
                var DE = new DynamicModifier()
                {
                    lifeCycle=StateLifeCycle.GivenTime,
                    givenTimeSeconds=givenTimeSeconds,
                    cause="DE 813: Crew casualties",
                    isCourseChangeBlocked=true,
                };
                DE.BeginAt(ctx.subject);

                var DE2 = new BatteryMountStatusModifier()
                {
                    lifeCycle=StateLifeCycle.GivenTime,
                    givenTimeSeconds=givenTimeSeconds,
                    cause="DE 813: Crew casualties",
                };
                DE2.BeginAt(ctx.subject);

                var DE3 = new TorpedoMountModifer()
                {
                    lifeCycle=StateLifeCycle.GivenTime,
                    givenTimeSeconds=givenTimeSeconds,
                    cause="DE 813: Crew casualties",
                    status=MountStatus.Disabled,
                };
                DE3.BeginAt(ctx.subject);
            }}
        };
    }
}