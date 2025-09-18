using YYZ.PathFinding;
using System.Collections.Generic;
using System;

namespace StrategicCombatCore
{
    public class DynamicCellGraph : IGraphEnumerable<Cell>
    {
        public IEnumerable<Cell> Neighbors(Cell pos) => pos.GetNeighbors();

        public float EstimateCost(Cell src, Cell dst)
        {
            return Math.Abs(src.x - dst.x) + Math.Abs(src.y - dst.y);
        }

        public float MoveCost(Cell src, Cell dst) => 1;

        public IEnumerable<Cell> Nodes()
        {
            foreach (var cell in StrategicGameState.Instance.cellMatrix)
                yield return cell;
        }
    }

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
            return Math.Abs(src.x - dst.x) + Math.Abs(src.y - dst.y);
        }

        public float MoveCost(Cell src, Cell dst) => 1;

        public IEnumerable<Cell> Nodes()
        {
            foreach (var cell in StrategicGameState.Instance.cellMatrix)
                yield return cell;
        }
    }

    public class DynamicCellGraphNavy : IGraphEnumerable<Cell>
    {
        public IEnumerable<Cell> Neighbors(Cell pos)
        {
            foreach (var nei in pos.GetNeighbors())
            {
                if (nei.IsNavyPassable())
                    yield return nei;
            }
        }

        public float EstimateCost(Cell src, Cell dst)
        {
            return Math.Abs(src.x - dst.x) + Math.Abs(src.y - dst.y);
        }

        public float MoveCost(Cell src, Cell dst) => 1;

        public IEnumerable<Cell> Nodes()
        {
            foreach (var cell in StrategicGameState.Instance.cellMatrix)
                yield return cell;
        }
    }
}