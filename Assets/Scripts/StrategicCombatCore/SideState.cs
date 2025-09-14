using CoreUtils;
using System.Collections.Generic;

namespace StrategicCombatCore
{
    public enum DiplomacyState
    {
        Neutral,
        War
    }

    public partial class DiplomacyRelation
    {
        public string sideObjectId;
        public float relationValue;
        public DiplomacyState state;

        public SideState GetSideState()
        {
            return EntityManager.Instance.Get<SideState>(sideObjectId);
        }
    }

    public class SideState : IObjectIdLabeled
    {
        public string objectId { get; set; }
        public IEnumerable<IObjectIdLabeled> GetSubObjects()
        {
            yield break;
        }
        public GlobalString name = new();
        public List<Country> countries = new();
        public List<DiplomacyRelation> diplomacyRelations = new();
        public float victoryPoints;
        public string remark;

        public override string ToString()
        {
            return $"SideState({name.mergedName})";
        }
    }
}