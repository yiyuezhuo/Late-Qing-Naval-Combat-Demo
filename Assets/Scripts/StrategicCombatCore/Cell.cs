using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Xml.Serialization;

using CoreUtils;
using NavalCombatCore;


namespace StrategicCombatCore
{

    public enum TerrainType : byte
    {
        Clear = 0,
        Rough = 1,
        Mountain = 2,
        Forest = 3,
        Jungle = 4,
        Desert = 5,
        Swamp = 6,
        ForestRough = 7,
        JungleRough = 8,
        DesertRough = 9,
        TropicalMountain = 10,
        SandDesert = 11,
        HeavyUrban = 12,
        LightUrban = 13,
        Field = 14,
        ShallowWater = 15,
        DeepWater = 16,
    }

    public class StrategicLocationLabel
    {
        public int x;
        public int y;
        public GlobalString name;
        // public int size;
    }

    public enum EdgeDirection: byte
    {
        Top,
        TopRight,
        BottomRight,
        Bottom,
        BottomLeft,
        TopLeft
    }

    public enum CornerType
    {
        TopRight,
        Right,
        BottomRight,
        BottomLeft,
        Left,
        TopLeft
    }

    public enum EdgeFeatureType
    {
        Road,
        Railroad,
        River
    }


    public partial class Cell
    {
        [XmlAttribute]
        public int x;

        [XmlAttribute]
        public int y;

        [XmlAttribute]
        public TerrainType terrain;

        // [XmlAttribute]
        // public Country country;

        [XmlIgnore]
        public List<EdgeDirection> roads = new();

        [XmlIgnore]
        public List<EdgeDirection> railroads = new();

        [XmlIgnore]
        public List<EdgeDirection> rivers = new();

        [XmlAttribute]
        public float longitude;

        // [XmlAttribute]
        // public float longitude;

        [XmlAttribute]
        public float latitude;

        // [XmlAttribute]
        // public bool groundControlPoint; // GCP (Ground Control Point) lon & lat would be used to infer other cell's lat & lon (Georeference).

        [XmlAttribute]
        public bool GroundControlPoint; // GCP (Ground Control Point) lon & lat would be used to infer other cell's lat & lon (Georeference).

        public GlobalString Label; // nullable
                                   // public bool ShouldSerializeLabel() => Label != null;

        [XmlAttribute("IsCoast")]
        public bool IsCoast;
        public bool ShouldSerializeIsCoast() => IsCoast; // Used by XmlSerializer

        // Move XmlElement to XmlAttribute hack
        // [XmlElement("IsCoast")]
        // public bool IsCoast2
        // {
        //     get => IsCoast;
        //     set => IsCoast = value;
        // }
        // public bool ShouldSerializeIsCoast2() => false;

        public bool ShouldSerializeGroundControlPoint() => GroundControlPoint; // Used by XmlSerializer

        string EncodeBoolArray(List<EdgeDirection> arr)
        {
            if (arr.Count == 0)
                return null;
            return string.Join("/", arr.Select(d => (byte)d)); // TOAW style encode
        }

        List<EdgeDirection> DecodeBoolArray(string arrStr)
        {
            if (arrStr == null)
                return new();
            return arrStr.Split('/').Select(x => (EdgeDirection)byte.Parse(x)).ToList();
        }

        [XmlAttribute]
        public string roadsStr
        {
            get => EncodeBoolArray(roads);
            set => roads = DecodeBoolArray(value);
        }

        [XmlAttribute]
        public string railroadsStr
        {
            get => EncodeBoolArray(railroads);
            set => railroads = DecodeBoolArray(value);
        }

        [XmlAttribute]
        public string riversStr
        {
            get => EncodeBoolArray(rivers);
            set => rivers = DecodeBoolArray(value);
        }

        [XmlAttribute]
        public string sideObjectIdHex;

        [XmlAttribute]
        public string sideObjectIdTop;

        [XmlAttribute]
        public string sideObjectIdTopRight;

        [XmlAttribute]
        public string sideObjectIdBottomRight;

        [XmlAttribute]
        public string sideObjectIdBottom;

        [XmlAttribute]
        public string sideObjectIdBottomLeft;

        [XmlAttribute]
        public string sideObjectIdTopLeft;

        public SideState GetHexSide()
        {
            return EntityManager.Instance.Get<SideState>(sideObjectIdHex);
        }

        [XmlAttribute]
        public string landBattleId;

        public LandBattle GetLandBattle()
        {
            return EntityManager.Instance.Get<LandBattle>(landBattleId);
        }

        public bool IsArmyPassable() => IsCoast || (terrain != TerrainType.ShallowWater && terrain != TerrainType.DeepWater);
        public bool IsNavyPassable() => IsCoast || terrain == TerrainType.ShallowWater || terrain == TerrainType.DeepWater;

