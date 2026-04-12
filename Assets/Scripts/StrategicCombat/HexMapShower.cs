using System;
using UnityEngine;
using TMPro;
using System.Linq;
using System.Collections.Generic;
using System.Collections;

using StrategicCombatCore;
using CoreUtils;

public enum CellLabelDisplayMode
{
    None,
    Label,
    XY,
    Coast
}

public class HexMapShower : SingletonMonoBehaviour<HexMapShower>
{
    public Renderer controlledRenderer;
    Texture2D terrainTypeTexture;
    Material material;

    // public Transform labelContainerTransform;
    public Transform roadContainerTransform;
    public Transform railroadContainerTransform;
    public Transform riverContainerTransform;
    public Transform blockSeaMovementContainerTransform;
    public Transform strategicGroupIconTransform;
    public Transform sideFlagContainerTransform;
    public Transform cellLabelContainerTransform;
    // public Transform pathLineContainerTransform;
    // public Transform missionWaypointLineContainerTransform;

    public GameObject locationLabelPrefab;
    public GameObject roadPrefab;
    public GameObject railroadPrefab;
    public GameObject riverPrefab;
    public GameObject blockSeaMovementPrefab;
    public GameObject strategicGroupIconPrefab;
    public GameObject sideFlagPrefab;
    public GameObject cellLabelPrefab;
    // public GameObject missionWaypointLinePrefab;

    public SpriteRenderer mapRenderer;

    static readonly Color blockSeaMovementColor = new(0.55f, 0.55f, 0.55f, 1f);
    static readonly Color riverColor = new(0.48776254f, 0.6639441f, 0.9150943f, 1f);
    static readonly EdgeDirection[] uniqueEdgeDirections =
    {
        EdgeDirection.Top,
        EdgeDirection.TopRight,
        EdgeDirection.BottomRight
    };

    const float riverMeshWidth = 0.04f;
    const float blockSeaMovementMeshWidth = 0.035f;
    const float sideBorderMeshWidth = 0.0525f;

    Mesh riverMesh;
    Mesh blockSeaMovementMesh;
    Mesh sideBorderMesh;
    Material vertexColorMaterial;

    // public LineRenderer pathLineRenderer;

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

    // bool _showSideFlag = true;
    bool _showSideFlag;
    public bool showSideFlag
    {
        get => _showSideFlag;
        set
        {
            if (value != _showSideFlag)
            {
                _showSideFlag = value;
                RefreshSideFlagVisibility();
            }
        }
    }

    CellLabelDisplayMode _cellLabelDisplayMode = CellLabelDisplayMode.Label;
    public CellLabelDisplayMode cellLabelDisplayMode
    {
        get => _cellLabelDisplayMode;
        set
        {
            if (value != _cellLabelDisplayMode)
            {
                _cellLabelDisplayMode = value;
                RefreshCellLabelDisplayMode();
            }
        }
    }

    protected void Awake()
    {
        // base.Awake();

        var gameState = StrategicGameState.Instance;
        gameState.mapRebuilt += OnMapRebuilt;
        gameState.mapCellUpdated += OnMapCellUpdated;
        gameState.edgeFeatureUpdated += OnEdgeFeatureUpdated;

        RefreshSideFlagVisibility();

        GamePreference.Instance.shortLabelLanguageTypeChanged += OnShortLabelLanguageTypeChanged;
    }

    public override void OnDestroy()
    {
        base.OnDestroy();

        var gameState = StrategicGameState.Instance;
        gameState.mapRebuilt -= OnMapRebuilt;
        gameState.mapCellUpdated -= OnMapCellUpdated;
        gameState.edgeFeatureUpdated -= OnEdgeFeatureUpdated;

        GamePreference.Instance.shortLabelLanguageTypeChanged -= OnShortLabelLanguageTypeChanged;

        DestroyMeshResource(riverMesh);
        DestroyMeshResource(blockSeaMovementMesh);
        DestroyMeshResource(sideBorderMesh);
        DestroyMaterialResource(vertexColorMaterial);
    }

    void OnShortLabelLanguageTypeChanged(object sender, EventArgs e)
    {
        if (cellLabelDisplayMode == CellLabelDisplayMode.Label)
        {
            RefreshCellLabelDisplayMode();
        }
    }

    void OnEdgeFeatureUpdated(object sender, EventArgs args)
    {
        RefreshEdgeFeature();
    }

