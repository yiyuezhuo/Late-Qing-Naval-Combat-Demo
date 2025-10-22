using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
        public Cell GetCell()
        {
            var parentGroup = Get();
            if (parentGroup == null || !parentGroup.IsOnMap())
                return null;
            return parentGroup.cell;
        }
        public SideState GetSide()
        {
            var parentGroup = Get();
            if (parentGroup == null)
                return null;
            return parentGroup.side;
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
            Independent,
            // Loaded, // Similar to NotDeployed, but it's actually attached loaded in a ship. Used to naval transfer
            // VolatileIndependent // Similar to Independent, but it would "dissolve" automatically if it's possible to combine to its parent. Use to naval transfer
        }
        public DeployState deployState; // generally, deployState should be set with SetDeployState()
        
        // Independent sub states:
        public bool autoCombinable; // if true, it will convert from independent to combining when applicable 
        public bool dissolvable; // if true, it would "dissolve" automatically if combine is applicable.
        
        public string containerObjectId; // Generally shipLog's objectId.
        
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
        public Cell cell => x != -1 && y != -1 ? StrategicGameState.Instance.cellMatrix[x, y] : null;

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
            var prevCell = cell;

            if (deployState == DeployState.Independent && prevCell != null)
            {
                prevCell.StrategicGroupReferences.RemoveAll(gp => gp.referenceId == objectId);
            }

            deployState = DeployState.Independent;
            x = toX;
            y = toY;

            toCell.StrategicGroupReferences.Add(new() { referenceId = objectId });

            if (moveThroughEdge && IsArmy())
            {

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
        
        public void UnloadFromContainer()
        {
            if(containerObjectId != null)
            {
                var container = EntityManager.Instance.Get<ShipLog>(containerObjectId);
                if(container != null)
                {
                    var containerGroup = container.strategicGroupReference.Get();
                    if(containerGroup != null)
                    {
                        MoveToXY(containerGroup.x, containerGroup.y, false);
                        container.loadedGroups.RemoveAll(r => r.referenceId == objectId);
                    }
                }
            }
        }

        public void RemoveFromMap() // When Independent is transitioned to Combined or NotDeployed
        {
            var prevCell = cell;

            if (prevCell == null)
            {
                return;
            }
            
            // var hexInfoMap = StrategicGameState.Instance.hexInfoMap;

            if (deployState == DeployState.Independent)
            {
                // cellInfo.strategicGroupReferences.RemoveAll(gp => gp.referenceId == objectId);
                cell.StrategicGroupReferences.RemoveAll(gp => gp.referenceId == objectId);
            }

            independentX = -1;
            independentY = -1;

            // deployState = DeployState.NotDeployed;

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

        public IEnumerable<T> WalkGroupMembers<T>(bool includeNotCombined=false) where T : IStrategicGroupMemberReferenceable
        {
            foreach (var subordinateRef in subordinatesCombined)
            {
                var subordinate = subordinateRef.Get();

                if (subordinate is T obj && obj != null)
                    yield return obj;

                if (subordinate is StrategicGroup group && group != null &&
                    (includeNotCombined || group.deployState == DeployState.Combined))
                {
                    foreach (var subObj in group.WalkGroupMembers<T>(includeNotCombined))
                    {
                        yield return subObj;
                    }
                }
            }
        }

        public double GetTransferWeightTons()
        {
            return WalkGroupMembers<LandUnit>().Sum(landUnit => landUnit.GetTransferWeightTons());
        }

        public void LoadToShip(ShipLog shipLog)
        {
            if (deployState == DeployState.Combined)
            {
                autoCombinable = true;
            }
            else if (deployState == DeployState.Independent)
            {
            }
            // deployState = DeployState.NotDeployed;
            RemoveFromMap();
            deployState = DeployState.NotDeployed;

            containerObjectId = shipLog.objectId;
            shipLog.loadedGroups.Add(new() { referenceId = objectId });
        }

        public void AttachTo(StrategicGroup newParentGroup)
        {
            var oldParentGroup = strategicGroupReference.Get();
            if (oldParentGroup != null)
            {
                oldParentGroup.subordinatesCombined.RemoveAll(f => f.referenceId == objectId);
            }
            if (newParentGroup != null)
            {
                newParentGroup.subordinatesCombined.Add(new() { referenceId = objectId });
            }
            strategicGroupReference.referenceId = newParentGroup?.objectId;
        }

        public void TransferLandUnit(LandUnit subLandUnit, StrategicGroup toGroup)
        {
            subordinatesCombined.RemoveAll(f => f.referenceId == subLandUnit.objectId);
            toGroup.subordinatesCombined.Add(new() { referenceId = subLandUnit.objectId });
        }

        public void Split()
        {
            if (subordinatesCombined.Count < 2)
                return;

            var newGroup = new StrategicGroup()
            {
                name = name.Add("/2"),
                type = type,
                size = size,
                country = country,
                deployState = DeployState.NotDeployed,
                // independentX = independentX,
                // independentY = independentY,
            };
            var gameState = StrategicGameState.Instance;
            var idx = gameState.strategicGroups.IndexOf(this);
            gameState.strategicGroups.Insert(idx + 1, newGroup);
            EntityManager.Instance.Register(newGroup, null);

            newGroup.AttachTo(strategicGroupReference.Get());

            if (deployState == DeployState.Independent)
            {
                newGroup.MoveToXY(independentX, independentY, false);
            }

            var transferElements = Enumerable.Range(0, subordinatesCombined.Count)
                .Where(idx => idx % 2 == 1)
                .Select(idx => subordinatesCombined[idx])
                .ToList();

            // var idxs = Enumerable.Range(0, subordinatesCombined.Count)
            //      .Where(idx => idx % 2 == 1).ToList();
            // var transferElements = idxs.Select(idx => subordinatesCombined[idx]);

            foreach (var transferElementRef in transferElements)
            {
                // subordinatesCombined.Remove(transferElement);
                // newGroup.subordinatesCombined.Add(transferElement);
                var element = transferElementRef.Get();
                MoveElementTo(element, newGroup);
            }
        }
        
        public void MoveElementTo(IStrategicGroupMemberReferenceable element, StrategicGroup otherGroup)
        {
            subordinatesCombined.RemoveAll(r => r.referenceId == element.objectId);
            otherGroup.subordinatesCombined.Add(new() { referenceId = element.objectId });
            element.strategicGroupReference.referenceId = otherGroup.objectId;
        }
    }
}

