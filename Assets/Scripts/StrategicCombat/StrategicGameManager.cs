
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Unity.Properties;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using System;
using System.Collections;
using UnityEngine.SceneManagement;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;


using CoreUtils;
using StrategicCombatCore;
using NavalCombatCore;
using YYZ.PathFinding;


public enum StrategicMapEditMode
{
    Select,
    PaintTerrain,
    PaintHexControlSide,
    // CreateOrEditLabel,
    // DeleteLabel,
    PaintHexPairFeatureBegin,
    PaintHexPairFeatureEnd,
    DeleteHexPairFeatureBegin,
    DeleteHexPairFeatureEnd,
    ToggleCoast,
    WaitOneshotCellClickCallback,
    WaypointPlotting
}

public class StrategicGameManager : SingletonMonoBehaviour<StrategicGameManager>
{
    [CreateProperty]
    public StrategicGameState gameState => StrategicGameState.Instance;

    public StrategicMapEditMode mapEditMode;
    public TerrainType currentTerrainType;
    public int tempMapWidth = 60;
    public int tempMapHeight = 40;
    public EdgeFeatureType currentEdgeFeatureType;

    
    public Transform gridSystemTransform;
    public Transform areaSystemTransform;
    // TODO: Extract and move those Area System related stuff to another class? 
    public Transform hitAreasRootTransform;
    public Transform counterContainerTransform;
    public GameObject strategicGroupIconPrefab;

    public bool fullInitialized = false;

    public class StartupConfig
    {
        public enum Mode
        {
            Empty,
            ReturnFromNavalGame,
            ScenPath
        }

        public Mode mode = Mode.ScenPath;
        // public Mode mode = Mode.Empty;
        // public Vector2 cameraPosXY;
        // public float cameraZoom;
        public StrategicViewState viewState; // reserved for ReturnFromNavalGame only now
        // public string scenSubPath = "Scenarios/StrategicGameState.xml";
        public string scenSubPath = "Scenarios/Vladivostok Squadron Raiding.xml";
        // public string scenSubPath = "Scenarios/First Sino-Japanese War.xml";
        public List<ShipLog> syncShipLogs;
        public VictoryStatus victoryStatus;
    }

    public static string lastOpenedScenarioPath; // Used to suggest save file name

    public static StartupConfig startupConfig = new StartupConfig();

    // public static string initialScenPath = "Strategic/StrategicGameState.xml";

    [CreateProperty]
    public bool showReferenceMap
    {
        get => HexMapShower.Instance.showReferenceMap;
        set => HexMapShower.Instance.showReferenceMap = value;
    }

    [CreateProperty]
    public bool showBorder
    {
        get => HexMapShower.Instance.showBorder;
        set => HexMapShower.Instance.showBorder = value;
    }

    [CreateProperty]
    public bool showAccurateSeaLand
    {
        get => HexMapShower.Instance.showAccurateSeaLand;
        set => HexMapShower.Instance.showAccurateSeaLand = value;
    }

    public Cell lastSelectedCell;
    public StrategicGroup lastSelectedStrategicGroup;
    Action<Cell> oneshotCellClickCallback;

    [CreateProperty]
    public bool showSideFlag
    {
        get => HexMapShower.Instance.showSideFlag;
        set => HexMapShower.Instance.showSideFlag = value;
    }

    public string currentSideStateObjectId;

    public bool currentLogOnly = true;

    public Transform pathLineContainerTransform;
    public GameObject pathLinePrefab;


    [CreateProperty]
    public string currentSideStateName => EntityManager.Instance.Get<SideState>(currentSideStateObjectId)?.name?.mergedName ?? "";

    [CreateProperty]
    public CellLabelDisplayMode cellLabelDisplayMode
    {
        get => HexMapShower.Instance.cellLabelDisplayMode;
        set => HexMapShower.Instance.cellLabelDisplayMode = value;
    }

