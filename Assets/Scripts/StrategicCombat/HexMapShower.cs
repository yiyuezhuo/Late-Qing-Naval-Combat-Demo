using System;
using UnityEngine;
using TMPro;

using StrategicCombatCore;
using System.Linq;

public class HexMapShower : SingletonDocument<HexMapShower>
{
    public Renderer controlledRenderer;
    Texture2D terrainTypeTexture;
    Material material;
    public Transform labelContainerTransform;
    public Transform roadContainerTransform;
    public GameObject locationLabelPrefab;
    public GameObject roadPrefab;

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

    bool _showAccurateSeaLand = true;
    public bool showAccurateSeaLand
    {
        get => _showAccurateSeaLand;
        set
        {
            if (value != _showAccurateSeaLand)
            {
                _showAccurateSeaLand = value;
                material.SetFloat("_AccurateSeaLand", _showAccurateSeaLand ? 1 : 0);
            }
        }
    }

    protected override void Awake()
    {
        base.Awake();

        var gameState = StrategicGameState.Instance;
        gameState.mapRebuilt += OnMapRebuilt;
        gameState.mapCellUpdated += OnMapCellUpdated;
        gameState.edgeFeatureUpdated += OnEdgeFeatureUpdated;
    }

    public override void OnDestroy()
    {
        base.Awake();

        var gameState = StrategicGameState.Instance;
        gameState.mapRebuilt -= OnMapRebuilt;
        gameState.mapCellUpdated -= OnMapCellUpdated;
        gameState.edgeFeatureUpdated -= OnEdgeFeatureUpdated;
    }

    void OnEdgeFeatureUpdated(object sender, EventArgs args)
    {
        var roadCellPairs = StrategicGameState.Instance.IterateRoadCellPairs().ToList();

        Utils.SyncTransformViewerLength(roadContainerTransform, roadCellPairs.Count, roadPrefab);

        var lineRenderers = roadContainerTransform.GetComponentsInChildren<LineRenderer>();
        for (int i = 0; i < roadCellPairs.Count; i++)
        {
            var (cellSrc, cellDst) = roadCellPairs[i];
            var (xf1, yf1) = CellXYToLocalXY(cellSrc.x, cellSrc.y);
            var (xf2, yf2) = CellXYToLocalXY(cellDst.x, cellDst.y);

            var lineRenderer = lineRenderers[i];
            lineRenderer.positionCount = 2;
            lineRenderer.SetPositions(new Vector3[2]{
                new Vector3(xf1, yf1, 0),
                new Vector3(xf2, yf2, 0)
            });
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        UpdateLabels();
    }

    void UpdateLabels()
    {
        var labels = StrategicGameState.Instance.labels;

        // var texts = labelContainerTransform.GetComponentsInChildren<TMP_Text>();
        // var diff = labels.Count - texts.Length;
        // if (diff > 0)
        // {
        //     for (int i = 0; i < diff; i++)
        //     {
        //         Instantiate(locationLabelPrefab, labelContainerTransform);
        //     }
        // }
        // else if (diff < 0)
        // {
        //     for (int i = 0; i < -diff; i++)
        //     {
        //         Destroy(texts[i].gameObject);
        //     }
        // }

        Utils.SyncTransformViewerLength(labelContainerTransform, labels.Count, locationLabelPrefab);

        // Bind
        var texts = labelContainerTransform.GetComponentsInChildren<TMP_Text>();
        var width = StrategicGameState.Instance.GetMapWidth();
        var height = StrategicGameState.Instance.GetMapHeight();
        for (int i = 0; i < labels.Count; i++)
        {
            var label = labels[i];
            var text = texts[i];
            text.text = label.name.english;
            // var dx = 0.5f;
            // var dy = label.x % 2 == 0 ? 0.5f : 1f;
            // text.transform.localPosition = new Vector3((label.x + dx) / width - 0.5f, (label.y + dy) / height - 0.5f, 0);
            var (xf, yf) = CellXYToLocalXY(label.x, label.y);
            text.transform.localPosition = new Vector3(xf, yf, 0);
        }
    }

    public (float, float) CellXYToLocalXY(int x, int y)
    {
        var dx = 0.5f;
        var dy = x % 2 == 0 ? 0.5f : 1f;
        var width = StrategicGameState.Instance.GetMapWidth();
        var height = StrategicGameState.Instance.GetMapHeight();
        return ((x + dx) / width - 0.5f, (y + dy) / height - 0.5f);
    }

    public void OnMapRebuilt(object sender, EventArgs args)
    {
        // GenerateTextureAndRefreshMaterial(StrategicGameState.Instance.terrainMatrix);
        GenerateTextureAndRefreshMaterial();
    }

    public void OnMapCellUpdated(object sender, (int, int) args)
    {
        var (x, y) = args;
        // Color32 color = new Color32((byte)StrategicGameState.Instance.terrainMatrix[x, y], 0, 0, 255);
        Color32 color = new Color32((byte)StrategicGameState.Instance.cellMatrix[x, y].terrain, 0, 0, 255);
        terrainTypeTexture.SetPixel(x, y, color);
        terrainTypeTexture.Apply();
    }

    public void GenerateTextureAndRefreshMaterial()
    {
        var gameState = StrategicGameState.Instance;

        var width = gameState.GetMapWidth();
        var height = gameState.GetMapHeight();

        // Update texture
        terrainTypeTexture = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Color32 color = new Color32((byte)gameState.cellMatrix[x, y].terrain, 0, 0, 255);
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
