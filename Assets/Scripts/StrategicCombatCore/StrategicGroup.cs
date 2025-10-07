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
            if (obj is ShipLog shipLog && shipLog.mapState == MapState.Destroyed)
                return 0;
            return 1; // Otherwise (Subunit), translate to 1. 
        }

        public int GetSubUnitSize() => Get()?.GetSubUnitSize() ?? 0;
        public int GetStrengthMen() => Get()?.GetStrengthMen() ?? 0;
        public float GetShipTons() => Get()?.GetShipTons() ?? 0f;
        public float GetCombinedPowerPoint(bool isTop) => Get()?.GetCombinedPowerPoint(isTop) ?? 0f;
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
    
    public class XY
    {
        [XmlAttribute]
        public int x;

        [XmlAttribute]
        public int y;
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
            Cavalry, // Cavalry Regiment
            Artillery,
            Engineer,
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
        public StrategicGroupReference strategicGroupReference { get; set; } = new();
        public string assignedMissionObjectId;

        public void SetAssignedMission(StrategicMission mission)
        {
            var oldMission = EntityManager.Instance.Get<StrategicMission>(assignedMissionObjectId);
            if (oldMission != null)
            {
                oldMission.groups.RemoveAll(r => r.referenceId == objectId);
            }

            if (mission == null)
            {
                assignedMissionObjectId = null;
            }
            else
            {
                assignedMissionObjectId = mission.objectId;
                mission.groups.Add(new() { referenceId = objectId });
            }
        }

        public SideState side => StrategicGameState.Instance.countryToSideStateMap.GetValueOrDefault(country);
        // public HexInfo hexInfo => StrategicGameState.Instance.hexInfoMap.GetValueOrDefault((x, y));

        [XmlIgnore]
        public List<StrategicGroup> currentStack
        {
            get
            {
                var currentSide = side;
                // return hexInfo.strategicGroupReferences.Select(r => r.Get()).Where(g => g.side == currentSide).ToList();
                return cell.StrategicGroupReferences.Select(r => r.Get()).Where(g => g.side == currentSide).ToList();
            }
        }

        [XmlIgnore]
        public Cell cell => StrategicGameState.Instance.cellMatrix[x, y];

        public void SetStrategicGroupReference(StrategicGroup group) => IStrategicGroupMemberReferenceable.SetStrategicGroupReference(this, group);

        public string remark; // Referenced by UITK

        public float moveProgressionKm = 0;

        public List<XY> plannedPath = new();

        public static Dictionary<StrategicUnitSize, string> sizeStrMap = new()
        {
            { StrategicUnitSize.Unspecified, "O" },
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

        public bool IsNavy() => type == Type.Fleet;
        public bool IsArmy() => type != Type.Fleet;

        public bool IsOnMap()
        {
            var pt = this;
            while (pt !=null && pt.deployState != DeployState.NotDeployed) // Combined or Independent
            {
                if (pt.deployState == DeployState.Independent)
                    return true;
                pt = pt.strategicGroupReference.Get(); 
            }
            return false;
        }

        public int GetSubUnitSize() => subordinatesCombined.Sum(r => r.GetSubUnitSize());
        public int GetStrengthMen() => subordinatesCombined.Sum(r => r.GetStrengthMen());
        public float GetShipTons() => subordinatesCombined.Sum(r => r.GetShipTons());
        public float GetCombinedPowerPoint(bool isTop)
        {
            if (!isTop && deployState != DeployState.Combined)
                return 0;
            return subordinatesCombined.Sum(r => r.GetCombinedPowerPoint(false));
        }

        public float GetSupplyCostTonsPerDay() // Combined
        {
            var supplySum = 0f;
            foreach (var subordinateRef in subordinatesCombined)
            {
                var subordinate = subordinateRef.Get();
                if (subordinate == null)
                    continue;
                if (subordinate is LandUnit landUnit && landUnit?.GetLandUnitTemplate()?.unitType != LandUnitType.Supply)
                {
                    supplySum += landUnit.GetSupplyCostTonsPerDay();
                }
                else if (subordinate is ShipLog shipLog)
                {
                    supplySum += shipLog.GetSupplyCostTonsPerDay();
                }
                else if (subordinate is StrategicGroup group)
                {
                    supplySum += group.GetSupplyCostTonsPerDay();
                }
            }
            return supplySum;
        }

        // From Vacuum or to vacuum, or move to other cell through vacuum.
        public void MoveToXY(int toX, int toY, bool moveThroughEdge)
        {
            var toCell = StrategicGameState.Instance.cellMatrix[toX, toY];

            if (deployState == DeployState.Independent && x != -1 && y != -1)
            {
                cell.StrategicGroupReferences.RemoveAll(gp => gp.referenceId == objectId);
            }

            deployState = DeployState.Independent;
            x = toX;
            y = toY;

            cell.StrategicGroupReferences.Add(new() { referenceId = objectId });

            if (moveThroughEdge)
            {
                var prevCell = cell;
                if (toCell.TryGetDirection(prevCell, out var edge))
                {
                    toCell.SetEdgeSide(edge, side);
                }

                prevCell.RefreshControlState();
                StrategicGameState.Instance.InvokeMapCellUpdated(prevCell.x, prevCell.y);
            }

            toCell.RefreshControlState();
            StrategicGameState.Instance.InvokeMapCellUpdated(toCell.x, toCell.y);
        }

        public void RemoveFromMap()
        {
            if (x == -1 && y == -1)
            {
                return;
            }

            var prevCell = cell;
            // var hexInfoMap = StrategicGameState.Instance.hexInfoMap;

            if (deployState == DeployState.Independent)
            {
                // cellInfo.strategicGroupReferences.RemoveAll(gp => gp.referenceId == objectId);
                cell.StrategicGroupReferences.RemoveAll(gp => gp.referenceId == objectId);
            }

            independentX = -1;
            independentY = -1;

            prevCell.RefreshControlState();
        }

        public void SetDeployState(DeployState newState)
        {
            if (newState == DeployState.Independent)
            {
                var parentGroup = strategicGroupReference.Get();
                if (parentGroup != null)
                {
                    MoveToXY(parentGroup.x, parentGroup.y, false);
                }
                else
                {
                    MoveToXY(0, 0, false);
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

        public float GetSpeedKmPerHour()
        {
            if (IsArmy())
            {
                var nextCell = GetPathNextCell();
                if (nextCell != null && cell.TryGetDirection(nextCell, out var edge))
                {
                    if (cell.GetEdgeSide(edge).objectId != side.objectId)
                        return 0; // edge control block
                    return GetSpeedKmPerHour(cell, nextCell);
                }
                return 0;
            }
            return 10; // 10km/h, cruise speed for ships
        }

        public Cell GetPathNextCell()
        {
            if (plannedPath.Count >= 2)
            {
                var nextXY = plannedPath[1];
                return StrategicGameState.Instance.cellMatrix[nextXY.x, nextXY.y];
            }
            return null;
        }

        public static float GetSpeedKmPerHour(Cell src, Cell dst)
        {
            if (src.TryGetDirection(dst, out var edge))
            {
                var terrainSpeed = terrainToSpeedKmPerHour.GetValueOrDefault(dst.terrain, 1);
                if (src.roads.Contains(edge) || src.railroads.Contains(edge))
                {
                    terrainSpeed = 2f;
                }
                return terrainSpeed;
            }
            return 1;
        }

        static float speedBase = 0.9f; // 0.9km/h

        // Road/Railroad: 2
        public static Dictionary<TerrainType, float> terrainToSpeedKmPerHour = new()
        {
            // {TerrainType.Clear, 1},  // 1km/h for general infantry
            {TerrainType.Clear, speedBase},
            {TerrainType.Rough, speedBase * 0.5f},
            {TerrainType.Mountain, speedBase * 0.3f},
            {TerrainType.Forest, speedBase * 0.5f},
            {TerrainType.Jungle, speedBase * 0.5f},
            {TerrainType.Desert, speedBase},
            {TerrainType.Swamp, speedBase * 0.3f},
            {TerrainType.ForestRough, speedBase * 0.4f},
            {TerrainType.JungleRough, speedBase * 0.4f},
            {TerrainType.DesertRough, speedBase * 0.5f},
            {TerrainType.TropicalMountain, speedBase * 0.3f},
            {TerrainType.SandDesert, speedBase * 0.3f},
            {TerrainType.Field, speedBase},
        };

        // public LandUnit GetCurrentSourceDepot() => ((IStrategicGroupMemberReferenceable)this).GetCurrentSourceDepot();
    }
}

