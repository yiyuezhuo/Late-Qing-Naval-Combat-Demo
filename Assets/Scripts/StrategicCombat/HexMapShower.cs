using System;
using UnityEngine;
using TMPro;

using StrategicCombatCore;
using System.Linq;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor.Experimental.GraphView;

public class HexMapShower : SingletonDocument<HexMapShower>
{
    public Renderer controlledRenderer;
    Texture2D terrainTypeTexture;
    Material material;
    public Transform labelContainerTransform;
    public Transform roadContainerTransform;
    public Transform railroadContainerTransform;
    public Transform riverContainerTransform;
    public GameObject locationLabelPrefab;
    public GameObject roadPrefab;
    public GameObject railroadPrefab;
    public GameObject riverPrefab;

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
        BindHexCrossLineRenderers(
            roadContainerTransform, roadPrefab,
            StrategicGameState.Instance.IterateCellPairsFor(EdgeFeatureType.Road).ToList()
        );

        BindHexCrossLineRenderers(
            railroadContainerTransform, railroadPrefab,
            StrategicGameState.Instance.IterateCellPairsFor(EdgeFeatureType.Railroad).ToList(),
            0, 0.05f, 0.05f
        );

        BindHexEdgeLineRenderers(
            riverContainerTransform, riverPrefab,
            StrategicGameState.Instance.IterateCellPairsFor(EdgeFeatureType.River).ToList()
        );
    }

    void BindHexCrossLineRenderers(Transform containerTransform, GameObject prefab, List<(Cell, Cell, EdgeDirection)> cellPairs, float z=0, float xOffset=0, float yOffset=0)
    {
        Utils.SyncTransformViewerLength(containerTransform, cellPairs.Count, prefab);

        var height = StrategicGameState.Instance.GetMapHeight();
        var width = StrategicGameState.Instance.GetMapWidth();

        var lineRenderers = containerTransform.GetComponentsInChildren<LineRenderer>();
        for (int i = 0; i < cellPairs.Count; i++)
        {
            var (cellSrc, cellDst, edgeDirection) = cellPairs[i];
            var (xf1, yf1) = CellXYToLocalXY(cellSrc.x, cellSrc.y);
            var (xf2, yf2) = CellXYToLocalXY(cellDst.x, cellDst.y);

            var lineRenderer = lineRenderers[i];
            lineRenderer.positionCount = 2;
            lineRenderer.SetPositions(new Vector3[2]{
                new Vector3(xf1 + xOffset / width, yf1 + yOffset / height, z),
                new Vector3(xf2 + xOffset / width, yf2 + yOffset / height, z)
            });
        }
    }

    void BindHexEdgeLineRenderers(Transform containerTransform, GameObject prefab, List<(Cell, Cell, EdgeDirection)> cellPairs, float z = 0, float xOffset = 0, float yOffset = 0)
    {
        Utils.SyncTransformViewerLength(containerTransform, cellPairs.Count, prefab);

        var height = StrategicGameState.Instance.GetMapHeight();
        var width = StrategicGameState.Instance.GetMapWidth();

        var lineRenderers = containerTransform.GetComponentsInChildren<LineRenderer>();
        for (int i = 0; i < cellPairs.Count; i++)
        {
            var (cellSrc, cellDst, edgeDirection) = cellPairs[i];

            var ((dx1, dy1), (dx2, dy2)) = DirectionTo2LocalDxDy(edgeDirection);

            var (xf, yf) = CellXYToLocalXY(cellSrc.x, cellSrc.y);

            // var (xf1, yf1) = CellXYToLocalXY(cellSrc.x, cellSrc.y);
            // var (xf2, yf2) = CellXYToLocalXY(cellDst.x, cellDst.y);
            // var xc = (xf1 + xf2) / 2;
            // var yc = (yf1 + yf2) / 2;

            // var angleDeg = edgeDirection switch
            // {
            //     EdgeDirection.Top => 0,
            //     EdgeDirection.TopRight => -60,
            //     EdgeDirection.BottomRight => -120,
            //     EdgeDirection.Bottom => 180,
            //     EdgeDirection.BottomLeft => 120,
            //     EdgeDirection.TopLeft => 60,
            //     _ => 0,
            // };
            // var angleRad = angleDeg * Mathf.Deg2Rad;

            // var length = 1f;
            // var x1 = xc + length * Mathf.Cos(angleRad);
            // var y1 = yc + length * Mathf.Sin(angleRad);
            // var x2 = xc + length * Mathf.Cos(angleRad + Mathf.PI);
            // var y2 = yc + length * Mathf.Sin(angleRad + Mathf.PI);

            var lineRenderer = lineRenderers[i];
            lineRenderer.positionCount = 2;
            lineRenderer.SetPositions(new Vector3[2]{
                new Vector3(xf + dx1,  yf + dy1, z),
                new Vector3(xf + dx2,  yf + dy2, z)
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

    // static float rad60 = Mathf.PI / 3f;
    // static float cos60deg = Mathf.Cos(rad60);
    // static float sin60deg = Mathf.Sin(rad60);

    // static Dictionary<CornerType, (float, float)> cornerToStandardHexLocation = new()
    // {
    //     { CornerType.TopRight, (cos60deg, sin60deg)},
    //     { CornerType.Right, (1, 0)},
    //     { CornerType.BottomRight, (cos60deg, -sin60deg)},
    //     { CornerType.BottomLeft, (-cos60deg, -sin60deg)},
    //     { CornerType.Left, (-1, 0)},
    //     { CornerType.TopLeft, (-cos60deg, sin60deg)},
    // };

    static float cornerOffset = 0.1f;

    static Dictionary<CornerType, (float, float)> cornerToStandardHexLocation = new()
    {
        { CornerType.TopRight, (0.5f - cornerOffset, 0.5f)},
        { CornerType.Right, (0.5f + cornerOffset, 0)},
        { CornerType.BottomRight, (0.5f - cornerOffset, -0.5f)},
        { CornerType.BottomLeft, (-0.5f + cornerOffset, -0.5f)},
        { CornerType.Left, (-0.5f - cornerOffset, 0)},
        { CornerType.TopLeft, (-0.5f + cornerOffset, 0.5f)},
    };

    public ((float, float), (float, float)) DirectionTo2LocalDxDy(EdgeDirection edgeDirection)
    {
        var (corner1, corner2) = Cell.edgeDirectionToCornerType[edgeDirection];
        var (dx1, dy1) = cornerToStandardHexLocation[corner1];
        var (dx2, dy2) = cornerToStandardHexLocation[corner2];

        var gameState = StrategicGameState.Instance;

        var width = gameState.GetMapWidth();
        var height = gameState.GetMapHeight();

        return (
            (dx1 / width, dy1 / height),
            (dx2 / width, dy2 / height)
        );


        // return (
        //     (dx1 / 0.867f / width / 2, dy1 / height / 2),
        //     (dx2 / 0.867f / width / 2, dy2 / height / 2)
        // );
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
