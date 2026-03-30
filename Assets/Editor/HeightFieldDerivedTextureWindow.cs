using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public enum ShoreFieldExportMode
{
    Packed,
    Separate
}

public class HeightFieldDerivedTextureWindow : EditorWindow
{
    const float GradientMagnitudeEpsilon = 1e-5f;

    Texture2D sourceHeightTexture;
    float landThreshold = 0f;
    float distanceEncodeMaxPixels = 255f;
    ShoreFieldExportMode exportMode = ShoreFieldExportMode.Packed;
    bool overwriteExisting;

    bool[] landMask;
    double[] distanceField;
    double[] oneDimensionalInput;
    double[] oneDimensionalOutput;
    int[] envelopeIndices;
    double[] envelopeIntersections;

    [MenuItem("Custom/GIS/Generate Shore Distance & Gradient")]
    public static void ShowWindow()
    {
        var window = GetWindow<HeightFieldDerivedTextureWindow>();
        window.titleContent = new GUIContent("Shore Field");
        window.minSize = new Vector2(420f, 220f);
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("Height Texture To Shore Field", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        sourceHeightTexture = (Texture2D)EditorGUILayout.ObjectField("Source Height Texture", sourceHeightTexture, typeof(Texture2D), false);
        landThreshold = EditorGUILayout.FloatField("Land Threshold", landThreshold);
        distanceEncodeMaxPixels = Mathf.Max(1f, EditorGUILayout.FloatField("Distance Encode Max Pixels", distanceEncodeMaxPixels));
        exportMode = (ShoreFieldExportMode)EditorGUILayout.EnumPopup("Export Mode", exportMode);
        overwriteExisting = EditorGUILayout.Toggle("Overwrite Existing", overwriteExisting);

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            exportMode == ShoreFieldExportMode.Packed
                ? "Exports packed preview PNG + metadata JSON. Distance PNG encoding preserves near-shore precision by linearly encoding only the first N pixels and saturating beyond that range."
                : "Exports separate preview PNGs. Distance PNG encoding preserves near-shore precision by linearly encoding only the first N pixels and saturating beyond that range.",
            MessageType.Info);

        using (new EditorGUI.DisabledScope(sourceHeightTexture == null))
        {
            if (GUILayout.Button("Generate"))
            {
                Generate();
            }
        }
    }

    void Generate()
    {
        try
        {
            if (!TryValidateSource(out var sourcePath))
            {
                return;
            }

            var directory = Path.GetDirectoryName(sourcePath)?.Replace("\\", "/");
            var fileName = Path.GetFileNameWithoutExtension(sourcePath);
            if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileName))
            {
                EditorUtility.DisplayDialog("Generate Shore Field", "Failed to resolve the source texture asset path.", "OK");
                return;
            }

            var width = sourceHeightTexture.width;
            var height = sourceHeightTexture.height;
            var pixelCount = checked(width * height);

            var packedPath = $"{directory}/{fileName}_shoreField.png";
            var packedJsonPath = $"{directory}/{fileName}_shoreField.json";
            var distancePath = $"{directory}/{fileName}_shoreDistance.png";
            var gradientPath = $"{directory}/{fileName}_shoreGradient.png";

            if (!ValidateOutputTargets(packedPath, packedJsonPath, distancePath, gradientPath))
            {
                return;
            }

            EnsureWorkingBuffers(pixelCount, width, height);

            EditorUtility.DisplayProgressBar("Generate Shore Field", "Reading source height texture...", 0.05f);
            if (!BuildLandMask(sourceHeightTexture, width, height, landThreshold))
            {
                EditorUtility.DisplayDialog("Generate Shore Field", "The selected texture contains no land pixels above the threshold.", "OK");
                return;
            }

            EditorUtility.DisplayProgressBar("Generate Shore Field", "Computing exact Euclidean distance field...", 0.2f);
            BuildDistanceField(width, height);

            EditorUtility.DisplayProgressBar("Generate Shore Field", "Converting distances...", 0.8f);
            ConvertSquaredDistancesToDistances(pixelCount);
            var maxDistance = GetMaxDistance(pixelCount);
            var distanceEncodeMax = Math.Min(maxDistance, distanceEncodeMaxPixels);

