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

        public float GetStrength() => stregnth;

        // public LandUnit GetCurrentSourceDepot() => ((IStrategicGroupMemberReferenceable)this).GetCurrentSourceDepot();
    }
}

