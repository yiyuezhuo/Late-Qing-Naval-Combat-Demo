using System;
using System.Collections.Generic;
using NavalCombatCore;
using Unity.Collections;
using UnityEngine;

public struct ShoreFieldSample
{
    public float distancePixels;
    public Vector2 gradient;
}

#pragma warning disable CS0649
[Serializable]
class ShoreFieldMetadata
{
    public string exportMode = string.Empty;
    public int width;
    public int height;
    public float landThreshold;
    public float maxDistancePixels;
    public float distanceEncodeMaxPixels;
}
#pragma warning restore CS0649

// Elevation Service Dependency Injector
public class ElevationProvider : MonoBehaviour, IElevationProvider
{
    const float DistanceTextureDecodeScale = 255f;

    public Texture2D baseHeightTexture;
    public Texture2D roiHeightTexture;
    public Texture2D roiShoreFieldTexture;
    public TextAsset roiShoreFieldMetadataJson;

    // _ROILatDeg0 ("ROI Latitude Deg 0", Float) = 15 // 30
    // _ROILatDeg1 ("ROI Latitude Deg 1", Float) = 55 // 41
    // _ROILonDeg0 ("ROI Longitude Deg 0", Float) = 105 // 116
    // _ROILonDeg1 ("ROI Longitude Deg 1", Float) = 146 // 131
    public float roiLatitudeDeg0 = 15;
    public float roiLatitudeDeg1 = 55;
    public float roiLongitudeDeg0 = 105;
    public float roiLongitudeDeg1 = 146;
    public bool useROI = true;

    NativeArray<ushort> baseHeightTextureRawArray;
    NativeArray<ushort> roiHeightTextureRawArray;
    NativeArray<Color32> roiShoreFieldTextureRawArray;
    Color32[] roiShoreFieldPixelsFallback;
    bool roiShoreFieldUseRawArray;

    public MeshRenderer meshRenderer;

    int roiShoreFieldWidth;
    int roiShoreFieldHeight;
    float roiShoreFieldLandThreshold;
    float roiShoreFieldMaxDistance;
    float roiShoreFieldDistanceDecodeMax;
    bool roiShoreFieldLoaded;
    bool roiShoreFieldDimensionsValid = true;
    bool roiShoreFieldWarningLogged;

    public void Awake()
    {
        baseHeightTextureRawArray = baseHeightTexture.GetRawTextureData<ushort>();
        roiHeightTextureRawArray = roiHeightTexture.GetRawTextureData<ushort>();

        ElevationService.Instance.elevationProvider = this;

        ApplyShaderTexturesAndParams();
        TryLoadROIShoreField();
        ValidateROIShoreFieldDimensions();

        var testLatLonList = new List<LatLon>()
        {
            new(35.6764f, 139.6500f), // Tokyo
            new(39.15f, 123.73f) // The Location of the Battle of Yalu river
        };

        foreach (var latLon in testLatLonList)
        {
            Debug.Log(latLon + ": " + ElevationService.Instance.GetElevation(latLon));
        }
    }

    public void SetBaseTexture(Texture2D newBaseHeightTexture)
    {
        baseHeightTexture = newBaseHeightTexture;
        baseHeightTextureRawArray = baseHeightTexture.GetRawTextureData<ushort>();
        ApplyShaderTexturesAndParams();
    }

    public void SetROITexture(Texture2D newRoiHeightTexture)
    {
        roiHeightTexture = newRoiHeightTexture;
        roiHeightTextureRawArray = roiHeightTexture.GetRawTextureData<ushort>();
        ApplyShaderTexturesAndParams();
        ValidateROIShoreFieldDimensions();
    }

    public void SetROIShoreField(Texture2D newRoiShoreFieldTexture, TextAsset newRoiShoreFieldMetadataJson = null)
    {
        roiShoreFieldTexture = newRoiShoreFieldTexture;
        if (newRoiShoreFieldMetadataJson != null)
        {
            roiShoreFieldMetadataJson = newRoiShoreFieldMetadataJson;
        }

        ApplyShaderTexturesAndParams();
        TryLoadROIShoreField();
        ValidateROIShoreFieldDimensions();
    }

    void ApplyShaderTexturesAndParams()
    {
        if (meshRenderer == null)
        {
            return;
        }

        var material = meshRenderer.material;
        material.SetTexture("_HeightTex", baseHeightTexture);
        material.SetTexture("_HeightTexROI", roiHeightTexture);
        material.SetTexture("_ShoreFieldTexROI", roiShoreFieldTexture);
        material.SetFloat("_ROILatDeg0", roiLatitudeDeg0);
        material.SetFloat("_ROILatDeg1", roiLatitudeDeg1);
        material.SetFloat("_ROILonDeg0", roiLongitudeDeg0);
        material.SetFloat("_ROILonDeg1", roiLongitudeDeg1);
        material.SetFloat("_UseROI", useROI ? 1f : 0f);
    }

