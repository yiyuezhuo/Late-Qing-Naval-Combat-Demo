using UnityEngine;
using NavalCombatCore;
using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.IO;

public struct ShoreFieldSample
{
    public float distancePixels;
    public Vector2 gradient;
}

// Elevation Service Dependency Injector
public class ElevationProvider : MonoBehaviour, IElevationProvider
{
    const string ROIShoreRuntimeMagic = "SFD1";
    const float GradientDecodeScale = 127f;

    public Texture2D baseHeightTexture;
    public Texture2D roiHeightTexture;

    // _ROILatDeg0 ("ROI Latitude Deg 0", Float) = 15 // 30
    // _ROILatDeg1 ("ROI Latitude Deg 1", Float) = 55 // 41
    // _ROILonDeg0 ("ROI Longitude Deg 0", Float) = 105 // 116
    // _ROILonDeg1 ("ROI Longitude Deg 1", Float) = 146 // 131
    public float roiLatitudeDeg0 = 15;
    public float roiLatitudeDeg1 = 55;
    public float roiLongitudeDeg0 = 105;
    public float roiLongitudeDeg1 = 146;
    public bool useROI = true;

    Unity.Collections.NativeArray<ushort> baseHeightTextureRawArray;
    Unity.Collections.NativeArray<ushort> roiHeightTextureRawArray;

    public MeshRenderer meshRenderer;

    public AssetReference baseHeightTextureAssetReference;
    public AssetReference roiHeightTextureAssetReference;
    public TextAsset roiShoreRuntimeBytes;

    ushort[] roiShoreRuntimeDistanceData;
    sbyte[] roiShoreRuntimeGradientXData;
    sbyte[] roiShoreRuntimeGradientYData;
    int roiShoreRuntimeWidth;
    int roiShoreRuntimeHeight;
    float roiShoreRuntimeLandThreshold;
    float roiShoreRuntimeMaxDistance;
    bool roiShoreRuntimeLoaded;
    bool roiShoreRuntimeDimensionsValid = true;
    bool roiShoreRuntimeWarningLogged;

    public void Awake()
    {
        // Assume placeholder existence of placeholder texture
        baseHeightTextureRawArray = baseHeightTexture.GetRawTextureData<ushort>();
        roiHeightTextureRawArray = roiHeightTexture.GetRawTextureData<ushort>();

        // ServiceLocator.Register<IElevationProvider>(this);
        ElevationService.Instance.elevationProvider = this;

        var testLatLonList = new List<LatLon>()
        {
            new(35.6764f, 139.6500f), // Tokyo
            new(39.15f, 123.73f) // The Location of the Battle of Yalu river
        };

        foreach (var latLon in testLatLonList)
        {
            Debug.Log(latLon + ": " + ElevationService.Instance.GetElevation(latLon));
        }

        AssetReferenceManager.Instance.LoadTexture2D(baseHeightTextureAssetReference, SetBaseTexture);
        // {
        //     AsyncOperationHandle handle = baseHeightTextureAssetReference.LoadAssetAsync<Texture2D>();
        //     handle.Completed += (AsyncOperationHandle obj) =>
        //     {
        //         if (obj.Status == AsyncOperationStatus.Succeeded)
        //         {
        //             Debug.Log($"AssetReference {baseHeightTextureAssetReference.RuntimeKey} Success to load.");
        //             SetBaseTexture(baseHeightTextureAssetReference.Asset as Texture2D);
        //         }
        //         else
        //         {
        //             Debug.LogError($"AssetReference {baseHeightTextureAssetReference.RuntimeKey} failed to load.");
        //         }
        //     };
        // }

        AssetReferenceManager.Instance.LoadTexture2D(roiHeightTextureAssetReference, SetROITexture);
        // {
        //     AsyncOperationHandle handle = roiHeightTextureAssetReference.LoadAssetAsync<Texture2D>();
        //     handle.Completed += (AsyncOperationHandle obj) =>
        //     {
        //         if (obj.Status == AsyncOperationStatus.Succeeded)
        //         {
        //             Debug.Log($"AssetReference {roiHeightTextureAssetReference.RuntimeKey} Success to load.");
        //             SetROITexture(roiHeightTextureAssetReference.Asset as Texture2D);
        //         }
        //         else
        //         {
        //             Debug.LogError($"AssetReference {roiHeightTextureAssetReference.RuntimeKey} failed to load.");
        //         }
        //     };
        // }

        TryLoadROIShoreRuntime();
        ValidateROIShoreRuntimeDimensions();

        // FetchHighPrecisionTextures();
    }

    private void OnDestroy()
    {
        if (baseHeightTextureAssetReference != null && baseHeightTextureAssetReference.IsValid())
            baseHeightTextureAssetReference.ReleaseAsset();

        if (roiHeightTextureAssetReference != null && roiHeightTextureAssetReference.IsValid())
            roiHeightTextureAssetReference.ReleaseAsset();
    }