        public SideState GetEdgeSide(EdgeDirection edgeDirection)
        {
            var edgeObjectId = edgeDirection switch
            {
                EdgeDirection.Top => sideObjectIdTop,
                EdgeDirection.TopRight => sideObjectIdTopRight,
                EdgeDirection.BottomRight => sideObjectIdBottomRight,
                EdgeDirection.Bottom => sideObjectIdBottom,
                EdgeDirection.BottomLeft => sideObjectIdBottomLeft,
                EdgeDirection.TopLeft => sideObjectIdTopLeft,
                _ => sideObjectIdHex
            };
            if (edgeObjectId == null)
            {
                edgeObjectId = sideObjectIdHex;
            }
            return EntityManager.Instance.Get<SideState>(edgeObjectId);
        }

        public float GetMassCenterY(SideState side)
        {
            var hexControlled = side.objectId == sideObjectIdHex;
            var ret = 0f;

            if (sideObjectIdTop == side.objectId || (sideObjectIdTop == null && hexControlled))
                ret += 1f;
            if (sideObjectIdTopRight == side.objectId || (sideObjectIdTopRight == null && hexControlled))
                ret += 1f;
            if (sideObjectIdTopLeft == side.objectId || (sideObjectIdTopLeft == null && hexControlled))
                ret += 1f;
            
            if (sideObjectIdBottomRight == side.objectId || (sideObjectIdBottomRight == null && hexControlled))
                ret -= 1f;
            if (sideObjectIdBottom == side.objectId || (sideObjectIdBottom == null && hexControlled))
                ret -= 1f;
            if (sideObjectIdBottomLeft == side.objectId || (sideObjectIdBottomLeft == null && hexControlled))
                ret -= 1f;

            return ret;
        }

        public void RefreshControlState()
        {
            if (!IsArmyPassable())
                return;
            
            var groups = StrategicGroupReferences
                .Select(r => r.Get())
                // .Where(g => g != null && g.IsArmy())
                .Where(g => g != null && g.IsArmy()).ToList();
            var activeSides = groups.Where(g => g.posture != StrategicGroup.GroupPostureType.Disengaged).Select(g => g.side).ToHashSet();
            var passiveSides =groups.Select(g => g.side).ToHashSet();

            if(passiveSides.Count <= 1)
            {
                sideObjectIdTop = null;
                sideObjectIdTopRight = null;
                sideObjectIdBottomRight = null;
                sideObjectIdBottom = null;
                sideObjectIdBottomLeft = null;
                sideObjectIdTopLeft = null;
            }
            if (passiveSides.Count == 1)
            {
                sideObjectIdHex = passiveSides.First().objectId;
            }
            else if(activeSides.Count == 1)
            {
                sideObjectIdTop ??= sideObjectIdHex;
                sideObjectIdTopRight ??= sideObjectIdHex;
                sideObjectIdBottomRight ??= sideObjectIdHex;
                sideObjectIdBottom ??= sideObjectIdHex;
                sideObjectIdBottomLeft ??= sideObjectIdHex;
                sideObjectIdTopLeft ??= sideObjectIdHex;

                sideObjectIdHex = activeSides.First().objectId;
            }
            
            if(activeSides.Count >= 2 && sideObjectIdHex == null)
            {
                sideObjectIdHex = RandomUtils.Sample(activeSides.ToList()).objectId;
            }
        }

        public void SetEdgeSide(EdgeDirection edgeDirection, SideState sideState)
        {
            switch (edgeDirection)
            {
                case EdgeDirection.Top:
                    sideObjectIdTop = sideState.objectId;
                    break;
                case EdgeDirection.TopRight:
                    sideObjectIdTopRight = sideState.objectId;
                    break;
                case EdgeDirection.BottomRight:
                    sideObjectIdBottomRight = sideState.objectId;
                    break;
                case EdgeDirection.Bottom:
                    sideObjectIdBottom = sideState.objectId;
                    break;
                case EdgeDirection.BottomLeft:
                    sideObjectIdBottomLeft = sideState.objectId;
                    break;
                case EdgeDirection.TopLeft:
                    sideObjectIdTopLeft = sideState.objectId;
                    break;
            }
        }

        // Used by XmlSerializer
        public bool ShouldSerializeStrategicGroupReferences() => StrategicGroupReferences != null && StrategicGroupReferences.Count > 0;
        public List<StrategicGroupReference> StrategicGroupReferences = new();

        public static Dictionary<EdgeDirection, (int, int)> directionToOffsetEven = new()
        {
            { EdgeDirection.Top, (0, 1) },
            { EdgeDirection.TopRight, (1, 0) },
            { EdgeDirection.BottomRight, (1, -1) },
            { EdgeDirection.Bottom, (0, -1) },
            { EdgeDirection.BottomLeft, (-1, -1) },
            { EdgeDirection.TopLeft, (-1, 0) },
        };

        public static Dictionary<EdgeDirection, (int, int)> directionToOffsetOdd = new()
        {
            { EdgeDirection.Top, (0, 1) },
            { EdgeDirection.TopRight, (1, 1) },
            { EdgeDirection.BottomRight, (1, 0) },
            { EdgeDirection.Bottom, (0, -1) },
            { EdgeDirection.BottomLeft, (-1, 0) },
            { EdgeDirection.TopLeft, (-1, 1) },
        };

