using System.Collections.Generic;
using System.Linq;
using CoreUtils;

namespace StrategicCombatCore
{


    public partial class LandUnit : IObjectIdLabeled, IStrategicGroupMemberReferenceable
    {
        public string objectId { get; set; }
        public GlobalString name = new();
        public int stregnth;
        public float supplyTons;
        public float supplyGeneratedTons; // Super Depot generate ~10,000 tons supply (Freight)
        public string remark;

        // public string strategicGroupId;
        public StrategicGroupReference strategicGroupReference { get; set; } = new();

        public string landUnitTemplateId;
        public LandUnitTemplate GetLandUnitTemplate() => EntityManager.Instance.Get<LandUnitTemplate>(landUnitTemplateId);

        public void SetStrategicGroupReference(StrategicGroup group) => IStrategicGroupMemberReferenceable.SetStrategicGroupReference(this, group);

        // public LandUnitSize size; // Move to LandUnitTemplate?
        public IEnumerable<IObjectIdLabeled> GetSubObjects()
        {
            yield break;
        }

        public float GetFirepower(IFirepowerContext ctx)
        {
            var template = GetLandUnitTemplate();
            return template.GetFirepower(ctx) * stregnth / template.strength;
        }

        public int GetStrengthMen() => stregnth;
        public float GetShipTons() => 0f;
        public int GetSubUnitSize() => 1;
        public float GetCombinedPowerPoint(bool isTop)
        {
            return stregnth / 500f; // 1 "battalion" =~= 1 pwr pt
        }

        public static float baseNormalSupplyCostTonPerMenDay = 0.001f;
        public static float baseCombatSupplyCostTonPerMenDay = 0.005f;
        public static float carryDays = 7;

        static Dictionary<LandUnitType, float> supplyCostCoefMap = new()
        {
            {LandUnitType.Cavalry, 2f},
            {LandUnitType.Artillery, 5f},
        };

        public float GetSupplyCapTons()
        {
            return GetSupplyCostTonsPerDay() * carryDays;
        }

        public float GetSupplyCostTonsPerDay() => GetSupplyCostTonsPerMenDay() * stregnth;

        public float GetSupplyCostTonsPerMenDay()
        {
            var template = GetLandUnitTemplate();
            if (template == null)
                return 0;
            if (template.unitType == LandUnitType.Supply)
                return GetSupplyCostTonsPerDayForDepot();

            var unitTypeCoef = supplyCostCoefMap.GetValueOrDefault(template.unitType, 1);
            return baseNormalSupplyCostTonPerMenDay * unitTypeCoef;
        }

        float GetSupplyCostTonsPerDayForDepot()
        {
            var parentGroup = strategicGroupReference.Get();
            if (parentGroup == null)
                return 0;
            var firstDepot = parentGroup.subordinatesCombined.FirstOrDefault(r => r.Get() is LandUnit landUnit && landUnit?.GetLandUnitTemplate()?.unitType == LandUnitType.Supply);
            if (firstDepot.Get() == this)
                return parentGroup.GetSupplyCostTonsPerDay();
            return 0;
        }

        // public LandUnit GetCurrentSourceDepot() => ((IStrategicGroupMemberReferenceable)this).GetCurrentSourceDepot();
    }
}

