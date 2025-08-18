using System.Collections.Generic;

using CoreUtils;

namespace StrategicCombatCore
{
    public enum StrategicUnitSize
    {
        Unspecified,
        ArmyGroup,
        Army,
        Corp, // IJA: 軍
        Division, // IJA: 師団
        Bridge, // IJA: 旅団
        Regiment, // IJA: 連隊
        Battalion, // IJA: 大隊, China: 营队
        Company, // IJA: 中隊, China: 哨
        Platoon, // IJA: 小隊
        Squad // IJA: 分隊
    }

    public enum LandUnitType
    {
        Infantry,
        Cavalry,
        Artillery,
        Enginner,
        Supply,
        MilitaryPolicy,
    }

    public interface ITreeNode
    {
    }

    public class LandUnitTemplate : IObjectIdLabeled
    {
        public string objectId { get; set; }
        public GlobalString name = new();
        public StrategicUnitSize size;
        public LandUnitType unitType;
        public int strength; // max strength
        public int guns;
        public float firepowerKgPerMin; // Firepower = shell weight kg / min
        public float targettingGain; // Field Gun = 1, Rifle = 10, (Chinese's poor marksmanship will give lower value, though it will not effect suppression)
        public float rangeMeter; // 0~rangeMeter, 200%~100% firepower, rangeMeter~2*rangemeter, 100%~0% firepower
        public float moraleCoef; // Japanese Regular = 1.0, "elite" chinese (Xiang army) = 0.5, regular chinese = 0.2, recruit = 0.05
        public float densityStrengthPerSqMeter; // Chinese's old dense line tactic will cause more damage, which works like TOAW defense
        public float assault; // when attacker arrive defender's position and defender does not fallback, the firepower is replaced with assault value.
        public float ammoCoef; // Ammo consumption is determined by firepowerKgPerMin and ammoCoef
        public float rationCoef; // Ration consumption is determined by strength and rationCoef (cavalry will have a higher value)
        public float weaponWeightKgPerStrength; // weight (used in capacity consumption of transport, replacement)
        public float carryingRationKg; // Carried ration (kg)
        public float carryingAmmoKg; // Carried ammo (kg)
        public bool isSupport; // Line-Filler vs Support unit
        public float strategicSpeedKmPerDay; // Consistent speed move speed on some level of road
        public float operationalSpeedKmPerDay; // Not-consistent speed but also move on non-road field
        public float tacticalSpeedMPerMin; // Base advance speed in assault. 

        public IEnumerable<IObjectIdLabeled> GetSubObjects()
        {
            yield break;
        }
    }

    public class LandUnit : IObjectIdLabeled
    {
        public string objectId { get; set; }
        public GlobalString name = new();
        public int stregnth;
        public string remark;

        public string strategicGroupId;

        // public LandUnitSize size; // Move to LandUnitTemplate?
        public IEnumerable<IObjectIdLabeled> GetSubObjects()
        {
            yield break;
        }
    }
}

