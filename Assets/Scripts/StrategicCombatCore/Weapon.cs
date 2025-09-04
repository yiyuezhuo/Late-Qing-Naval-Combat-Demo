using System.Collections.Generic;
using System.Reflection;
using CoreUtils;


namespace StrategicCombatCore
{
    public class Weapon : IObjectIdLabeled
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

        public class RateOfFire // Round per minute
        {
            public float slow;
            public float normal;
            public float rapid;
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