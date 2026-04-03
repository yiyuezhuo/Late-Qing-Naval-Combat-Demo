using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Data;

namespace YYZ.PathFinding
{

    public interface INodeEnumerable<IndexT>
    {
        IEnumerable<IndexT> Nodes();
    }

    public interface IGeneralGraph<IndexT>
    {
        /// <summary>
        /// src and dst are expected to be neighbor.
        /// </summary>
        float MoveCost(IndexT src, IndexT dst);

        IEnumerable<IndexT> Neighbors(IndexT pos);
    }

    public interface IGraph<IndexT> : IGeneralGraph<IndexT>
    {
        /// <summary>
        /// A heuristic comes from Euclidean space or something like that.
        /// </summary>
        float EstimateCost(IndexT src, IndexT dst);
    }

    public interface IGeneralGraphEnumerable<IndexT> : IGeneralGraph<IndexT>, INodeEnumerable<IndexT>
    {
    }

    public interface IGraphEnumerable<IndexT> : IGraph<IndexT>, INodeEnumerable<IndexT>, IGeneralGraphEnumerable<IndexT>
    {
    }


    public static class PathFinding<IndexT>
    {
        class DijkstraQueueNode
        {
            public IndexT node;
            public float cost;
            public long order;
        }

        class DijkstraQueueNodeComparer : IComparer<DijkstraQueueNode>
        {
            public int Compare(DijkstraQueueNode x, DijkstraQueueNode y)
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

        class AStarQueueNode
        {
            public IndexT node;
            public float fScore;
            public long order;
        }

        class AStarQueueNodeComparer : IComparer<AStarQueueNode>
        {
            public int Compare(AStarQueueNode x, AStarQueueNode y)
            {
                if (ReferenceEquals(x, y))
                    return 0;
                if (x is null)
                    return -1;
                if (y is null)
                    return 1;

                var scoreCmp = x.fScore.CompareTo(y.fScore);
                if (scoreCmp != 0)
                    return scoreCmp;

                return x.order.CompareTo(y.order);
            }
        }

        static List<IndexT> ReconstructPath(Dictionary<IndexT, IndexT> cameFrom, IndexT current)
        {
            var total_path = new List<IndexT> { current };
            while (cameFrom.ContainsKey(current))
            {
                current = cameFrom[current];
                total_path.Add(current);
            }
            total_path.Reverse();
            return total_path;
        }

        static float TryGet(Dictionary<IndexT, float> dict, IndexT key)
        {
            /*
            if (dict.ContainsKey(key))
            {
                return dict[key];
            }
            return float.PositiveInfinity;
            */
            if(dict.TryGetValue(key, out var value))
                return value;
            return float.PositiveInfinity;
        }

        /// <summary>
        /// Path finding using A* algorithm, if failed it will return a empty list.
        /// </summary>
        public static List<IndexT> AStar(IGraph<IndexT> graph, IndexT src, IndexT dst)
        {
            AStar2(graph, src, dst, out var path);
            return path;
        }

        public static float AStar2(IGraph<IndexT> graph, IndexT src, IndexT dst, out List<IndexT> path)
        {
            var cameFrom = new Dictionary<IndexT, IndexT>();
            var gScore = new Dictionary<IndexT, float> { { src, 0 } };
            var fScore = new Dictionary<IndexT, float> { { src, graph.EstimateCost(src, dst) } };
            var openQueue = new SortedSet<AStarQueueNode>(new AStarQueueNodeComparer());
            long nextOrder = 0;

            openQueue.Add(new AStarQueueNode
            {
                node = src,
                fScore = fScore[src],
                order = nextOrder++
            });

            while (openQueue.Count > 0)
            {
                var currentNode = openQueue.Min;
                openQueue.Remove(currentNode);

                var current = currentNode.node;
                if (currentNode.fScore != TryGet(fScore, current))
                    continue;

                if (EqualityComparer<IndexT>.Default.Equals(current, dst))
                {
                    path = ReconstructPath(cameFrom, current);
                    return TryGet(gScore, current);
                }

                foreach (IndexT neighbor in graph.Neighbors(current)) // TODO: Switch to graph.NeighborsWithMoveCost API to prevent hash calculation 
                {
                    float tentative_gScore = TryGet(gScore, current) + graph.MoveCost(current, neighbor);
                    if (tentative_gScore < TryGet(gScore, neighbor))
                    {
                        cameFrom[neighbor] = current;
                        gScore[neighbor] = tentative_gScore;
                        var neighborFScore = tentative_gScore + graph.EstimateCost(neighbor, dst);
                        fScore[neighbor] = neighborFScore;

                        openQueue.Add(new AStarQueueNode
                        {
                            node = neighbor,
                            fScore = neighborFScore,
                            order = nextOrder++
                        });
                    }
                }
            }
            path = new List<IndexT>();
            return float.PositiveInfinity;
            // return new List<IndexT>(); // failure
        }

