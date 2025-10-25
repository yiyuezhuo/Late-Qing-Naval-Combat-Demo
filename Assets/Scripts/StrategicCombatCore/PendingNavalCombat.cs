using CoreUtils;
using System.Collections.Generic;

namespace StrategicCombatCore
{
    public partial class PendingNavalCombat : IObjectIdLabeled
    {
        public string objectId { get; set; }

        public IEnumerable<IObjectIdLabeled> GetSubObjects()
        {
            yield break;
        }

        public XY xy = new();
    }
}