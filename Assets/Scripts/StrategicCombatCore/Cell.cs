using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml.Serialization;

using CoreUtils;
using UnityEditor.Experimental.GraphView;


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


    public class Cell
    {
        [XmlAttribute]
        public int x;

        [XmlAttribute]
        public int y;

        [XmlAttribute]
        public TerrainType terrain;

        [XmlAttribute]
        public Country country;

        [XmlIgnore]
        public List<EdgeDirection> roads = new();

        [XmlIgnore]
        public List<EdgeDirection> railroads = new();

        [XmlIgnore]
        public List<EdgeDirection> rivers = new();

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

        public bool TryGetDirection((int, int) xy, out EdgeDirection edgeDirection)
        {
            var offsetToDirection = x % 2 == 0 ? offsetToDirectionEven : offsetToDirectionsetOdd;
            return offsetToDirection.TryGetValue(xy, out edgeDirection);
        }

        public bool TryGetDirection(Cell other, out EdgeDirection edgeDirection) => TryGetDirection((other.x - x, other.y - y), out edgeDirection);

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

        // [XmlIgnore]
        // public List<CellEdge> edges = new();
    }
}