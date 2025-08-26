using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using CoreUtils;
using NavalCombatCore;

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
            if(obj is ShipLog shipLog && shipLog.mapState == MapState.Destroyed)
                return 0;
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
            Fleet,
            CoastArtillery
        }
        public Type type;
        public StrategicUnitSize size;
        public Country country;
        public enum DeployState
        {
            NotDeployed,
            Combined,
            Independent
        }
        public DeployState deployState; // generally, deployState should be set with SetDeployState()
        public int independentX = -1;
        public int independentY = -1;

        [XmlIgnore]
        public int x
        {
            get
            {
                if (deployState == DeployState.NotDeployed)
                {
                    return -1;
                }
                else if (deployState == DeployState.Combined)
                {
                    return strategicGroupReference.Get()?.x ?? -1;
                }
                return independentX;
            }
            set
            {
                if (deployState == DeployState.Independent)
                {
                    independentX = value;
                }
            }
        }

        [XmlIgnore]
        public int y
        {
            get
            {
                if (deployState == DeployState.NotDeployed)
                {
                    return -1;
                }
                else if (deployState == DeployState.Combined)
                {
                    return strategicGroupReference.Get()?.y ?? -1;
                }
                return independentY;
            }
            set
            {
                if (deployState == DeployState.Independent)
                {
                    independentY = value;
                }
            }
        }

        public LeaderReference leaderReference = new();

        public List<StrategicGroupMemberReference> subordinatesCombined = new();
        // public List<StrategicGroupMemberReference> subordinatesInCommandOfChain = new();

        // public string strategicGroupId;
        public StrategicGroupReference strategicGroupReference{ get; set; } = new();

        public SideState side => StrategicGameState.Instance.countryToSideStateMap.GetValueOrDefault(country);
        public HexInfo hexInfo => StrategicGameState.Instance.hexInfoMap.GetValueOrDefault((x, y));

        [XmlIgnore]
        public List<StrategicGroup> currentStack
        {
            get
            {
                var currentSide = side;
                return hexInfo.strategicGroupReferences.Select(r => r.Get()).Where(g => g.side == currentSide).ToList();
            }
        }

        [XmlIgnore]
        public Cell cell => StrategicGameState.Instance.cellMatrix[x, y];

        public void SetStrategicGroupReference(StrategicGroup group) => IStrategicGroupMemberReferenceable.SetStrategicGroupReference(this, group);

        public string remark;

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

        public override string ToString()
        {
            return $"StrategicGroup({name.GetMergedName()})";
        }

        public void DeployToXY(int toX, int toY)
        {
            var hexInfoMap = StrategicGameState.Instance.hexInfoMap;

            if (deployState == DeployState.Independent && x != -1 && y != -1 && hexInfoMap.TryGetValue((x, y), out var cellInfo))
            {
                cellInfo.strategicGroupReferences.RemoveAll(gp => gp.referenceId == objectId);
            }

            deployState = DeployState.Independent;
            x = toX;
            y = toY;

            if (!hexInfoMap.TryGetValue((x, y), out cellInfo))
            {
                cellInfo = hexInfoMap[(x, y)] = new();
                cellInfo.x = x;
                cellInfo.y = y;
            }
            cellInfo.strategicGroupReferences.Add(new() { referenceId = objectId });
        }

        public void RemoveFromMap()
        {
            var hexInfoMap = StrategicGameState.Instance.hexInfoMap;

            if (deployState == DeployState.Independent && x != -1 && y != -1 && hexInfoMap.TryGetValue((x, y), out var cellInfo))
            {
                cellInfo.strategicGroupReferences.RemoveAll(gp => gp.referenceId == objectId);
            }

            independentX = -1;
            independentY = -1;
        }

        public void SetDeployState(DeployState newState)
        {
            if (newState == DeployState.Independent)
            {
                var parentGroup = strategicGroupReference.Get();
                if (parentGroup != null)
                {
                    DeployToXY(parentGroup.x, parentGroup.y);
                }
                else
                {
                    DeployToXY(0, 0);
                }
            }
            else if (newState == DeployState.NotDeployed || newState == DeployState.Combined)
            {
                RemoveFromMap();
                deployState = newState;
            }
        }

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