    void RefreshEdgeFeature()
    {
        EnsureBlockSeaMovementRenderSetup();

        BindHexCrossLineRenderers(
            roadContainerTransform, roadPrefab,
            StrategicGameState.Instance.IterateCellPairsFor(EdgeFeatureType.Road).ToList(),
            smoothConnectedLines: true
        );

        BindHexCrossLineRenderers(
            railroadContainerTransform, railroadPrefab,
            StrategicGameState.Instance.IterateCellPairsFor(EdgeFeatureType.Railroad).ToList(),
            0, 0.05f, 0.05f,
            smoothConnectedLines: true
        );

        BindHexEdgeMesh(
            riverContainerTransform, "RiverMesh",
            ref riverMesh,
            StrategicGameState.Instance.IterateCellPairsFor(EdgeFeatureType.River),
            riverMeshWidth,
            riverColor,
            sortingOrder: mapRenderer != null ? mapRenderer.sortingOrder + 1 : 0
        );

        if (blockSeaMovementContainerTransform != null && blockSeaMovementPrefab != null)
        {
            BindHexEdgeMesh(
                blockSeaMovementContainerTransform, "BlockSeaMovementMesh",
                ref blockSeaMovementMesh,
                StrategicGameState.Instance.IterateCellPairsFor(EdgeFeatureType.BlockSeaMovement),
                blockSeaMovementMeshWidth,
                blockSeaMovementColor,
                sortingOrder: mapRenderer != null ? mapRenderer.sortingOrder + 2 : 1
            );
        }

        RefreshBlockSeaMovementVisibility();
    }

    void BindHexCrossLineRenderers(Transform containerTransform, GameObject prefab, List<(Cell, Cell, EdgeDirection)> cellPairs, float z = 0, float xOffset = 0, float yOffset = 0, bool smoothConnectedLines = false)
    {
        var height = StrategicGameState.Instance.GetMapHeight();
        var width = StrategicGameState.Instance.GetMapWidth();
        var polylines = smoothConnectedLines
            ? StrategicLineRenderUtils.BuildEdgeFeaturePolylines(
                cellPairs,
                cell =>
                {
                    var (xf, yf) = CellXYToLocalXY(cell.x, cell.y);
                    return controlledRenderer.transform.TransformPoint(xf + xOffset / width, yf + yOffset / height, z);
                })
            : null;

        var lineCount = smoothConnectedLines ? polylines.Count : cellPairs.Count;
        Utils.SyncTransformViewerLength(containerTransform, lineCount, prefab);

        var lineRenderers = containerTransform.GetComponentsInChildren<LineRenderer>();
        if (smoothConnectedLines)
        {
            for (int i = 0; i < polylines.Count; i++)
            {
                var lineRenderer = lineRenderers[i];
                StrategicLineRenderUtils.ConfigureLineRenderer(lineRenderer, polylines[i].loop);
                lineRenderer.positionCount = polylines[i].positions.Length;
                lineRenderer.SetPositions(polylines[i].positions);
            }
            return;
        }

        for (int i = 0; i < cellPairs.Count; i++)
        {
            var (cellSrc, cellDst, edgeDirection) = cellPairs[i];
            var (xf1, yf1) = CellXYToLocalXY(cellSrc.x, cellSrc.y);
            var (xf2, yf2) = CellXYToLocalXY(cellDst.x, cellDst.y);

            var lineRenderer = lineRenderers[i];
            StrategicLineRenderUtils.ConfigureLineRenderer(lineRenderer);
            lineRenderer.positionCount = 2;
            var p0 = controlledRenderer.transform.TransformPoint(xf1 + xOffset / width, yf1 + yOffset / height, z);
            var p1 = controlledRenderer.transform.TransformPoint(xf2 + xOffset / width, yf2 + yOffset / height, z);
            lineRenderer.SetPositions(new Vector3[2] { p0, p1 });
        }
    }