    // void FetchHighPrecisionTextures() // TODO: This method should not be placed in "ElevationProvider" though.
    // {
    //     UnityWebRequestImageReader.Instance.RequestIfNotRequestedYet(new ImageFetchTask()
    //     {
    //         path = Application.streamingAssetsPath + "/Pictures/GIS/full_uint16_16384x8192.png",
    //         continueCallback = SetBaseTexture
    //     });

    //     UnityWebRequestImageReader.Instance.RequestIfNotRequestedYet(new ImageFetchTask()
    //     {
    //         path = Application.streamingAssetsPath + "/Pictures/GIS/aoi_105_146_15_55_uint16_9840x9600.png",
    //         continueCallback = SetROITexture
    //     });
    // }

    public void SetBaseTexture(Texture2D baseHeightTexture)
    {
        this.baseHeightTexture = baseHeightTexture;
        meshRenderer.material.SetTexture("_HeightTex", baseHeightTexture);
        baseHeightTextureRawArray = baseHeightTexture.GetRawTextureData<ushort>();
    }

    public void SetROITexture(Texture2D roiHeightTexture)
    {
        this.roiHeightTexture = roiHeightTexture;
        meshRenderer.material.SetTexture("_HeightTexROI", roiHeightTexture);
        roiHeightTextureRawArray = roiHeightTexture.GetRawTextureData<ushort>();
        ValidateROIShoreRuntimeDimensions();
    }

    public ushort GetTextureArrayValue(Unity.Collections.NativeArray<ushort> arr, int width, int height, float lonMin, float lonMax, float latMin, float latMax, LatLon latLon)
    {
        var u = (latLon.LonDeg - lonMin) / (lonMax - lonMin);
        var v = (latLon.LatDeg - latMin) / (latMax - latMin);
        var lonIdx = (int)Math.Floor(u * width);
        var latIdx = (int)Math.Floor(v * height);
        return arr[latIdx * width + lonIdx];
    }

    public bool IsUsingROIForElevation(LatLon latLon)
    {
        return useROI && IsInROIRange(latLon);
    }

    public bool HasValidROIShoreRuntime()
    {
        ValidateROIShoreRuntimeDimensions();
        return roiShoreRuntimeLoaded && roiShoreRuntimeDimensionsValid;
    }

    public bool TrySampleROIShoreField(LatLon latLon, out ShoreFieldSample sample)
    {
        sample = default;
        if (!IsUsingROIForElevation(latLon) || !HasValidROIShoreRuntime())
        {
            return false;
        }

        if (!TryGetROIPixelCoords(latLon, out var x, out var y))
        {
            return false;
        }

        sample = SampleROIShoreFieldBilinear(x, y);
        return true;
    }

    bool IsInROIRange(LatLon latLon)
    {
        return latLon.LatDeg >= roiLatitudeDeg0
            && latLon.LatDeg <= roiLatitudeDeg1
            && latLon.LonDeg >= roiLongitudeDeg0
            && latLon.LonDeg <= roiLongitudeDeg1;
    }

    bool TryGetROIPixelCoords(LatLon latLon, out float x, out float y)
    {
        x = 0f;
        y = 0f;
        if (!IsInROIRange(latLon) || roiHeightTexture == null || roiHeightTexture.width <= 0 || roiHeightTexture.height <= 0)
        {
            return false;
        }

        var u = Mathf.Clamp01((latLon.LonDeg - roiLongitudeDeg0) / (roiLongitudeDeg1 - roiLongitudeDeg0));
        var v = Mathf.Clamp01((latLon.LatDeg - roiLatitudeDeg0) / (roiLatitudeDeg1 - roiLatitudeDeg0));
        x = u * Mathf.Max(0, roiShoreRuntimeWidth - 1);
        y = v * Mathf.Max(0, roiShoreRuntimeHeight - 1);
        return true;
    }

    ShoreFieldSample SampleROIShoreFieldBilinear(float x, float y)
    {
        var x0 = Mathf.Clamp(Mathf.FloorToInt(x), 0, roiShoreRuntimeWidth - 1);
        var y0 = Mathf.Clamp(Mathf.FloorToInt(y), 0, roiShoreRuntimeHeight - 1);
        var x1 = Mathf.Min(x0 + 1, roiShoreRuntimeWidth - 1);
        var y1 = Mathf.Min(y0 + 1, roiShoreRuntimeHeight - 1);

        var tx = Mathf.Clamp01(x - x0);
        var ty = Mathf.Clamp01(y - y0);

        var s00 = DecodeROIShoreFieldSample(x0, y0);
        var s10 = DecodeROIShoreFieldSample(x1, y0);
        var s01 = DecodeROIShoreFieldSample(x0, y1);
        var s11 = DecodeROIShoreFieldSample(x1, y1);

        var topDistance = Mathf.Lerp(s00.distancePixels, s10.distancePixels, tx);
        var bottomDistance = Mathf.Lerp(s01.distancePixels, s11.distancePixels, tx);
        var distance = Mathf.Lerp(topDistance, bottomDistance, ty);

        var topGradient = Vector2.Lerp(s00.gradient, s10.gradient, tx);
        var bottomGradient = Vector2.Lerp(s01.gradient, s11.gradient, tx);
        var gradient = Vector2.Lerp(topGradient, bottomGradient, ty);
        if (gradient.sqrMagnitude > 1f)
        {
            gradient.Normalize();
        }

        return new ShoreFieldSample
        {
            distancePixels = distance,
            gradient = gradient
        };
    }

