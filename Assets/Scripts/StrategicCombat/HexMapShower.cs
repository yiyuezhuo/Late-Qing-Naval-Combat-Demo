using System;
using UnityEngine;
using TMPro;
using System.Linq;
using System.Collections.Generic;
using System.Collections;

using StrategicCombatCore;

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

    public SpriteRenderer mapRenderer;

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

            var lineRenderer = lineRenderers[i];
            lineRenderer.positionCount = 2;
            lineRenderer.SetPositions(new Vector3[2]{
                new Vector3(xf + dx1,  yf + dy1, z),
                new Vector3(xf + dx2,  yf + dy2, z)
            });
        }
    }

    static string terrainStr = "Clear,Rough,Mountain,Forest,Jungle,Desert,Swamp,Rough_Forest,Rough_Jungle,Rough_Desert,Tropical Mountain,Sand Desert,Heavy Urban,Light Urban,Field,Shallow Water,Deep Water";

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // UnityWebRequestImageReader.Instance.FetchSprite("Assets/Textures/China/China.png");
        StartCoroutine(TerrainArrayCoroutine());
        StartCoroutine(ReferenceMapCoroutine()); // TODO: Delay to first reference toggle click?
    }

    IEnumerator TerrainArrayCoroutine()
    {
        var terrainNames = terrainStr.Split(",");
        var terrainPaths = terrainNames.Select(name => $"{Application.streamingAssetsPath}/Pictures/Terrain/{name}.jpg").ToList();
        var textures = terrainPaths.Select(path => UnityWebRequestImageReader.Instance.FetchTexture2D(path)).ToList();

        // Polling texture fetching
        var completed = true;
        for (int j = 0; j < 600; j++) // 60s timeout
        {
            completed = true;

            for (var i = 0; i < terrainPaths.Count; i++)
            {
                var texture = textures[i];
                if (texture == null)
                {
                    var path = terrainPaths[i];
                    textures[i] = UnityWebRequestImageReader.Instance.FetchTexture2D(path);
                    completed = false;
                }
            }

            if (completed)
                break;

            Debug.Log("Retrying to fetch textures...");
            yield return new WaitForSeconds(0.1f);
        }

        // Create Texture Array
        if (completed)
        {
            int width = textures[0].width;
            int height = textures[0].height;

            int slices = textures.Count;
            // TextureFormat format = TextureFormat.RGBA32;
            TextureFormat format = TextureFormat.RGB24;
            bool mipChain = false;

            // Create the texture array and apply the parameters
            Texture2DArray textureArray = new Texture2DArray(width, height, slices, format, mipChain);

            // Copy each texture into the array
            for (int i = 0; i < textures.Count; i++)
            {
                // Debug.Log($"Texture readable: {textures[i].isReadable}");
                Graphics.CopyTexture(textures[i], 0, 0, textureArray, i, 0);
            }

            textureArray.Apply(true);

            mapRenderer.material.SetTexture("_TerrainTexArray", textureArray);
            Debug.Log("Dynamic Texture Array Creation Complated");
        }
        else
        {
            Debug.LogError("Load Dynamic Texture Array failed");
        }
    }

    IEnumerator ReferenceMapCoroutine()
    {
        var path = $"{Application.streamingAssetsPath}/Pictures/Maps/First_Sino_Japanese_War_Reference.jpg";
        for (int i = 0; i < 600; i++) // 60s timeout
        {
            var texture = UnityWebRequestImageReader.Instance.FetchTexture2D(path);
            if (texture != null)
            {
                mapRenderer.material.SetTexture("_ReferenceTexture", texture);
                Debug.Log("Reference Map is loaded");
                yield break;
            }
            yield return new WaitForSeconds(0.1f);
        }
        Debug.LogError("Reference Map is not loaded");
    }

    // Update is called once per frame
    void Update()
    {
        UpdateLabels();
    }

    void UpdateLabels()
    {
        var labels = StrategicGameState.Instance.labels;

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

    public static (float, float) CellXYToLocalXY(int x, int y)
    {
        var dx = 0.5f;
        var dy = x % 2 == 0 ? 0.5f : 1f;
        var width = StrategicGameState.Instance.GetMapWidth();
        var height = StrategicGameState.Instance.GetMapHeight();
        return ((x + dx) / width - 0.5f, (y + dy) / height - 0.5f);
    }

    static float cornerOffset = 0.1f; // Not the exact value

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
