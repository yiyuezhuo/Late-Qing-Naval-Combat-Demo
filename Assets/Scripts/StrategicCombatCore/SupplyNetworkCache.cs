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
        readonly Dictionary<SideState, Dictionary<Cell, LandUnit>> nearestFriendlyBaseDepotBySideCache = new();

        public LandUnit GetNearestFriendlyBaseDepot(StrategicGroup group)
        {
            var srcCell = group?.cell;
            var sideState = group?.side;
            if (srcCell == null || sideState == null)
                return null;

            var depotByCell = GetNearestFriendlyBaseDepotBySide(sideState);
            return depotByCell.GetValueOrDefault(srcCell);
        }

        public AStarResult<Cell> GetLandSupplyPath(SideState sideState, Cell srcCell, Cell dstCell)
        {
            if (sideState == null || srcCell == null || dstCell == null)
                return default;

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

        Dictionary<Cell, LandUnit> GetNearestFriendlyBaseDepotBySide(SideState sideState)
        {
            if (!nearestFriendlyBaseDepotBySideCache.TryGetValue(sideState, out var depotByCell))
            {
                depotByCell = BuildNearestFriendlyBaseDepotBySide(sideState);
                nearestFriendlyBaseDepotBySideCache[sideState] = depotByCell;
            }
            return depotByCell;
        }

        Dictionary<Cell, LandUnit> BuildNearestFriendlyBaseDepotBySide(SideState sideState)
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

                var state = new DepotPathState() { cost = 0, depot = depot };
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

                    bestStates[previousCell] = new DepotPathState() { cost = nextCost, depot = currentState.depot };
                    openSet.Add(new QueueNode(previousCell, nextCost, nextOrder++));
                }
            }

            return bestStates.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.depot);
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
    }
}
