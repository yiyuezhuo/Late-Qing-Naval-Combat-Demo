using UnityEngine;

public class SphereController : SingletonMonoBehaviour<SphereController>
{
    MeshRenderer meshRenderer;

    public void Awake()
    {
        var diameter = Utils.r * 2f;
        transform.localScale = new Vector3(diameter, diameter, diameter);

        meshRenderer = GetComponent<MeshRenderer>();
    }

    public bool earthDarkTheme
    {
        get => meshRenderer.material.GetFloat("_UseDark") == 1;
        set => meshRenderer.material.SetFloat("_UseDark", value ? 1 : 0);
    }
    
}