        public static AStarResult<IndexT> AStar3(IGraph<IndexT> graph, IndexT src, IndexT dst)
        {
            var cost = AStar2(graph, src, dst, out var path);
            return new AStarResult<IndexT>(){Cost=cost, Path=path};
        }


        public static DijkstraResult<IndexT> Dijkstra(IGeneralGraph<IndexT> graph, IEnumerable<IndexT> srcIter, Func<IndexT, bool> Predicate, float budget)
        {
            var ret = new DijkstraResult<IndexT>();

            var nodeToPath = ret.nodeToPath = new Dictionary<IndexT, Path<IndexT>>();
            var queue = new SortedSet<DijkstraQueueNode>(new DijkstraQueueNodeComparer());
            long nextOrder = 0;

            foreach (var src in srcIter ?? Enumerable.Empty<IndexT>())
            {
                if (nodeToPath.TryGetValue(src, out var currentBest) && currentBest.cost <= 0f)
                    continue;

                nodeToPath[src] = new Path<IndexT>() { cost = 0f };
                queue.Add(new DijkstraQueueNode
                {
                    node = src,
                    cost = 0f,
                    order = nextOrder++
                });
            }

            while (queue.Count > 0)
            {
                var current = queue.Min;
                queue.Remove(current);

                if (!nodeToPath.TryGetValue(current.node, out var currentPath) || current.cost != currentPath.cost)
                    continue;

                if (Predicate(current.node))
                {
                    ret.pickedNode = current.node;
                    break;
                }

                if (budget - current.cost <= 0) // We allows the value to be negative, but it will not be allowed to "propagate".
                    continue;

                foreach (var nei in graph.Neighbors(current.node))
                {
                    var nextCost = current.cost + graph.MoveCost(current.node, nei);
                    if (nodeToPath.TryGetValue(nei, out var bestPath) && bestPath.cost <= nextCost)
                        continue;

                    nodeToPath[nei] = new Path<IndexT>()
                    {
                        cost = nextCost,
                        prev = current.node
                    };
                    queue.Add(new DijkstraQueueNode
                    {
                        node = nei,
                        cost = nextCost,
                        order = nextOrder++
                    });
                }
            }
            return ret;
        }

        public static DijkstraResult<IndexT> GetReachable(IGeneralGraph<IndexT> graph, IndexT src, float budget)
        {
            var srcIter = new IndexT[] { src };
            return Dijkstra(graph, srcIter, DummyFalsePredicate, budget);
        }

        public static bool DummyFalsePredicate(IndexT node) => false;

        public static List<IndexT> ExploreNearestTarget(IGeneralGraph<IndexT> graph, IndexT src, Func<IndexT, bool> Predicate)
        {
            var srcIter = new IndexT[] { src };
            var result = Dijkstra(graph, srcIter, Predicate, float.PositiveInfinity);
            return result.Reconstruct(result.pickedNode);
        }


