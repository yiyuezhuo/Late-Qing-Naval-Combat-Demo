using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml.Serialization;

using CoreUtils;


namespace StrategicCombatCore
{


    public class SerializedCells
    {
        public int width;
        public int height;
        public List<Cell> records;
    }

    public class StrategicGameState : AbstractGameState
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

        public HighCommand highCommand = new();

        public List<LandUnitTemplate> landUnitTemplates = new();
        public List<LandUnit> landUnits = new();
        public List<Weapon> weapons = new();
        public List<StrategicGroup> strategicGroups = new();

        public event EventHandler mapRebuilt;
        public event EventHandler<(int, int)> mapCellUpdated;
        public event EventHandler edgeFeatureUpdated;

        public void AddEdgeFeature(Cell cell1, Cell cell2, EdgeFeatureType edgeFeatureType)
        {

            if (cell1.TryGetDirection(cell2, out var edgeDirection))
            {
                cell1.GetEdgeDirectionsFor(edgeFeatureType).Add(edgeDirection);
            }
            if (cell2.TryGetDirection(cell1, out edgeDirection))
            {
                cell2.GetEdgeDirectionsFor(edgeFeatureType).Add(edgeDirection);
            }
            edgeFeatureUpdated?.Invoke(this, EventArgs.Empty);
        }

        public void DeleteEdgeFeature(Cell cell1, Cell cell2, EdgeFeatureType edgeFeatureType)
        {
            if (cell1.TryGetDirection(cell2, out var edgeDirection))
            {
                cell1.GetEdgeDirectionsFor(edgeFeatureType).RemoveAll(d => d == edgeDirection);
            }
            if (cell2.TryGetDirection(cell1, out edgeDirection))
            {
                cell2.GetEdgeDirectionsFor(edgeFeatureType).RemoveAll(d => d == edgeDirection);
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
            highCommand = newInstance.highCommand;
            landUnitTemplates = newInstance.landUnitTemplates;
            landUnits = newInstance.landUnits;
            weapons = newInstance.weapons;
            strategicGroups = newInstance.strategicGroups;

            shipLogs = newInstance.shipLogs;

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

        public IEnumerable<(Cell, Cell, EdgeDirection)> IterateCellPairsFor(EdgeFeatureType edgeFeatureType)
        {
            for (int x = 0; x < cellMatrix.GetLength(0); x++)
            {
                for (int y = 0; y < cellMatrix.GetLength(1); y++)
                {
                    var cell = cellMatrix[x, y];

                    foreach (var edgeDirection in cell.GetEdgeDirectionsFor(edgeFeatureType))
                    {
                        var neighbor = cell.GetNeighbor(edgeDirection);
                        yield return (cell, neighbor, edgeDirection);
                    }
                }
            }
        }

        public override void ResetAndRegisterAll()
        {
            base.ResetAndRegisterAll();

            foreach (var landUnit in landUnits)
            {
                EntityManager.Instance.Register(landUnit, null);
            }

            foreach (var landUnitTemplate in landUnitTemplates)
            {
                EntityManager.Instance.Register(landUnitTemplate, null);
            }

            foreach (var weapon in weapons)
                EntityManager.Instance.Register(weapon, null);

            foreach (var strategicGroup in strategicGroups)
                EntityManager.Instance.Register(strategicGroup, null);
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