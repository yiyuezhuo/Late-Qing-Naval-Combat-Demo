using System.Collections.Generic;
using System.Xml.Serialization;
using CoreUtils;
using NavalCombatCore;

namespace StrategicCombatCore
{
    public partial class Theater : IObjectIdLabeled, INamed
    {
        public string objectId { get; set; }
        public GlobalString name = new();
        public string sideObjectId;
        public List<XY> cells = new();
        public List<XY> frontlineCells = new();

        public IEnumerable<IObjectIdLabeled> GetSubObjects()
        {
            yield break;
        }

        public SideState GetSide() => EntityManager.Instance.Get<SideState>(sideObjectId);
        public GlobalString GetName() => name;

        [XmlIgnore]
        public SideState side => GetSide();

        public override string ToString()
        {
            return $"Theater({name?.GetMergedName()}, {cells?.Count ?? 0}, frontline {frontlineCells?.Count ?? 0})";
        }
    }
}