        public class PathComparer : IComparer<IndexT>
        {
            Dictionary<IndexT, Path<IndexT>> nodeToPath;
            public PathComparer(Dictionary<IndexT, Path<IndexT>> nodeToPath)
            {
                this.nodeToPath = nodeToPath;
            }
            public int Compare(IndexT x, IndexT y)
            {
                return nodeToPath[x].cost.CompareTo(nodeToPath[y].cost);
            }
        }

        /// <summary>
        /// ccw: ccw > 0 if three points make a counter-clockwise turn, clockwise if ccw < 0, and collinear if ccw = 0.
        /// </summary>
        static float ccw(System.Numerics.Vector2 p1, System.Numerics.Vector2 p2, System.Numerics.Vector2 p3)
        {
            var p1p2 = p2 - p1;
            var p1p3 = p3 - p1;
            return p1p2.X * p1p3.Y - p1p2.Y * p1p3.X;
        }

        static float Dot(System.Numerics.Vector2 a, System.Numerics.Vector2 b)
        {
            return a.X * b.X + a.Y * b.Y;
        }

        static float CosAngle(System.Numerics.Vector2 a, System.Numerics.Vector2 b)
        {
            var p = Dot(a, b);
            var angle = Dot(a, b) / Math.Sqrt(Dot(a, a)) / Math.Sqrt(Dot(b, b));
            return (float)angle;
        }

        class GramScanComparer : IComparer<System.Numerics.Vector2>
        {
            Dictionary<System.Numerics.Vector2, float> angleMap = new Dictionary<System.Numerics.Vector2, float>();
            System.Numerics.Vector2 p0;

            public GramScanComparer(IEnumerable<System.Numerics.Vector2> points, System.Numerics.Vector2 p0)
            {
                foreach (var p in points)
                    angleMap[p] = CosAngle(p0, p);
                this.p0 = p0;
            }
            public int Compare(System.Numerics.Vector2 a, System.Numerics.Vector2 b)
            {
                var ret = angleMap[a].CompareTo(angleMap[b]);
                if (ret == 0)
                    return Dot(a - p0, a - p0).CompareTo(Dot(b - p0, b - p0));
                return ret;
            }
        }

        public static Stack<System.Numerics.Vector2> GrahamScan(IEnumerable<System.Numerics.Vector2> pointIter)
        {
            // https://en.wikipedia.org/wiki/Graham_scan

            var p0 = new System.Numerics.Vector2(float.PositiveInfinity, float.PositiveInfinity);
            foreach (var point in pointIter)
                if (point.Y < p0.Y || (point.Y == p0.Y && point.X < p0.X))
                    p0 = point;

            var points = pointIter.ToList();
            points.Sort(new GramScanComparer(points, p0));

            var stackWithoutTop = new Stack<System.Numerics.Vector2>(); // We don't include top in the stack, since it's not convenient to peek second element of this stack implementation.
            var top = points.First();

            foreach (var point in points.Skip(1))
            {
                while (stackWithoutTop.Count >= 1 && ccw(stackWithoutTop.Peek(), top, point) <= 0)
                    top = stackWithoutTop.Pop();

                stackWithoutTop.Push(top);
                top = point;
            }

            stackWithoutTop.Push(top); // The stack is with top from now.
            var stack = stackWithoutTop;

            return stack;
        }

        public class RegionConvexHull
        {
            public List<IndexT> boundaries;
            public HashSet<IndexT> boundariesSet;
            Func<IndexT, System.Numerics.Vector2> CenterFor;

            public RegionConvexHull(List<IndexT> boundaries, HashSet<IndexT> boundariesSet, Func<IndexT, System.Numerics.Vector2> CenterFor)
            {
                this.boundaries = boundaries;
                this.boundariesSet = boundariesSet;
                this.CenterFor = CenterFor;
            }

            public bool IsInside(IndexT region) // Need checks
            {
                if (boundaries.Count <= 1)
                    return region.Equals(boundaries[0]);

                var tail = boundaries.First();
                foreach (var head in boundaries.Skip(1))
                    if (ccw(CenterFor(tail), CenterFor(head), CenterFor(region)) <= 0)
                        return false;
                return true;
            }

        }

