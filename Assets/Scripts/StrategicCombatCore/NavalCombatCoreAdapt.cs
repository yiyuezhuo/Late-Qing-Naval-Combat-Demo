using StrategicCombatCore;

namespace NavalCombatCore
{
    public partial class ShipLog : IStrategicGroupMemberReferenceable
    {
        // public string strategicGroupId;
        public StrategicGroupReference strategicGroupReference { get; set; } = new();
        public void SetStrategicGroupReference(StrategicGroup group) => IStrategicGroupMemberReferenceable.SetStrategicGroupReference(this, group);
    }
}