using UnityEngine;

public class SphereController : SingletonMonoBehaviour<SphereController>
{
    MeshRenderer meshRenderer;

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