    void BindHexEdgeLineRenderers(Transform containerTransform, GameObject prefab, List<(Cell, Cell, EdgeDirection)> cellPairs, float z = 0, float xOffset = 0, float yOffset = 0, Color? lineColor = null)
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
            StrategicLineRenderUtils.ConfigureLineRenderer(lineRenderer);
            lineRenderer.positionCount = 2;
            var p0 = controlledRenderer.transform.TransformPoint(xf + dx1, yf + dy1, z);
            var p1 = controlledRenderer.transform.TransformPoint(xf + dx2, yf + dy2, z);
            lineRenderer.SetPositions(new Vector3[2] { p0, p1 });
            if (lineColor.HasValue)
            {
                lineRenderer.startColor = lineColor.Value;
                lineRenderer.endColor = lineColor.Value;
            }
        }
    }

    void BindHexEdgeMesh(
        Transform containerTransform,
        string meshObjectName,
        ref Mesh mesh,
        IEnumerable<(Cell, Cell, EdgeDirection)> cellPairs,
        float lineWidth,
        Color lineColor,
        int sortingOrder)
    {
        if (containerTransform == null)
            return;

        ClearLineRendererChildren(containerTransform);

        var meshTransform = EnsureMeshRenderer(containerTransform, meshObjectName, ref mesh, sortingOrder);
        var meshBuilder = new StrategicMapLineMeshBuilder();
        foreach (var (cellSrc, cellDst, edgeDirection) in cellPairs)
        {
            if (cellSrc == null || cellDst == null)
                continue;

            var (p0, p1) = GetHexEdgeLocalSegment(cellSrc, edgeDirection, meshTransform);
            meshBuilder.AddLine(p0, p1, lineWidth, lineColor);
        }

        meshBuilder.ApplyTo(mesh);
    }

    void EnsureBlockSeaMovementRenderSetup()
    {
        if (blockSeaMovementContainerTransform == null)
        {
            var container = new GameObject("BlockSeaMovementContainer");
            var containerTransform = container.transform;
            containerTransform.SetParent(transform, false);
            containerTransform.SetSiblingIndex(riverContainerTransform != null ? riverContainerTransform.GetSiblingIndex() + 1 : transform.childCount);
            blockSeaMovementContainerTransform = containerTransform;
        }

        if (blockSeaMovementPrefab == null)
        {
            blockSeaMovementPrefab = riverPrefab;
        }
    }

    void RefreshBlockSeaMovementVisibility()
    {
        if (blockSeaMovementContainerTransform == null)
            return;

        var manager = StrategicGameManager.Instance;
        var show = GamePreference.Instance.isInEditMode
            && manager != null
            && manager.currentEdgeFeatureType == EdgeFeatureType.BlockSeaMovement
            && (
                manager.mapEditMode == StrategicMapEditMode.ToggleHexPairFeatureBegin
                || manager.mapEditMode == StrategicMapEditMode.ToggleHexPairFeatureEnd
            );

        if (blockSeaMovementContainerTransform.gameObject.activeSelf != show)
        {
            blockSeaMovementContainerTransform.gameObject.SetActive(show);
        }
    }

    Transform EnsureMeshRenderer(Transform containerTransform, string meshObjectName, ref Mesh mesh, int sortingOrder)
    {
        var meshTransform = containerTransform.Find(meshObjectName);
        if (meshTransform == null)
        {
            var meshObject = new GameObject(meshObjectName);
            meshTransform = meshObject.transform;
            meshTransform.SetParent(containerTransform, false);
            meshObject.AddComponent<MeshFilter>();
            meshObject.AddComponent<MeshRenderer>();
        }

        var meshFilter = meshTransform.GetComponent<MeshFilter>();
        var meshRenderer = meshTransform.GetComponent<MeshRenderer>();
        if (mesh == null)
        {
            mesh = new Mesh
            {
                name = meshObjectName
            };
            mesh.MarkDynamic();
        }

        meshFilter.sharedMesh = mesh;
        meshRenderer.sharedMaterial = GetVertexColorMaterial();
        if (mapRenderer != null)
        {
            meshRenderer.sortingLayerID = mapRenderer.sortingLayerID;
        }
        meshRenderer.sortingOrder = sortingOrder;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;

        return meshTransform;
    }

    static void ClearLineRendererChildren(Transform containerTransform)
    {
        if (containerTransform == null)
            return;

        for (var i = containerTransform.childCount - 1; i >= 0; i--)
        {
            var child = containerTransform.GetChild(i);
            if (child.GetComponent<LineRenderer>() == null)
                continue;

            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }
    }

    Material GetVertexColorMaterial()
    {
        if (vertexColorMaterial != null)
            return vertexColorMaterial;

        var shader = Shader.Find("Unlit/SimpleEntity");
        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }
        vertexColorMaterial = new Material(shader)
        {
            name = "Strategic Map Vertex Color"
        };
        if (vertexColorMaterial.HasProperty("_MainColor"))
        {
            vertexColorMaterial.SetColor("_MainColor", Color.white);
        }
        return vertexColorMaterial;
    }

    (Vector3, Vector3) GetHexEdgeLocalSegment(Cell cell, EdgeDirection edgeDirection, Transform targetTransform, float z = 0)
    {
        var ((dx1, dy1), (dx2, dy2)) = DirectionTo2LocalDxDy(edgeDirection);
        var (xf, yf) = CellXYToLocalXY(cell.x, cell.y);
        var p0 = controlledRenderer.transform.TransformPoint(xf + dx1, yf + dy1, z);
        var p1 = controlledRenderer.transform.TransformPoint(xf + dx2, yf + dy2, z);
        return (
            targetTransform.InverseTransformPoint(p0),
            targetTransform.InverseTransformPoint(p1)
        );
    }

    Vector3 GetCellLocalCenter(Cell cell, Transform targetTransform, float z = 0)
    {
        var (xf, yf) = CellXYToLocalXY(cell.x, cell.y);
        var point = controlledRenderer.transform.TransformPoint(xf, yf, z);
        return targetTransform.InverseTransformPoint(point);
    }

    static void DestroyMeshResource(Mesh mesh)
    {
        if (mesh == null)
            return;

        if (Application.isPlaying)
        {
            Destroy(mesh);
        }
        else
        {
            DestroyImmediate(mesh);
        }
    }

    static void DestroyMaterialResource(Material material)
    {
        if (material == null)
            return;

        if (Application.isPlaying)
        {
            Destroy(material);
        }
        else
        {
            DestroyImmediate(material);
        }
    }


    // void LayoutStackTransform(List<Transform> transforms, Vector3 basePos, float stackSpace)
    // {
    //     var count = transforms.Count;
    //     if (count == 1)
    //     {
    //         transforms[0].position = basePos;
    //         return;
    //     }
    //     var step = stackSpace / (count - 1);
    //     for (int i = 0; i < count; i++)
    //     {
    //         var delta = -stackSpace / 2 + i * step;
    //         transforms[i].position = basePos + new Vector3(delta, delta, 0);
    //     }
    // }

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
        RefreshBlockSeaMovementVisibility();
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

    public void Refresh()
    {
        // GenerateTextureAndRefreshMaterial();
        RefreshMap();
        RefreshEdgeFeature();
    }

    class CellViewer
    {
        public SpriteRenderer flagRenderer;
        public TMP_Text text;
        public Color defaultTextColor;
    }

    Dictionary<(int, int), CellViewer> gridCellViewerMap = new();
    Dictionary<(int, int), float> influenceOverlayValues = new();
    bool influenceOverlayActive;
    float influenceOverlayMaxAbs;
    bool theaterOverlayActive;
    Dictionary<(int, int), string> theaterOverlayTexts = new();
    // Dictionary<string, CellViewer> areaCellViewerMap = new();

    public void RefreshMap()
    {
        // GenerateTextureAndRefreshMaterial(StrategicGameState.Instance.terrainMatrix);
        GenerateTextureAndRefreshMaterial();

        // Build Cell-oriented viewer map
        var width = StrategicGameState.Instance.GetMapWidth();
        var height = StrategicGameState.Instance.GetMapHeight();

        for (var x = 0; x < width; x++)
        {
            for (var y = 0; y < height; y++)
            {
                gridCellViewerMap[(x, y)] = new();
            }
        }

        var length = width * height;

        Utils.SyncTransformViewerLength(sideFlagContainerTransform, length, sideFlagPrefab);
        var sideFlagRenderers = sideFlagContainerTransform.GetComponentsInChildren<SpriteRenderer>();

        Utils.SyncTransformViewerLength(cellLabelContainerTransform, length, cellLabelPrefab);
        var cellLabels = cellLabelContainerTransform.GetComponentsInChildren<TMP_Text>();

        foreach (var ((x, y), cellViewer) in gridCellViewerMap)
        {
            var idx = x + y * width;
            cellViewer.flagRenderer = sideFlagRenderers[idx];
            cellViewer.text = cellLabels[idx];
            cellViewer.defaultTextColor = cellViewer.text.color;

            var (xf, yf) = CellXYToLocalXY(x, y);
            var vec = controlledRenderer.transform.TransformPoint(xf, yf, 0);

            cellViewer.flagRenderer.transform.position = new Vector3(vec.x, vec.y, 0);
            cellViewer.flagRenderer.gameObject.name = $"Flag_{x}_{y}";

            cellViewer.text.transform.position = new Vector3(vec.x, vec.y, 0);
            cellViewer.text.gameObject.name = $"Text_{x}_{y}";
        }

        RefreshSideFlags(); // SideState may not be ready here, so StrategicGameManager will require an extra call in the startup.
        RefreshCellLabelDisplayMode();
    }

    public void OnMapRebuilt(object sender, EventArgs args)
    {
        if(StrategicGameState.Instance.scenarioState.enableGridSystem)
        {
            RefreshMap();
        }
    }

    public void RefreshCellLabelDisplayMode(int x, int y)
    {
        var cellViewer = gridCellViewerMap[(x, y)];

        if (theaterOverlayActive)
        {
            cellViewer.text.text = theaterOverlayTexts.GetValueOrDefault((x, y), string.Empty);
            cellViewer.text.color = cellViewer.defaultTextColor;
            return;
        }

        if (influenceOverlayActive)
        {
            var value = influenceOverlayValues.GetValueOrDefault((x, y));
            cellViewer.text.text = StrategicInfluenceMapUtility.FormatValue(value);
            cellViewer.text.color = StrategicInfluenceMapUtility.GetValueColor(value, influenceOverlayMaxAbs);
            return;
        }

        cellViewer.text.color = cellViewer.defaultTextColor;

        // CellLabelDisplayMode
        if (cellLabelDisplayMode == CellLabelDisplayMode.None)
        {
            cellViewer.text.text = "";
        }
        else if (cellLabelDisplayMode == CellLabelDisplayMode.Label)
        {
            cellViewer.text.text = StrategicGameState.Instance.cellMatrix[x, y].Label?.GetShortName() ?? "";
        }
        else if (cellLabelDisplayMode == CellLabelDisplayMode.XY)
        {
            cellViewer.text.text = $"({x}, {y})";
        }
        else if (cellLabelDisplayMode == CellLabelDisplayMode.Coast)
        {
            cellViewer.text.text = StrategicGameState.Instance.cellMatrix[x, y].IsCoast ? "Coast" : "";
        }
    }

    public void RefreshCellLabelDisplayMode()
    {
        foreach (var (x, y) in gridCellViewerMap.Keys)
        {
            RefreshCellLabelDisplayMode(x, y);
        }
    }

    void RefreshSideFlagVisibility()
    {
        if (sideFlagContainerTransform != null)
        {
            sideFlagContainerTransform.gameObject.SetActive(showSideFlag && !influenceOverlayActive);
        }
    }

    public void SetInfluenceOverlay(Dictionary<(int, int), float> values, float maxAbs)
    {
        influenceOverlayValues = values != null
            ? new Dictionary<(int, int), float>(values)
            : new Dictionary<(int, int), float>();
        influenceOverlayActive = true;
        influenceOverlayMaxAbs = maxAbs;
        RefreshSideFlagVisibility();
        RefreshCellLabelDisplayMode();
    }

    public void ClearInfluenceOverlay()
    {
        influenceOverlayActive = false;
        influenceOverlayValues.Clear();
        influenceOverlayMaxAbs = 0f;
        RefreshSideFlagVisibility();
        RefreshCellLabelDisplayMode();
    }

    public void SetTheaterOverlay(IEnumerable<XY> cells)
    {
        var overlayTexts = (cells ?? Enumerable.Empty<XY>())
            .Where(cell => cell != null && string.IsNullOrWhiteSpace(cell.areaCellObjectId))
            .GroupBy(cell => (cell.x, cell.y))
            .ToDictionary(group => group.Key, _ => "X");
        SetTheaterOverlayTexts(overlayTexts);
    }

    public void SetTheaterOverlayTexts(IDictionary<(int, int), string> overlayTexts)
    {
        theaterOverlayTexts = (overlayTexts ?? new Dictionary<(int, int), string>())
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        theaterOverlayActive = true;
        RefreshCellLabelDisplayMode();
    }

    public void ClearTheaterOverlay()
    {
        theaterOverlayActive = false;
        theaterOverlayTexts.Clear();
        RefreshCellLabelDisplayMode();
    }

    public void RefreshSideFlags()
    {
        foreach (var xy in gridCellViewerMap.Keys)
        {
            SyncSideFlag(xy);
        }
        RefreshSideBorderMesh();
    }

    void SyncSideFlag((int, int) xy)
    {
        var (x, y) = xy;
        var cellViewer = gridCellViewerMap[xy];

        var hexSideStateObjectId = StrategicGameState.Instance.cellMatrix[x, y].sideObjectIdHex;
        // var hexSideStateObjectId = "dd43c3f3-1a02-46ca-b287-4ac069c23218";
        // if (hexSideStateObjectId == null)
        //     return;
        var sideState = EntityManager.Instance.Get<SideState>(hexSideStateObjectId);
        if (sideState == null || sideState.countries.Count == 0)
        {
            cellViewer.flagRenderer.sprite = null;
            return;
        }
        var name = sideState.countries[0].ToString();
        var path = $"{Application.streamingAssetsPath}/Pictures/Flags/{name}.png";
        // var path = $"{Application.streamingAssetsPath}/Pictures/Flags/Japan.png";
        UnityWebRequestImageReader.Instance.RequestIfNotRequestedYetOtherwiseExecuteDirectly(new()
        {
            path = path,
            spriteCallbacks = new()
            {
                sprite =>
                {
                    cellViewer.flagRenderer.sprite = sprite;
                }
            }
        });
    }

    // public void OnMapCellUpdated(object sender, (int, int) args)
    public void OnMapCellUpdated(object sender, Cell cell)
    {
        // var (x, y) = args;
        // Color32 color = new Color32((byte)StrategicGameState.Instance.terrainMatrix[x, y], 0, 0, 255);
        if (cell == null || !cell.IsGridCell())
            return;

        if (terrainTypeTexture == null || gridCellViewerMap == null || !gridCellViewerMap.ContainsKey((cell.x, cell.y)))
            return;

        if(cell.IsGridCell())
        {
            Color32 color = new Color32((byte)cell.terrain, 0, 0, 255);
            terrainTypeTexture.SetPixel(cell.x, cell.y, color);
            terrainTypeTexture.Apply();

            SyncSideFlag((cell.x, cell.y));
            RefreshCellLabelDisplayMode(cell.x, cell.y);
            RefreshSideBorderMesh();
        }
    }

    void RefreshSideBorderMesh()
    {
        if (controlledRenderer == null)
            return;

        var meshTransform = EnsureMeshRenderer(transform, "SideBorderMesh", ref sideBorderMesh, mapRenderer != null ? mapRenderer.sortingOrder + 3 : 2);
        var meshBuilder = new StrategicMapLineMeshBuilder();
        var gameState = StrategicGameState.Instance;
        var width = gameState.GetMapWidth();
        var height = gameState.GetMapHeight();

        for (var x = 0; x < width; x++)
        {
            for (var y = 0; y < height; y++)
            {
                var cell = gameState.cellMatrix[x, y];
                if (cell == null || !cell.IsGridCell())
                    continue;

                foreach (var edgeDirection in uniqueEdgeDirections)
                {
                    var neighbor = cell.GetNeighbor(edgeDirection);
                    if (neighbor == null || !neighbor.IsGridCell())
                        continue;

                    var cellSide = cell.GetHexSide();
                    var neighborSide = neighbor.GetHexSide();
                    if (cellSide == null && neighborSide == null)
                        continue;

                    if (cellSide != null && neighborSide != null && neighborSide.objectId == cellSide.objectId)
                        continue;

                    var (p0, p1) = GetHexEdgeLocalSegment(cell, edgeDirection, meshTransform);
                    var cellCenter = GetCellLocalCenter(cell, meshTransform);
                    var edgeCenter = (p0 + p1) * 0.5f;
                    var cellSideNormal = cellCenter - edgeCenter;
                    if (cellSideNormal.sqrMagnitude <= Mathf.Epsilon)
                        continue;

                    var cellColor = GetSidePrimaryCountryColor(cellSide);
                    var neighborColor = GetSidePrimaryCountryColor(neighborSide);
                    meshBuilder.AddSplitLine(p0, p1, cellSideNormal.normalized, sideBorderMeshWidth, cellColor, neighborColor);
                }
            }
        }

        meshBuilder.ApplyTo(sideBorderMesh);
    }

    static Color GetSidePrimaryCountryColor(SideState sideState)
    {
        if (sideState == null)
            return Color.gray;

        var country = sideState.countries.FirstOrDefault();
        return StyleConstants.countryColorMap.GetValueOrDefault(country, Color.gray);
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
        // transform.localScale = new Vector3(width, height, 0);
        controlledRenderer.transform.localScale = new Vector3(width * 0.867f, height, 0);
    }
}

