using System;
using System.Collections.Generic;
using GeographicLib;
using NavalCombatCore;
using UnityEngine;

public enum PathfindingFailureReason
{
    None,
    ShoreFieldUnavailable,
    OutsideROI,
    SourceBlocked,
    DestinationBlocked,
    NoPath
}

public sealed class PathfindingResult
{
    public bool success;
    public List<LatLon> points = new();
    public float routedDistanceMeters;
    public PathfindingFailureReason failureReason;
}

public sealed class ROIShoreFieldPathfinder
{
    const float DiagonalCost = 1.41421356f;
    const int EndpointSearchRadius = 64;

    readonly ElevationProvider provider;
    readonly int stridePixels;
    readonly int coarseWidth;
    readonly int coarseHeight;
    readonly float[] coarseDistancePixels;
    readonly float[] gScore;
    readonly int[] cameFrom;
    readonly int[] nodeStamp;
    readonly int[] closedStamp;
    readonly MinHeap openSet;

    int currentSearchStamp;

    struct CoarseNode
    {
        public int x;
        public int y;
        public int index;

        public CoarseNode(int x, int y, int width)
        {
            this.x = x;
            this.y = y;
            index = y * width + x;
        }
    }

    sealed class MinHeap
    {
        int[] indices;
        float[] priorities;

        public int Count { get; private set; }

        public MinHeap(int capacity)
        {
            indices = new int[Math.Max(4, capacity)];
            priorities = new float[indices.Length];
        }

        public void Clear() => Count = 0;

        public void Push(int index, float priority)
        {
            EnsureCapacity(Count + 1);
            var pos = Count++;
            indices[pos] = index;
            priorities[pos] = priority;
            SiftUp(pos);
        }

        public bool TryPop(out int index, out float priority)
        {
            if (Count <= 0)
            {
                index = default;
                priority = default;
                return false;
            }

            index = indices[0];
            priority = priorities[0];
            Count--;
            if (Count > 0)
            {
                indices[0] = indices[Count];
                priorities[0] = priorities[Count];
                SiftDown(0);
            }

            return true;
        }

        void EnsureCapacity(int capacity)
        {
            if (capacity <= indices.Length)
            {
                return;
            }

            var newCapacity = Math.Max(capacity, indices.Length * 2);
            Array.Resize(ref indices, newCapacity);
            Array.Resize(ref priorities, newCapacity);
        }

        void SiftUp(int index)
        {
            while (index > 0)
            {
                var parent = (index - 1) / 2;
                if (priorities[parent] <= priorities[index])
                {
                    break;
                }

                Swap(parent, index);
                index = parent;
            }
        }

        void SiftDown(int index)
        {
            while (true)
            {
                var left = index * 2 + 1;
                var right = left + 1;
                var smallest = index;

                if (left < Count && priorities[left] < priorities[smallest])
                {
                    smallest = left;
                }

                if (right < Count && priorities[right] < priorities[smallest])
                {
                    smallest = right;
                }

                if (smallest == index)
                {
                    break;
                }

                Swap(index, smallest);
                index = smallest;
            }
        }

        void Swap(int a, int b)
        {
            (indices[a], indices[b]) = (indices[b], indices[a]);
            (priorities[a], priorities[b]) = (priorities[b], priorities[a]);
        }
    }

    public ROIShoreFieldPathfinder(ElevationProvider provider, int stridePixels = 8)
    {
        this.provider = provider;
        this.stridePixels = Math.Max(1, stridePixels);

        if (provider == null || !provider.HasValidROIShoreField())
        {
            return;
        }

        coarseWidth = Math.Max(1, Mathf.CeilToInt(provider.ROIShoreFieldWidth / (float)this.stridePixels));
        coarseHeight = Math.Max(1, Mathf.CeilToInt(provider.ROIShoreFieldHeight / (float)this.stridePixels));

        var nodeCount = coarseWidth * coarseHeight;
        coarseDistancePixels = new float[nodeCount];
        gScore = new float[nodeCount];
        cameFrom = new int[nodeCount];
        nodeStamp = new int[nodeCount];
        closedStamp = new int[nodeCount];
        openSet = new MinHeap(nodeCount / 16 + 16);

        BuildCoarseDistanceField();
    }

    public bool IsReady => provider != null && coarseDistancePixels != null;

