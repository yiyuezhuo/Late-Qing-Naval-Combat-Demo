using System.Collections.Generic;
using System.Xml.Serialization;
using CoreUtils;
using NavalCombatCore;

namespace StrategicCombatCore
{
    public class FrontlineCellInfo
    {
        [XmlAttribute]
        public int x;

        [XmlAttribute]
        public int y;

        [XmlAttribute]
        public float weightRequested;

        [XmlIgnore]
        public XY xy => new() { x = x, y = y };
    }

    public partial class Theater : IObjectIdLabeled, INamed
    {
        public string objectId { get; set; }
        public GlobalString name = new();
        public string sideObjectId;
        public List<XY> cells = new();
        public List<FrontlineCellInfo> frontlineCellInfos = new();

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
            return $"Theater({name?.GetMergedName()}, {cells?.Count ?? 0}, frontline {frontlineCellInfos?.Count ?? 0})";
        }
    }
}
