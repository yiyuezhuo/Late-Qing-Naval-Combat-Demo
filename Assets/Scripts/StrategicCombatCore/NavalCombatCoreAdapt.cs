using StrategicCombatCore;

namespace NavalCombatCore
{
    public partial class ShipLog : IStrategicGroupMemberReferenceable
    {
        // public string strategicGroupId;
        public StrategicGroupReference strategicGroupReference { get; set; } = new();
        public void SetStrategicGroupReference(StrategicGroup group) => IStrategicGroupMemberReferenceable.SetStrategicGroupReference(this, group);
        public float supplyTons;
        public float fixedHours; // Fixed by Tactical Combat Resolution
                                 // GetDepot

        public int GetStrengthMen() => shipClass?.complementMen ?? 0;
        public float GetShipTons() => shipClass?.displacementTons ?? 0;
        public int GetSubUnitSize() => 1;
        public float GetCombinedPowerPoint(bool isTop)
        {
            if (mapState == MapState.Deployed)
            {
                var shipTons = shipClass?.displacementTons ?? 0;
                return shipTons / 1000;
            }
            return 0;
        }
        public float GetSupplyCapTons()
        {
            var meShipClass = shipClass;
            if (meShipClass == null)
                return 0;
            if (meShipClass.type == ShipType.Transport)
                return shipClass.displacementTons / 2; // 50% of displacement for supply (coal + load) for transport ship
            return shipClass.displacementTons / 10; // 10% of displacement for supply (coal only) for combat ship
        }

        public float GetSupplyCostTonsPerDay()
        {
            return (shipClass?.displacementTons ?? 0) / 10 / 7; // ~1.5% of displacement of supply is consumed per day
        }

        // public LandUnit GetCurrentSourceDepot() => ((IStrategicGroupMemberReferenceable)this).GetCurrentSourceDepot();

    }
}