using UnityEngine;
using NavalCombatCore;
using System;
using System.Collections.Generic;
using System.Collections;


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

    public void Awake()
    {
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