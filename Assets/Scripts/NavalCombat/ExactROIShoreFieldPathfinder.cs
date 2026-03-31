using System;
using System.Collections.Generic;
using GeographicLib;
using NavalCombatCore;
using UnityEngine;

public sealed class ExactROIShoreFieldPathfinder
{
    const int DefaultSearchWindowSize = 1024;
    const float DiagonalCost = 1.41421356f;

    readonly ElevationProvider provider;
    readonly int windowMinX;
    readonly int windowMinY;
    readonly int windowWidth;
    readonly int windowHeight;
    readonly int sourcePixelX;
    readonly int sourcePixelY;
    readonly float[] gScore;
    readonly int[] cameFrom;
    readonly int[] nodeStamp;
    readonly int[] closedStamp;
    readonly MinHeap openSet;

    int currentSearchStamp;

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

    public ExactROIShoreFieldPathfinder(ElevationProvider provider, LatLon source, int searchWindowSize = DefaultSearchWindowSize)
    {
        this.provider = provider;
        if (provider == null || !provider.HasValidROIShoreField())
        {
            return;
        }

        if (!provider.TryGetROIPixelCoordsRounded(source, out sourcePixelX, out sourcePixelY))
        {
            return;
        }

        var clampedWindowWidth = Math.Min(searchWindowSize, provider.ROIShoreFieldWidth);
        var clampedWindowHeight = Math.Min(searchWindowSize, provider.ROIShoreFieldHeight);

        windowWidth = Math.Max(1, clampedWindowWidth);
        windowHeight = Math.Max(1, clampedWindowHeight);
        windowMinX = Mathf.Clamp(sourcePixelX - windowWidth / 2, 0, Math.Max(0, provider.ROIShoreFieldWidth - windowWidth));
        windowMinY = Mathf.Clamp(sourcePixelY - windowHeight / 2, 0, Math.Max(0, provider.ROIShoreFieldHeight - windowHeight));

        var nodeCount = windowWidth * windowHeight;
        gScore = new float[nodeCount];
        cameFrom = new int[nodeCount];
        nodeStamp = new int[nodeCount];
        closedStamp = new int[nodeCount];
        openSet = new MinHeap(nodeCount / 16 + 16);
    }

    public bool IsReady => provider != null && gScore != null && windowWidth > 0 && windowHeight > 0;

    public bool TryGetWindowNodeIndex(LatLon latLon, out int localIndex)
    {
        localIndex = -1;
        if (!provider.TryGetROIPixelCoordsRounded(latLon, out var pixelX, out var pixelY))
        {
            return false;
        }

        if (!IsPixelInsideWindow(pixelX, pixelY))
        {
            return false;
        }

        localIndex = ToLocalIndex(pixelX - windowMinX, pixelY - windowMinY);
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

        if (!provider.TryGetROIPixelCoordsRounded(source, out var sourceX, out var sourceY)
            || !provider.TryGetROIPixelCoordsRounded(destination, out var destinationX, out var destinationY))
        {
            result.failureReason = PathfindingFailureReason.OutsideROI;
            return result;
        }

        if (!IsPixelInsideWindow(destinationX, destinationY))
        {
            result.failureReason = PathfindingFailureReason.SearchWindowExceeded;
            return result;
        }

        if (!TryGetDistancePixels(sourceX, sourceY, out var sourceDistance))
        {
            result.failureReason = PathfindingFailureReason.ShoreFieldUnavailable;
            return result;
        }

        if (sourceDistance < thresholdPixels)
        {
            result.failureReason = PathfindingFailureReason.SourceBlocked;
            return result;
        }

        if (!TryGetDistancePixels(destinationX, destinationY, out var destinationDistance))
        {
            result.failureReason = PathfindingFailureReason.ShoreFieldUnavailable;
            return result;
        }

        if (destinationDistance < thresholdPixels)
        {
            result.failureReason = PathfindingFailureReason.DestinationBlocked;
            return result;
        }

        var startIndex = ToLocalIndex(sourceX - windowMinX, sourceY - windowMinY);
        var endIndex = ToLocalIndex(destinationX - windowMinX, destinationY - windowMinY);
        var rawPath = RunAStar(startIndex, endIndex, thresholdPixels);
        if (rawPath == null || rawPath.Count == 0)
        {
            result.failureReason = PathfindingFailureReason.NoPath;
            return result;
        }

        var simplifiedPath = SimplifyPath(rawPath, thresholdPixels);
        result.points = BuildLatLonPath(source, destination, simplifiedPath);
        result.routedDistanceMeters = ComputeDistanceMeters(result.points);
        result.success = true;
        result.failureReason = PathfindingFailureReason.None;
        return result;
    }