sealed class StrategicMapLineMeshBuilder
{
    readonly List<Vector3> vertices = new();
    readonly List<int> triangles = new();
    readonly List<Color> colors = new();
    readonly List<Vector2> uvs = new();

    public void AddLine(Vector3 p0, Vector3 p1, float width, Color color)
    {
        var edge = p1 - p0;
        if (edge.sqrMagnitude <= Mathf.Epsilon)
            return;

        var normal = new Vector3(-edge.y, edge.x, 0f).normalized;
        var offset = normal * (width * 0.5f);
        AddQuad(p0 - offset, p1 - offset, p1 + offset, p0 + offset, color);
    }

    public void AddSplitLine(Vector3 p0, Vector3 p1, Vector3 sideNormal, float width, Color side0Color, Color side1Color)
    {
        var edge = p1 - p0;
        if (edge.sqrMagnitude <= Mathf.Epsilon || sideNormal.sqrMagnitude <= Mathf.Epsilon)
            return;

        var offset = sideNormal.normalized * (width * 0.5f);
        AddQuad(p0, p1, p1 + offset, p0 + offset, side0Color);
        AddQuad(p1, p0, p0 - offset, p1 - offset, side1Color);
    }

    void AddQuad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Color color)
    {
        var index = vertices.Count;
        vertices.Add(p0);
        vertices.Add(p1);
        vertices.Add(p2);
        vertices.Add(p3);

        colors.Add(color);
        colors.Add(color);
        colors.Add(color);
        colors.Add(color);

        uvs.Add(new Vector2(0f, 0f));
        uvs.Add(new Vector2(1f, 0f));
        uvs.Add(new Vector2(1f, 1f));
        uvs.Add(new Vector2(0f, 1f));

        triangles.Add(index);
        triangles.Add(index + 1);
        triangles.Add(index + 2);
        triangles.Add(index);
        triangles.Add(index + 2);
        triangles.Add(index + 3);
    }

    public void ApplyTo(Mesh mesh)
    {
        mesh.Clear();
        if (vertices.Count == 0)
            return;

        mesh.indexFormat = vertices.Count > 65535
            ? UnityEngine.Rendering.IndexFormat.UInt32
            : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.SetVertices(vertices);
        mesh.SetColors(colors);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
    }
}

