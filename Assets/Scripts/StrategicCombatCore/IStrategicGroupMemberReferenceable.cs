using System.Collections.Generic;
using CoreUtils;
using YYZ;

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
            var oldGroup = self.strategicGroupReference.Get();
            if (oldGroup != null)
            {
                oldGroup.subordinatesCombined.RemoveAll(r => r.referenceId == self.objectId);
            }

            if (group == null)
            {
                self.strategicGroupReference.referenceId = null;
            }
            else
            {
                self.strategicGroupReference.referenceId = group.objectId;
                group.subordinatesCombined.Add(new StrategicGroupMemberReference() { referenceId = group.objectId });
            }
        }

        // LandUnit GetCurrentSourceDepot();

        public LandUnit GetCurrentSourceDepot()
        {
            var pt = strategicGroupReference.Get();
            var accessed = new HashSet<StrategicGroup>() { pt };
            while (pt != null)
            {
                foreach (var subordinateRef in pt.subordinatesCombined)
                {
                    var subordinate = subordinateRef.Get();
                    if (subordinate is LandUnit landUnit && landUnit != this)
                    {
                        var landUnitTemplate = landUnit.GetLandUnitTemplate();
                        if (landUnitTemplate != null && landUnitTemplate.unitType == LandUnitType.Supply)
                        {
                            return landUnit;
                        }
                    }
                }
                pt = pt.strategicGroupReference.Get();

                if (accessed.Contains(pt))
                {
                    ServiceLocator.Get<ILoggerService>().LogError("Looping OOB Detected!");
                    return null;
                }
                accessed.Add(pt);
            }
            return null;
        }

        public string GetParentName() => strategicGroupReference.Get()?.name?.mergedName ?? "[Undefined or Invalid]";
        public string GetCurrentSourceDepotName() => GetCurrentSourceDepot()?.name?.mergedName ?? "[Not Defined]";
    }
}