    void Start()
    {
        GamePreference.Instance.SetShortLabelLanguageTypeByLocale(LocalizationSettings.SelectedLocale);

        var width = tempMapWidth;
        var height = tempMapHeight;

        SuperGameState.Instance.currentGameMode = GameMode.Strategic;

        // Default state
        if (StrategicGameState.Instance.cellMatrix == null)
        {
            StrategicGameState.Instance.GenerateTerrainMatrix(width, height);
        }

        if (startupConfig.mode == StartupConfig.Mode.Empty)
        {
            Debug.Log("Empty mode startup");

            HexMapShower.Instance.Refresh();

            FinishInitialization();
        }
        else if (startupConfig.mode == StartupConfig.Mode.ReturnFromNavalGame)
        {
            Debug.Log("ReturnFromNavalGame mode startup");

            RestoreFromReturnFromNavalGame();
            HexMapShower.Instance.Refresh();

            FinishInitialization();
        }
        else if (startupConfig.mode == StartupConfig.Mode.ScenPath)
        {
            Debug.Log($"ScenPath mode startup: {startupConfig.scenSubPath}");

            lastOpenedScenarioPath = startupConfig.scenSubPath;

            // Try to fetch default scenario file and update the state
            var scenFullPath = Application.streamingAssetsPath + "/" + startupConfig.scenSubPath;
            StartCoroutine(StreamingTextAssetManager.Instance.FetchText(scenFullPath, initialScenText =>
            {
                // StartCoroutine(
                //     OnScenTextLoaded(initialScenText)
                // );
                IEnumerator Cor()
                {
                    yield return OnScenTextLoaded(initialScenText);

                    FinishInitialization();
                }

                StartCoroutine(Cor());
            }));

            // fullInitialized = true; // Moved to OnScenTextLoaded (Fuck Unity' async model)
        }
    }

    protected void Awake()
    {
        var gameState = StrategicGameState.Instance;

        gameState.mapRebuilt += OnMapRebuilt;
        gameState.mapCellUpdated += OnMapCellUpdated;

        GamePreference.Instance.shortLabelLanguageTypeChanged += OnShortLabelLanguageTypeChanged;
    }

    public override void OnDestroy()
    {
        base.OnDestroy();

        var gameState = StrategicGameState.Instance;
        gameState.mapRebuilt -= OnMapRebuilt;
        gameState.mapCellUpdated -= OnMapCellUpdated;
        GamePreference.Instance.shortLabelLanguageTypeChanged -= OnShortLabelLanguageTypeChanged;
    }

    void OnShortLabelLanguageTypeChanged(object sender, EventArgs e)
    {
        if(StrategicGameState.Instance.scenarioState.enableAreaSystem)
        {
            RefreshAllAreaCellLabel();
        }
    }

    void OnMapRebuilt(object sender, EventArgs args)
    {
        if(StrategicGameState.Instance.scenarioState.enableAreaSystem)
        {
            RefreshAllAreaCellLabel();
        }
    }

    void OnMapCellUpdated(object sender, Cell cell)
    {
        if(cell.IsAreaCell()) // Grid Cell is handled by other handler
        {
            if(areaCellObjectIdToHitArea.TryGetValue(cell.objectId, out var hitArea))
            {
                hitArea.SyncLabel();
            }
        }
    }

    void RefreshAllAreaCellLabel()
    {
        foreach(var areaCell in areaCellObjectIdToHitArea.Values)
        {
            areaCell.SyncLabel();
        }
    }

    public void PrepareReturnFromNavalGame()
    {
        var pos = PlaneCameraController.Instance.transform.position;

        startupConfig = new()
        {
            mode = StartupConfig.Mode.ReturnFromNavalGame,
            // cameraPosXY = new Vector2(pos.x, pos.y),
            // cameraZoom = PlaneCameraController.Instance.cam.orthographicSize
            viewState = CaptureViewState()
        };
    }

    public void RestoreFromReturnFromNavalGame()
    {
        // var trans = PlaneCameraController.Instance.transform;
        // trans.position = new Vector3(startupConfig.cameraPosXY.x, startupConfig.cameraPosXY.y, trans.position.z);
        // PlaneCameraController.Instance.cam.orthographicSize = startupConfig.cameraZoom;
        ApplyViewState(startupConfig.viewState);

        StrategicGameState.Instance.UpdateFromTacticalResult(startupConfig.syncShipLogs, startupConfig.victoryStatus);
    }

    public IEnumerator OnScenTextLoaded(string initialScenText)
    {
        var fullState = XmlUtils.FromXML<StrategicFullState>(initialScenText);
        var strategicGameState = fullState.gameState;
        // var strategicGameState = XmlUtils.FromXML<StrategicGameState>(initialScenText);
        StrategicGameState.Instance.UpdateTo(strategicGameState);
        // StrategicGameState.Instance.ResetAndRegisterAll(); // workaround for view update bug happend when wait following yield return 

        // TODO: Save StreamingAssetReference state in the StrategicGameState?
        yield return StreamingAssetReference.Instance.TryToCompleteFromStreamingAssetReference(StrategicGameState.Instance);

        StrategicGameState.Instance.ResetAndRegisterAll();

        HexMapShower.Instance.RefreshSideFlags();
        // HexMapShower.Instance.showSideFlag = false;

        ApplyViewState(fullState.viewState);

        // if(StrategicGameState.Instance.scenarioState.enableAreaSystem)
        // {
        //     StrategicGameState.Instance.InvokeMapRebuilt();
        // }

        TempFix();

        // fullInitialized = true;
    }

    public Dictionary<string, HitArea> areaCellObjectIdToHitArea = new();

