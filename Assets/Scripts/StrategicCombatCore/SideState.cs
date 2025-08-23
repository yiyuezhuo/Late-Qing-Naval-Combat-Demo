using CoreUtils;
using System.Collections.Generic;

namespace StrategicCombatCore
{
    public class SideState : IObjectIdLabeled
    {
        public string objectId { get; set; }
        public IEnumerable<IObjectIdLabeled> GetSubObjects()
        {
            yield break;
        }
        public GlobalString name = new();
        public List<Country> countries = new();
        public float victoryPoints;
        public string remark;

        public override string ToString()
        {
            return $"SideState({name.mergedName})";
        }
    }
}