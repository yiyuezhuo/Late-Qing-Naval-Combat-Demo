using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using CoreUtils;

namespace StrategicCombatCore
{
    public partial class StrategicGroupMemberReference
    {
        public string referenceId;

        public IObjectIdLabeled Get()
        {
            return EntityManager.Instance.Get<IObjectIdLabeled>(referenceId);
        }

        public int GetCombinedSubUnitSize()
        {
            var obj = Get();
            if (obj == null)
                return 0;
            if (obj is StrategicGroup group)
                return group.GetCombinedSubUnitSize();
            return 1; // Otherwise (Subunit), translate to 1. 
        }
    }

    public partial class StrategicGroup : IObjectIdLabeled
    {
        public string objectId { get; set; }
        public GlobalString name = new();
        public enum Type
        {
            General,
            HeadQuarter,
            Infantry,
            Fleet
        }
        public Type type;
        public StrategicUnitSize size;
        public Country country;
        public List<StrategicGroupMemberReference> subordinatesCombined = new();
        public List<StrategicGroupMemberReference> subordinatesInCommandOfChain = new();

        public string strategicGroupId;

        public static Dictionary<StrategicUnitSize, string> sizeStrMap = new()
        {
            { StrategicUnitSize.Unspecified, "" },
            { StrategicUnitSize.ArmyGroup, "XXXXX" },
            { StrategicUnitSize.Army, "XXXX" },
            { StrategicUnitSize.Corp, "XXX" },
            { StrategicUnitSize.Division, "XX" },
            { StrategicUnitSize.Bridge, "X" },
            { StrategicUnitSize.Regiment, "III" },
            { StrategicUnitSize.Battalion, "II" },
            { StrategicUnitSize.Company, "I" },
            { StrategicUnitSize.Platoon, "···" },
            { StrategicUnitSize.Squad, "··" },
        };

        public int GetCombinedSubUnitSize()
        {
            return subordinatesCombined.Sum(r => r.GetCombinedSubUnitSize());
        }

        public string GetSizeStr()
        {
            return sizeStrMap.GetValueOrDefault(size, "?");
        }

        public IEnumerable<IObjectIdLabeled> GetSubObjects()
        {
            yield break;
        }
    }
}