    public bool TryGetCoarseNodeIndex(LatLon latLon, out int coarseIndex)
    {
        coarseIndex = -1;
        if (!IsReady || !provider.TryGetROIPixelCoords(latLon, out var rawX, out var rawY))
        {
            return false;
        }

        coarseIndex = ToCoarseIndex(
            Mathf.Clamp(Mathf.RoundToInt(rawX / stridePixels), 0, coarseWidth - 1),
            Mathf.Clamp(Mathf.RoundToInt(rawY / stridePixels), 0, coarseHeight - 1));
        return true;
    }

    public PathfindingResult FindPath(LatLon source, LatLon destination, float thresholdPixels)
    {
        var result = new PathfindingResult();
        if (!IsReady || !provider.HasValidROIShoreField())
        {
            result.failureReason = PathfindingFailureReason.ShoreFieldUnavailable;
            return result;
        }

        if (!provider.TryGetROIPixelCoords(source, out var sourcePixelX, out var sourcePixelY)
            || !provider.TryGetROIPixelCoords(destination, out var destinationPixelX, out var destinationPixelY))
        {
            result.failureReason = PathfindingFailureReason.OutsideROI;
            return result;
        }

        if (!provider.TryGetROIShoreFieldDistancePixels(source, out var sourceDistance))
        {
            result.failureReason = PathfindingFailureReason.ShoreFieldUnavailable;
            return result;
        }

        if (sourceDistance < thresholdPixels)
        {
            result.failureReason = PathfindingFailureReason.SourceBlocked;
            return result;
        }

        if (!provider.TryGetROIShoreFieldDistancePixels(destination, out var destinationDistance))
        {
            result.failureReason = PathfindingFailureReason.ShoreFieldUnavailable;
            return result;
        }

        if (destinationDistance < thresholdPixels)
        {
            result.failureReason = PathfindingFailureReason.DestinationBlocked;
            return result;
        }

        if (!TryFindNearestPassableCoarseNode(sourcePixelX, sourcePixelY, thresholdPixels, out var startNode)
            || !TryFindNearestPassableCoarseNode(destinationPixelX, destinationPixelY, thresholdPixels, out var endNode))
        {
            result.failureReason = PathfindingFailureReason.NoPath;
            return result;
        }

        var coarsePath = RunAStar(startNode.index, endNode.index, thresholdPixels);
        if (coarsePath == null || coarsePath.Count == 0)
        {
            result.failureReason = PathfindingFailureReason.NoPath;
            return result;
        }

        var simplifiedPath = SimplifyPath(coarsePath, thresholdPixels);
        result.points = BuildLatLonPath(source, destination, simplifiedPath);
        result.routedDistanceMeters = ComputeDistanceMeters(result.points);
        result.success = true;
        result.failureReason = PathfindingFailureReason.None;
        return result;
    }

    void BuildCoarseDistanceField()
    {
        var centerOffset = stridePixels * 0.5f;
        for (var y = 0; y < coarseHeight; y++)
        {
            for (var x = 0; x < coarseWidth; x++)
            {
                var pixelX = Mathf.Clamp(Mathf.RoundToInt(x * stridePixels + centerOffset), 0, provider.ROIShoreFieldWidth - 1);
                var pixelY = Mathf.Clamp(Mathf.RoundToInt(y * stridePixels + centerOffset), 0, provider.ROIShoreFieldHeight - 1);
                if (!provider.TryGetROIShoreFieldDistancePixels(pixelX, pixelY, out var distancePixels))
                {
                    distancePixels = 0f;
                }

                coarseDistancePixels[ToCoarseIndex(x, y)] = distancePixels;
            }
        }
    }

    int ToCoarseIndex(int x, int y) => y * coarseWidth + x;

    bool TryFindNearestPassableCoarseNode(float pixelX, float pixelY, float thresholdPixels, out CoarseNode coarseNode)
    {
        coarseNode = default;
        var centerX = Mathf.Clamp(Mathf.RoundToInt(pixelX / stridePixels), 0, coarseWidth - 1);
        var centerY = Mathf.Clamp(Mathf.RoundToInt(pixelY / stridePixels), 0, coarseHeight - 1);

        var found = false;
        var bestDistanceSq = float.PositiveInfinity;
        for (var radius = 0; radius <= EndpointSearchRadius; radius++)
        {
            var minX = Mathf.Max(0, centerX - radius);
            var maxX = Mathf.Min(coarseWidth - 1, centerX + radius);
            var minY = Mathf.Max(0, centerY - radius);
            var maxY = Mathf.Min(coarseHeight - 1, centerY + radius);

            for (var y = minY; y <= maxY; y++)
            {
                for (var x = minX; x <= maxX; x++)
                {
                    if (radius > 0
                        && x > minX && x < maxX
                        && y > minY && y < maxY)
                    {
                        continue;
                    }

                    var index = ToCoarseIndex(x, y);
                    if (coarseDistancePixels[index] < thresholdPixels)
                    {
                        continue;
                    }

                    var dx = x - centerX;
                    var dy = y - centerY;
                    var distanceSq = dx * dx + dy * dy;
                    if (distanceSq < bestDistanceSq)
                    {
                        coarseNode = new CoarseNode(x, y, coarseWidth);
                        bestDistanceSq = distanceSq;
                        found = true;
                    }
                }
            }

            if (found)
            {
                return true;
            }
        }

        return false;
    }