    ShoreFieldSample DecodeROIShoreFieldSample(int x, int y)
    {
        var index = y * roiShoreRuntimeWidth + x;
        return new ShoreFieldSample
        {
            distancePixels = roiShoreRuntimeDistanceData[index] / (float)ushort.MaxValue * roiShoreRuntimeMaxDistance,
            gradient = new Vector2(
                roiShoreRuntimeGradientXData[index] / GradientDecodeScale,
                roiShoreRuntimeGradientYData[index] / GradientDecodeScale)
        };
    }

    void TryLoadROIShoreRuntime()
    {
        roiShoreRuntimeLoaded = false;
        roiShoreRuntimeDimensionsValid = true;
        roiShoreRuntimeWarningLogged = false;

        if (roiShoreRuntimeBytes == null)
        {
            return;
        }

        try
        {
            using var stream = new MemoryStream(roiShoreRuntimeBytes.bytes, false);
            using var reader = new BinaryReader(stream);

            var magic = reader.ReadString();
            if (magic != ROIShoreRuntimeMagic)
            {
                Debug.LogWarning($"ROI shore runtime magic mismatch: expected {ROIShoreRuntimeMagic}, got {magic}.");
                return;
            }

            roiShoreRuntimeWidth = reader.ReadInt32();
            roiShoreRuntimeHeight = reader.ReadInt32();
            roiShoreRuntimeLandThreshold = reader.ReadSingle();
            roiShoreRuntimeMaxDistance = reader.ReadSingle();

            if (roiShoreRuntimeWidth <= 0 || roiShoreRuntimeHeight <= 0 || roiShoreRuntimeMaxDistance < 0f)
            {
                Debug.LogWarning("ROI shore runtime file contains invalid dimensions or max distance.");
                return;
            }

            var pixelCount = checked(roiShoreRuntimeWidth * roiShoreRuntimeHeight);
            roiShoreRuntimeDistanceData = new ushort[pixelCount];
            roiShoreRuntimeGradientXData = new sbyte[pixelCount];
            roiShoreRuntimeGradientYData = new sbyte[pixelCount];

            for (var i = 0; i < pixelCount; i++)
            {
                roiShoreRuntimeDistanceData[i] = reader.ReadUInt16();
                roiShoreRuntimeGradientXData[i] = reader.ReadSByte();
                roiShoreRuntimeGradientYData[i] = reader.ReadSByte();
            }

            roiShoreRuntimeLoaded = true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to load ROI shore runtime bytes: {ex.Message}");
            roiShoreRuntimeLoaded = false;
        }
    }

    void ValidateROIShoreRuntimeDimensions()
    {
        roiShoreRuntimeDimensionsValid = roiShoreRuntimeLoaded;
        if (!roiShoreRuntimeLoaded || roiHeightTexture == null)
        {
            return;
        }

        roiShoreRuntimeDimensionsValid = roiShoreRuntimeWidth == roiHeightTexture.width
            && roiShoreRuntimeHeight == roiHeightTexture.height;
        if (!roiShoreRuntimeDimensionsValid && !roiShoreRuntimeWarningLogged)
        {
            Debug.LogWarning(
                $"ROI shore runtime dimensions ({roiShoreRuntimeWidth}x{roiShoreRuntimeHeight}) do not match ROI height texture dimensions ({roiHeightTexture.width}x{roiHeightTexture.height}). " +
                "ROI shore-field avoidance will fall back to the legacy logic.");
            roiShoreRuntimeWarningLogged = true;
        }
    }

    public float GetElevation(LatLon latLon)
    {
        var useROITexture = IsUsingROIForElevation(latLon);
        var value = useROITexture ? GetTextureArrayValue(
            roiHeightTextureRawArray,
            roiHeightTexture.width, roiHeightTexture.height,
            roiLongitudeDeg0, roiLongitudeDeg1,
            roiLatitudeDeg0, roiLatitudeDeg1,
            latLon
        ) : GetTextureArrayValue(
            baseHeightTextureRawArray,
            baseHeightTexture.width, baseHeightTexture.height,
            -180, 180,
            -90, 90,
            latLon
        );
        return value;
    }
}
