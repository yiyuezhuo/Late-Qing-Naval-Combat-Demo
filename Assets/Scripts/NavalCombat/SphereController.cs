using System;
using UnityEngine;
using NavalCombatCore;

public class SphereController : SingletonMonoBehaviour<SphereController>
{
    MeshRenderer meshRenderer;
    long lastSunMinuteStamp = long.MinValue;

    static readonly LatLon equatorPrime = new(0f, 0f);
    static readonly LatLon equatorEast90 = new(0f, 90f);
    static readonly LatLon northPole = new(90f, 0f);

    static bool _earthDarkTheme = false;
    public static bool earthDarkTheme
    {
        get => _earthDarkTheme;
        set
        {
            if(_earthDarkTheme != value)
            {
                _earthDarkTheme = value;
                var instance = Instance;
                if(instance != null)
                {
                    Instance.shaderEarthDarkTheme = value;
                }
            }
        }
    }

    static bool _useSeaTexture = true;
    public static bool useSeaTexture
    {
        get => _useSeaTexture;
        set
        {
            if(_useSeaTexture != value)
            {
                _useSeaTexture = value;
                var instance = Instance;
                if(instance != null)
                {
                    Instance.shaderUseSeaTexture = value;
                }
            }
        }
    }

    public void Awake()
    {
        var diameter = Utils.r * 2f;
        transform.localScale = new Vector3(diameter, diameter, diameter);

        meshRenderer = GetComponent<MeshRenderer>();

        shaderEarthDarkTheme = earthDarkTheme;
        shaderUseSeaTexture = useSeaTexture;
        RefreshSunDirection(force: true);
    }

    void Update()
    {
        RefreshSunDirection(force: false);
    }

    void RefreshSunDirection(bool force)
    {
        if (meshRenderer == null)
        {
            return;
        }

        var scenarioState = NavalGameState.Instance?.scenarioState;
        if (scenarioState == null)
        {
            return;
        }

        var dateTime = scenarioState.dateTime;
        var minuteStamp = dateTime.Ticks / TimeSpan.TicksPerMinute;
        if (!force && minuteStamp == lastSunMinuteStamp)
        {
            return;
        }
        lastSunMinuteStamp = minuteStamp;

        var sx = Mathf.Sin(NavalUtils.GetSunPosition(dateTime, equatorEast90).altitudeDeg * Mathf.Deg2Rad);
        var sy = Mathf.Sin(NavalUtils.GetSunPosition(dateTime, northPole).altitudeDeg * Mathf.Deg2Rad);
        var sz = -Mathf.Sin(NavalUtils.GetSunPosition(dateTime, equatorPrime).altitudeDeg * Mathf.Deg2Rad);

        var sunDirWorld = new Vector3(sx, sy, sz);
        if (sunDirWorld.sqrMagnitude < 1e-8f)
        {
            return;
        }

        var sunDirObj = transform.InverseTransformDirection(sunDirWorld.normalized);
        meshRenderer.material.SetVector("_SunDirObj", new Vector4(sunDirObj.x, sunDirObj.y, sunDirObj.z, 0f));
    }

    bool shaderEarthDarkTheme
    {
        get => meshRenderer.material.GetFloat("_UseDark") == 1;
        set => meshRenderer.material.SetFloat("_UseDark", value ? 1 : 0);
    }
    
    bool shaderUseSeaTexture
    {
        get => meshRenderer.material.GetFloat("_UseSeaTex") == 1;
        set => meshRenderer.material.SetFloat("_UseSeaTex", value ? 1 : 0);
    }
    
}