    bool IsPixelInsideWindow(int pixelX, int pixelY)
    {
        return pixelX >= windowMinX
            && pixelX < windowMinX + windowWidth
            && pixelY >= windowMinY
            && pixelY < windowMinY + windowHeight;
    }

    bool TryGetDistancePixels(int pixelX, int pixelY, out float distancePixels)
    {
        distancePixels = 0f;
        if (!IsReady || !IsPixelInsideWindow(pixelX, pixelY))
        {
            return false;
        }

        return provider.TryGetROIShoreFieldDistancePixels(pixelX, pixelY, out distancePixels);
    }

    bool TryGetLocalDistancePixels(int localX, int localY, out float distancePixels)
    {
        distancePixels = 0f;
        if (!IsReady || localX < 0 || localX >= windowWidth || localY < 0 || localY >= windowHeight)
        {
            return false;
        }

        return provider.TryGetROIShoreFieldDistancePixels(windowMinX + localX, windowMinY + localY, out distancePixels);
    }

    int ToLocalIndex(int localX, int localY) => localY * windowWidth + localX;

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
            var currentX = currentIndex % windowWidth;
            var currentY = currentIndex / windowWidth;

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
                    if (nextX < 0 || nextX >= windowWidth || nextY < 0 || nextY >= windowHeight)
                    {
                        continue;
                    }

                    var nextIndex = ToLocalIndex(nextX, nextY);
                    if (closedStamp[nextIndex] == currentSearchStamp)
                    {
                        continue;
                    }

                    if (!TryGetLocalDistancePixels(nextX, nextY, out var nextDistance) || nextDistance < thresholdPixels)
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
        var srcX = srcIndex % windowWidth;
        var srcY = srcIndex / windowWidth;
        var dstX = dstIndex % windowWidth;
        var dstY = dstIndex / windowWidth;
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

            var prevX = prev % windowWidth;
            var prevY = prev / windowWidth;
            var currentX = current % windowWidth;
            var currentY = current / windowWidth;
            var nextX = next % windowWidth;
            var nextY = next / windowWidth;

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
        var startX = startIndex % windowWidth;
        var startY = startIndex / windowWidth;
        var endX = endIndex % windowWidth;
        var endY = endIndex / windowWidth;

        var deltaX = endX - startX;
        var deltaY = endY - startY;
        var steps = Math.Max(Math.Abs(deltaX), Math.Abs(deltaY)) * 2;
        if (steps <= 0)
        {
            return TryGetLocalDistancePixels(startX, startY, out var startDistance) && startDistance >= thresholdPixels;
        }

        for (var step = 0; step <= steps; step++)
        {
            var t = step / (float)steps;
            var x = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(startX, endX, t)), 0, windowWidth - 1);
            var y = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(startY, endY, t)), 0, windowHeight - 1);
            if (!TryGetLocalDistancePixels(x, y, out var distancePixels) || distancePixels < thresholdPixels)
            {
                return false;
            }
        }

        return true;
    }

    List<LatLon> BuildLatLonPath(LatLon source, LatLon destination, List<int> localPath)
    {
        var points = new List<LatLon> { source };
        for (var i = 0; i < localPath.Count; i++)
        {
            var index = localPath[i];
            var localX = index % windowWidth;
            var localY = index / windowWidth;
            points.Add(provider.ROIPixelCoordsToLatLon(windowMinX + localX, windowMinY + localY));
        }

        points.Add(destination);
        return DeduplicatePoints(points);
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
