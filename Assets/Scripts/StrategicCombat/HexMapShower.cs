using System;
using StrategicCombatCore;
using UnityEngine;
using TMPro;
using System.Security.Cryptography.X509Certificates;

public class HexMapShower : SingletonDocument<HexMapShower>
{
    public Renderer controlledRenderer;
    Texture2D terrainTypeTexture;
    Material material;
    public Transform labelContainerTransform;
    public GameObject locationLabelPrefab;

    bool _showReferenceMap;
    public bool showReferenceMap
    {
        get => _showReferenceMap;
        set
        {
            if (value != _showReferenceMap)
            {
                _showReferenceMap = value;
                material.SetFloat("_ShowReferenceTexture", showReferenceMap ? 1 : 0);
            }
        }
    }

    bool _showBorder = true;
    public bool showBorder
    {
        get => _showBorder;
        set
        {
            if (value != _showBorder)
            {
                _showBorder = value;
                material.SetFloat("_Border", showBorder ? 1 : 0);
            }
        }
    }

    protected override void Awake()
    {
        base.Awake();

        StrategicGameState.Instance.mapRebuilt += OnMapRebuilt;
        StrategicGameState.Instance.mapCellUpdated += OnMapCellUpdated;
    }

    public override void OnDestroy()
    {
        base.Awake();

        StrategicGameState.Instance.mapRebuilt -= OnMapRebuilt;
        StrategicGameState.Instance.mapCellUpdated -= OnMapCellUpdated;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        var labels = StrategicGameState.Instance.labels;

        var texts = labelContainerTransform.GetComponentsInChildren<TMP_Text>();
        var diff = labels.Count - texts.Length;
        if (diff > 0)
        {
            for (int i = 0; i < diff; i++)
            {
                Instantiate(locationLabelPrefab, labelContainerTransform);
            }
        }
        else if (diff < 0)
        {
            for (int i = 0; i < -diff; i++)
            {
                Destroy(texts[i]);
            }
        }

        texts = labelContainerTransform.GetComponentsInChildren<TMP_Text>();
        var width = StrategicGameState.Instance.GetMapWidth();
        var height = StrategicGameState.Instance.GetMapHeight();
        for (int i = 0; i < labels.Count; i++)
        {
            var label = labels[i];
            var text = texts[i];
            text.text = label.name.english;
            var dx = 0.5f;
            var dy = label.x % 2 == 0 ? 0.5f : 1f;
            text.transform.localPosition = new Vector3((label.x + dx) / width - 0.5f, (label.y + dy) / height - 0.5f, 0);
        }
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
        terrainTypeTexture.Apply();
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
        material = controlledRenderer.material;

        material.SetTexture("_TerrainTypeTex", terrainTypeTexture);
        material.SetInt("_Width", width);
        material.SetInt("_Height", height);
        material.SetFloat("_ShowReferenceTexture", showReferenceMap ? 1 : 0);
        material.SetFloat("_Border", showBorder ? 1 : 0);
        // material.SetTexture("_TerrainTexArray", terrainTexArray);

        controlledRenderer.material = material;

        // Update scale
        transform.localScale = new Vector3(width, height, 0);

    }
}