static class StrategicLineRenderUtils
{
    const int SmoothSubdivision = 4;
    const int MaxSmoothSamples = 256;
    const int CornerVertices = 4;
    const int CapVertices = 4;

    public readonly struct RenderPolyline
    {
        public readonly Vector3[] positions;
        public readonly bool loop;

        public RenderPolyline(Vector3[] positions, bool loop)
        {
            this.positions = positions;
            this.loop = loop;
        }
    }

    readonly struct CellNodeKey
    {
        public readonly int x;
        public readonly int y;

        public CellNodeKey(int x, int y)
        {
            this.x = x;
            this.y = y;
        }
    }

    readonly struct CellEdgeKey
    {
        public readonly CellNodeKey a;
        public readonly CellNodeKey b;

        public CellEdgeKey(CellNodeKey lhs, CellNodeKey rhs)
        {
            if (Compare(lhs, rhs) <= 0)
            {
                a = lhs;
                b = rhs;
            }
            else
            {
                a = rhs;
                b = lhs;
            }
        }

        static int Compare(CellNodeKey lhs, CellNodeKey rhs)
        {
            var xCompare = lhs.x.CompareTo(rhs.x);
            return xCompare != 0 ? xCompare : lhs.y.CompareTo(rhs.y);
        }
    }

    sealed class CellNodeKeyEqualityComparer : IEqualityComparer<CellNodeKey>
    {
        public bool Equals(CellNodeKey lhs, CellNodeKey rhs) => lhs.x == rhs.x && lhs.y == rhs.y;