        public static RegionConvexHull RegionConvexHullFor(IGraph<IndexT> graph, IEnumerable<IndexT> regions, Func<IndexT, System.Numerics.Vector2> CenterFor)
        {
            var center2region = new Dictionary<System.Numerics.Vector2, IndexT>();
            foreach (var region in regions)
                center2region[CenterFor(region)] = region;

            var stack = GrahamScan(center2region.Keys);
            var src = center2region[stack.Pop()];
            var boundariesSet = new HashSet<IndexT>() { src };
            var boundaries = new List<IndexT>() { src };

            while (stack.Count > 0)
            {
                var dst = center2region[stack.Pop()];
                foreach (var region in AStar(graph, src, dst))
                    if (!boundariesSet.Contains(region))
                    {
                        boundariesSet.Add(region);
                        boundaries.Add(region);
                    }

                src = dst;
            }

            boundaries.Reverse();

            return new RegionConvexHull(boundaries, boundariesSet, CenterFor);
        }


        public static List<IndexT> RegionConvexHullWrapper(IGraph<IndexT> graph, IEnumerable<IndexT> regions, Func<IndexT, System.Numerics.Vector2> CenterFor)
        {
            var hull = RegionConvexHullFor(graph, regions, CenterFor);
            var set = new HashSet<IndexT>();
            foreach (var b in hull.boundaries)
                foreach (var region in graph.Neighbors(b))
                    if (!hull.boundariesSet.Contains(region) && !hull.IsInside(region))
                        set.Add(region);

            /*
            if(set.Count == 0)
                System.Console.WriteLine("WTF");
            */

            return set.ToList();
        }
    }

    public interface IPath<IndexT>
    {
        public IndexT prev{get;}
        public float cost{get;}
    }

    public class Path<IndexT>: IPath<IndexT>
    {
        public IndexT prev{get;set;}
        public float cost{get;set;}

        public override string ToString() => $"Path({prev}, {cost})";
    }


    public class DijkstraResult<IndexT> // TODO: Move it outside of the static class
    {
        public Dictionary<IndexT, Path<IndexT>> nodeToPath;
        public IndexT pickedNode;

        public List<IndexT> Reconstruct(IndexT node)
        {
            var p = node;
            var ret = new List<IndexT>();
            while (p != null)
            {
                ret.Add(p);
                p = nodeToPath[p].prev;
            }
            ret.Reverse();
            return ret;
        }

        public float Cost(IndexT node) => nodeToPath[node].cost;
    }


    public class AStarResult<IndexT> // src -> node_1 -> ... -> dst
    {
        public float Cost;
        public List<IndexT> Path;

        public AStarResult<IndexT> Reverse()
        {
            var path = new List<IndexT>(Path);
            path.Reverse();
            return new AStarResult<IndexT>(){Cost=Cost, Path=path};
        }
    }

    // Frozen and its family
    
    public interface IAStarOutput<T>: IEnumerable<T>
    {
        float Cost{get;}
    }

    public interface IDijkstraOutput<T>: IEnumerable<KeyValuePair<T, IPath<T>>>
    {
        bool TryGetValue(T key, out IPath<T> ret);
    }

    public class AStarOutput<T>: IAStarOutput<T>
    {
        public AStarResult<T> Result;

        public float Cost{get=>Result.Cost;}
        public IEnumerator<T> GetEnumerator() => Result.Path.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
    }

    public class AStarOutputTransformed<IT, ET>: IAStarOutput<ET> // TODO: Refactor to use 
    {
        public AStarResult<IT> Result;
        public Func<IT, ET> Transform;

        public float Cost{get => Result.Cost;}
        public IEnumerator<ET> GetEnumerator() => Result.Path.Select(Transform).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
    }

    public class DijkstraOutput<T>: IDijkstraOutput<T>
    {
        public DijkstraResult<T> Result;

