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
    public Transform strategicGroupIconTransform;
    public Transform sideFlagContainerTransform;
    public Transform cellLabelContainerTransform;
    // public Transform pathLineContainerTransform;
    // public Transform missionWaypointLineContainerTransform;

    public GameObject locationLabelPrefab;
    public GameObject roadPrefab;
    public GameObject railroadPrefab;
    public GameObject riverPrefab;
    public GameObject strategicGroupIconPrefab;
    public GameObject sideFlagPrefab;
    public GameObject cellLabelPrefab;
    // public GameObject missionWaypointLinePrefab;

    public SpriteRenderer mapRenderer;

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
                sideFlagContainerTransform.gameObject.SetActive(showSideFlag);
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

        sideFlagContainerTransform.gameObject.SetActive(showSideFlag);

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

    void BindHexCrossLineRenderers(Transform containerTransform, GameObject prefab, List<(Cell, Cell, EdgeDirection)> cellPairs, float z = 0, float xOffset = 0, float yOffset = 0)
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
            var p0 = controlledRenderer.transform.TransformPoint(xf1 + xOffset / width, yf1 + yOffset / height, z);
            var p1 = controlledRenderer.transform.TransformPoint(xf2 + xOffset / width, yf2 + yOffset / height, z);
            lineRenderer.SetPositions(new Vector3[2] { p0, p1 });
            // lineRenderer.SetPositions(new Vector3[2]{
            //     new Vector3(xf1 + xOffset / width, yf1 + yOffset / height, z),
            //     new Vector3(xf2 + xOffset / width, yf2 + yOffset / height, z)
            // });
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
            var p0 = controlledRenderer.transform.TransformPoint(xf + dx1, yf + dy1, z);
            var p1 = controlledRenderer.transform.TransformPoint(xf + dx2, yf + dy2, z);
            lineRenderer.SetPositions(new Vector3[2] { p0, p1 });
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
    }

    Dictionary<(int, int), CellViewer> gridCellViewerMap = new();
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
