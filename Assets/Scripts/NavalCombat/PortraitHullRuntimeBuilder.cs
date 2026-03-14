using System.Collections.Generic;
using UnityEngine;

internal static class PortraitHullRuntimeBuilder
{
    static readonly Dictionary<Texture2D, Mesh> meshCache = new();
    static readonly HashSet<Texture2D> failedTextures = new();
    const int MinStations = 24;
    const int MaxStations = 120;
    const int SmoothRadius = 2;
    const int SmoothPasses = 3;
    const float InsetRatio = 0.08f;
    const float MinHalfWidth = 0.0035f;

    public static Mesh GetOrBuildNormalizedHullMesh(Texture2D portraitTex, float alphaThreshold = 0.1f)
    {
        if (portraitTex == null)
            return null;

        if (meshCache.TryGetValue(portraitTex, out var mesh))
            return mesh;

        if (failedTextures.Contains(portraitTex))
            return null;

        mesh = BuildNormalizedHullMesh(portraitTex, alphaThreshold);
        if (mesh != null)
        {
            meshCache[portraitTex] = mesh;
            return mesh;
        }

        failedTextures.Add(portraitTex);
        return null;
    }

    static Mesh BuildNormalizedHullMesh(Texture2D portraitTex, float alphaThreshold)
    {
        Color32[] pixels;
        try
        {
            pixels = portraitTex.GetPixels32();
        }
        catch (UnityException ex)
        {
            Debug.LogWarning($"Runtime hull preview requires a readable texture for {portraitTex.name}: {ex.Message}");
            return null;
        }

        var width = portraitTex.width;
        var height = portraitTex.height;
        if (pixels == null || pixels.Length != width * height)
            return null;

        var stationCount = Mathf.Clamp(width / 4, MinStations, MaxStations);
        var minRunPixels = Mathf.Max(2, Mathf.RoundToInt(height * 0.05f));

        var rawCenters = new float[stationCount];
        var rawHalfWidths = new float[stationCount];
        var valid = new bool[stationCount];

        for (int i = 0; i < stationCount; i++)
        {
            var xStart = Mathf.FloorToInt((float)i / stationCount * width);
            var xEnd = Mathf.CeilToInt((float)(i + 1) / stationCount * width);
            xEnd = Mathf.Clamp(xEnd, xStart + 1, width);

            if (!TryExtractPrimaryVerticalRun(pixels, width, height, xStart, xEnd, alphaThreshold, minRunPixels, out var minY, out var maxY))
                continue;

            valid[i] = true;
            var bottom = (minY + 0.5f) / height - 0.5f;
            var top = (maxY + 0.5f) / height - 0.5f;
            rawCenters[i] = (top + bottom) * 0.5f;
            rawHalfWidths[i] = Mathf.Max((top - bottom) * 0.5f, MinHalfWidth);
        }

        if (!TryFindLargestValidRange(valid, out var startIndex, out var endIndex))
            return null;

        var rangeLength = endIndex - startIndex + 1;
        if (rangeLength < 4)
            return null;

        var centers = new float[rangeLength];
        var halfWidths = new float[rangeLength];
        var xs = new float[rangeLength];

        for (int i = 0; i < rangeLength; i++)
        {
            var sourceIndex = startIndex + i;
            centers[i] = rawCenters[sourceIndex];
            halfWidths[i] = rawHalfWidths[sourceIndex];

            var u = (sourceIndex + 0.5f) / stationCount;
            xs[i] = u - 0.5f;
        }

        SmoothInPlace(centers);
        SmoothInPlace(halfWidths);

        var insetNormalized = Mathf.Max(1.5f / height, 0.004f);
        for (int i = 0; i < halfWidths.Length; i++)
        {
            halfWidths[i] = Mathf.Max(halfWidths[i] * (1f - InsetRatio) - insetNormalized, MinHalfWidth);
        }

        var vertices = new List<Vector3>(rangeLength * 4);
        var uvs = new List<Vector2>(rangeLength * 4);
        var triangles = new List<int>((rangeLength - 1) * 18 + 12);

        var deckStarboard = new int[rangeLength];
        var deckPort = new int[rangeLength];
        var keelStarboard = new int[rangeLength];
        var keelPort = new int[rangeLength];

        for (int i = 0; i < rangeLength; i++)
        {
            var x = xs[i];
            var center = centers[i];
            var halfWidth = halfWidths[i];

            deckStarboard[i] = AddVertex(vertices, uvs, new Vector3(x, center - halfWidth, 0f), new Vector2(i / Mathf.Max(1f, rangeLength - 1), 1f));
            deckPort[i] = AddVertex(vertices, uvs, new Vector3(x, center + halfWidth, 0f), new Vector2(i / Mathf.Max(1f, rangeLength - 1), 1f));
            keelStarboard[i] = AddVertex(vertices, uvs, new Vector3(x, center - halfWidth, 1f), new Vector2(i / Mathf.Max(1f, rangeLength - 1), 0f));
            keelPort[i] = AddVertex(vertices, uvs, new Vector3(x, center + halfWidth, 1f), new Vector2(i / Mathf.Max(1f, rangeLength - 1), 0f));
        }

        for (int i = 0; i < rangeLength - 1; i++)
        {
            AddQuad(triangles, deckPort[i], deckPort[i + 1], keelPort[i + 1], keelPort[i]);
            AddQuad(triangles, deckStarboard[i + 1], deckStarboard[i], keelStarboard[i], keelStarboard[i + 1]);
            AddQuad(triangles, keelStarboard[i], keelPort[i], keelPort[i + 1], keelStarboard[i + 1]);
        }

        AddQuad(triangles, deckStarboard[0], deckPort[0], keelPort[0], keelStarboard[0]);
        AddQuad(triangles, deckPort[rangeLength - 1], deckStarboard[rangeLength - 1], keelStarboard[rangeLength - 1], keelPort[rangeLength - 1]);

        var mesh = new Mesh
        {
            name = $"RuntimeHull_{portraitTex.name}"
        };

        if (vertices.Count > 65535)
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0, true);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static bool TryExtractPrimaryVerticalRun(
        Color32[] pixels,
        int width,
        int height,
        int xStart,
        int xEnd,
        float alphaThreshold,
        int minRunPixels,
        out int minY,
        out int maxY)
    {
        minY = 0;
        maxY = 0;

        var occupied = new bool[height];
        for (int y = 0; y < height; y++)
        {
            for (int x = xStart; x < xEnd; x++)
            {
                var alpha = pixels[y * width + x].a / 255f;
                if (alpha >= alphaThreshold)
                {
                    occupied[y] = true;
                    break;
                }
            }
        }

        var bestRunLength = 0;
        var bestStart = -1;
        var runStart = -1;

        for (int y = 0; y <= height; y++)
        {
            var filled = y < height && occupied[y];
            if (filled)
            {
                if (runStart < 0)
                    runStart = y;
                continue;
            }

            if (runStart < 0)
                continue;

            var runEnd = y - 1;
            var runLength = runEnd - runStart + 1;
            if (runLength > bestRunLength)
            {
                bestRunLength = runLength;
                bestStart = runStart;
                maxY = runEnd;
            }
            runStart = -1;
        }

        if (bestStart < 0 || bestRunLength < minRunPixels)
            return false;

        minY = bestStart;
        return true;
    }

