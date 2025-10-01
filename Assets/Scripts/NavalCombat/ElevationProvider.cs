using UnityEngine;
using NavalCombatCore;
using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

// Elevation Service Dependency Injector
public class ElevationProvider : MonoBehaviour, IElevationProvider
{
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
    }

    public ushort GetTextureArrayValue(Unity.Collections.NativeArray<ushort> arr, int width, int height, float lonMin, float lonMax, float latMin, float latMax, LatLon latLon)
    {
        var u = (latLon.LonDeg - lonMin) / (lonMax - lonMin);
        var v = (latLon.LatDeg - latMin) / (latMax - latMin);
        var lonIdx = (int)Math.Floor(u * width);
        var latIdx = (int)Math.Floor(v * height);
        return arr[latIdx * width + lonIdx];
    }

    public float GetElevation(LatLon latLon)
    {
        var inROIRange = latLon.LatDeg >= roiLatitudeDeg0 && latLon.LatDeg <= roiLatitudeDeg1 && latLon.LonDeg >= roiLongitudeDeg0 && latLon.LonDeg <= roiLongitudeDeg1;
        var useROITexture = useROI && inROIRange;
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