        public static Dictionary<(int, int), EdgeDirection> offsetToDirectionEven = new()
        {
            { (0, 1), EdgeDirection.Top },
            { (1, 0), EdgeDirection.TopRight },
            { (1, -1), EdgeDirection.BottomRight },
            { (0, -1), EdgeDirection.Bottom },
            { (-1, -1), EdgeDirection.BottomLeft },
            { (-1, 0), EdgeDirection.TopLeft },
        };

        public static Dictionary<(int, int), EdgeDirection> offsetToDirectionsetOdd = new()
        {
            { (0, 1), EdgeDirection.Top },
            { (1, 1), EdgeDirection.TopRight },
            { (1, 0), EdgeDirection.BottomRight },
            { (0, -1), EdgeDirection.Bottom },
            { (-1, 0), EdgeDirection.BottomLeft },
            { (-1, 1), EdgeDirection.TopLeft },
        };

        public static Dictionary<EdgeDirection, (CornerType, CornerType)> edgeDirectionToCornerType = new()
        {
            { EdgeDirection.Top, (CornerType.TopRight, CornerType.TopLeft) },
            { EdgeDirection.TopRight, (CornerType.TopRight, CornerType.Right) },
            { EdgeDirection.BottomRight, (CornerType.Right, CornerType.BottomRight) },
            { EdgeDirection.Bottom, (CornerType.BottomRight, CornerType.BottomLeft) },
            { EdgeDirection.BottomLeft, (CornerType.BottomLeft, CornerType.Left) },
            { EdgeDirection.TopLeft, (CornerType.Left, CornerType.TopLeft) },
        };

        public (int, int) GetOffset(EdgeDirection edgeDirection)
        {
            var directionToOffset = x % 2 == 0 ? directionToOffsetEven : directionToOffsetOdd;
            return directionToOffset[edgeDirection];
        }

        public Cell GetNeighbor(EdgeDirection edgeDirection)
        {
            var (dx, dy) = GetOffset(edgeDirection);
            var x2 = x + dx;
            var y2 = y + dy;

            var gameState = StrategicGameState.Instance;
            if (x2 >= 0 && x2 < gameState.GetMapWidth() && y2 >= 0 && y2 < gameState.GetMapHeight())
            {
                return gameState.cellMatrix[x2, y2];
            }
            return null;
        }

        static List<EdgeDirection> defaultDirectionsOrder = new()
        {
            EdgeDirection.Top,
            EdgeDirection.TopRight,
            EdgeDirection.BottomRight,
            EdgeDirection.Bottom,
            EdgeDirection.BottomLeft,
            EdgeDirection.TopLeft,
        };

        public IEnumerable<Cell> GetNeighbors()
        {
            foreach (var edge in defaultDirectionsOrder)
            {
                var hex = GetNeighbor(edge);
                if (hex != null)
                    yield return hex;
            }
        }

        // public bool TryGetDirection((int, int) xy, out EdgeDirection edgeDirection)
        // {
        //     var offsetToDirection = x % 2 == 0 ? offsetToDirectionEven : offsetToDirectionsetOdd;
        //     return offsetToDirection.TryGetValue(xy, out edgeDirection);
        // }

        // public bool TryGetDirection(Cell other, out EdgeDirection edgeDirection) => TryGetDirection((other.x - x, other.y - y), out edgeDirection);
        
        public bool TryGetDirection(Cell other, out EdgeDirection edgeDirection)
        {
            var dxy = (other.x - x, other.y - y);
            var offsetToDirection = x % 2 == 0 ? offsetToDirectionEven : offsetToDirectionsetOdd;
            return offsetToDirection.TryGetValue(dxy, out edgeDirection);
        }

        public List<EdgeDirection> GetEdgeDirectionsFor(EdgeFeatureType edgeFeatureType)
        {
            return edgeFeatureType switch
            {
                EdgeFeatureType.Road => roads,
                EdgeFeatureType.Railroad => railroads,
                EdgeFeatureType.River => rivers,
                _ => roads
            };
        }

        public void AddEdgeFeature(EdgeDirection edgeDirection, EdgeFeatureType edgeFeatureType)
        {
            var directions = GetEdgeDirectionsFor(edgeFeatureType);

            if (directions.IndexOf(edgeDirection) == -1)
            {
                directions.Add(edgeDirection);
            }
        }

        public void RemoveEdgeFeature(EdgeDirection edgeDirection, EdgeFeatureType edgeFeatureType)
        {
            var directions = GetEdgeDirectionsFor(edgeFeatureType);
            directions.RemoveAll(d => d == edgeDirection);
        }

        public override string ToString()
        {
            return $"Cell({x}, {y}, {terrain})";
        }

        // [XmlIgnore]
        // public List<CellEdge> edges = new();
    }
}