    static bool TryFindLargestValidRange(bool[] valid, out int bestStart, out int bestEnd)
    {
        bestStart = -1;
        bestEnd = -1;

        var runStart = -1;
        for (int i = 0; i <= valid.Length; i++)
        {
            var isValid = i < valid.Length && valid[i];
            if (isValid)
            {
                if (runStart < 0)
                    runStart = i;
                continue;
            }

            if (runStart < 0)
                continue;

            var runEnd = i - 1;
            if (bestStart < 0 || runEnd - runStart > bestEnd - bestStart)
            {
                bestStart = runStart;
                bestEnd = runEnd;
            }
            runStart = -1;
        }

        return bestStart >= 0 && bestEnd >= bestStart;
    }

    static void SmoothInPlace(float[] values)
    {
        var scratch = new float[values.Length];
        for (int pass = 0; pass < SmoothPasses; pass++)
        {
            for (int i = 0; i < values.Length; i++)
            {
                var acc = 0f;
                var count = 0;
                for (int j = Mathf.Max(0, i - SmoothRadius); j <= Mathf.Min(values.Length - 1, i + SmoothRadius); j++)
                {
                    acc += values[j];
                    count++;
                }
                scratch[i] = acc / count;
            }

            for (int i = 0; i < values.Length; i++)
            {
                values[i] = scratch[i];
            }
        }
    }

    static int AddVertex(
        List<Vector3> vertices,
        List<Vector2> uvs,
        Vector3 position,
        Vector2 uv)
    {
        var index = vertices.Count;
        vertices.Add(position);
        uvs.Add(uv);
        return index;
    }

    static void AddQuad(List<int> triangles, int a, int b, int c, int d)
    {
        triangles.Add(a);
        triangles.Add(b);
        triangles.Add(c);

        triangles.Add(a);
        triangles.Add(c);
        triangles.Add(d);
    }
}
