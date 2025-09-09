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

        public float GetFirepower(IFirepowerContext ctx)
        {
            var roundPerMinute = rateOfFireRoundPerMinute.Get(ctx.rofType);
            var accCoef = Math.Max(0, 1 - ctx.distanceMeter / effectiveRangeMeter / 2);
            return roundPerMinute * shellWeightKg * accCoef;
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