using System.Collections.Generic;
using System.Linq;
using CoreUtils;

namespace StrategicCombatCore
{


    public partial class LandUnit : IObjectIdLabeled, IStrategicGroupMemberReferenceable, ISupplyNetworkNode
    {
        public string objectId { get; set; }
        public GlobalString name = new();
        public int stregnth;
        public double supplyTons;
        public double supplyGeneratedTons; // Super Depot generate ~10,000 tons supply (Freight)
        public string remark;

        // public string strategicGroupId;
        public StrategicGroupReference strategicGroupReference { get; set; } = new();

        public string landUnitTemplateId;
        public LandUnitTemplate GetLandUnitTemplate() => EntityManager.Instance.Get<LandUnitTemplate>(landUnitTemplateId);

        public void SetStrategicGroupReference(StrategicGroup group) => IStrategicGroupMemberReferenceable.SetStrategicGroupReference(this, group);

        public SupplyTransferState supplyTransferState = new();

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

        // public static float baseNormalSupplyCostTonPerMenDay = 0.001f;
        public static float baseNormalSupplyCostTonPerMenDay = 0.003f; // 3kg/Day/Man 
        public static float baseCombatSupplyCostTonPerMenDay = 0.015f;
        public static float carryDays = 7;
        public static float depotReserveDays = 30;

        static Dictionary<LandUnitType, float> supplyCostCoefMap = new()
        {
            {LandUnitType.Cavalry, 3f},
            {LandUnitType.Artillery, 10f},
        };

        public float GetSupplyCapTons()
        {
            var template = GetLandUnitTemplate();
            if (template == null)
                return 0;
            if (template.unitType == LandUnitType.Supply)
                return GetSupplyCostTonsPerDayForDepot() * depotReserveDays;

            return GetSupplyCostTonsPerDay() * carryDays;
        }

        public float GetSupplyCostTonsPerDay() => GetSupplyCostTonsPerMenDay() * stregnth;

        public float GetSupplyCostTonsPerMenDay()
        {
            var template = GetLandUnitTemplate();
            if (template == null)
                return 0;
            if (template.unitType == LandUnitType.Supply)
                return 0; // request & sent is not modeled as supply cost itself (since we may introduce supply's cost due to itself later)
            // return GetSupplyCostTonsPerDayForDepot();

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

        public Cell cell => strategicGroupReference.GetCell();

        public SideState side => strategicGroupReference.GetSide();

        GlobalString ISupplyNetworkNode.GetName() => name;
        public double GetSupplyTons() => supplyTons;
        public void SetSupplyTons(double value) => supplyTons = value;
        public SupplyTransferState GetSupplyTransferState() => supplyTransferState;
        public bool IsDepotSameCellOnlySupply() => false;
    }
}

