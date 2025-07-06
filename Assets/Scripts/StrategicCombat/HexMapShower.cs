using System;
using StrategicCombatCore;
using UnityEngine;

public class HexMapShower : SingletonDocument<HexMapShower>
{
    public Renderer controlledRenderer;
    public Texture2D terrainTypeTexture;

    protected override void Awake()
    {
        base.Awake();

        StrategicGameState.Instance.mapRebuilt += OnMapRebuilt;
    }

    public override void OnDestroy()
    {
        base.Awake();

        StrategicGameState.Instance.mapRebuilt -= OnMapRebuilt;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    public void OnMapRebuilt(object sender, EventArgs args)
    {
        GenerateTextureAndRefreshMaterial(StrategicGameState.Instance.terrainMatrix);
    }

    public void OnMapCellUpdated(object sender, (int, int) args)
    {
        var (x, y) = args;
        Color32 color = new Color32((byte)StrategicGameState.Instance.terrainMatrix[x, y], 0, 0, 255);
        terrainTypeTexture.SetPixel(x, y, color);
    }

    public void GenerateTextureAndRefreshMaterial(TerrainType[,] terrainMatrix)
    {
        var width = terrainMatrix.GetLength(0);
        var height = terrainMatrix.GetLength(1);

        // Update texture
        terrainTypeTexture = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Color32 color = new Color32((byte)terrainMatrix[x, y], 0, 0, 255);
                terrainTypeTexture.SetPixel(x, y, color);
            }
        }
        terrainTypeTexture.filterMode = FilterMode.Point;
        terrainTypeTexture.wrapMode = TextureWrapMode.Clamp;
        terrainTypeTexture.Apply();

        // Update Material
        var material = controlledRenderer.material;

        material.SetTexture("_TerrainTypeTex", terrainTypeTexture);
        material.SetInt("_Width", width);
        material.SetInt("_Height", height);
        // material.SetTexture("_TerrainTexArray", terrainTexArray);

        controlledRenderer.material = material;

        // Update scale
        transform.localScale = new Vector3(width, height, 0);

    }
}