    public ushort GetTextureArrayValue(NativeArray<ushort> arr, int width, int height, float lonMin, float lonMax, float latMin, float latMax, LatLon latLon)
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

    public bool HasValidROIShoreField()
    {
        ValidateROIShoreFieldDimensions();
        return roiShoreFieldLoaded && roiShoreFieldDimensionsValid;
    }

    public int ROIShoreFieldWidth => roiShoreFieldWidth;
    public int ROIShoreFieldHeight => roiShoreFieldHeight;
    public float ROILatitudeDeg0 => roiLatitudeDeg0;
    public float ROILatitudeDeg1 => roiLatitudeDeg1;
    public float ROILongitudeDeg0 => roiLongitudeDeg0;
    public float ROILongitudeDeg1 => roiLongitudeDeg1;

    public bool TrySampleROIShoreField(LatLon latLon, out ShoreFieldSample sample)
    {
        sample = default;
        if (!IsUsingROIForElevation(latLon) || !HasValidROIShoreField())
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

    public bool TryGetROIShoreFieldDistancePixels(LatLon latLon, out float distancePixels)
    {
        distancePixels = 0f;
        if (!TrySampleROIShoreField(latLon, out var sample))
        {
            return false;
        }

        distancePixels = sample.distancePixels;
        return true;
    }

    public bool TryGetROIShoreFieldDistancePixels(int x, int y, out float distancePixels)
    {
        distancePixels = 0f;
        if (!TryGetROIShoreFieldSample(x, y, out var sample))
        {
            return false;
        }

        distancePixels = sample.distancePixels;
        return true;
    }

    public bool TryGetROIShoreFieldSample(int x, int y, out ShoreFieldSample sample)
    {
        sample = default;
        if (!HasValidROIShoreField()
            || x < 0 || x >= roiShoreFieldWidth
            || y < 0 || y >= roiShoreFieldHeight)
        {
            return false;
        }

        sample = DecodeROIShoreFieldSample(x, y);
        return true;
    }

    public LatLon ROIPixelCoordsToLatLon(float x, float y)
    {
        if (roiShoreFieldWidth <= 1 || roiShoreFieldHeight <= 1)
        {
            return new LatLon(roiLatitudeDeg0, roiLongitudeDeg0);
        }

        var u = Mathf.Clamp01(x / (roiShoreFieldWidth - 1f));
        var v = Mathf.Clamp01(y / (roiShoreFieldHeight - 1f));
        var lonDeg = Mathf.Lerp(roiLongitudeDeg0, roiLongitudeDeg1, u);
        var latDeg = Mathf.Lerp(roiLatitudeDeg0, roiLatitudeDeg1, v);
        return new LatLon(latDeg, lonDeg);
    }

    bool IsInROIRange(LatLon latLon)
    {
        return latLon.LatDeg >= roiLatitudeDeg0
            && latLon.LatDeg <= roiLatitudeDeg1
            && latLon.LonDeg >= roiLongitudeDeg0
            && latLon.LonDeg <= roiLongitudeDeg1;
    }

    public bool TryGetROIPixelCoords(LatLon latLon, out float x, out float y)
    {
        x = 0f;
        y = 0f;
        if (!IsInROIRange(latLon) || roiHeightTexture == null || roiHeightTexture.width <= 0 || roiHeightTexture.height <= 0)
        {
            return false;
        }

        var u = Mathf.Clamp01((latLon.LonDeg - roiLongitudeDeg0) / (roiLongitudeDeg1 - roiLongitudeDeg0));
        var v = Mathf.Clamp01((latLon.LatDeg - roiLatitudeDeg0) / (roiLatitudeDeg1 - roiLatitudeDeg0));
        x = u * Mathf.Max(0, roiShoreFieldWidth - 1);
        y = v * Mathf.Max(0, roiShoreFieldHeight - 1);
        return true;
    }

    public bool TryGetROIPixelCoordsRounded(LatLon latLon, out int x, out int y)
    {
        x = 0;
        y = 0;
        if (!TryGetROIPixelCoords(latLon, out var rawX, out var rawY))
        {
            return false;
        }

        x = Mathf.Clamp(Mathf.RoundToInt(rawX), 0, Mathf.Max(0, roiShoreFieldWidth - 1));
        y = Mathf.Clamp(Mathf.RoundToInt(rawY), 0, Mathf.Max(0, roiShoreFieldHeight - 1));
        return true;
    }

    ShoreFieldSample SampleROIShoreFieldBilinear(float x, float y)
    {
        var x0 = Mathf.Clamp(Mathf.FloorToInt(x), 0, roiShoreFieldWidth - 1);
        var y0 = Mathf.Clamp(Mathf.FloorToInt(y), 0, roiShoreFieldHeight - 1);
        var x1 = Mathf.Min(x0 + 1, roiShoreFieldWidth - 1);
        var y1 = Mathf.Min(y0 + 1, roiShoreFieldHeight - 1);

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
        var index = y * roiShoreFieldWidth + x;
        var pixel = GetROIShoreFieldPixel(index);

        var gradient = new Vector2(
            pixel.g / DistanceTextureDecodeScale * 2f - 1f,
            pixel.b / DistanceTextureDecodeScale * 2f - 1f);
        if (gradient.sqrMagnitude > 1f)
        {
            gradient.Normalize();
        }

        return new ShoreFieldSample
        {
            distancePixels = pixel.r / DistanceTextureDecodeScale * roiShoreFieldDistanceDecodeMax,
            gradient = gradient
        };
    }

    Color32 GetROIShoreFieldPixel(int index)
    {
        return roiShoreFieldUseRawArray ? roiShoreFieldTextureRawArray[index] : roiShoreFieldPixelsFallback[index];
    }

    void TryLoadROIShoreField()
    {
        roiShoreFieldLoaded = false;
        roiShoreFieldDimensionsValid = true;
        roiShoreFieldWarningLogged = false;
        roiShoreFieldUseRawArray = false;
        roiShoreFieldPixelsFallback = null;

        if (roiShoreFieldTexture == null || roiShoreFieldMetadataJson == null)
        {
            return;
        }

        try
        {
            var metadata = JsonUtility.FromJson<ShoreFieldMetadata>(roiShoreFieldMetadataJson.text);
            if (metadata == null || metadata.maxDistancePixels < 0f)
            {
                Debug.LogWarning("ROI shore-field metadata is missing or invalid.");
                return;
            }

            roiShoreFieldLandThreshold = metadata.landThreshold;
            roiShoreFieldMaxDistance = metadata.maxDistancePixels;
            roiShoreFieldDistanceDecodeMax = metadata.distanceEncodeMaxPixels > 0f
                ? metadata.distanceEncodeMaxPixels
                : metadata.maxDistancePixels;
            roiShoreFieldWidth = roiShoreFieldTexture.width;
            roiShoreFieldHeight = roiShoreFieldTexture.height;

            if (metadata.width > 0 && metadata.height > 0
                && (metadata.width != roiShoreFieldWidth || metadata.height != roiShoreFieldHeight))
            {
                Debug.LogWarning(
                    $"ROI shore-field metadata dimensions ({metadata.width}x{metadata.height}) do not match texture dimensions ({roiShoreFieldWidth}x{roiShoreFieldHeight}).");
            }

            if (!TryLoadROIShoreFieldPixels())
            {
                return;
            }

            roiShoreFieldLoaded = true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to load ROI shore-field texture data: {ex.Message}");
            roiShoreFieldLoaded = false;
        }
    }

    bool TryLoadROIShoreFieldPixels()
    {
        var pixelCount = checked(roiShoreFieldWidth * roiShoreFieldHeight);

        try
        {
            roiShoreFieldTextureRawArray = roiShoreFieldTexture.GetRawTextureData<Color32>();
            if (roiShoreFieldTextureRawArray.Length == pixelCount)
            {
                roiShoreFieldUseRawArray = true;
                return true;
            }
        }
        catch (Exception)
        {
        }

        if (!roiShoreFieldTexture.isReadable)
        {
            Debug.LogWarning("ROI shore-field texture is not readable, so ROI shore-field avoidance will fall back to the legacy logic.");
            return false;
        }

        var pixels = roiShoreFieldTexture.GetPixels32();
        if (pixels == null || pixels.Length != pixelCount)
        {
            Debug.LogWarning("ROI shore-field texture pixel data size is invalid.");
            return false;
        }

        roiShoreFieldPixelsFallback = pixels;
        roiShoreFieldUseRawArray = false;
        return true;
    }

    void ValidateROIShoreFieldDimensions()
    {
        roiShoreFieldDimensionsValid = roiShoreFieldLoaded;
        if (!roiShoreFieldLoaded || roiHeightTexture == null)
        {
            return;
        }

        roiShoreFieldDimensionsValid = roiShoreFieldWidth == roiHeightTexture.width
            && roiShoreFieldHeight == roiHeightTexture.height;
        if (!roiShoreFieldDimensionsValid && !roiShoreFieldWarningLogged)
        {
            Debug.LogWarning(
                $"ROI shore-field texture dimensions ({roiShoreFieldWidth}x{roiShoreFieldHeight}) do not match ROI height texture dimensions ({roiHeightTexture.width}x{roiHeightTexture.height}). " +
                "ROI shore-field avoidance will fall back to the legacy logic.");
            roiShoreFieldWarningLogged = true;
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