        public bool TryGetValue(T key, out IPath<T> ret)
        {
            // return Result.nodeToPath.TryGetValue(key, out ret);
            
            var b = Result.nodeToPath.TryGetValue(key, out var ret2);
            ret = ret2;
            return b;
            
        }

        public IEnumerator<KeyValuePair<T, IPath<T>>> GetEnumerator() => Result.nodeToPath.Select(KV => new KeyValuePair<T, IPath<T>>(KV.Key, KV.Value)).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
    }

    public class DijkstraOutputTransformed<IT, ET>: IDijkstraOutput<ET>
    {
        public DijkstraResult<IT> Result;
        public Func<IT, ET> I2E;
        public Func<ET, IT> E2I;

        public bool TryGetValue(ET key, out IPath<ET> value)
        {
            var ret = Result.nodeToPath.TryGetValue(E2I(key), out var p);
            value = AsET(p);
            return ret;
        }

        Path<ET> AsET(Path<IT> p) => p == null ? null : new Path<ET>(){cost=p.cost, prev=I2E(p.prev)};

        public IEnumerator<KeyValuePair<ET, IPath<ET>>> GetEnumerator() => Result.nodeToPath.Select(KV => new KeyValuePair<ET, IPath<ET>>(I2E(KV.Key), AsET(KV.Value))).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
    }


    // public class AStarOutputTransformed<ET, IT>

    /*
    public class AStarOutputTransformed<ET, IT>: IAStarOutput<ET>
    {
        public AStarOutput<IT> Output;
        // public Func<ET, IT> E2I;
        public Func<IT, ET> I2E;

        public float Cost{get=>Output.Cost;}

        public class AStarEnumerator: IEnumerator
        {
            public IEnumerator<IT> enumerator;
            // public Func<ET, IT> E2I;
            public Func<IT, ET> I2E;

            object IEnumerator.Current{get=>this.Current}
            public ET Current{get => I2E(enumerator.Current);}
            public bool MoveNext() => enumerator.MoveNext();
            public void Reset() => enumerator.Reset();
        }

        public IEnumerator<T> GetEnumerator() => new AStarEnumerator(){enumerator=Output.GetEnumerator()};
        */
    
    

    public interface IPathFinder<T>
    {
        IAStarOutput<T> AStar(T src, T dst);
        // IDijkstraOutput<T> Dijkstra(IEnumerable<T> srcIter, Func<T, bool> Predicate, float budget);

        IDijkstraOutput<T> GetReachable(T src, float budget);
        IDijkstraOutput<T> GetShortpathForMultiple(T src, IEnumerable<T> allTargets);

        /*
        public IDijkstraOutput<T> GetReachable(T src, float budget)
        {
            
            var srcIter = new T[] { src };
            return Dijkstra(srcIter, PathFinding<T>.DummyFalsePredicate, budget);
        }

        public IDijkstraOutput<T> ExploreNearestTarget(T src, Func<T, bool> Predicate)
        {
            var srcIter = new T[] { src };
            var result = Dijkstra(srcIter, Predicate, float.PositiveInfinity);
            return result;
        }
        */
    }

    public abstract class BasePathFinder<T>: IPathFinder<T>
    {
        public abstract IAStarOutput<T> AStar(T src, T dst);
        public abstract IDijkstraOutput<T> Dijkstra(IEnumerable<T> srcIter, Func<T, bool> Predicate, float budget);

        public IDijkstraOutput<T> GetReachable(T src, float budget)
        {
            
            var srcIter = new T[] { src };
            return Dijkstra(srcIter, PathFinding<T>.DummyFalsePredicate, budget);
        }

        public IDijkstraOutput<T> ExploreNearestTarget(T src, Func<T, bool> Predicate)
        {
            var srcIter = new T[] { src };
            var result = Dijkstra(srcIter, Predicate, float.PositiveInfinity);
            return result;
        }

        public IDijkstraOutput<T> GetShortpathForMultiple(T src, IEnumerable<T> allTargetIter)
        {
            throw new NotImplementedException(); // TODO: Implement or refactor?
        }
    }

