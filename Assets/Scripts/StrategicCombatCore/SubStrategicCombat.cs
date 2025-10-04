using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using CoreUtils;
using NavalCombatCore;
using Unity.VisualScripting;


namespace StrategicCombatCore
{
    public class SubStrategicCombatItem
    {
        public StrategicGroupMemberReference groupReference = new();
        public float commitPercent = 1; // 0f ~ 1f

        public float GetFirepower(IFirepowerContext ctx)
        {
            var obj = groupReference.Get();
            if (obj is LandUnit landUnit)
            {
                return landUnit.GetFirepower(ctx) * commitPercent;
            }
            // TODO: Add ship log
            return 0;
        }

        public float GetStrength()
        {
            var obj = groupReference.Get();
            if (obj is LandUnit landUnit)
            {
                return landUnit.GetStrengthMen() * commitPercent;
            }
            // TODO: Add ship log
            return 0;
        }
    }

    public enum SubStrategicCombatType
    {
        SimpleMassFire,
        Fire,
        NapoleonicAssault,
        SkirmisherAssault
    }

    public partial class SubStrategicCombat: IFirepowerContext // A sub-combat denotes a general infantry assault, roughtly a scenario in the Squad Battle
    {
        public enum MoraleClass
        {
            F = 1, // Abysmal
            E = 2, // Inferior
            D = 3, // Below Average
            C = 4, // Average
            B = 5, // Superior
            A = 6, // Elite
        }

        public partial class CombatSideState
        {
            public SubStrategicCombat combat;
            public List<SubStrategicCombatItem> items = new();
            // public float morale;
            // public float fatigue;
            public float suppression; // 0~300% (0~100% == Disrupted, firepower reduced. 100%~200% == Pinned, Advance Speed Reduced, 200%~300% = Dismoralized, Forced Retreat)
            public float effectiveness = 1; // 0~100%, Effectiveness
            public float lossPercent; // 0~100%
                                      // public MoraleClass nominalMoraleClass = MoraleClass.C;
            public float morale = 4; // weighted average of MoraleClass's nominal value

            public float GetFirepower() // Combat Value
            {
                return items.Sum(x => x.GetFirepower(combat)) * GetCombatValueCoef();
            }

            public float GetStrength() => items.Sum(x => x.GetStrength());

            public float GetMoraleDynamic()
            {
                return Math.Max(0, morale - suppression - (1 - effectiveness) * 4);
            }

            public float GetCombatValueCoef()
            {
                var supressionCoef = 1 - Math.Min(0.5f, suppression / 2);
                return supressionCoef * effectiveness * (1 - lossPercent);
            }

            public float GetMovementSpeedCoef() => Math.Min(1, 2 - suppression); // 0~100% => 100%, 100~200% => 100%~0%, 200%~300% => 0%~-100%
        }

        public Weapon.RateOfFire.Type rofType => Weapon.RateOfFire.Type.Normal;

        public CombatSideState attacker; // = new();
        public CombatSideState defender; // = new();
        public SubStrategicCombatType type;
        public int currentTurn; // generally 1 turn = 5min, roughly a turn in the Squad Battle, 
        public int maxTurn = 10;
        public float distanceMeter { get; set; } = 300; // 40 meter = 1 Squad Battle hex
        public float widthMeter = 1000;

        public SubStrategicCombat() //
        {
            // So new SubStrategicCombat{attacker=new()} is not valid
            // attacker is set to public, for UI purpose.
            attacker = new CombatSideState()
            {
                combat=this
            };
            defender = new CombatSideState()
            {
                combat=this
            };
        }

        // public float hexEquivalent => distanceMeter / 40;

        public class CombatSideStateDynamic
        {
            public float strength;
            public float strengthPerHex;
            public float caombatValue; // firepower
            public float inflictCombatValuePerHex;
            public float inflictedCombatValuePerHex;
        }
        
        public CombatSideStateDynamic ExtractCombatSideStateDynamic(CombatSideState sideState)
        {
            var hexEquivalent = distanceMeter / 40;
            var strength = sideState.GetStrength();
            var firepower = attacker.GetFirepower();
            return new CombatSideStateDynamic
            {
                strength = strength,
                strengthPerHex = strength / hexEquivalent,
                caombatValue = firepower,
                // TODO: Handle Depth effect
                inflictCombatValuePerHex = firepower / hexEquivalent
            };
        }

        public void ConnectCombatSideStateDynamic(CombatSideStateDynamic sideState, CombatSideStateDynamic other)
        {
            sideState.inflictedCombatValuePerHex = other.inflictedCombatValuePerHex;
        }

        public void ResolveMassFire()
        {
            var atk = ExtractCombatSideStateDynamic(attacker);
            var def = ExtractCombatSideStateDynamic(defender);

            ConnectCombatSideStateDynamic(atk, def);
            ConnectCombatSideStateDynamic(def, atk);
        }

        // public float CalculateSoftAttack(LandUnitTemplate landUnitTemplate)
        // {

        // }

        // public float CalculateSoftAttack(LandUnit landUnit)
        // {
        //     // landUnit.GetLandUnitTemplate()
        // }

        // public float CalculateSoftAttack(ShipLog shipLog)
        // {
        //     return 1; // TODO: Derive value from battery situation.
        // }

        // public float CalculateSoftAttack(SubStrategicCombatItem item)
        // {

        // }
    }
}