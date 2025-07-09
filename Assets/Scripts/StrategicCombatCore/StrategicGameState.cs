using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml.Serialization;

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

    public class CornerBool // sparse representation for road, river. Reduce XML Serialization size
    {
        public EdgeDirection corner; // 0 => top, 1 => right+top, 2 => right+bottom, 3 => bottom, ...
        public bool value;

        public string Encode()
        {
            var b = value ? 1 : 0;
            return $"{(byte)corner}-{b}";
        }

        public static CornerBool Deocde(string s)
        {
            var a = s.Split('-');
            return new CornerBool
            {
                corner = (EdgeDirection)byte.Parse(a[0]),
                value = int.Parse(a[1]) == 1
            };
        }
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
        public List<CornerBool> roads = new();

        [XmlIgnore]
        public List<CornerBool> railroads = new();

        [XmlIgnore]
        public List<CornerBool> rivers = new();

        string EncodeBoolArray(List<CornerBool> arr)
        {
            if (arr.Count == 0)
                return null;
            return string.Join("/", arr.Select(x => x.Encode())); // TOAW style encode
        }

        List<CornerBool> DecodeBoolArray(string arrStr)
        {
            if (arrStr == null)
                return new();
            return arrStr.Split('/').Select(x => CornerBool.Deocde(x)).ToList();
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
        // [XmlIgnore]
        // public TerrainType[,] terrainMatrix; // (x, y) => TerrainType

        [XmlIgnore]
        public Cell[,] cellMatrix;

        // public SerializedMatrix<TerrainType> serializedMatrix
        // {
        //     get
        //     {
        //         return new();

        //         // var width = terrainMatrix.GetLength(0);
        //         // var height = terrainMatrix.GetLength(1);
        //         // var records = new List<CellRecord<TerrainType>>();
        //         // for (var x = 0; x < width; x++)
        //         // {
        //         //     for (var y = 0; y < height; y++)
        //         //     {
        //         //         records.Add(new CellRecord<TerrainType> { x = x, y = y, value = terrainMatrix[x, y] });
        //         //     }
        //         // }
        //         // return new()
        //         // {
        //         //     width = width,
        //         //     height = height,
        //         //     records = records
        //         // };
        //     }
        //     set
        //     {
        //         cellMatrix = new Cell[value.width, value.height];
        //         for (int x = 0; x < cellMatrix.GetLength(0); x++)
        //             for (int y = 0; y < cellMatrix.GetLength(1); y++)
        //                 cellMatrix[x, y] = new();

        //         foreach (var record in value.records)
        //         {
        //             var cell = cellMatrix[record.x, record.y];

        //             cell.x = record.x;
        //             cell.y = record.y;
        //             cell.terrain = record.value;
        //         }

        //         mapRebuilt?.Invoke(this, EventArgs.Empty);

        //         // terrainMatrix = new TerrainType[value.width, value.height];
        //         // foreach (var record in value.records)
        //         // {
        //         //     terrainMatrix[record.x, record.y] = record.value;
        //         // }

        //         // mapRebuilt?.Invoke(this, EventArgs.Empty);
        //     }
        // }

        public SerializedCells serializedCells
        {
            get
            {
                // UnityEngine.Debug.LogWarning("serializedCells.getter");

                // if (cellMatrix == null)
                //     return null; // XmlSerializer will firstly call the getter, if it's null, then setter is called. Otherwise, element is appended to the current list.

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
                // UnityEngine.Debug.LogWarning("serializedCells.setter");

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

        // public int GetMapWidth() => terrainMatrix.GetLength(0);
        // public int GetMapHeight() => terrainMatrix.GetLength(1);
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