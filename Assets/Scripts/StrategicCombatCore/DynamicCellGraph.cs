using YYZ.PathFinding;
using System.Collections.Generic;
using System;
using System.Linq;
using NavalCombatCore;

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
        public SideState movingSide;
        public bool preventHostileControl;

        public IEnumerable<Cell> Neighbors(Cell pos)
        {
            foreach (var nei in pos.GetNeighbors())
            {
                if (IsPassable(nei))
                    yield return nei;
            }
        }

        public bool IsPassable(Cell cell)
        {
            return cell != null &&
                cell.IsArmyPassable() &&
                !IsHostileControlled(cell);
        }

        bool IsHostileControlled(Cell cell)
        {
            var cellSide = cell?.GetHexSide();
            return preventHostileControl &&
                movingSide != null &&
                cellSide != null &&
                cellSide != movingSide;
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
            if (src.GetHexSide() == side)
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

    public class DynamicCellGraphArmyWithNavalTransport : IGraphEnumerable<DynamicCellGraphArmyWithNavalTransport.Node>
    {
        public readonly struct Node : IEquatable<Node>
        {
            public readonly Cell cell;
            public readonly bool navalTransportState;

            public Node(Cell cell, bool navalTransportState)
            {
                this.cell = cell;
                this.navalTransportState = navalTransportState;
            }

            public bool Equals(Node other) => cell == other.cell && navalTransportState == other.navalTransportState;
            public override bool Equals(object obj) => obj is Node other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(cell, navalTransportState);
        }

        const float EmbarkCostHours = 72f;
        const float LandingCostHours = 24f;
        const float NavalTransportSpeedKnots = 6f;

        public SideState movingSide;
        public bool preventHostileControl;

        public IEnumerable<Node> Neighbors(Node pos)
        {
            if (pos.cell == null)
                yield break;

            if (pos.navalTransportState)
            {
                foreach (var nei in pos.cell.GetNeighbors())
                {
                    if (CanMoveByNavalTransport(pos.cell, nei))
                        yield return new Node(nei, true);
                }

                if (CanLandAt(pos.cell))
                    yield return new Node(pos.cell, false);
            }
            else
            {
                foreach (var nei in pos.cell.GetNeighbors())
                {
                    if (CanMoveByLand(nei))
                        yield return new Node(nei, false);
                }

                if (CanEmbarkAt(pos.cell))
                    yield return new Node(pos.cell, true);
            }
        }

        public float EstimateCost(Node src, Node dst) => 0f;

        public float MoveCost(Node src, Node dst)
        {
            if (src.cell == dst.cell && !src.navalTransportState && dst.navalTransportState)
                return EmbarkCostHours;
            if (src.cell == dst.cell && src.navalTransportState && !dst.navalTransportState)
                return LandingCostHours;

            var distanceKm = src.cell.GetDistanceUnsafe(dst.cell);
            if (src.navalTransportState && dst.navalTransportState)
                return distanceKm / (NavalTransportSpeedKnots * MeasureUtils.navalMileToKilometer);

            return distanceKm / StrategicGroup.GetSpeedKmPerHour(src.cell, dst.cell);
        }

        public IEnumerable<Node> Nodes()
        {
            var gameState = StrategicGameState.Instance;
            if (gameState.scenarioState.enableGridSystem)
            {
                foreach (var cell in gameState.cellMatrix)
                {
                    yield return new Node(cell, false);
                    yield return new Node(cell, true);
                }
            }
            if (gameState.scenarioState.enableAreaSystem)
            {
                foreach (var cell in gameState.areaCells)
                {
                    yield return new Node(cell, false);
                    yield return new Node(cell, true);
                }
            }
        }

        bool CanMoveByNavalTransport(Cell src, Cell dst)
        {
            return dst != null &&
                dst.IsNavyPassable() &&
                !src.HasEdgeFeatureTo(dst, EdgeFeatureType.BlockSeaMovement) &&
                !StrategicGroup.CellHasHostileFortifiedBaseFor(dst, movingSide);
        }

        bool CanEmbarkAt(Cell cell)
        {
            return cell != null &&
                cell.IsCoast &&
                cell.IsArmyPassable() &&
                !IsHostileControlled(cell) &&
                cell.StrategicGroupReferences
                    .Select(reference => reference.Get())
                    .Any(group => group != null && group.IsBase() && group.side == movingSide);
        }

        bool CanMoveByLand(Cell cell)
        {
            return cell != null &&
                cell.IsArmyPassable() &&
                !IsHostileControlled(cell);
        }

        bool CanLandAt(Cell cell)
        {
            return cell != null &&
                cell.IsCoast &&
                cell.IsArmyPassable() &&
                !IsHostileControlled(cell);
        }

        bool IsHostileControlled(Cell cell)
        {
            var cellSide = cell?.GetHexSide();
            return preventHostileControl &&
                movingSide != null &&
                cellSide != null &&
                cellSide != movingSide;
        }
    }
}