    public void FinishInitialization() // I wonder if is it better to use a dedicated method for this.
    {
        StrategicGameState.Instance.InvokeMapRebuilt();

        RefreshGridSystemAreaSystemVisibility();
        
        fullInitialized = true; // enable all independent observer (eg Update based view state controller)

        throw new Exception("Test Exception");
    }

    void ApplyViewState(StrategicViewState viewState)
    {
        // Update Camera according to the view state
        // var cam = Camera.main;

        var cam = PlaneCameraController.Instance.cam;
        cam.transform.position = new Vector3(
            viewState.xPosition,
            viewState.yPosition,
            cam.transform.position.z
        );
        cam.orthographicSize = viewState.orthographicSize;

        var hitAreaMap = hitAreasRootTransform.GetComponentsInChildren<HitArea>(true).ToDictionary(h => h.hitAreaObjectId, h => h);
        foreach(var hitAreaMapRecord in viewState.hitAreaMapRecords)
        {
            if(hitAreaMap.TryGetValue(hitAreaMapRecord.hitAreaObjectId, out var hitArea))
            {
                hitArea.areaCellObjectId = hitAreaMapRecord.areaCellObjectId;
                areaCellObjectIdToHitArea[hitAreaMapRecord.areaCellObjectId] = hitArea;
            }
            else
            {
                Debug.LogWarning($"Misasligned map: {hitAreaMapRecord.hitAreaObjectId} -> {hitAreaMapRecord.areaCellObjectId}");
            }
        }
    }

    public static void TempFix()
    {
        // foreach(var group in StrategicGameState.Instance.strategicGroups)
        // {
        //     foreach(var unitRef in group.subordinatesCombined)
        //     {
        //         var unit = unitRef.Get();
        //         unit.strategicGroupReference.referenceId = group.objectId;
        //     }
        // }

        // var lines = string.Join("\n", StrategicGameState.Instance.landUnits.Select(landUnit => landUnit.name.chineseSimplified));
        // Debug.Log(lines);

        // foreach (var group in StrategicGameState.Instance.strategicGroups)
        // {
        //     var parentGroup = group.strategicGroupReference.Get();
        //     if (parentGroup != null)
        //     {
        //         var matched = parentGroup.subordinatesCombined.Any(ordRef => ordRef.Get() == group);
        //         if (!matched)
        //         {
        //             Debug.Log($"Fix: {group.name.mergedName}");
        //             group.strategicGroupReference.referenceId = null;
        //         }
        //     }
        // }

        // foreach (var label in StrategicGameState.Instance.labels)
        // {
        //     var cell = StrategicGameState.Instance.cellMatrix[label.x, label.y];
        //     cell.Label = label.name;
        // }

        // foreach (var cell in StrategicGameState.Instance.cellMatrix)
        // {
        //     cell.GroundControlPoint = cell.groundControlPoint;
        // }

        // HexMapShower.Instance.RefreshBindSideFlags();

        // foreach (var ((x, y), hexInfo) in StrategicGameState.Instance.hexInfoMap)
        // {
        //     var hex = StrategicGameState.Instance.cellMatrix[x, y];
        //     hex.StrategicGroupReferences.AddRange(hexInfo.strategicGroupReferences);
        // }

        // foreach (var hexInfo in StrategicGameState.Instance.hexInfoMap.Values)
        // {
        //     hexInfo.strategicGroupReference.Clear();
        // }

        // foreach (var group in StrategicGameState.Instance.strategicGroups)
        // {
        //     if(group.deployState == StrategicGroup.DeployState.Independent)
        //         group.DeployToXY(group.x, group.y);
        // }

        // foreach (var group in StrategicGameState.Instance.strategicGroups)
        // {
        //     foreach (var subordinate in group.subordinatesCombined)
        //     {
        //         var obj = subordinate.Get();
        //         obj.strategicGroupReference.referenceId = group.objectId;
        //     }
        // }

        // foreach (var namedShip in StrategicGameState.Instance.namedShips)
        // {
        //     namedShip.defaultLeaderReference = new LeaderReference()
        //     {
        //         referenceObjectId = namedShip.defaultLeaderObjectId
        //     };
        // }
        // foreach (var cell in StrategicGameState.Instance.cellMatrix)
        // {
        //     cell.longitude = cell.longtitude;
        // }

        
    }

    public Vector2 ToCenter(Vector2 xy)
    {
        if (xy.x % 2 >= 1)
        {
            return new Vector2(Mathf.Floor(xy.x), Mathf.Floor(xy.y)) + new Vector2(0.5f, 1.0f);
        }
        return new Vector2(Mathf.Floor(xy.x), Mathf.Floor(xy.y)) + new Vector2(0.5f, 0.5f);
    }

