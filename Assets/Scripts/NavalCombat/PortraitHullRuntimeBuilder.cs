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
    const int SplineSubdivision = 4;
    const int MinSplineSamples = 48;

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

        var splineSampleCount = Mathf.Max(MinSplineSamples, (rangeLength - 1) * SplineSubdivision + 1);
        var splineXs = new float[splineSampleCount];
        var splineCenters = ResampleCatmullRom(centers, splineSampleCount);
        var splineHalfWidths = ResampleCatmullRom(halfWidths, splineSampleCount);

        var minX = xs[0];
        var maxX = xs[xs.Length - 1];
        for (int i = 0; i < splineSampleCount; i++)
        {
            var t = splineSampleCount <= 1 ? 0f : (float)i / (splineSampleCount - 1);
            splineXs[i] = Mathf.Lerp(minX, maxX, t);
            splineHalfWidths[i] = Mathf.Max(splineHalfWidths[i], MinHalfWidth);
        }

        var vertices = new List<Vector3>(splineSampleCount * 4);
        var uvs = new List<Vector2>(splineSampleCount * 4);
        var triangles = new List<int>((splineSampleCount - 1) * 18 + 12);

        var deckStarboard = new int[splineSampleCount];
        var deckPort = new int[splineSampleCount];
        var keelStarboard = new int[splineSampleCount];
        var keelPort = new int[splineSampleCount];

        for (int i = 0; i < splineSampleCount; i++)
        {
            var x = splineXs[i];
            var center = splineCenters[i];
            var halfWidth = splineHalfWidths[i];
            var u = splineSampleCount <= 1 ? 0f : (float)i / (splineSampleCount - 1);

            deckStarboard[i] = AddVertex(vertices, uvs, new Vector3(x, center - halfWidth, 0f), new Vector2(u, 1f));
            deckPort[i] = AddVertex(vertices, uvs, new Vector3(x, center + halfWidth, 0f), new Vector2(u, 1f));
            keelStarboard[i] = AddVertex(vertices, uvs, new Vector3(x, center - halfWidth, 1f), new Vector2(u, 0f));
            keelPort[i] = AddVertex(vertices, uvs, new Vector3(x, center + halfWidth, 1f), new Vector2(u, 0f));
        }

        for (int i = 0; i < splineSampleCount - 1; i++)
        {
            AddQuad(triangles, deckPort[i], deckPort[i + 1], keelPort[i + 1], keelPort[i]);
            AddQuad(triangles, deckStarboard[i + 1], deckStarboard[i], keelStarboard[i], keelStarboard[i + 1]);
            AddQuad(triangles, keelStarboard[i], keelPort[i], keelPort[i + 1], keelStarboard[i + 1]);
        }

        AddQuad(triangles, deckStarboard[0], deckPort[0], keelPort[0], keelStarboard[0]);
        AddQuad(triangles, deckPort[splineSampleCount - 1], deckStarboard[splineSampleCount - 1], keelStarboard[splineSampleCount - 1], keelPort[splineSampleCount - 1]);

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

    static float[] ResampleCatmullRom(float[] source, int sampleCount)
    {
        var result = new float[sampleCount];
        if (source.Length == 0)
            return result;
        if (source.Length == 1)
        {
            for (int i = 0; i < sampleCount; i++)
            {
                result[i] = source[0];
            }
            return result;
        }

        for (int i = 0; i < sampleCount; i++)
        {
            var tGlobal = sampleCount <= 1 ? 0f : (float)i / (sampleCount - 1) * (source.Length - 1);
            var segment = Mathf.Clamp(Mathf.FloorToInt(tGlobal), 0, source.Length - 2);
            var t = tGlobal - segment;

            var p0 = source[Mathf.Max(segment - 1, 0)];
            var p1 = source[segment];
            var p2 = source[segment + 1];
            var p3 = source[Mathf.Min(segment + 2, source.Length - 1)];

            result[i] = CatmullRom(p0, p1, p2, p3, t);
        }

        result[0] = source[0];
        result[sampleCount - 1] = source[source.Length - 1];
        return result;
    }

    static float CatmullRom(float p0, float p1, float p2, float p3, float t)
    {
        var t2 = t * t;
        var t3 = t2 * t;
        return 0.5f * (
            (2f * p1) +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3
        );
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