        public int GetHashCode(CellNodeKey obj) => System.HashCode.Combine(obj.x, obj.y);
    }

    sealed class CellEdgeKeyEqualityComparer : IEqualityComparer<CellEdgeKey>
    {
        public bool Equals(CellEdgeKey lhs, CellEdgeKey rhs) => lhs.a.x == rhs.a.x
            && lhs.a.y == rhs.a.y
            && lhs.b.x == rhs.b.x
            && lhs.b.y == rhs.b.y;

        public int GetHashCode(CellEdgeKey obj) => System.HashCode.Combine(obj.a.x, obj.a.y, obj.b.x, obj.b.y);
    }

    static readonly CellNodeKeyEqualityComparer cellNodeKeyComparer = new();
    static readonly CellEdgeKeyEqualityComparer cellEdgeKeyComparer = new();

    public static void ConfigureLineRenderer(LineRenderer lineRenderer, bool loop = false)
    {
        if (lineRenderer == null)
            return;

        lineRenderer.numCornerVertices = CornerVertices;
        lineRenderer.numCapVertices = CapVertices;
        lineRenderer.loop = loop;
    }

    public static Vector3[] BuildSmoothPolyline(IReadOnlyList<Vector3> anchors, bool loop = false)
    {
        if (anchors == null || anchors.Count == 0)
            return System.Array.Empty<Vector3>();

        if (anchors.Count <= 2)
            return anchors.ToArray();

        if (loop)
        {
            var sampleCount = Mathf.Min(MaxSmoothSamples, Mathf.Max(anchors.Count * SmoothSubdivision, anchors.Count));
            var result = new Vector3[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                var tGlobal = (float)i / sampleCount * anchors.Count;
                var segment = Mathf.FloorToInt(tGlobal);
                var t = tGlobal - segment;

                var p0 = anchors[Mod(segment - 1, anchors.Count)];
                var p1 = anchors[Mod(segment, anchors.Count)];
                var p2 = anchors[Mod(segment + 1, anchors.Count)];
                var p3 = anchors[Mod(segment + 2, anchors.Count)];
                result[i] = CatmullRom(p0, p1, p2, p3, t);
            }
            return result;
        }

        var openSampleCount = Mathf.Min(MaxSmoothSamples, Mathf.Max((anchors.Count - 1) * SmoothSubdivision + 1, anchors.Count));
        var openResult = new Vector3[openSampleCount];
        for (int i = 0; i < openSampleCount; i++)
        {
            var tGlobal = openSampleCount <= 1 ? 0f : (float)i / (openSampleCount - 1) * (anchors.Count - 1);
            var segment = Mathf.Clamp(Mathf.FloorToInt(tGlobal), 0, anchors.Count - 2);
            var t = tGlobal - segment;

            var p0 = anchors[Mathf.Max(segment - 1, 0)];
            var p1 = anchors[segment];
            var p2 = anchors[segment + 1];
            var p3 = anchors[Mathf.Min(segment + 2, anchors.Count - 1)];
            openResult[i] = CatmullRom(p0, p1, p2, p3, t);
        }

        openResult[0] = anchors[0];
        openResult[openSampleCount - 1] = anchors[anchors.Count - 1];
        return openResult;
    }