    public Vector2Int FromCenter(Vector2 xy)
    {
        if (xy.x % 2 >= 1)
        {
            var _xy = xy - new Vector2(0.5f, 1.0f);
            return new Vector2Int(Mathf.RoundToInt(_xy.x), Mathf.RoundToInt(_xy.y));
        }
        else
        {
            var _xy = xy - new Vector2(0.5f, 0.5f);
            return new Vector2Int(Mathf.RoundToInt(_xy.x), Mathf.RoundToInt(_xy.y));
        }
    }

    public Vector2Int GetCellXY(Vector2 uv)
    {
        var xy = uv * new Vector2(StrategicGameState.Instance.GetMapWidth(), StrategicGameState.Instance.GetMapHeight());

        float minMag = float.MaxValue;
        Vector2 minCenter = Vector2.zero;

        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                var testCenter = ToCenter(xy + new Vector2(dx, dy));
                var testDiff = testCenter - xy;
                if (testDiff.sqrMagnitude < minMag)
                {
                    minMag = testDiff.sqrMagnitude;
                    minCenter = testCenter;
                }
            }
        }

        return FromCenter(minCenter);
    }

    public void Update()
    {
        if(fullInitialized)
        {
            UpdateView();
            HandleInput();
        }
    }

    void UpdatePathLines()
    {
        // Update Path Lines
        var pathLineActiveStrategicGroups = new List<StrategicGroup>();

        var selectedGroup = lastSelectedStrategicGroup; // Consider only the selected strategic group now.
        if (selectedGroup != null)
        {
            pathLineActiveStrategicGroups.Add(selectedGroup);
        }

        Utils.SyncTransformViewerLength(pathLineContainerTransform, pathLineActiveStrategicGroups.Count, pathLinePrefab);
        var pathLineControllers = pathLineContainerTransform.GetComponentsInChildren<PathLineController>();

        for (int i = 0; i < pathLineActiveStrategicGroups.Count; i++)
        {
            var group = pathLineActiveStrategicGroups[i];
            var controller = pathLineControllers[i];
            // var progressPercent = group.moveProgressionKm / 50;
            var valid = group.TryGetDistanceToNextLocationInPlannedPathWithoutProgression(out var cellDistKm);
            var progressPercent = valid ? group.moveProgressionKm / cellDistKm : 0;
            controller.Sync(group.plannedPath, progressPercent);
        }
    }


    void UpdateView()
    {
        // var gameState = StrategicGameState.Instance;
        // if(gameState.scenarioState.enableAreaSystem)
        // {
        //     var observableStrategicGroups = StrategicGameState.Instance.GetOrderedObservableStrategicGroups().Where(g => g.IsOnAreaCell()).ToList();

        //     BindAreaSystemStrategicGroupIcons(areaSystemCounterContainerTransform, strategicGroupIconPrefab, observableStrategicGroups);
        // }

        var observableStrategicGroups = StrategicGameState.Instance.GetOrderedObservableStrategicGroups().ToList();
        BindStrategicGroupIcons(counterContainerTransform, strategicGroupIconPrefab, observableStrategicGroups);

        UpdatePathLines();
    }

    void BindStrategicGroupIcons(Transform containerTransform, GameObject prefab, List<StrategicGroup> strategicGroups)
    {
        // Sync Views & create mapping
        Utils.SyncTransformViewerLength(containerTransform, strategicGroups.Count, prefab);

        var worldSpaceGroupIcons = containerTransform.GetComponentsInChildren<WorldSpaceGroupIcon>();
        var groupToView = new Dictionary<StrategicGroup, WorldSpaceGroupIcon>();
        for (int i = 0; i < strategicGroups.Count; i++)
        {
            var strategicGroup = strategicGroups[i];
            var worldSpaceGroupIcon = worldSpaceGroupIcons[i];
            worldSpaceGroupIcon.SetDataSource(strategicGroup);

            groupToView[strategicGroup] = worldSpaceGroupIcon;
        }

        // Area System Binding
        var strategicGroupsOnArea = strategicGroups.Where(g => g.IsOnAreaCell());

        foreach(var g in strategicGroupsOnArea.GroupBy(group => group.areaCellObjectId))
        {
            if(areaCellObjectIdToHitArea.TryGetValue(g.Key, out var hitArea))
            {
                var xf = hitArea.transform.position.x;
                var yf = hitArea.transform.position.y;

                Utils.LayoutStackTransform(
                    g.Select(gp => groupToView[gp].transform).ToList(),
                    new Vector3(xf, yf, 0),
                    0.05f
                );
            }
        }

        // Grid System Binding
        var strategicGroupsOnGrid = strategicGroups.Where(g => g.IsOnGridCell());
        var hexMapShower = HexMapShower.Instance;
        foreach (var g in strategicGroupsOnGrid.GroupBy(group => (group.x, group.y)))
        {
            (var x, var y) = g.Key;
            var (xf, yf) = HexMapShower.CellXYToLocalXY(x, y);
            var vec = hexMapShower.controlledRenderer.transform.TransformPoint(xf, yf, 0);

            // var gl = g.GroupBy(_g => _g.country).ToList();
            // var gl = g.GroupBy(_g => StrategicGameState.Instance.countryToSideStateMap[_g.country]).ToList();
            var gl = g.GroupBy(_g => _g.side).ToList();

            if (gl.Count == 1)
            {
                Utils.LayoutStackTransform(
                    gl[0].Select(gp => groupToView[gp].transform).ToList(),
                    new Vector3(vec.x, vec.y, 0),
                    0.05f
                );
            }
            else
            {
                // gl.Sort((gp1, gp2) => gp1.Key.name.english[0].CompareTo(gp2.Key.name.english[0])); // FIXME: Fragile to empty string
                var cell = gl.First().First().cell;

                var side0yScore = cell.GetMassCenterY(gl[0].Key);
                var side1yScore = cell.GetMassCenterY(gl[1].Key);

                var gTop = gl[0];
                var gBottom = gl[1];

                if (side0yScore < side1yScore)
                {
                    gTop = gl[1];
                    gBottom = gl[0];
                }
                else if(side0yScore == side1yScore)
                {
                    if(gl[0].Key.name.english[0] > gl[1].Key.name.english[1])
                    {
                        gTop = gl[1];
                        gBottom = gl[0];
                    }
                }

                Utils.LayoutStackTransform(
                    gTop.Select(gp => groupToView[gp].transform).ToList(),
                    new Vector3(vec.x, vec.y + 0.25f, 0),
                    0.05f
                );

                // Assume 2 sides can be in the same hex at most.
                Utils.LayoutStackTransform(
                    gBottom.Select(gp => groupToView[gp].transform).ToList(),
                    new Vector3(vec.x, vec.y - 0.25f, 0),
                    0.05f
                );
            }
        }
    }

    // public void BindAreaSystemStrategicGroupIcons(Transform containerTransform, GameObject prefab, List<StrategicGroup> strategicGroups)
    // {
    //     var gameState = StrategicGameState.Instance;

    //     Utils.SyncTransformViewerLength(containerTransform, strategicGroups.Count, prefab);

    //     var worldSpaceGroupIcons = containerTransform.GetComponentsInChildren<WorldSpaceGroupIcon>();
    //     var groupToView = new Dictionary<StrategicGroup, WorldSpaceGroupIcon>();
    //     for (int i = 0; i < strategicGroups.Count; i++)
    //     {
    //         var strategicGroup = strategicGroups[i];
    //         var worldSpaceGroupIcon = worldSpaceGroupIcons[i];
    //         worldSpaceGroupIcon.SetDataSource(strategicGroup);

    //         groupToView[strategicGroup] = worldSpaceGroupIcon;
    //     }

    //     foreach(var g in strategicGroups.GroupBy(group => group.areaCellObjectId))
    //     {
    //         if(areaCellObjectIdToHitArea.TryGetValue(g.Key, out var hitArea))
    //         {
    //             var xf = hitArea.transform.position.x;
    //             var yf = hitArea.transform.position.y;

    //             Utils.LayoutStackTransform(
    //                 g.Select(gp => groupToView[gp].transform).ToList(),
    //                 new Vector3(xf, yf, 0),
    //                 0.05f
    //             );
    //         }
    //     }
    // }

    void HandleInput()
    {
        var controlPressing = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        var altPressing = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

        if (!EventSystem.current.IsPointerOverGameObject())
        {
            var leftClicking = Input.GetMouseButtonDown(0);
            var rightClicking = Input.GetMouseButtonDown(1);

            if (leftClicking || rightClicking) // left click
            {
                var cam = PlaneCameraController.Instance.cam;

                // UITK World Spcace enforce a 3D collider, so we can only use 3D Raycast
                var ray = cam.ScreenPointToRay(Input.mousePosition);
                if (mapEditMode == StrategicMapEditMode.Select && Physics.Raycast(ray, out var hitInfo) && hitInfo.collider.CompareTag("Icon")) // click on group
                {
                    // Group Inco Click

                    Debug.Log($"hitInfo.collider={hitInfo.collider}");

                    var group = hitInfo.collider.GetComponent<WorldSpaceGroupIcon>()?.currentDataSource;
                    var groupSide = group.side;
                    // var hexInfo = group.hexInfo;
                    var strategicGroupReferences = group.cell.StrategicGroupReferences;
                    var currentStack = group.currentStack;
                    var topStackGroup = currentStack[^1];

                    Debug.Log($"group={group}, groupSide={groupSide}, currentStack={currentStack}, topStackGroup={topStackGroup}");

                    if (rightClicking && lastSelectedStrategicGroup == topStackGroup)
                    {
                        // lastSelectedStrategicGroup = topStackGroup;
                        var idx = StrategicGameState.Instance.strategicGroups.IndexOf(topStackGroup);
                        if (group != null && idx != -1)
                        {
                            StrategicGroupEditor.Instance.Show();
                            BehaviourUtils.Instance.ScheduleToSetSelectionForListView(StrategicGroupEditor.Instance.objectListView, idx);
                        }
                    }

                    if (leftClicking)
                    {
                        if (lastSelectedStrategicGroup != topStackGroup) // New Click => Select
                        {
                            lastSelectedStrategicGroup = topStackGroup;
                        }
                        else // Repeat Left Click => Toggle Stack
                        {
                            strategicGroupReferences.RemoveAll(r => r.referenceId == topStackGroup.objectId);
                            strategicGroupReferences.Insert(0, new() { referenceId = topStackGroup.objectId });
                            currentStack = group.currentStack;
                            topStackGroup = currentStack[^1];

                            lastSelectedStrategicGroup = topStackGroup;
                        }
                    }

                    lastSelectedCell = group.cell;

                    // hexInfo.strategicGroupReferences.Select(r => r.Get()).Where(g => g.country)
                }
                else if (leftClicking) // click on map (cell)
                {
                    var worldPoint = cam.ScreenToWorldPoint(Input.mousePosition);

                    var hit = Physics2D.Raycast(worldPoint, Vector2.zero);
                    if (hit.collider != null)
                    {

                        if (hit.collider.CompareTag("Map")) // Grid System: Map
                        {
                            // Map Click
                            Debug.Log($"Map Hit: {hit.collider} {hit.point}");

                            var localPoint = hit.collider.transform.InverseTransformPoint(hit.point);
                            var uv = new Vector2(localPoint.x + 0.5f, localPoint.y + 0.5f);
                            var cellXY = GetCellXY(uv);

                            Debug.Log($"localPoint={localPoint}, cellXY={cellXY}");

                            if (cellXY.x >= 0 && cellXY.x < StrategicGameState.Instance.GetMapWidth() && cellXY.y >= 0 && cellXY.y < StrategicGameState.Instance.GetMapHeight())
                            {
                                var activeCell = StrategicGameState.Instance.cellMatrix[cellXY.x, cellXY.y];
                                HandleCellClick(activeCell);
                            }
                        }
                        else if(hit.collider.CompareTag("Hit Area")) // Area System: Hit Area
                        {
                            // Map Click
                            Debug.Log($"Hit Area Hit: {hit.collider} {hit.point}");

                            var hitArea = hit.collider.GetComponent<HitArea>();
                            if(hitArea != null && hitArea.hitAreaObjectId != null && hitArea.hitAreaObjectId != "")
                            {
                                if(hitArea.areaCellObjectId == null || hitArea.areaCellObjectId == "") // Shit unity serializer hassle
                                {
                                    Debug.Log($"(Placeholder) Create a dynamic Area Cell and bind to it: {hitArea.hitAreaObjectId}");
                                    DialogRoot.Instance.PopupUnbindHitAreaDialog(hitArea);
                                }
                                else // Normal map click
                                {
                                    Debug.Log($"(Placeholder) to handle a map click: {hitArea.hitAreaObjectId}");
                                    var areaCell = EntityManager.Instance.Get<Cell>(hitArea.areaCellObjectId);
                                    if(areaCell != null)
                                    {
                                        HandleCellClick(areaCell);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (Input.GetKeyDown(KeyCode.Insert))
            {
                ScheduleOneshotCellClickCallback(cell =>
                {
                    DialogRoot.Instance.PopupStrategicGroupPickerDialog(group =>
                    {
                        // group.MoveToXY(cell.x, cell.y, false);
                        group.MoveToCell(cell, false);
                    });
                    // Debug.Log("ScheduleOneshotCellClickCallback"); // Popup Dialog to select a group.
                    mapEditMode = StrategicMapEditMode.Select;
                });
            }
            if (Input.GetKeyDown(KeyCode.M) && altPressing)
            {
                StartToEditMove();
            }
            if (Input.GetKeyDown(KeyCode.M) && !altPressing)
            {
                TryToStartMakeNewMove();
            }
            if (Input.GetKeyDown(KeyCode.A))
            {
                TryToStartAppendMove();
            }
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                mapEditMode = StrategicMapEditMode.Select;
            }
        }
    }

    public void TryToStartAppendMove()
    {
        if (lastSelectedStrategicGroup == null)
            return;

        ScheduleOneshotCellClickCallback(cell =>
        {
            var strategicGroup = lastSelectedStrategicGroup;

            var p = strategicGroup.plannedPath;
            var appending = p.Count >= 2;
            var srcCell = appending ? StrategicGameState.Instance.cellMatrix[p[^1].x, p[^1].y] : strategicGroup.cell;
            var dstCell = cell;

            IGraphEnumerable<Cell> graph = strategicGroup.IsArmy() ? new DynamicCellGraphArmy() : new DynamicCellGraphNavy();

            var pathCells = PathFinding<Cell>.AStar(graph, srcCell, dstCell);
            if (appending)
            {
                // strategicGroup.plannedPath.AddRange(pathCells.Skip(1).Select(c => new XY() { x = c.x, y = c.y }));
                strategicGroup.plannedPath.AddRange(pathCells.Skip(1).Select(c => c.ToXY()));
            }
            else
            {
                strategicGroup.plannedPath.Clear();
                // strategicGroup.plannedPath.AddRange(pathCells.Select(c => new XY() { x = c.x, y = c.y }));
                strategicGroup.plannedPath.AddRange(pathCells.Select(c => c.ToXY()));
                strategicGroup.moveProgressionKm = 0;
            }

            Debug.Log("Append path");
        });
    }

    public void TryToStartMakeNewMove()
    {
        if (lastSelectedStrategicGroup == null)
            return;

        ScheduleOneshotCellClickCallback(cell =>
        {
            var strategicGroup = lastSelectedStrategicGroup;
            // TODO: Set PlannedPath
            if (strategicGroup.deployState == StrategicGroup.DeployState.Independent)
            {
                var srcCell = strategicGroup.cell;
                var dstCell = cell;

                IGraphEnumerable<Cell> graph = strategicGroup.IsArmy() ? new DynamicCellGraphArmy() : new DynamicCellGraphNavy();

                var pathCells = PathFinding<Cell>.AStar(graph, srcCell, dstCell);
                // strategicGroup.plannedPath.Clear();
                // strategicGroup.moveProgressionKm = 0;
                strategicGroup.ClearPlannedPath();
                // strategicGroup.plannedPath.AddRange(pathCells.Select(c => new XY() { x = c.x, y = c.y }));
                strategicGroup.plannedPath.AddRange(pathCells.Select(c => c.ToXY()));


                Debug.Log("Set path");
            }

        });
    }

    public void StartToEditMove()
    {
        ScheduleOneshotCellClickCallback(cell =>
        {
            if (lastSelectedStrategicGroup != null)
            {
                // lastSelectedStrategicGroup.MoveToXY(cell.x, cell.y, false);
                lastSelectedStrategicGroup.MoveToCell(cell, false);
                // lastSelectedStrategicGroup.plannedPath.Clear();
                lastSelectedStrategicGroup.ClearPlannedPath();
            }
        });
    }

    [CreateProperty]
    public bool selectedCellValid => lastSelectedCell != null;

    [CreateProperty]
    public bool selectedStrategicGroupValid => lastSelectedStrategicGroup != null;

    public void ScheduleOneshotCellClickCallback(Action<Cell> callback)
    {
        mapEditMode = StrategicMapEditMode.WaitOneshotCellClickCallback;
        oneshotCellClickCallback = callback;
    }

    // void HandleMapClick(Vector2Int cellXY)
    void HandleCellClick(Cell activeCell)
    {
        if (mapEditMode == StrategicMapEditMode.Select)
        {
            lastSelectedCell = activeCell;
            lastSelectedStrategicGroup = null;
        }
        else if (mapEditMode == StrategicMapEditMode.WaitOneshotCellClickCallback)
        {
            oneshotCellClickCallback(activeCell);
            mapEditMode = StrategicMapEditMode.Select;
        }
        else if (mapEditMode == StrategicMapEditMode.WaypointPlotting)
        {
            var selectedMission = StrategicMissionEditor.Instance.selectedObject;
            if (selectedMission != null)
            {
                if (selectedMission.waypoints.Count == 0)
                {
                    // selectedMission.waypoints.Add(new XY() { x = activeCell.x, y = activeCell.y }); // set start
                    selectedMission.waypoints.Add(activeCell.ToXY()); // set start
                }
                else
                {
                    var lastWaypoint = selectedMission.waypoints[^1];
                    var srcCell = StrategicGameState.Instance.cellMatrix[lastWaypoint.x, lastWaypoint.y];
                    var dstCell = activeCell;

                    IGraphEnumerable<Cell> graph = new DynamicCellGraphNavy(); // TODO: Generalize to army?

                    var pathCells = PathFinding<Cell>.AStar(graph, srcCell, dstCell);
                    if (pathCells.Count < 2)
                    {
                        selectedMission.waypoints.Clear();
                    }
                    else
                    {
                        // selectedMission.waypoints.AddRange(pathCells.Skip(1).Select(cell => new XY() { x = cell.x, y = cell.y }));
                        selectedMission.waypoints.AddRange(pathCells.Skip(1).Select(cell => cell.ToXY()));
                    }
                }
            }
            else
            {
                mapEditMode = StrategicMapEditMode.Select;
            }
        }
        else
        {
            HandleCellEditClick(activeCell);
        }
    }

    // void HandleMapEditClick(Vector2Int cellXY)
    void HandleCellEditClick(Cell activeCell)
    {
        if (mapEditMode == StrategicMapEditMode.PaintTerrain)
        {
            // StrategicGameState.Instance.SetMapCellTerrain(activeCell.x, activeCell.y, currentTerrainType);
            StrategicGameState.Instance.SetMapCellTerrain(activeCell, currentTerrainType);

            Debug.Log($"SetMapCellTerrain({activeCell.x}, {activeCell.y}, {currentTerrainType})");
        }
        if (mapEditMode == StrategicMapEditMode.PaintHexControlSide)
        {
            // StrategicGameState.Instance.SetMapControlSide(activeCell.x, activeCell.y, currentSideStateObjectId);
            StrategicGameState.Instance.SetMapControlSide(activeCell, currentSideStateObjectId);

            Debug.Log($"PaintHexControlSide({activeCell.x}, {activeCell.y}, {currentTerrainType})");
        }
        if (mapEditMode == StrategicMapEditMode.ToggleCoast)
        {
            // StrategicGameState.Instance.ToggleCoast(activeCell.x, activeCell.y);
            StrategicGameState.Instance.ToggleCoast(activeCell);

            Debug.Log($"ToggleCoast({activeCell.x}, {activeCell.y})");
        }

        if (mapEditMode == StrategicMapEditMode.PaintHexPairFeatureBegin)
        {
            lastSelectedCell = activeCell;
            mapEditMode = StrategicMapEditMode.PaintHexPairFeatureEnd;
        }
        else if (mapEditMode == StrategicMapEditMode.PaintHexPairFeatureEnd)
        {
            if (lastSelectedCell != null)
            {
                var cell = activeCell;
                StrategicGameState.Instance.AddEdgeFeature(lastSelectedCell, cell, currentEdgeFeatureType);
                mapEditMode = StrategicMapEditMode.PaintHexPairFeatureBegin;
            }
        }

        if (mapEditMode == StrategicMapEditMode.DeleteHexPairFeatureBegin)
        {
            lastSelectedCell = activeCell;
            mapEditMode = StrategicMapEditMode.DeleteHexPairFeatureEnd;
        }
        else if (mapEditMode == StrategicMapEditMode.DeleteHexPairFeatureEnd)
        {
            if (lastSelectedCell != null)
            {
                var cell = activeCell;
                StrategicGameState.Instance.DeleteEdgeFeature(lastSelectedCell, cell, currentEdgeFeatureType);
                mapEditMode = StrategicMapEditMode.DeleteHexPairFeatureBegin;
            }
        }
    }

    [CreateProperty]
    public bool enableFogOfWar
    {
        get => StrategicGameState.Instance.scenarioState.enableFogOfWar;
        set => StrategicGameState.Instance.scenarioState.enableFogOfWar = value;
    }

    [CreateProperty]
    public string fogOfWarViewerSideStateName => EntityManager.Instance.Get<SideState>(StrategicGameState.Instance.scenarioState.fogOfWarViewerSideObjectId)?.name?.GetMergedName() ?? "";

    [CreateProperty]
    public string referenceTimeZoneDateTimeOffsetString => CoreParameter.Instance.GetReferenceTimeZoneDateTimeOffsetString(gameState.scenarioState.dateTime);
    // public void Advance1Hour()
    // {
    //     StrategicGameState.Instance.Advance1Hour(1);
    // }

    public StrategicViewState CaptureViewState()
    {
        // var cam = Camera.main;
        var cam = PlaneCameraController.Instance.cam;

        var hitAreaMapRecords = hitAreasRootTransform.GetComponentsInChildren<HitArea>(true).Select(
            s => new StrategicViewState.HitAreaMapRecord(){
                hitAreaObjectId = s.hitAreaObjectId,
                areaCellObjectId = s.areaCellObjectId
            }
        ).ToList();

        return new()
        {
            xPosition = cam.transform.position.x,
            yPosition = cam.transform.position.y,
            orthographicSize = cam.orthographicSize,
            hitAreaMapRecords = hitAreaMapRecords
        };
    }

    public void RefreshGridSystemAreaSystemVisibility()
    {
        var scenarioState = gameState.scenarioState;
        gridSystemTransform.gameObject.SetActive(scenarioState.enableGridSystem);
        areaSystemTransform.gameObject.SetActive(scenarioState.enableAreaSystem);
    }

}