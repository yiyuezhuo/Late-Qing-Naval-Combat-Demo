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

        BindHexEdgeLineRenderers(
            riverContainerTransform, riverPrefab,
            StrategicGameState.Instance.IterateCellPairsFor(EdgeFeatureType.River).ToList()
        );

        if (blockSeaMovementContainerTransform != null && blockSeaMovementPrefab != null)
        {
            BindHexEdgeLineRenderers(
                blockSeaMovementContainerTransform, blockSeaMovementPrefab,
                StrategicGameState.Instance.IterateCellPairsFor(EdgeFeatureType.BlockSeaMovement).ToList(),
                lineColor: blockSeaMovementColor
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

    // void UpdateStrategicGroupIcons()
    // {
    //     // var observableStrategicGroups = StrategicGameState.Instance.GetObservabledStrategicGroups().ToList();
    //     var observableStrategicGroups = StrategicGameState.Instance.GetOrderedObservableStrategicGroups().ToList();
    //     // GetOrderedObservableStrategicGroups
    //     BindStrategicGroupIcons(strategicGroupIconTransform, strategicGroupIconPrefab, observableStrategicGroups);
    // }

    // void BindStrategicGroupIcons(Transform containerTransform, GameObject prefab, List<StrategicGroup> strategicGroups)
    // {
    //     Utils.SyncTransformViewerLength(containerTransform, strategicGroups.Count, prefab);

    //     var worldSpaceGroupIcons = containerTransform.GetComponentsInChildren<WorldSpaceGroupIcon>();
    //     var groupToView = new Dictionary<StrategicGroup, WorldSpaceGroupIcon>();
    //     for (int i = 0; i < strategicGroups.Count; i++)
    //     {
    //         var strategicGroup = strategicGroups[i];
    //         var worldSpaceGroupIcon = worldSpaceGroupIcons[i];
    //         worldSpaceGroupIcon.SetDataSource(strategicGroup);

    //         // var (xf, yf) = CellXYToLocalXY(strategicGroup.x, strategicGroup.y);
    //         // worldSpaceGroupIcon.transform.position = controlledRenderer.transform.TransformPoint(xf, yf, 0);

    //         groupToView[strategicGroup] = worldSpaceGroupIcon;
    //     }

    //     foreach (var g in strategicGroups.GroupBy(group => (group.x, group.y)))
    //     {
    //         (var x, var y) = g.Key;
    //         var (xf, yf) = CellXYToLocalXY(x, y);
    //         var vec = controlledRenderer.transform.TransformPoint(xf, yf, 0);

    //         // var gl = g.GroupBy(_g => _g.country).ToList();
    //         // var gl = g.GroupBy(_g => StrategicGameState.Instance.countryToSideStateMap[_g.country]).ToList();
    //         var gl = g.GroupBy(_g => _g.side).ToList();

    //         if (gl.Count == 1)
    //         {
    //             Utils.LayoutStackTransform(
    //                 gl[0].Select(gp => groupToView[gp].transform).ToList(),
    //                 new Vector3(vec.x, vec.y, 0),
    //                 0.05f
    //             );
    //         }
    //         else
    //         {
    //             // gl.Sort((gp1, gp2) => gp1.Key.name.english[0].CompareTo(gp2.Key.name.english[0])); // FIXME: Fragile to empty string
    //             var cell = gl.First().First().cell;

    //             var side0yScore = cell.GetMassCenterY(gl[0].Key);
    //             var side1yScore = cell.GetMassCenterY(gl[1].Key);

    //             var gTop = gl[0];
    //             var gBottom = gl[1];

    //             if (side0yScore < side1yScore)
    //             {
    //                 gTop = gl[1];
    //                 gBottom = gl[0];
    //             }
    //             else if(side0yScore == side1yScore)
    //             {
    //                 if(gl[0].Key.name.english[0] > gl[1].Key.name.english[1])
    //                 {
    //                     gTop = gl[1];
    //                     gBottom = gl[0];
    //                 }
    //             }

    //             Utils.LayoutStackTransform(
    //                 gTop.Select(gp => groupToView[gp].transform).ToList(),
    //                 new Vector3(vec.x, vec.y + 0.25f, 0),
    //                 0.05f
    //             );

    //             // Assume 2 sides can be in the same hex at most.
    //             Utils.LayoutStackTransform(
    //                 gBottom.Select(gp => groupToView[gp].transform).ToList(),
    //                 new Vector3(vec.x, vec.y - 0.25f, 0),
    //                 0.05f
    //             );
    //         }
    //     }
    // }


    // void UpdatePathLines()
    // {
    //     // Update Path Lines
    //     var pathLineActiveStrategicGroups = new List<StrategicGroup>();

    //     var selectedGroup = StrategicGameManager.Instance.lastSelectedStrategicGroup; // Consider only the selected strategic group now.
    //     if (selectedGroup != null)
    //     {
    //         pathLineActiveStrategicGroups.Add(selectedGroup);
    //     }

    //     Utils.SyncTransformViewerLength(pathLineContainerTransform, pathLineActiveStrategicGroups.Count, pathLinePrefab);
    //     var pathLineControllers = pathLineContainerTransform.GetComponentsInChildren<PathLineController>();

    //     for (int i = 0; i < pathLineActiveStrategicGroups.Count; i++)
    //     {
    //         var group = pathLineActiveStrategicGroups[i];
    //         var controller = pathLineControllers[i];
    //         var progressPercent = group.moveProgressionKm / 50;
    //         controller.Sync(group.plannedPath, progressPercent);
    //     }
    // }


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

    public void RefreshSideFlags()
    {
        foreach (var xy in gridCellViewerMap.Keys)
        {
            SyncSideFlag(xy);
        }
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
        if(cell.IsGridCell())
        {
            Color32 color = new Color32((byte)cell.terrain, 0, 0, 255);
            terrainTypeTexture.SetPixel(cell.x, cell.y, color);
            terrainTypeTexture.Apply();

            SyncSideFlag((cell.x, cell.y));
            RefreshCellLabelDisplayMode(cell.x, cell.y);
        }
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
