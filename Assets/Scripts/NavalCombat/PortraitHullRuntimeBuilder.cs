using System;
using System.Collections.Generic;
using UnityEngine;

internal static class PortraitHullRuntimeBuilder
{
    static readonly Dictionary<Texture2D, Mesh> meshCache = new();
    static readonly HashSet<Texture2D> failedTextures = new();

    public static Mesh GetOrBuildNormalizedHullMesh(Texture2D portraitTex, float alphaThreshold = 0.1f)
    {
        if (portraitTex == null)
            return null;

        if (meshCache.TryGetValue(portraitTex, out var mesh))
            return mesh;

        if (failedTextures.Contains(portraitTex))
            return null;

        try
        {
            mesh = BuildNormalizedHullMesh(portraitTex, alphaThreshold);
            if (mesh != null)
            {
                meshCache[portraitTex] = mesh;
                return mesh;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to build runtime hull mesh for texture {portraitTex.name}: {ex.Message}");
        }

        failedTextures.Add(portraitTex);
        return null;
    }

    static Mesh BuildNormalizedHullMesh(Texture2D portraitTex, float alphaThreshold)
    {
        // Tight sprite mesh gives us a contour-following cap instead of the previous voxelized squares.
        Sprite sprite = null;
        try
        {
            sprite = Sprite.Create(
                portraitTex,
                new Rect(0f, 0f, portraitTex.width, portraitTex.height),
                new Vector2(0.5f, 0.5f),
                1f,
                0,
                SpriteMeshType.Tight,
                Vector4.zero,
                true
            );

            var mesh = BuildFromSprite(sprite, portraitTex, alphaThreshold);
            if (mesh != null)
                return mesh;
        }
        finally
        {
            if (sprite != null)
                UnityEngine.Object.Destroy(sprite);
        }

        return null;
    }

    static Mesh BuildFromSprite(Sprite sprite, Texture2D portraitTex, float alphaThreshold)
    {
        if (sprite == null)
            return null;

        var spriteVertices = sprite.vertices;
        var spriteTriangles = sprite.triangles;
        if (spriteVertices == null || spriteVertices.Length < 3 || spriteTriangles == null || spriteTriangles.Length < 3)
            return null;

        var contour = GetLargestPhysicsShape(sprite);
        if (contour == null || contour.Count < 3)
            return null;

        var width = Mathf.Max(1f, portraitTex.width);
        var height = Mathf.Max(1f, portraitTex.height);

        var vertices = new List<Vector3>(spriteVertices.Length * 2 + contour.Count * 4);
        var uvs = new List<Vector2>(spriteVertices.Length * 2 + contour.Count * 4);
        var triangles = new List<int>(spriteTriangles.Length * 2 + contour.Count * 6);

        var topStart = vertices.Count;
        for (int i = 0; i < spriteVertices.Length; i++)
        {
            var vertex = spriteVertices[i];
            vertices.Add(ToNormalizedVertex(vertex, width, height, 0f));
            uvs.Add(new Vector2(vertex.x / width + 0.5f, vertex.y / height + 0.5f));
        }

        for (int i = 0; i < spriteTriangles.Length; i += 3)
        {
            triangles.Add(topStart + spriteTriangles[i]);
            triangles.Add(topStart + spriteTriangles[i + 1]);
            triangles.Add(topStart + spriteTriangles[i + 2]);
        }

        var bottomStart = vertices.Count;
        for (int i = 0; i < spriteVertices.Length; i++)
        {
            var vertex = spriteVertices[i];
            vertices.Add(ToNormalizedVertex(vertex, width, height, 1f));
            uvs.Add(new Vector2(vertex.x / width + 0.5f, vertex.y / height + 0.5f));
        }

        for (int i = 0; i < spriteTriangles.Length; i += 3)
        {
            triangles.Add(bottomStart + spriteTriangles[i + 2]);
            triangles.Add(bottomStart + spriteTriangles[i + 1]);
            triangles.Add(bottomStart + spriteTriangles[i]);
        }

        if (SignedArea(contour) < 0f)
            contour.Reverse();

        var sideUvCursor = 0f;
        for (int i = 0; i < contour.Count; i++)
        {
            var a = contour[i];
            var b = contour[(i + 1) % contour.Count];

            var aTop = ToNormalizedVertex(a, width, height, 0f);
            var bTop = ToNormalizedVertex(b, width, height, 0f);
            var bBottom = ToNormalizedVertex(b, width, height, 1f);
            var aBottom = ToNormalizedVertex(a, width, height, 1f);

            var segmentLength = Vector2.Distance(
                new Vector2(aTop.x, aTop.y),
                new Vector2(bTop.x, bTop.y)
            );

            sideUvCursor = AddSideQuad(
                vertices,
                uvs,
                triangles,
                aTop,
                bTop,
                bBottom,
                aBottom,
                Mathf.Max(segmentLength, 0.0001f),
                sideUvCursor
            );
        }

        if (vertices.Count == 0 || triangles.Count == 0)
            return null;

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

    static List<Vector2> GetLargestPhysicsShape(Sprite sprite)
    {
        var shapeCount = sprite.GetPhysicsShapeCount();
        if (shapeCount <= 0)
            return null;

        List<Vector2> largest = null;
        var workingShape = new List<Vector2>();
        var largestArea = 0f;

        for (int i = 0; i < shapeCount; i++)
        {
            workingShape.Clear();
            sprite.GetPhysicsShape(i, workingShape);
            if (workingShape.Count < 3)
                continue;

            var area = Mathf.Abs(SignedArea(workingShape));
            if (area <= largestArea)
                continue;

            largestArea = area;
            largest = new List<Vector2>(workingShape);
        }

        return largest;
    }

    static Vector3 ToNormalizedVertex(Vector2 spriteVertex, float width, float height, float z)
    {
        return new Vector3(
            spriteVertex.x / width,
            spriteVertex.y / height,
            z
        );
    }

    static float SignedArea(IReadOnlyList<Vector2> polygon)
    {
        var area = 0f;
        for (int i = 0; i < polygon.Count; i++)
        {
            var a = polygon[i];
            var b = polygon[(i + 1) % polygon.Count];
            area += a.x * b.y - b.x * a.y;
        }
        return area * 0.5f;
    }

    static float AddSideQuad(
        List<Vector3> vertices,
        List<Vector2> uvs,
        List<int> triangles,
        Vector3 a,
        Vector3 b,
        Vector3 c,
        Vector3 d,
        float edgeLengthNormalized,
        float uStart)
    {
        var start = vertices.Count;
        var uEnd = uStart + edgeLengthNormalized;

        vertices.Add(a);
        vertices.Add(b);
        vertices.Add(c);
        vertices.Add(d);

        uvs.Add(new Vector2(uStart, 1f));
        uvs.Add(new Vector2(uEnd, 1f));
        uvs.Add(new Vector2(uEnd, 0f));
        uvs.Add(new Vector2(uStart, 0f));

        triangles.Add(start + 0);
        triangles.Add(start + 1);
        triangles.Add(start + 2);
        triangles.Add(start + 0);
        triangles.Add(start + 2);
        triangles.Add(start + 3);
        return uEnd;
    }
}
