using YYZ.PathFinding;
using System.Collections.Generic;
using System;
using System.Linq;

namespace StrategicCombatCore
{
    // public class DynamicCellGraph : IGraphEnumerable<Cell>
    // {
    //     public IEnumerable<Cell> Neighbors(Cell pos) => pos.GetNeighbors();

    //     public float EstimateCost(Cell src, Cell dst)
    //     {
    //         return Math.Abs(src.x - dst.x) + Math.Abs(src.y - dst.y);
    //     }

    //     public float MoveCost(Cell src, Cell dst) => 1;

    //     public IEnumerable<Cell> Nodes()
    //     {
    //         foreach (var cell in StrategicGameState.Instance.cellMatrix)
    //             yield return cell;
    //     }
    // }

    public class DynamicCellGraphArmy : IGraphEnumerable<Cell>
    {
        public IEnumerable<Cell> Neighbors(Cell pos)
        {
            foreach (var nei in pos.GetNeighbors())
            {
                if (nei.IsArmyPassable())
                    yield return nei;
            }
        }

        public float EstimateCost(Cell src, Cell dst)
        {
            // return Math.Abs(src.x - dst.x) + Math.Abs(src.y - dst.y);
            return (Math.Abs(src.x - dst.x) + Math.Abs(src.y - dst.y)) / 2f;
        }


        // public float MoveCost(Cell src, Cell dst) => 1;
        public float MoveCost(Cell src, Cell dst) => 1f / StrategicGroup.GetSpeedKmPerHour(src, dst);

        public IEnumerable<Cell> Nodes()
        {
            foreach (var cell in StrategicGameState.Instance.cellMatrix)
                yield return cell;
        }
    }

    public class DynamicCellGraphArmyTheaterFrontline : IGraphEnumerable<Cell>
    {
        public SideState movingSide;

        public IEnumerable<Cell> Neighbors(Cell pos)
        {
            foreach (var nei in pos.GetNeighbors())
            {
                if (nei.IsArmyPassable() && !IsHostileHexControlled(nei))
                    yield return nei;
            }
        }

        bool IsHostileHexControlled(Cell cell)
        {
            var hexSide = cell?.GetHexSide();
            return hexSide != null && hexSide != movingSide;
        }

        public float EstimateCost(Cell src, Cell dst)
        {
            return (Math.Abs(src.x - dst.x) + Math.Abs(src.y - dst.y)) / 2f;
        }

        public float MoveCost(Cell src, Cell dst) => 1f / StrategicGroup.GetSpeedKmPerHour(src, dst);

        public IEnumerable<Cell> Nodes()
        {
            foreach (var cell in StrategicGameState.Instance.cellMatrix)
                yield return cell;
        }
    }

    public class DynamicLandSupplyNetworkingGraph : IGraphEnumerable<Cell>
    {
        public SideState side;

        public IEnumerable<Cell> Neighbors(Cell pos)
        {
            foreach (var nei in pos.GetNeighbors())
            {
                if (nei.IsArmyPassable() && IsLandSupplyPassable(pos, nei))
                    yield return nei;
            }
        }

        bool IsLandSupplyPassable(Cell src, Cell dst)
        {
            if (dst.GetHexSide() == side)
                return true;
            if (src.TryGetDirection(dst, out var edge) && src.GetEdgeSide(edge) == side)
                return true;
            return false;
        }

        public float EstimateCost(Cell src, Cell dst)
        {
            // return Math.Abs(src.x - dst.x) + Math.Abs(src.y - dst.y);
            return (Math.Abs(src.x - dst.x) + Math.Abs(src.y - dst.y)) / 2f;
        }

        // public float MoveCost(Cell src, Cell dst) => 1;
        public float MoveCost(Cell src, Cell dst) => 1f / StrategicGroup.GetSpeedKmPerHour(src, dst);

        public IEnumerable<Cell> Nodes()
        {
            foreach (var cell in StrategicGameState.Instance.cellMatrix)
                yield return cell;
        }
    }

    public class DynamicLandRetreatGraph : IGraphEnumerable<Cell> // Retreating pathfinding can pass neutral area (supply networking can't) but can't pass hositle controlled area 
    {
        public SideState side;

        public IEnumerable<Cell> Neighbors(Cell pos)
        {
            foreach (var nei in pos.GetNeighbors())
            {
                if (nei.IsArmyPassable() && IsRetreatPassable(pos, nei))
                    yield return nei;
            }
        }

        bool IsRetreatPassable(Cell src, Cell dst)
        {
            var dstHexSide = dst.GetHexSide();
            if (dstHexSide != null && dstHexSide != side)
                return false;
            if(src.TryGetDirection(dst, out var edge))
            {
                var edgeSide = src.GetEdgeSide(edge);
                if(edgeSide != null && edgeSide != side)
                    return false;
            }
            return true;
        }

        public float EstimateCost(Cell src, Cell dst)
        {
            // return Math.Abs(src.x - dst.x) + Math.Abs(src.y - dst.y);
            return (Math.Abs(src.x - dst.x) + Math.Abs(src.y - dst.y)) / 2f;
        }

        // public float MoveCost(Cell src, Cell dst) => 1;
        public float MoveCost(Cell src, Cell dst) => 1f / StrategicGroup.GetSpeedKmPerHour(src, dst);

        public IEnumerable<Cell> Nodes()
        {
            foreach (var cell in StrategicGameState.Instance.cellMatrix)
                yield return cell;
        }
    }

    public class DynamicCellGraphNavy : IGraphEnumerable<Cell>
    {
        public SideState movingSide;

        public IEnumerable<Cell> Neighbors(Cell pos)
        {
            foreach (var nei in pos.GetNeighbors())
            {
                if (nei.IsNavyPassable() &&
                    !pos.HasEdgeFeatureTo(nei, EdgeFeatureType.BlockSeaMovement) &&
                    !StrategicGroup.CellHasHostileFortifiedBaseFor(nei, movingSide))
                {
                    yield return nei;
                }
            }
        }

        public float EstimateCost(Cell src, Cell dst)
        {
            // return Math.Abs(src.x - dst.x) + Math.Abs(src.y - dst.y);
            if(src.IsGridCell() && dst.IsGridCell())
            {
                return Math.Abs(src.x - dst.x) + Math.Abs(src.y - dst.y);
            }
            else
            {
                return Math.Abs(src.latitude - dst.latitude) + Math.Abs(src.longitude - dst.longitude);
            }
        }

        // public float MoveCost(Cell src, Cell dst) => 1;
        public float MoveCost(Cell src, Cell dst)
        {
            if(src.IsAreaCell() && dst.IsAreaCell())
            {
                var conn = src.CellConnections.FirstOrDefault(c => c.GetOther() == dst);
                return conn.cost;
            }
            else
            {
                return 1;
            }
        }

        public IEnumerable<Cell> Nodes()
        {
            var gameState = StrategicGameState.Instance;
            if(gameState.scenarioState.enableGridSystem)
            {
                foreach (var cell in gameState.cellMatrix)
                    yield return cell;
            }
            if(gameState.scenarioState.enableAreaSystem)
            {
                foreach (var cell in gameState.areaCells)
                    yield return cell;
            }
        }
    }
}
