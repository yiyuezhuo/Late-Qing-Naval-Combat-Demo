using System;
using System.Collections.Generic;
using System.Linq;
using CoreUtils;

namespace StrategicCombatCore
{


    public partial class LandUnit : IObjectIdLabeled, IStrategicGroupMemberReferenceable, ISupplyNetworkNode, INamed
    {
        public string objectId { get; set; }
        public GlobalString name = new();
        public int strength;
        public bool strengthManualOverride;

        // Effectivenss
        public float suppression; // 0.0~1.0 (0%~100%), generally used in 1 hour combat resolution, restored after 1 hour.
        public float morale = 1; // 0.0~1.0 (0%~100%)
        public float fatigue; // 0.0~1.0 (0%~100%)
        
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

        /// <summary>
        /// Restore suppression, morale, fatigue
        /// </summary>
        public void RestoreEffectivness()
        {
            if(strength <= 0)
            {
                suppression = 1;
                morale = 0;
                fatigue = 1;
                return;
            }

            var recoveryCoef = GetRecoveryCoef();
            suppression = Math.Max(0, suppression - Math.Max(0.25f, suppression * 0.8f) * recoveryCoef); // -80% of max suppression of 25% suppression
            morale = Math.Min(1, morale + 0.1f * recoveryCoef); // +10% morale per turn
            fatigue = Math.Max(0, fatigue - 0.025f * recoveryCoef); // -2.5% fatigue per turn
        }

        public float GetFirepower(IFirepowerContext ctx)
        {
            var template = GetLandUnitTemplate();
            return template.GetFirepower(ctx) * strength / template.strength;
        }

        public int GetStrengthMen() => strength;
        public float GetShipTons() => 0f;
        // public float GetCombatShipTons() => 0f;
        public int GetSubUnitSize() => 1;
        public float GetCombinedPowerPoint(bool isTop)
        {
            return strength / 500f; // 1 "battalion" =~= 1 pwr pt
        }

        // WITP-like port & repire shipyard, only valid is unit template is a port.
        public int portLevel;
        public int repairShipyardLevel;

        // Later those value may derived from weapon and template parameter, but now it's determined simply with strength and type.

        // public static float baseNormalSupplyCostTonPerMenDay = 0.001f;
        static float baseNormalSupplyCostTonPerMenDay = 0.003f; // 3kg/Day/Man 
        // static float baseCombatSupplyCostTonPerMenDay = 0.015f;
        static float carryDays = 7;
        static float depotReserveDays = 30;

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

        public float GetSupplyCostTonsPerDay() => GetSupplyCostTonsPerMenDay() * strength;

        public bool IsOutOfSupply() => supplyTons <= 0;

        public float GetRecoveryCoef() => IsOutOfSupply() ? 0.5f : 1f;

        public float GetLandBattleFirepowerCoef(bool isGlobalAttacker)
        {
            if (!IsOutOfSupply())
                return 1f;
            return isGlobalAttacker ? 0.25f : 0.5f;
        }

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

        static double baseNormalTransferWeightTonPerMen = 0.5; // 500kg per man

        static Dictionary<LandUnitType, double> transferWeightCoefMap = new()
        {
            {LandUnitType.Cavalry, 2},
            {LandUnitType.Artillery, 3},
        };

        public double GetTransferWeightTons()
        {
            var template = GetLandUnitTemplate();
            if (template == null)
                return 0;
            var unitTypeCoef = transferWeightCoefMap.GetValueOrDefault(template.unitType, 1);
            return strength * baseNormalTransferWeightTonPerMen * unitTypeCoef + supplyTons;
        }

        public Cell cell => strategicGroupReference.GetCell();

        public SideState side => strategicGroupReference.GetSide();

        GlobalString ISupplyNetworkNode.GetName() => name;
        public double GetSupplyTons() => supplyTons;
        public void SetSupplyTons(double value) => supplyTons = value;
        public SupplyTransferState GetSupplyTransferState() => supplyTransferState;
        public bool IsDepotSameCellOnlySupply() => false;

        public override string ToString() => $"LandUnit({name.GetMergedName()})";

        // public double GetTransferWeightTons() => supplyTons + supplyGeneratedTons;
        public float GetDirectCommandUsage() => strength;

        static Dictionary<LandUnitType, float> typeChanceCoefMap = new()
        {
            {LandUnitType.Cavalry, 3},
            {LandUnitType.Engineer, 1.5f},
            {LandUnitType.Artillery, 0.5f},
            {LandUnitType.MountainArtillery, 0.75f}
        };

        public float GetChance()
        {
            var unitType = GetLandUnitTemplate()?.unitType ?? LandUnitType.Infantry;
            var typeChanceCoef = typeChanceCoefMap.GetValueOrDefault(unitType, 1);
            return strength * typeChanceCoef;
        }

        // public float GetTargetWeight()
        // {
            
        // }

        public float GetLethality()
        {
            var template = GetLandUnitTemplate();
            if(template != null && template.strength > 0)
            {
                var leth = template.GetLethality() * strength / template.strength;
                leth *= suppression / 2 + 0.5f; // 0~100% suppression (-0%~-50%) leth
                leth *= morale / 2 + 0.5f;
                leth *= fatigue / 2 + 0.5f;
                return leth;
            }
            return 0;
        }

        public float GetLethality(bool isGlobalAttacker) => GetLethality() * GetLandBattleFirepowerCoef(isGlobalAttacker);

        public GlobalString GetName() => name;
    }
}