    public class DirectGraphPathFinder<T>: BasePathFinder<T>
    {
        public IGraph<T> Graph;

        public override IAStarOutput<T> AStar(T src, T dst) => new AStarOutput<T>(){Result=PathFinding<T>.AStar3(Graph, src, dst)};
        public override IDijkstraOutput<T> Dijkstra(IEnumerable<T> srcIter, Func<T, bool> Predicate, float budget) => new DijkstraOutput<T>(){Result=PathFinding<T>.Dijkstra(Graph, srcIter, Predicate, budget)};
        // public DijkstraResult<T> GetReachable(T src, float budget) => 
    }
    

    public class MappedGraphPathFinder<IT, ET>: BasePathFinder<ET>
    {
        public IGraph<IT> Graph;

        public Func<IT, ET> I2E;
        public Func<ET, IT> E2I;

        public override IAStarOutput<ET> AStar(ET src, ET dst)
        {
            var result = PathFinding<IT>.AStar3(Graph, E2I(src), E2I(dst));
            return new AStarOutputTransformed<IT, ET>(){Result=result, Transform=I2E};
        }

        public override IDijkstraOutput<ET> Dijkstra(IEnumerable<ET> srcIter, Func<ET, bool> Predicate, float budget)
        {
            var result = PathFinding<IT>.Dijkstra(Graph, srcIter.Select(E2I), (i) => Predicate(I2E(i)), budget); // prevent Predicate or I2E call for special case like Reachable?
            return new DijkstraOutputTransformed<IT, ET>(){Result=result, I2E=I2E, E2I=E2I};
        }
    }

    public class Embedding2D // TODO: Use Struct?
    {
        public float X;
        public float Y;

        public float Distance(Embedding2D other)
        {
            var dx = X - other.X;
            var dy = Y - other.Y;
            return MathF.Sqrt(dx*dx + dy*dy);
        }
    }

    public class FrozenEdge
    {
        public float Distance;
        public FrozenNode Node;
    }

    public class FrozenNode
    {
        public Embedding2D Embedding;
        public Dictionary<FrozenNode, FrozenEdge> EdgeMap;
    }

    public class FrozenGraph<ET>: IGraphEnumerable<FrozenNode>
    {
        Dictionary<ET, FrozenNode> e2iMap;
        Dictionary<FrozenNode, ET> i2eMap;
        List<FrozenNode> nodes;
        float estimateCoef = 1;

        public IEnumerable<FrozenNode> Nodes() => nodes;
        public IEnumerable<FrozenNode> Neighbors(FrozenNode node) => node.EdgeMap.Keys;
        public float MoveCost(FrozenNode src, FrozenNode dst) => src.EdgeMap[dst].Distance;
        public float EstimateCost(FrozenNode src, FrozenNode dst) => src.Embedding.Distance(dst.Embedding) * estimateCoef;

        public ET I2E(FrozenNode i) => i == null ? default(ET) : i2eMap[i];
        public FrozenNode E2I(ET e) => e2iMap[e];

        public IPathFinder<ET> GetPathFinder()
        {
            return new MappedGraphPathFinder<FrozenNode, ET>(){Graph=this, I2E=I2E, E2I=E2I};
        }

        public static FrozenGraph<ET> GetGraph(IGraphEnumerable<ET> graph, Func<ET, (float, float)> embeddingMap, float estimateCoef=1f)
        {
            var nodes = new List<FrozenNode>();
            var e2iMap = new Dictionary<ET, FrozenNode>();
            var i2eMap = new Dictionary<FrozenNode, ET>();

            foreach(var node in graph.Nodes())
            {
                // var nodeTransformed = transform(node);
                (var x, var y) = embeddingMap(node);
                var nodeTransformed = new FrozenNode(){Embedding = new Embedding2D(){X=x, Y=y}, EdgeMap=new()};

                nodes.Add(nodeTransformed);
                e2iMap[node] = nodeTransformed;
                i2eMap[nodeTransformed] = node;
            }

            foreach(var node in graph.Nodes())
            {
                foreach(var neiNode in graph.Neighbors(node))
                {
                    var nodeI = e2iMap[node];
                    var neiNodeI = e2iMap[neiNode];

                    var edge = new FrozenEdge(){Distance=graph.MoveCost(node, neiNode), Node=neiNodeI};
                    nodeI.EdgeMap[neiNodeI] = edge;
                }
            }

            return new FrozenGraph<ET>(){e2iMap=e2iMap, i2eMap=i2eMap, nodes=nodes, estimateCoef=estimateCoef};
        }
    }

