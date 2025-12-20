using CoreUtils;
using System.Collections.Generic;
using System.Linq;

namespace StrategicCombatCore
{
    public partial class PendingNavalCombat : IObjectIdLabeled
    {
        public string objectId { get; set; }

        public IEnumerable<IObjectIdLabeled> GetSubObjects()
        {
            yield break;
        }

        public class PendingNavalCombatSideState
        {
            public string sideObjectId;
            public SideState side => EntityManager.Instance.Get<SideState>(sideObjectId);

            public List<string> groupObjectIds = new();
            public List<StrategicGroup> GetGroups() => groupObjectIds.Select(x => EntityManager.Instance.Get<StrategicGroup>(x)).ToList();
        }

        public XY xy = new();

        // public Cell cell => StrategicGameState.Instance.cellMatrix[xy.x, xy.y];
        public Cell cell => xy.GetCell();

        public PendingNavalCombatSideState sideState0 = new();
        public PendingNavalCombatSideState sideState1 = new();
    }
}