using System.Collections.Generic;
using System.Linq;
using NavalCombatCore;
using YYZ.PathFinding;

namespace StrategicCombatCore
{
    public class SupplyNetworkCache
    {
        readonly Dictionary<(SideState side, Cell src, Cell dst), AStarResult<Cell>> landSupplyPathfindingCache = new();
        readonly Dictionary<SideState, List<StrategicGroup>> friendlyBaseGroupsBySideCache = new();
        readonly Dictionary<SideState, NearestFriendlyBaseDepotCache> nearestFriendlyBaseDepotCacheBySide = new();

        public LandUnit GetNearestFriendlyBaseDepot(StrategicGroup group)
        {
            var srcCell = group?.cell;
            var sideState = group?.side;
            if (srcCell == null || sideState == null)
                return null;

            return GetNearestFriendlyBaseDepotCache(sideState).GetDepot(srcCell);
        }

        public AStarResult<Cell> GetLandSupplyPath(SideState sideState, Cell srcCell, Cell dstCell)
        {
            if (sideState == null || srcCell == null || dstCell == null)
                return BuildFailedPathResult();

            if (srcCell == dstCell)
            {
                return new AStarResult<Cell>()
                {
                    Cost = 0f,
                    Path = new List<Cell>() { srcCell }
                };
            }

            if (TryGetPathToNearestFriendlyBaseDepot(sideState, srcCell, dstCell, out var nearestDepotPathResult))
                return nearestDepotPathResult;

            var graph = new DynamicLandSupplyNetworkingGraph() { side = sideState };
            return GetLandSupplyPath(graph, sideState, srcCell, dstCell);
        }

        AStarResult<Cell> GetLandSupplyPath(DynamicLandSupplyNetworkingGraph graph, SideState sideState, Cell srcCell, Cell dstCell)
        {
            var key = (sideState, srcCell, dstCell);
            if (!landSupplyPathfindingCache.TryGetValue(key, out var result))
            {
                landSupplyPathfindingCache[key] = result = PathFinding<Cell>.AStar3(graph, srcCell, dstCell);
            }
            return result;
        }

        public bool TryGetPathToNearestFriendlyBaseDepot(SideState sideState, Cell srcCell, Cell dstCell, out AStarResult<Cell> result)
        {
            result = default;
            if (sideState == null || srcCell == null || dstCell == null)
                return false;

            var cache = GetNearestFriendlyBaseDepotCache(sideState);
            if (!cache.TryGetPathResult(srcCell, dstCell, out result))
                return false;

            return true;
        }

        NearestFriendlyBaseDepotCache GetNearestFriendlyBaseDepotCache(SideState sideState)
        {
            if (!nearestFriendlyBaseDepotCacheBySide.TryGetValue(sideState, out var depotCache))
            {
                depotCache = BuildNearestFriendlyBaseDepotCache(sideState);
                nearestFriendlyBaseDepotCacheBySide[sideState] = depotCache;
            }
            return depotCache;
        }

        NearestFriendlyBaseDepotCache BuildNearestFriendlyBaseDepotCache(SideState sideState)
        {
            var reverseGraph = new ReverseDynamicLandSupplyNetworkingGraph() { side = sideState };
            var bestStates = new Dictionary<Cell, DepotPathState>();
            var openSet = new SortedSet<QueueNode>(new QueueNodeComparer());
            var nextOrder = 0L;

            foreach (var baseGroup in GetFriendlyBaseGroups(sideState))
            {
                var depot = baseGroup.GetFirstDepot();
                var cell = baseGroup.cell;
                if (depot == null || cell == null)
                    continue;

                var state = new DepotPathState()
                {
                    cost = 0,
                    depot = depot,
                    depotCell = cell,
                    nextCellTowardDepot = null
                };
                if (bestStates.TryGetValue(cell, out var currentBest) && currentBest.cost <= 0)
                    continue;

                bestStates[cell] = state;
                openSet.Add(new QueueNode(cell, 0, nextOrder++));
            }

            while (openSet.Count > 0)
            {
                var currentNode = openSet.Min;
                openSet.Remove(currentNode);

                var currentCell = currentNode.cell;
                var currentState = bestStates[currentCell];
                if (currentNode.cost > currentState.cost)
                    continue;

                foreach (var previousCell in reverseGraph.Neighbors(currentCell))
                {
                    var nextCost = currentState.cost + reverseGraph.MoveCost(currentCell, previousCell);
                    if (bestStates.TryGetValue(previousCell, out var previousBest) && previousBest.cost <= nextCost)
                        continue;

                    bestStates[previousCell] = new DepotPathState()
                    {
                        cost = nextCost,
                        depot = currentState.depot,
                        depotCell = currentState.depotCell,
                        nextCellTowardDepot = currentCell
                    };
                    openSet.Add(new QueueNode(previousCell, nextCost, nextOrder++));
                }
            }

            return new NearestFriendlyBaseDepotCache(bestStates);
        }