    public static List<RenderPolyline> BuildEdgeFeaturePolylines(
        IReadOnlyList<(Cell, Cell, EdgeDirection)> cellPairs,
        System.Func<Cell, Vector3> cellToPoint)
    {
        var polylines = new List<RenderPolyline>();
        if (cellPairs == null || cellPairs.Count == 0 || cellToPoint == null)
            return polylines;

        var nodeCells = new Dictionary<CellNodeKey, Cell>(cellNodeKeyComparer);
        var adjacency = new Dictionary<CellNodeKey, HashSet<CellNodeKey>>(cellNodeKeyComparer);
        var edges = new HashSet<CellEdgeKey>(cellEdgeKeyComparer);

        foreach (var (cellSrc, cellDst, _) in cellPairs)
        {
            if (cellSrc == null || cellDst == null || cellSrc.IsAreaCell() || cellDst.IsAreaCell())
                continue;

            var srcKey = new CellNodeKey(cellSrc.x, cellSrc.y);
            var dstKey = new CellNodeKey(cellDst.x, cellDst.y);
            var edgeKey = new CellEdgeKey(srcKey, dstKey);
            if (!edges.Add(edgeKey))
                continue;

            nodeCells[srcKey] = cellSrc;
            nodeCells[dstKey] = cellDst;
            AddNeighbor(adjacency, srcKey, dstKey);
            AddNeighbor(adjacency, dstKey, srcKey);
        }

        var visitedEdges = new HashSet<CellEdgeKey>(cellEdgeKeyComparer);
        foreach (var node in adjacency.Keys.OrderBy(key => key.x).ThenBy(key => key.y))
        {
            if (adjacency[node].Count == 2)
                continue;

            foreach (var neighbor in adjacency[node].OrderBy(key => key.x).ThenBy(key => key.y))
            {
                var edge = new CellEdgeKey(node, neighbor);
                if (visitedEdges.Contains(edge))
                    continue;

                var chainNodes = TraceOpenChain(node, neighbor, adjacency, visitedEdges);
                AppendPolyline(polylines, chainNodes, nodeCells, cellToPoint, false);
            }
        }

        foreach (var edge in edges.OrderBy(edgeKey => edgeKey.a.x).ThenBy(edgeKey => edgeKey.a.y).ThenBy(edgeKey => edgeKey.b.x).ThenBy(edgeKey => edgeKey.b.y))
        {
            if (visitedEdges.Contains(edge))
                continue;

            var loopNodes = TraceClosedLoop(edge, adjacency, visitedEdges);
            AppendPolyline(polylines, loopNodes, nodeCells, cellToPoint, true);
        }

        return polylines;
    }