    List<int> RunAStar(int startIndex, int endIndex, float thresholdPixels)
    {
        openSet.Clear();
        currentSearchStamp++;
        if (currentSearchStamp == int.MaxValue)
        {
            Array.Clear(nodeStamp, 0, nodeStamp.Length);
            Array.Clear(closedStamp, 0, closedStamp.Length);
            currentSearchStamp = 1;
        }

        nodeStamp[startIndex] = currentSearchStamp;
        gScore[startIndex] = 0f;
        cameFrom[startIndex] = -1;
        openSet.Push(startIndex, EstimateCost(startIndex, endIndex));

        while (openSet.TryPop(out var currentIndex, out var priority))
        {
            if (closedStamp[currentIndex] == currentSearchStamp)
            {
                continue;
            }

            if (nodeStamp[currentIndex] != currentSearchStamp)
            {
                continue;
            }

            if (priority > gScore[currentIndex] + EstimateCost(currentIndex, endIndex) + 1e-4f)
            {
                continue;
            }

            if (currentIndex == endIndex)
            {
                return ReconstructPath(endIndex);
            }

            closedStamp[currentIndex] = currentSearchStamp;
            var currentX = currentIndex % coarseWidth;
            var currentY = currentIndex / coarseWidth;

            for (var deltaY = -1; deltaY <= 1; deltaY++)
            {
                for (var deltaX = -1; deltaX <= 1; deltaX++)
                {
                    if (deltaX == 0 && deltaY == 0)
                    {
                        continue;
                    }

                    var nextX = currentX + deltaX;
                    var nextY = currentY + deltaY;
                    if (nextX < 0 || nextX >= coarseWidth || nextY < 0 || nextY >= coarseHeight)
                    {
                        continue;
                    }

                    var nextIndex = ToCoarseIndex(nextX, nextY);
                    if (closedStamp[nextIndex] == currentSearchStamp || coarseDistancePixels[nextIndex] < thresholdPixels)
                    {
                        continue;
                    }

                    var moveCost = deltaX != 0 && deltaY != 0 ? DiagonalCost : 1f;
                    var tentativeGScore = gScore[currentIndex] + moveCost;
                    if (nodeStamp[nextIndex] != currentSearchStamp || tentativeGScore < gScore[nextIndex])
                    {
                        nodeStamp[nextIndex] = currentSearchStamp;
                        gScore[nextIndex] = tentativeGScore;
                        cameFrom[nextIndex] = currentIndex;
                        openSet.Push(nextIndex, tentativeGScore + EstimateCost(nextIndex, endIndex));
                    }
                }
            }
        }

        return null;
    }

    float EstimateCost(int srcIndex, int dstIndex)
    {
        var srcX = srcIndex % coarseWidth;
        var srcY = srcIndex / coarseWidth;
        var dstX = dstIndex % coarseWidth;
        var dstY = dstIndex / coarseWidth;
        return Vector2.Distance(new Vector2(srcX, srcY), new Vector2(dstX, dstY));
    }

    List<int> ReconstructPath(int endIndex)
    {
        var path = new List<int>();
        var current = endIndex;
        while (current >= 0)
        {
            path.Add(current);
            current = cameFrom[current];
        }

        path.Reverse();
        return path;
    }

