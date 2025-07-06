using System;
using System.Collections.Generic;
using System.Xml.Serialization;


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

    public class SerializedMatrix<T>
    {
        public int width;
        public int height;
        public List<CellRecord<T>> records;
    }

    public class StrategicGameState
    {
        [XmlIgnore]
        public TerrainType[,] terrainMatrix; // (x, y) => TerrainType

        public event EventHandler mapRebuilt;
        public event EventHandler<(int, int)> mapCellUpdated;

        public SerializedMatrix<TerrainType> serializedMatrix
        {
            get
            {
                var width = terrainMatrix.GetLength(0);
                var height = terrainMatrix.GetLength(1);
                var records = new List<CellRecord<TerrainType>>();
                for (var x = 0; x < width; x++)
                {
                    for (var y = 0; y < height; y++)
                    {
                        records.Add(new CellRecord<TerrainType> { x = x, y = y, value = terrainMatrix[y, x] });
                    }
                }
                return new()
                {
                    width = width,
                    height = height,
                    records = records
                };
            }
            set
            {
                terrainMatrix = new TerrainType[value.width, value.height];
                foreach (var record in value.records)
                {
                    terrainMatrix[record.x, record.y] = record.value;
                }

                mapRebuilt?.Invoke(this, EventArgs.Empty);
            }
        }

        public void GenerateTerrainMat(int width, int height)
        {
            terrainMatrix = new TerrainType[width, height];

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