using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml.Serialization;
using Acornima.Ast;
using CoreUtils;


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

    public class CellRecord<T>
    {
        [XmlAttribute]
        public int x;

        [XmlAttribute]
        public int y;

        [XmlAttribute]
        public T value;
    }

    // public enum HexPairFeatureType
    // {
    //     Road,
    //     Railroad
    // }

    // public class HexPairFeatureRecord<T>
    // {
    //     [XmlAttribute]
    //     public int x1;

    //     [XmlAttribute]
    //     public int y1;

    //     [XmlAttribute]
    //     public int x2;

    //     [XmlAttribute]
    //     public int y2;

    //     [XmlAttribute]
    //     public T value;
    // }

    public class SerializedMatrix<T>
    {
        public int width;
        public int height;
        public List<CellRecord<T>> records;
    }

    public class StrategicLocationLabel
    {
        public int x;
        public int y;
        public GlobalString name;
        // public int size;
    }

    // public class CellEdge
    // {
    //     public Cell cell;

    //     public bool road;

    //     public bool railroad;
    //     public bool seaLandBlocked;
    // }

    public enum EdgeDirection: byte
    {
        Top,
        TopRight,
        BottomRight,
        Bottom,
        BottomLeft,
        TopLeft
    }

    // public class EdgeBool // sparse representation for road, river. Reduce XML Serialization size
    // {
    //     public EdgeDirection edgeDirection; // 0 => top, 1 => right+top, 2 => right+bottom, 3 => bottom, ...
    //     public bool value;

    //     public string Encode()
    //     {
    //         var b = value ? 1 : 0;
    //         return $"{(byte)edgeDirection}-{b}";
    //     }

    //     public static EdgeBool Deocde(string s)
    //     {
    //         var a = s.Split('-');
    //         return new EdgeBool
    //         {
    //             edgeDirection = (EdgeDirection)byte.Parse(a[0]),
    //             value = int.Parse(a[1]) == 1
    //         };
    //     }
    // }

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

        // [XmlIgnore]
        // public List<CellEdge> edges = new();
    }

    public class SerializedCells
    {
        public int width;
        public int height;
        public List<Cell> records;
    }

    public class StrategicGameState
    {
        [XmlIgnore]
        public Cell[,] cellMatrix;


        public SerializedCells serializedCells
        {
            get
            {
                var records = new List<Cell>();
                for (var x = 0; x < GetMapWidth(); x++)
                {
                    for (var y = 0; y < GetMapHeight(); y++)
                    {
                        records.Add(cellMatrix[x, y]);
                    }
                }
                return new()
                {
                    width = GetMapWidth(),
                    height = GetMapHeight(),
                    records=records
                };
            }
            set
            {
                cellMatrix = new Cell[value.width, value.height];

                foreach (var cell in value.records)
                {
                    cellMatrix[cell.x, cell.y] = cell;
                }

                mapRebuilt?.Invoke(this, EventArgs.Empty);
            }
        }

        public List<StrategicLocationLabel> labels = new();

        public event EventHandler mapRebuilt;
        public event EventHandler<(int, int)> mapCellUpdated;
        public event EventHandler edgeFeatureUpdated;

        public void AddRoad(Cell cell1, Cell cell2)
        {
            if (cell1.TryGetDirection(cell2, out var edgeDirection))
            {
                cell1.roads.Add(edgeDirection);
            }
            if (cell2.TryGetDirection(cell1, out edgeDirection))
            {
                cell2.roads.Add(edgeDirection);
            }
            edgeFeatureUpdated?.Invoke(this, EventArgs.Empty);
        }

        public void DeleteRoad(Cell cell1, Cell cell2)
        {
            if (cell1.TryGetDirection(cell2, out var edgeDirection))
            {
                cell1.roads.RemoveAll(d => d == edgeDirection);
            }
            if (cell2.TryGetDirection(cell1, out edgeDirection))
            {
                cell2.roads.RemoveAll(d => d == edgeDirection);
            }
            edgeFeatureUpdated?.Invoke(this, EventArgs.Empty);
        }

        public int GetMapWidth() => cellMatrix.GetLength(0);
        public int GetMapHeight() => cellMatrix.GetLength(1);


        public void SetMapCellTerrain(int x, int y, TerrainType terrainType)
        {
            // terrainMatrix[x, y] = terrainType;
            cellMatrix[x, y].terrain = terrainType;

            mapCellUpdated?.Invoke(this, (x, y));
        }

        public void UpdateTo(StrategicGameState newInstance)
        {
            // terrainMatrix = newInstance.terrainMatrix;
            cellMatrix = newInstance.cellMatrix;
            labels = newInstance.labels;

            mapRebuilt?.Invoke(this, EventArgs.Empty);
            edgeFeatureUpdated?.Invoke(this, EventArgs.Empty);
        }


        public void GenerateTerrainMatrix(int width, int height)
        {
            // terrainMatrix = new TerrainType[width, height];
            cellMatrix = new Cell[width, height];

            for (int x = 0; x < cellMatrix.GetLength(0); x++)
                for (int y = 0; y < cellMatrix.GetLength(1); y++)
                    cellMatrix[x, y] = new();

            mapRebuilt?.Invoke(this, EventArgs.Empty);
        }

        public IEnumerable<(Cell, Cell)> IterateRoadCellPairs()
        {
            for (int x = 0; x < cellMatrix.GetLength(0); x++)
            {
                for (int y = 0; y < cellMatrix.GetLength(1); y++)
                {
                    var cell = cellMatrix[x, y];
                    foreach (var edgeDirection in cell.roads)
                    {
                        var neighbor = cell.GetNeighbor(edgeDirection);
                        yield return (cell, neighbor);
                    }
                }
            }
        }

        static StrategicGameState _instance;
        public static StrategicGameState Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new();
                }
                return _instance;
            }
        }
    }
}