    List<int> SimplifyPath(List<int> path, float thresholdPixels)
    {
        if (path.Count <= 2)
        {
            return path;
        }

        var withoutCollinear = RemoveCollinear(path);
        if (withoutCollinear.Count <= 2)
        {
            return withoutCollinear;
        }

        var simplified = new List<int> { withoutCollinear[0] };
        var anchor = 0;
        while (anchor < withoutCollinear.Count - 1)
        {
            var furthest = anchor + 1;
            for (var candidate = anchor + 2; candidate < withoutCollinear.Count; candidate++)
            {
                if (IsStraightPassable(withoutCollinear[anchor], withoutCollinear[candidate], thresholdPixels))
                {
                    furthest = candidate;
                }
                else
                {
                    break;
                }
            }

            simplified.Add(withoutCollinear[furthest]);
            anchor = furthest;
        }

        return simplified;
    }

    List<int> RemoveCollinear(List<int> path)
    {
        if (path.Count <= 2)
        {
            return path;
        }

        var simplified = new List<int> { path[0] };
        for (var i = 1; i < path.Count - 1; i++)
        {
            var prev = path[i - 1];
            var current = path[i];
            var next = path[i + 1];

            var prevX = prev % coarseWidth;
            var prevY = prev / coarseWidth;
            var currentX = current % coarseWidth;
            var currentY = current / coarseWidth;
            var nextX = next % coarseWidth;
            var nextY = next / coarseWidth;

            var dirAX = currentX - prevX;
            var dirAY = currentY - prevY;
            var dirBX = nextX - currentX;
            var dirBY = nextY - currentY;

            if (dirAX * dirBY == dirAY * dirBX)
            {
                continue;
            }

            simplified.Add(current);
        }

        simplified.Add(path[path.Count - 1]);
        return simplified;
    }

    bool IsStraightPassable(int startIndex, int endIndex, float thresholdPixels)
    {
        var startX = startIndex % coarseWidth;
        var startY = startIndex / coarseWidth;
        var endX = endIndex % coarseWidth;
        var endY = endIndex / coarseWidth;

        var deltaX = endX - startX;
        var deltaY = endY - startY;
        var steps = Math.Max(Math.Abs(deltaX), Math.Abs(deltaY)) * 2;
        if (steps <= 0)
        {
            return coarseDistancePixels[startIndex] >= thresholdPixels;
        }

        for (var step = 0; step <= steps; step++)
        {
            var t = step / (float)steps;
            var x = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(startX, endX, t)), 0, coarseWidth - 1);
            var y = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(startY, endY, t)), 0, coarseHeight - 1);
            if (coarseDistancePixels[ToCoarseIndex(x, y)] < thresholdPixels)
            {
                return false;
            }
        }

        return true;
    }

    List<LatLon> BuildLatLonPath(LatLon source, LatLon destination, List<int> coarsePath)
    {
        var points = new List<LatLon> { source };
        for (var i = 0; i < coarsePath.Count; i++)
        {
            var index = coarsePath[i];
            var x = index % coarseWidth;
            var y = index / coarseWidth;
            var latLon = provider.ROIPixelCoordsToLatLon(GetCoarsePixelCenterX(x), GetCoarsePixelCenterY(y));
            points.Add(latLon);
        }

        points.Add(destination);
        return DeduplicatePoints(points);
    }

    float GetCoarsePixelCenterX(int coarseX)
    {
        return Mathf.Clamp(coarseX * stridePixels + stridePixels * 0.5f, 0f, Mathf.Max(0f, provider.ROIShoreFieldWidth - 1f));
    }

    float GetCoarsePixelCenterY(int coarseY)
    {
        return Mathf.Clamp(coarseY * stridePixels + stridePixels * 0.5f, 0f, Mathf.Max(0f, provider.ROIShoreFieldHeight - 1f));
    }

    static List<LatLon> DeduplicatePoints(List<LatLon> points)
    {
        if (points.Count <= 1)
        {
            return points;
        }

        var deduped = new List<LatLon> { points[0] };
        for (var i = 1; i < points.Count; i++)
        {
            var last = deduped[deduped.Count - 1];
            var current = points[i];
            if (Mathf.Abs(last.LatDeg - current.LatDeg) < 1e-5f
                && Mathf.Abs(last.LonDeg - current.LonDeg) < 1e-5f)
            {
                continue;
            }

            deduped.Add(current);
        }

        return deduped;
    }

    static float ComputeDistanceMeters(List<LatLon> points)
    {
        var totalMeters = 0f;
        for (var i = 1; i < points.Count; i++)
        {
            var inverseLine = Geodesic.WGS84.InverseLine(
                points[i - 1].LatDeg, points[i - 1].LonDeg,
                points[i].LatDeg, points[i].LonDeg);
            totalMeters += (float)inverseLine.Distance;
        }

        return totalMeters;
    }
}