        List<StrategicGroup> GetFriendlyBaseGroups(SideState sideState)
        {
            if (!friendlyBaseGroupsBySideCache.TryGetValue(sideState, out var baseGroups))
            {
                baseGroups = StrategicGameState.Instance.strategicGroups
                    .Where(group => group != null && group.type == StrategicGroup.Type.Base && group.side == sideState)
                    .ToList();
                friendlyBaseGroupsBySideCache[sideState] = baseGroups;
            }
            return baseGroups;
        }

        class ReverseDynamicLandSupplyNetworkingGraph
        {
            public SideState side;
            readonly DynamicLandSupplyNetworkingGraph forwardGraph = new();

            public IEnumerable<Cell> Neighbors(Cell pos)
            {
                foreach (var neighbor in pos.GetNeighbors())
                {
                    if (forwardGraph.side != side)
                        forwardGraph.side = side;

                    if (neighbor.IsArmyPassable() && forwardGraph.Neighbors(neighbor).Contains(pos))
                        yield return neighbor;
                }
            }

            public float MoveCost(Cell src, Cell dst)
            {
                if (forwardGraph.side != side)
                    forwardGraph.side = side;
                return forwardGraph.MoveCost(dst, src);
            }
        }

        class DepotPathState
        {
            public float cost;
            public LandUnit depot;
            public Cell depotCell;
            public Cell nextCellTowardDepot;
        }

        class NearestFriendlyBaseDepotCache
        {
            readonly Dictionary<Cell, DepotPathState> bestStates;

            public NearestFriendlyBaseDepotCache(Dictionary<Cell, DepotPathState> bestStates)
            {
                this.bestStates = bestStates ?? new Dictionary<Cell, DepotPathState>();
            }

            public LandUnit GetDepot(Cell srcCell)
            {
                if (srcCell == null)
                    return null;

                return bestStates.TryGetValue(srcCell, out var state)
                    ? state.depot
                    : null;
            }

            public bool TryGetPathResult(Cell srcCell, Cell dstCell, out AStarResult<Cell> result)
            {
                result = default;
                if (srcCell == null || dstCell == null)
                    return false;
                if (!bestStates.TryGetValue(srcCell, out var srcState))
                    return false;
                if (srcState.depotCell != dstCell)
                    return false;

                var path = new List<Cell>();
                var currentCell = srcCell;
                while (currentCell != null)
                {
                    path.Add(currentCell);
                    if (currentCell == dstCell)
                    {
                        result = new AStarResult<Cell>()
                        {
                            Cost = srcState.cost,
                            Path = path
                        };
                        return true;
                    }

                    if (!bestStates.TryGetValue(currentCell, out var currentState))
                        return false;

                    currentCell = currentState.nextCellTowardDepot;
                }

                return false;
            }
        }

        class QueueNode
        {
            public QueueNode(Cell cell, float cost, long order)
            {
                this.cell = cell;
                this.cost = cost;
                this.order = order;
            }

            public Cell cell;
            public float cost;
            public long order;
        }

        class QueueNodeComparer : IComparer<QueueNode>
        {
            public int Compare(QueueNode x, QueueNode y)
            {
                if (ReferenceEquals(x, y))
                    return 0;
                if (x is null)
                    return -1;
                if (y is null)
                    return 1;

                var costCmp = x.cost.CompareTo(y.cost);
                if (costCmp != 0)
                    return costCmp;

                return x.order.CompareTo(y.order);
            }
        }

        static AStarResult<Cell> BuildFailedPathResult()
        {
            return new AStarResult<Cell>()
            {
                Cost = float.PositiveInfinity,
                Path = new List<Cell>()
            };
        }
    }
}