    static void AppendPolyline(
        List<RenderPolyline> polylines,
        List<CellNodeKey> nodeKeys,
        Dictionary<CellNodeKey, Cell> nodeCells,
        System.Func<Cell, Vector3> cellToPoint,
        bool loop)
    {
        if (nodeKeys == null || nodeKeys.Count < 2)
            return;

        var anchorPoints = nodeKeys.Select(key => cellToPoint(nodeCells[key])).ToArray();
        polylines.Add(new RenderPolyline(BuildSmoothPolyline(anchorPoints, loop), loop));
    }

    static List<CellNodeKey> TraceOpenChain(
        CellNodeKey start,
        CellNodeKey next,
        Dictionary<CellNodeKey, HashSet<CellNodeKey>> adjacency,
        HashSet<CellEdgeKey> visitedEdges)
    {
        var chain = new List<CellNodeKey> { start };
        var prev = start;
        var current = next;
        visitedEdges.Add(new CellEdgeKey(prev, current));
        chain.Add(current);

        while (adjacency[current].Count == 2)
        {
            var neighbors = adjacency[current].ToList();
            var candidate = cellNodeKeyComparer.Equals(neighbors[0], prev) ? neighbors[1] : neighbors[0];
            var edge = new CellEdgeKey(current, candidate);
            if (visitedEdges.Contains(edge))
                break;

            visitedEdges.Add(edge);
            prev = current;
            current = candidate;
            chain.Add(current);
        }

        return chain;
    }

    static List<CellNodeKey> TraceClosedLoop(
        CellEdgeKey startEdge,
        Dictionary<CellNodeKey, HashSet<CellNodeKey>> adjacency,
        HashSet<CellEdgeKey> visitedEdges)
    {
        var chain = new List<CellNodeKey> { startEdge.a, startEdge.b };
        var start = startEdge.a;
        var prev = startEdge.a;
        var current = startEdge.b;
        visitedEdges.Add(startEdge);

        while (true)
        {
            var neighbors = adjacency[current].ToList();
            var candidate = cellNodeKeyComparer.Equals(neighbors[0], prev) ? neighbors[1] : neighbors[0];
            var edge = new CellEdgeKey(current, candidate);
            if (visitedEdges.Contains(edge))
                break;

            visitedEdges.Add(edge);
            prev = current;
            current = candidate;
            if (cellNodeKeyComparer.Equals(current, start))
                break;

            chain.Add(current);
        }

        return chain;
    }

    static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        var t2 = t * t;
        var t3 = t2 * t;
        return 0.5f * (
            (2f * p1) +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3
        );
    }

    static int Mod(int value, int divisor)
    {
        var result = value % divisor;
        return result < 0 ? result + divisor : result;
    }

    static void AddNeighbor(Dictionary<CellNodeKey, HashSet<CellNodeKey>> adjacency, CellNodeKey node, CellNodeKey neighbor)
    {
        if (!adjacency.TryGetValue(node, out var neighbors))
        {
            neighbors = new HashSet<CellNodeKey>(cellNodeKeyComparer);
            adjacency[node] = neighbors;
        }

        neighbors.Add(neighbor);
    }
}
