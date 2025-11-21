using System;
using System.Collections.Generic;
using CoreUtils;


namespace StrategicCombatCore
{
    public interface IFirepowerContext
    {
        public Weapon.RateOfFire.Type rofType{ get; }
        public float distanceMeter{ get;}
    }

    public partial class Weapon : IObjectIdLabeled
    {
        public string objectId { get; set; }
        public GlobalString name = new();
        public IEnumerable<IObjectIdLabeled> GetSubObjects()
        {
            yield break;
        }

        public PictureReference pictureReference = new();

        public string remark;
        public RateOfFire rateOfFireRoundPerMinute = new();
        public float burstRadiusMeter;
        public float shellWeightKg;
        public float weightKg;
        public int crew;
        public float reliability;
        // public float melee;
        public MeleeAbility meleeAbility;
        public float effectiveRangeMeter;
        public float maxRangeMeter;
        public int load;
        public float muzzleVelocityMeterPerSecond;
        public float calibreMM;
        public bool isGun;

        // obsolete
        public float GetFirepower(IFirepowerContext ctx)
        {
            var roundPerMinute = rateOfFireRoundPerMinute.Get(ctx.rofType);
            var accCoef = Math.Max(0, 1 - ctx.distanceMeter / effectiveRangeMeter / 2);
            return roundPerMinute * shellWeightKg * accCoef;
        }

        public float GetLethality() // SB Style Leth value
        {
            if(isGun) // Gun
            {
                // SB x CO2 EQ Mapping
                // r0 => 1
                // r1 => 1 + 0.5 + 0.5 = 2
                // r2 => 1 + 2/3 + 2/3 + 1/3 + 1/3 = 3
                var burstRadiusCoef = 1 + burstRadiusMeter / 10;
                return rateOfFireRoundPerMinute.rapid * shellWeightKg * 3.5f * reliability * burstRadiusCoef;
            }
            // Rifle
            return rateOfFireRoundPerMinute.rapid * reliability;
        }

        public class RateOfFire // Round per minute
        {
            public float slow;
            public float normal;
            public float rapid;

            public enum Type
            {
                Slow,
                Normal,
                Rapid
            }

            public float Get(Type type)
            {
                return type switch
                {
                    Type.Slow => slow,
                    Type.Normal => normal,
                    Type.Rapid => rapid,
                    _ => normal,
                };
            }
        }

        public enum MeleeAbility
        {
            None,
            Buttstroke, // Rifle without Bayonet
            Bayonet,
            Blade, // Sword,
            Spear, // Spear, Lance
        }
    }
}