    public class FrozenGraph2D<ET>: IPathFinder<ET>
    {
        PathFinding2.Graph2D Graph;
        Dictionary<ET, int> e2iMap;
        List<ET> i2eMap;
        public ET I2E(int i) => i == -1 ? default(ET) : i2eMap[i];
        public int E2I(ET e) => e2iMap[e];
        
        public class AStarOutput: List<ET>, IAStarOutput<ET>
        {
            public float Cost{get;set;}
        }

        public class DijkstraOutput: Dictionary<ET, IPath<ET>>, IDijkstraOutput<ET>
        {
        }

        public IAStarOutput<ET> AStar(ET src, ET dst)
        {
            (var cost, var path) = Graph.AStar(E2I(src), E2I(dst));
            var ret = new AStarOutput(){Cost=cost};
            ret.AddRange(path.Select(I2E));
            return ret;
        }

        public IDijkstraOutput<ET> GetReachable(ET src, float budget)
        {
            var dict = Graph.Dijkstra(new int[]{E2I(src)}, budget);
            return CreateDijkstraOutput(dict);
        }

        public IDijkstraOutput<ET> GetShortpathForMultiple(ET src, IEnumerable<ET> allTargetIter)
        {
            var _allTargetIter = allTargetIter.Select(E2I);
            var dict = Graph.DijkstraAllTarget(new int[]{E2I(src)}, _allTargetIter);
            return CreateDijkstraOutput(dict);
        }

        DijkstraOutput CreateDijkstraOutput(Dictionary<int, PathFinding2.Graph2D.Arrow> dict)
        {
            var ret = new DijkstraOutput();

            foreach((var i, var arrow) in dict)
                ret[I2E(i)] = new Path<ET>(){cost=arrow.Cost, prev=I2E(arrow.Prev)};

            return ret;
        }

        public static IPathFinder<ET> GetPathFinder(IGraphEnumerable<ET> graph, Func<ET, (float, float)> embeddingMap, float estimateCoef=1f)
        {
            var nodes = new List<PathFinding2.Node2D>();
            var e2iMap = new Dictionary<ET, int>();
            var i2eMap = new List<ET>();

            var idx = 0;
            foreach(var node in graph.Nodes())
            {
                // var nodeTransformed = transform(node);
                (var x, var y) = embeddingMap(node);
                var nodeTransformed = new PathFinding2.Node2D(){X=x, Y=y};

                nodes.Add(nodeTransformed);
                e2iMap[node] = idx;
                i2eMap.Add(node);
                
                idx++;
            }

            foreach(var node in graph.Nodes())
            {
                var edges = new List<PathFinding2.Edge2D>();
                var nodeI = e2iMap[node];

                foreach(var neiNode in graph.Neighbors(node))
                {
                    var neiNodeI = e2iMap[neiNode];

                    var edge = new PathFinding2.Edge2D(){Cost=graph.MoveCost(node, neiNode), Target=neiNodeI};
                    edges.Add(edge);
                }
                nodes[nodeI].Edges = edges.ToArray();
            }

            var baseGraph = new PathFinding2.Graph2D(){Nodes=nodes.ToArray(), EstimateCostCoef=estimateCoef};

            return new FrozenGraph2D<ET>(){Graph=baseGraph, e2iMap=e2iMap, i2eMap=i2eMap};
        }

    }

    // int graph and its family

}
