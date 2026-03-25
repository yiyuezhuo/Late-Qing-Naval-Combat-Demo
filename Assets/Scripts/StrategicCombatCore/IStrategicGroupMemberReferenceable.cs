using CoreUtils;

namespace StrategicCombatCore
{
    public interface IStrategicGroupMemberReferenceable : IObjectIdLabeled
    {
        StrategicGroupReference strategicGroupReference { get; set; }
        public float GetShipTons();
        // public float GetCombatShipTons();
        public int GetStrengthMen();
        public int GetSubUnitSize();
        public float GetCombinedPowerPoint(bool isTop);
        // public float GetEffectiveShipTons(); // Not Deployed & Destroyed would be cancelled. (Deploy state need to be set in the auto shiplog generator script)
        // public int GetEffectiveStrengthMen();
        // public int GetEffectiveUnitSize();

        void SetStrategicGroupReference(StrategicGroup group);

        // group == null => Unset
        static void SetStrategicGroupReference(IStrategicGroupMemberReferenceable self, StrategicGroup group)
        {
            StrategicGroup.ReassignMember(self, group);
        }

        public LandUnit GetCurrentSourceDepot()
        {
            if (this is StrategicGroup group)
                return group.GetCurrentSourceDepot();

            return strategicGroupReference.Get()?.GetCurrentSourceDepot();
        }

        public string GetParentName() => strategicGroupReference.Get()?.name?.mergedName ?? "[Undefined or Invalid]";
        public string GetCurrentSourceDepotName() => GetCurrentSourceDepot()?.name?.mergedName ?? "[Not Defined]";
    }
}