            if (overwriteExisting)
            {
                DeleteExistingAssets(packedPath, packedJsonPath, distancePath, gradientPath);
            }

            UnityEngine.Object primaryAsset;
            EditorUtility.DisplayProgressBar("Generate Shore Field", "Writing output textures...", 0.9f);
            if (exportMode == ShoreFieldExportMode.Packed)
            {
                WritePackedTexturePng(width, height, packedPath, distanceEncodeMax);
                WritePackedMetadataJson(width, height, packedJsonPath, (float)maxDistance, (float)distanceEncodeMax);
                primaryAsset = AssetDatabase.LoadAssetAtPath<Texture2D>(packedPath);
            }
            else
            {
                WriteDistanceTexturePng(width, height, distancePath, distanceEncodeMax);
                WriteGradientTexturePng(width, height, gradientPath);
                primaryAsset = AssetDatabase.LoadAssetAtPath<Texture2D>(distancePath);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorGUIUtility.PingObject(primaryAsset);
            Selection.activeObject = primaryAsset;
            EditorUtility.DisplayDialog("Generate Shore Field", "Shore field generation completed.", "OK");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            EditorUtility.DisplayDialog("Generate Shore Field", $"Generation failed:\n{ex.Message}", "OK");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    bool TryValidateSource(out string sourcePath)
    {
        sourcePath = null;
        if (sourceHeightTexture == null)
        {
            EditorUtility.DisplayDialog("Generate Shore Field", "Select a source height texture first.", "OK");
            return false;
        }

        if (!sourceHeightTexture.isReadable)
        {
            EditorUtility.DisplayDialog("Generate Shore Field", "The source height texture must be readable.", "OK");
            return false;
        }

        if (sourceHeightTexture.width <= 0 || sourceHeightTexture.height <= 0)
        {
            EditorUtility.DisplayDialog("Generate Shore Field", "The source height texture has an invalid size.", "OK");
            return false;
        }

        sourcePath = AssetDatabase.GetAssetPath(sourceHeightTexture);
        if (string.IsNullOrEmpty(sourcePath))
        {
            EditorUtility.DisplayDialog("Generate Shore Field", "The source height texture must be an imported Unity asset.", "OK");
            return false;
        }

        return true;
    }

    bool ValidateOutputTargets(string packedPath, string packedJsonPath, string distancePath, string gradientPath)
    {
        if (exportMode == ShoreFieldExportMode.Packed)
        {
            if (AssetExistsAndCannotOverwrite(packedPath) || AssetExistsAndCannotOverwrite(packedJsonPath))
            {
                return false;
            }

            return true;
        }

        if (AssetExistsAndCannotOverwrite(distancePath))
        {
            return false;
        }

        if (AssetExistsAndCannotOverwrite(gradientPath))
        {
            return false;
        }

        return true;
    }

    bool AssetExistsAndCannotOverwrite(string assetPath)
    {
        if (overwriteExisting || !AssetPathExists(assetPath))
        {
            return false;
        }

        EditorUtility.DisplayDialog("Generate Shore Field", $"Target asset already exists:\n{assetPath}\n\nEnable Overwrite Existing to replace it.", "OK");
        return true;
    }

    void DeleteExistingAssets(string packedPath, string packedJsonPath, string distancePath, string gradientPath)
    {
        if (exportMode == ShoreFieldExportMode.Packed)
        {
            DeleteAssetIfExists(packedPath);
            DeleteAssetIfExists(packedJsonPath);
            return;
        }

        DeleteAssetIfExists(distancePath);
        DeleteAssetIfExists(gradientPath);
    }

    static bool AssetPathExists(string assetPath)
    {
        return File.Exists(AssetPathToAbsolutePath(assetPath));
    }

    static void DeleteAssetIfExists(string assetPath)
    {
        if (AssetPathExists(assetPath))
        {
            AssetDatabase.DeleteAsset(assetPath);
        }
    }

    static string AssetPathToAbsolutePath(string assetPath)
    {
        return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), assetPath));
    }

    void EnsureWorkingBuffers(int pixelCount, int width, int height)
    {
        if (landMask == null || landMask.Length != pixelCount)
        {
            landMask = new bool[pixelCount];
        }

        if (distanceField == null || distanceField.Length != pixelCount)
        {
            distanceField = new double[pixelCount];
        }

        var maxDimension = Mathf.Max(width, height);
        if (oneDimensionalInput == null || oneDimensionalInput.Length != maxDimension)
        {
            oneDimensionalInput = new double[maxDimension];
            oneDimensionalOutput = new double[maxDimension];
            envelopeIndices = new int[maxDimension];
            envelopeIntersections = new double[maxDimension + 1];
        }
    }

    bool BuildLandMask(Texture2D texture, int width, int height, float threshold)
    {
        Array.Clear(landMask, 0, landMask.Length);
        var hasLand = false;

        if (TryBuildLandMaskFromRawUShort(texture, width, height, threshold, ref hasLand))
        {
            return hasLand;
        }

        var pixels = texture.GetPixels();
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var index = y * width + x;
                var heightValue = pixels[index].grayscale;
                var isLand = heightValue > threshold;
                landMask[index] = isLand;
                hasLand |= isLand;
            }
        }

        return hasLand;
    }

    bool TryBuildLandMaskFromRawUShort(Texture2D texture, int width, int height, float threshold, ref bool hasLand)
    {
        if (!IsRawUShortTexture(texture, width, height))
        {
            return false;
        }

        var rawData = texture.GetRawTextureData<ushort>();
        for (var i = 0; i < rawData.Length; i++)
        {
            var isLand = rawData[i] > threshold;
            landMask[i] = isLand;
            hasLand |= isLand;
        }

        return true;
    }

    static bool IsRawUShortTexture(Texture2D texture, int width, int height)
    {
        if (texture.mipmapCount != 1)
        {
            return false;
        }

        return texture.format == TextureFormat.R16;
    }

    void BuildDistanceField(int width, int height)
    {
        var infiniteDistance = GetDistanceTransformSentinelSquared(width, height);
        for (var i = 0; i < distanceField.Length; i++)
        {
            distanceField[i] = landMask[i] ? 0d : infiniteDistance;
        }

        for (var x = 0; x < width; x++)
        {
            for (var y = 0; y < height; y++)
            {
                oneDimensionalInput[y] = distanceField[y * width + x];
            }

            DistanceTransform1D(oneDimensionalInput, height);

            for (var y = 0; y < height; y++)
            {
                distanceField[y * width + x] = oneDimensionalOutput[y];
            }

            if ((x & 63) == 0)
            {
                var progress = Mathf.Lerp(0.2f, 0.5f, x / Mathf.Max(1f, width - 1f));
                EditorUtility.DisplayProgressBar("Generate Shore Field", "Computing exact Euclidean distance field...", progress);
            }
        }

        for (var y = 0; y < height; y++)
        {
            var rowStart = y * width;
            for (var x = 0; x < width; x++)
            {
                oneDimensionalInput[x] = distanceField[rowStart + x];
            }

            DistanceTransform1D(oneDimensionalInput, width);

            for (var x = 0; x < width; x++)
            {
                distanceField[rowStart + x] = oneDimensionalOutput[x];
            }

            if ((y & 63) == 0)
            {
                var progress = Mathf.Lerp(0.5f, 0.8f, y / Mathf.Max(1f, height - 1f));
                EditorUtility.DisplayProgressBar("Generate Shore Field", "Computing exact Euclidean distance field...", progress);
            }
        }
    }

    static double GetDistanceTransformSentinelSquared(int width, int height)
    {
        var maxDx = Math.Max(0, width - 1);
        var maxDy = Math.Max(0, height - 1);
        return (double)maxDx * maxDx + (double)maxDy * maxDy + 1d;
    }

    void DistanceTransform1D(double[] input, int length)
    {
        var k = 0;
        envelopeIndices[0] = 0;
        envelopeIntersections[0] = double.NegativeInfinity;
        envelopeIntersections[1] = double.PositiveInfinity;

        for (var q = 1; q < length; q++)
        {
            var s = Intersection(q, envelopeIndices[k], input);
            while (s <= envelopeIntersections[k])
            {
                k--;
                s = Intersection(q, envelopeIndices[k], input);
            }

            k++;
            envelopeIndices[k] = q;
            envelopeIntersections[k] = s;
            envelopeIntersections[k + 1] = double.PositiveInfinity;
        }

        k = 0;
        for (var q = 0; q < length; q++)
        {
            while (envelopeIntersections[k + 1] < q)
            {
                k++;
            }

            var diff = q - envelopeIndices[k];
            oneDimensionalOutput[q] = diff * diff + input[envelopeIndices[k]];
        }
    }

    static double Intersection(int q, int vk, double[] input)
    {
        return ((input[q] + (double)q * q) - (input[vk] + (double)vk * vk)) / (2d * (q - vk));
    }

    void ConvertSquaredDistancesToDistances(int pixelCount)
    {
        for (var i = 0; i < pixelCount; i++)
        {
            distanceField[i] = Math.Sqrt(distanceField[i]);
        }
    }

    double GetMaxDistance(int pixelCount)
    {
        var maxDistance = 0d;
        for (var i = 0; i < pixelCount; i++)
        {
            if (distanceField[i] > maxDistance)
            {
                maxDistance = distanceField[i];
            }
        }

        return maxDistance;
    }

    void WritePackedTexturePng(int width, int height, string assetPath, double maxDistance)
    {
        var pixels = new Color32[checked(width * height)];
        FillPackedTexturePixels(pixels, width, height, maxDistance);
        WritePngTexture(assetPath, width, height, pixels, TextureFormat.RGB24);
    }

    void WriteDistanceTexturePng(int width, int height, string assetPath, double maxDistance)
    {
        var pixels = new Color32[checked(width * height)];
        FillDistanceTexturePixels(pixels, width, height, maxDistance);
        WritePngTexture(assetPath, width, height, pixels, TextureFormat.RGB24);
    }

    void WriteGradientTexturePng(int width, int height, string assetPath)
    {
        var pixels = new Color32[checked(width * height)];
        FillGradientTexturePixels(pixels, width, height);
        WritePngTexture(assetPath, width, height, pixels, TextureFormat.RGB24);
    }

    void FillPackedTexturePixels(Color32[] pixels, int width, int height, double maxDistance)
    {
        for (var y = 0; y < height; y++)
        {
            var rowStart = y * width;
            for (var x = 0; x < width; x++)
            {
                var index = rowStart + x;
                var gradient = ComputeGradient(x, y, width, height, index);
                var distanceByte = EncodeNormalizedByte(distanceField[index], maxDistance);
                pixels[index] = new Color32(
                    distanceByte,
                    EncodeSignedUnitByte(gradient.x),
                    EncodeSignedUnitByte(gradient.y),
                    255);
            }

            if ((y & 127) == 0)
            {
                var progress = Mathf.Lerp(0.9f, 0.98f, y / Mathf.Max(1f, height - 1f));
                EditorUtility.DisplayProgressBar("Generate Shore Field", "Writing output textures...", progress);
            }
        }
    }

    void FillDistanceTexturePixels(Color32[] pixels, int width, int height, double maxDistance)
    {
        for (var y = 0; y < height; y++)
        {
            var rowStart = y * width;
            for (var x = 0; x < width; x++)
            {
                var index = rowStart + x;
                var distanceByte = EncodeNormalizedByte(distanceField[index], maxDistance);
                pixels[index] = new Color32(
                    distanceByte,
                    distanceByte,
                    distanceByte,
                    255);
            }

            if ((y & 127) == 0)
            {
                var progress = Mathf.Lerp(0.9f, 0.98f, y / Mathf.Max(1f, height - 1f));
                EditorUtility.DisplayProgressBar("Generate Shore Field", "Writing output textures...", progress);
            }
        }
    }

    void FillGradientTexturePixels(Color32[] pixels, int width, int height)
    {
        var neutralBlue = (byte)128;
        for (var y = 0; y < height; y++)
        {
            var rowStart = y * width;
            for (var x = 0; x < width; x++)
            {
                var index = rowStart + x;
                var gradient = ComputeGradient(x, y, width, height, index);

                pixels[index] = new Color32(
                    EncodeSignedUnitByte(gradient.x),
                    EncodeSignedUnitByte(gradient.y),
                    neutralBlue,
                    255);
            }

            if ((y & 127) == 0)
            {
                var progress = Mathf.Lerp(0.9f, 0.98f, y / Mathf.Max(1f, height - 1f));
                EditorUtility.DisplayProgressBar("Generate Shore Field", "Writing output textures...", progress);
            }
        }
    }

    void WritePngTexture(string assetPath, int width, int height, Color32[] pixels, TextureFormat textureFormat)
    {
        var texture = new Texture2D(width, height, textureFormat, false, true)
        {
            name = Path.GetFileNameWithoutExtension(assetPath),
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Point
        };
        texture.SetPixels32(pixels);
        texture.Apply(false, false);

        var bytes = texture.EncodeToPNG();
        File.WriteAllBytes(AssetPathToAbsolutePath(assetPath), bytes);
        DestroyImmediate(texture);
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        ConfigureTextureImporter(assetPath);
    }

    void WritePackedMetadataJson(int width, int height, string assetPath, float maxDistance, float distanceEncodeMax)
    {
        var metadata = new ShoreFieldPackedMetadata()
        {
            exportMode = "Packed",
            width = width,
            height = height,
            landThreshold = landThreshold,
            maxDistancePixels = maxDistance,
            distanceEncodeMaxPixels = distanceEncodeMax,
            distanceEncoding = "8-bit linear encoding over [0, distanceEncodeMaxPixels]; values above that range are clamped to 255",
            gradientEncoding = "signed unit gradient mapped to 0..1; decode by value/255 * 2 - 1"
        };

        File.WriteAllText(AssetPathToAbsolutePath(assetPath), JsonUtility.ToJson(metadata, true));
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
    }

    static byte EncodeNormalizedByte(double value, double maxValue)
    {
        if (maxValue <= 0d)
        {
            return 0;
        }

        var normalized = Math.Min(value, maxValue) / maxValue * 255d;
        return (byte)Mathf.Clamp((int)Math.Round(normalized), 0, 255);
    }

    static byte EncodeSignedUnitByte(float value)
    {
        var normalized = value * 0.5f + 0.5f;
        return (byte)Mathf.Clamp(Mathf.RoundToInt(normalized * 255f), 0, 255);
    }

    static void ConfigureTextureImporter(string assetPath)
    {
        if (AssetImporter.GetAtPath(assetPath) is not TextureImporter importer)
        {
            return;
        }

        importer.textureType = TextureImporterType.Default;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = false;
        importer.sRGBTexture = false;
        importer.mipmapEnabled = false;
        importer.isReadable = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.SaveAndReimport();
    }

    Vector2 ComputeGradient(int x, int y, int width, int height, int index)
    {
        if (landMask[index])
        {
            return Vector2.zero;
        }

        var left = distanceField[y * width + Mathf.Max(0, x - 1)];
        var right = distanceField[y * width + Mathf.Min(width - 1, x + 1)];
        var down = distanceField[Mathf.Max(0, y - 1) * width + x];
        var up = distanceField[Mathf.Min(height - 1, y + 1) * width + x];

        var dx = x == 0 ? right - distanceField[index]
            : x == width - 1 ? distanceField[index] - left
            : (right - left) * 0.5d;
        var dy = y == 0 ? up - distanceField[index]
            : y == height - 1 ? distanceField[index] - down
            : (up - down) * 0.5d;

        var gradient = new Vector2((float)dx, (float)dy);
        if (gradient.sqrMagnitude <= GradientMagnitudeEpsilon * GradientMagnitudeEpsilon)
        {
            return Vector2.zero;
        }

        return gradient.normalized;
    }

    [Serializable]
    class ShoreFieldPackedMetadata
    {
        public string exportMode;
        public int width;
        public int height;
        public float landThreshold;
        public float maxDistancePixels;
        public float distanceEncodeMaxPixels;
        public string distanceEncoding;
        public string gradientEncoding;
    }
}
