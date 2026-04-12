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
        readonly Dictionary<SideState, RoutedSupplyNetworkCache> routedSupplyNetworkCacheBySide = new();

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

        public LandUnit GetRoutedSourceDepot(ISupplyNetworkNode node)
        {
            if (node?.side == null || node.cell == null)
                return null;

            return GetRoutedSupplyNetworkCache(node.side).GetSourceDepot(node);
        }

        public bool TryGetRoutedSupplyPath(ISupplyNetworkNode requestUnit, LandUnit requestedDepot, out AStarResult<Cell> result)
        {
            result = default;
            if (requestUnit?.side == null || requestUnit.cell == null || requestedDepot?.cell == null)
                return false;

            return GetRoutedSupplyNetworkCache(requestUnit.side).TryGetPathResult(requestUnit, requestedDepot, out result);
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

        RoutedSupplyNetworkCache GetRoutedSupplyNetworkCache(SideState sideState)
        {
            if (!routedSupplyNetworkCacheBySide.TryGetValue(sideState, out var routeCache))
            {
                routeCache = BuildRoutedSupplyNetworkCache(sideState);
                routedSupplyNetworkCacheBySide[sideState] = routeCache;
            }
            return routeCache;
        }

        RoutedSupplyNetworkCache BuildRoutedSupplyNetworkCache(SideState sideState)
        {
            return new RoutedSupplyNetworkCache(sideState, GetFriendlyBaseGroups(sideState));
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

        enum SupplySourceKind
        {
            Land,
            Coast
        }

        class RoutedSupplyNetworkCache
        {
            readonly SideState sideState;
            readonly ReverseDynamicLandSupplyNetworkingGraph reverseGraph;
            readonly Dictionary<Cell, List<LandUnit>> depotsByCell = new();
            readonly Dictionary<Cell, RoutedCellState> cellStates = new();
            readonly Dictionary<LandUnit, RoutedDepotState> depotStates = new();
            readonly Dictionary<LandUnit, SupplySourceKind> rootDepotKinds = new();
            readonly SortedSet<RoutedQueueNode> openSet = new(new RoutedQueueNodeComparer());
            long nextOrder;

            public RoutedSupplyNetworkCache(SideState sideState, IEnumerable<StrategicGroup> baseGroups)
            {
                this.sideState = sideState;
                reverseGraph = new ReverseDynamicLandSupplyNetworkingGraph() { side = sideState };
                BuildDepotIndex(baseGroups);
                BuildRoutes();
            }

            public LandUnit GetSourceDepot(ISupplyNetworkNode node)
            {
                if (node is LandUnit landUnit && IsBaseDepot(landUnit))
                {
                    return depotStates.TryGetValue(landUnit, out var depotState)
                        ? depotState.upstreamDepot
                        : null;
                }

                return cellStates.TryGetValue(node.cell, out var cellState)
                    ? cellState.depot
                    : null;
            }

            public bool TryGetPathResult(ISupplyNetworkNode requestUnit, LandUnit requestedDepot, out AStarResult<Cell> result)
            {
                result = default;
                if (requestUnit?.cell == null || requestedDepot?.cell == null)
                    return false;

                if (requestUnit.cell == requestedDepot.cell)
                {
                    result = new AStarResult<Cell>()
                    {
                        Cost = 0f,
                        Path = new List<Cell>() { requestUnit.cell }
                    };
                    return true;
                }

                if (requestUnit is LandUnit requestDepot && IsBaseDepot(requestDepot))
                {
                    if (!depotStates.TryGetValue(requestDepot, out var depotState) ||
                        depotState.upstreamDepot != requestedDepot)
                    {
                        return false;
                    }

                    result = new AStarResult<Cell>()
                    {
                        Cost = depotState.cost,
                        Path = new List<Cell>(depotState.pathToUpstreamDepot)
                    };
                    return true;
                }

                if (!cellStates.TryGetValue(requestUnit.cell, out var cellState) ||
                    cellState.depot != requestedDepot)
                {
                    return false;
                }

                return TryBuildCellPathResult(requestUnit.cell, requestedDepot.cell, cellState.cost, out result);
            }

            void BuildDepotIndex(IEnumerable<StrategicGroup> baseGroups)
            {
                foreach (var baseGroup in baseGroups ?? Enumerable.Empty<StrategicGroup>())
                {
                    var depot = baseGroup?.GetFirstDepot();
                    var cell = baseGroup?.cell;
                    if (depot == null || cell == null)
                        continue;

                    if (!depotsByCell.TryGetValue(cell, out var depots))
                    {
                        depots = new List<LandUnit>();
                        depotsByCell[cell] = depots;
                    }

                    if (!depots.Contains(depot))
                        depots.Add(depot);
                }
            }

            void BuildRoutes()
            {
                var landSeeds = depotsByCell.Values
                    .SelectMany(depots => depots)
                    .Where(IsLandSourceDepot)
                    .ToList();
                RunPass(SupplySourceKind.Land, landSeeds);

                var hasCoastDepot = depotsByCell.Values
                    .SelectMany(depots => depots)
                    .Any(IsCoastDepot);
                if (!hasCoastDepot)
                    return;

                var coastSeeds = depotsByCell.Values
                    .SelectMany(depots => depots)
                    .Where(depot => IsCoastDepot(depot) && !HasLandRoute(depot))
                    .ToList();
                RunPass(SupplySourceKind.Coast, coastSeeds);
            }

            void RunPass(SupplySourceKind kind, List<LandUnit> seedDepots)
            {
                openSet.Clear();
                foreach (var depot in seedDepots)
                {
                    if (depot?.cell == null)
                        continue;

                    if (kind == SupplySourceKind.Coast && HasLandRoute(depot))
                        continue;

                    rootDepotKinds.TryAdd(depot, kind);
                    AddOpenNode(depot.cell, depot, 0f, null, kind);
                }

                while (openSet.Count > 0)
                {
                    var currentNode = openSet.Min;
                    openSet.Remove(currentNode);

                    if (!ShouldProcessNode(currentNode))
                        continue;

                    cellStates[currentNode.cell] = new RoutedCellState()
                    {
                        cost = currentNode.cost,
                        depot = currentNode.sourceDepot,
                        depotCell = currentNode.sourceDepot.cell,
                        nextCellTowardDepot = currentNode.previous?.cell,
                        sourceKind = currentNode.sourceKind
                    };

                    if (TryResetAtNewDepot(currentNode))
                        continue;

                    AddNeighborNodes(currentNode);
                }
            }

            bool ShouldProcessNode(RoutedQueueNode node)
            {
                if (node?.cell == null || node.sourceDepot?.cell == null)
                    return false;

                if (!cellStates.TryGetValue(node.cell, out var currentState))
                    return true;

                if (currentState.sourceKind == SupplySourceKind.Land && node.sourceKind == SupplySourceKind.Coast)
                    return false;

                if (currentState.sourceKind == node.sourceKind && currentState.cost <= node.cost)
                    return false;

                return currentState.sourceKind != SupplySourceKind.Land || node.sourceKind == SupplySourceKind.Land;
            }

            bool TryResetAtNewDepot(RoutedQueueNode node)
            {
                if (!depotsByCell.TryGetValue(node.cell, out var depots))
                    return false;

                var resetAny = false;
                foreach (var depot in depots)
                {
                    if (depot == node.sourceDepot)
                        continue;

                    if (HasRouteForPass(depot, node.sourceKind))
                        continue;

                    depotStates[depot] = new RoutedDepotState()
                    {
                        upstreamDepot = node.sourceDepot,
                        cost = node.cost,
                        pathToUpstreamDepot = BuildPathToSource(node),
                        sourceKind = node.sourceKind
                    };

                    AddOpenNode(node.cell, depot, 0f, null, node.sourceKind);
                    resetAny = true;
                }

                return resetAny;
            }

            void AddNeighborNodes(RoutedQueueNode currentNode)
            {
                foreach (var previousCell in reverseGraph.Neighbors(currentNode.cell))
                {
                    var nextCost = currentNode.cost + reverseGraph.MoveCost(currentNode.cell, previousCell);
                    AddOpenNode(previousCell, currentNode.sourceDepot, nextCost, currentNode, currentNode.sourceKind);
                }
            }

            void AddOpenNode(Cell cell, LandUnit sourceDepot, float cost, RoutedQueueNode previous, SupplySourceKind sourceKind)
            {
                if (cellStates.TryGetValue(cell, out var currentState))
                {
                    if (currentState.sourceKind == SupplySourceKind.Land && sourceKind == SupplySourceKind.Coast)
                        return;

                    if (currentState.sourceKind == sourceKind && currentState.cost <= cost)
                        return;
                }

                openSet.Add(new RoutedQueueNode(cell, sourceDepot, cost, nextOrder++, previous, sourceKind));
            }

            bool TryBuildCellPathResult(Cell srcCell, Cell dstCell, float cost, out AStarResult<Cell> result)
            {
                result = default;
                var path = new List<Cell>();
                var currentCell = srcCell;
                var maxSteps = cellStates.Count + 1;

                for (var i = 0; i < maxSteps && currentCell != null; i++)
                {
                    path.Add(currentCell);
                    if (currentCell == dstCell)
                    {
                        result = new AStarResult<Cell>()
                        {
                            Cost = cost,
                            Path = path
                        };
                        return true;
                    }

                    if (!cellStates.TryGetValue(currentCell, out var state))
                        return false;

                    currentCell = state.nextCellTowardDepot;
                }

                return false;
            }

            static List<Cell> BuildPathToSource(RoutedQueueNode node)
            {
                var path = new List<Cell>();
                while (node != null)
                {
                    path.Add(node.cell);
                    node = node.previous;
                }
                return path;
            }

            bool HasRouteForPass(LandUnit depot, SupplySourceKind kind)
            {
                if (kind == SupplySourceKind.Coast && HasLandRoute(depot))
                    return true;

                if (rootDepotKinds.TryGetValue(depot, out var rootKind))
                    return rootKind == kind || rootKind == SupplySourceKind.Land;

                if (depotStates.TryGetValue(depot, out var depotState))
                    return depotState.sourceKind == kind || depotState.sourceKind == SupplySourceKind.Land;

                return false;
            }

            bool HasLandRoute(LandUnit depot)
            {
                if (rootDepotKinds.TryGetValue(depot, out var rootKind) && rootKind == SupplySourceKind.Land)
                    return true;

                return depotStates.TryGetValue(depot, out var depotState) &&
                    depotState.sourceKind == SupplySourceKind.Land;
            }

            static bool IsLandSourceDepot(LandUnit depot)
            {
                return IsBaseDepot(depot) && depot.supplyGeneratedTons > 0;
            }

            static bool IsCoastDepot(LandUnit depot)
            {
                return IsBaseDepot(depot) && depot.cell?.IsCoast == true;
            }

            static bool IsBaseDepot(LandUnit depot)
            {
                var parentGroup = depot?.parentGroupReference.Get();
                return parentGroup?.type == StrategicGroup.Type.Base &&
                    parentGroup.GetFirstDepot() == depot;
            }
        }

        class RoutedCellState
        {
            public float cost;
            public LandUnit depot;
            public Cell depotCell;
            public Cell nextCellTowardDepot;
            public SupplySourceKind sourceKind;
        }

        class RoutedDepotState
        {
            public LandUnit upstreamDepot;
            public float cost;
            public List<Cell> pathToUpstreamDepot = new();
            public SupplySourceKind sourceKind;
        }

        class RoutedQueueNode
        {
            public RoutedQueueNode(
                Cell cell,
                LandUnit sourceDepot,
                float cost,
                long order,
                RoutedQueueNode previous,
                SupplySourceKind sourceKind)
            {
                this.cell = cell;
                this.sourceDepot = sourceDepot;
                this.cost = cost;
                this.order = order;
                this.previous = previous;
                this.sourceKind = sourceKind;
            }

            public Cell cell;
            public LandUnit sourceDepot;
            public float cost;
            public long order;
            public RoutedQueueNode previous;
            public SupplySourceKind sourceKind;
        }

        class RoutedQueueNodeComparer : IComparer<RoutedQueueNode>
        {
            public int Compare(RoutedQueueNode x, RoutedQueueNode y)
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
