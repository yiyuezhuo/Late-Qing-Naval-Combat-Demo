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

        // public LandUnit GetCurrentSourceDepot() => ((IStrategicGroupMemberReferenceable)this).GetCurrentSourceDepot();

    }
}