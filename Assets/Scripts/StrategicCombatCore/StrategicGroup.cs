using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using CoreUtils;

namespace StrategicCombatCore
{
    public partial class StrategicGroupMemberReference
    {
        public string referenceId;

        public IStrategicGroupMemberReferenceable Get()
        {
            return EntityManager.Instance.Get<IStrategicGroupMemberReferenceable>(referenceId);
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

    public partial class StrategicGroupReference
    {
        public string referenceId;

        public StrategicGroup Get()
        {
            return EntityManager.Instance.Get<StrategicGroup>(referenceId);
        }

        public bool isReferenceAny() => referenceId != null && referenceId != "";


    }

    public interface IStrategicGroupMemberReferenceable : IObjectIdLabeled
    {
        StrategicGroupReference strategicGroupReference { get; set; }

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
                group.subordinatesCombined.Add(new StrategicGroupMemberReference() { referenceId=group.objectId});
            }
        }
    }

    public partial class StrategicGroup : IObjectIdLabeled, IStrategicGroupMemberReferenceable
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
        // public List<StrategicGroupMemberReference> subordinatesInCommandOfChain = new();

        // public string strategicGroupId;
        public StrategicGroupReference strategicGroupReference{ get; set; } = new();
        public void SetStrategicGroupReference(StrategicGroup group) => IStrategicGroupMemberReferenceable.SetStrategicGroupReference(this, group);

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

