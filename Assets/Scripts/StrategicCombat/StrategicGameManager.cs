
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
using YYZ;


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
    WaypointPlotting,
    RectanglePlotting
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

    public Transform missionWaypointLineContainerTransform;
    public GameObject missionWaypointLinePrefab;

    public bool fullInitialized = false;

    public class StartupConfig
    {
        public enum Mode
        {
            Empty,
            ReturnFromNavalGame,
            ScenPath,
            FullState
        }

        public Mode mode = Mode.ScenPath;
        public StrategicFullState fullState = null;
        // public Mode mode = Mode.Empty;
        // public Vector2 cameraPosXY;
        // public float cameraZoom;
        public StrategicViewState viewState; // reserved for ReturnFromNavalGame only now
        // public string scenSubPath = "Scenarios/StrategicGameState.xml";
        // public string scenSubPath = "Scenarios/Vladivostok Squadron Raiding.xml";
        public string scenSubPath = "Scenarios/First Sino-Japanese War.xml";
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
    public NavalContactReport lastSelectedNavalContactReport;
    IObjectIdLabeled _lastSelectedObject;
    public IObjectIdLabeled lastSelectedObject
    {
        get => _lastSelectedObject;
        set
        {
            if(_lastSelectedObject != value)
            {
                _lastSelectedObject = value;

                lastSelectedStrategicGroup = _lastSelectedObject as StrategicGroup;
                lastSelectedNavalContactReport = _lastSelectedObject as NavalContactReport;
            }
        }
    }

    Action<Cell> oneshotCellClickCallback;

    [CreateProperty]
    public bool showSideFlag
    {
        get => HexMapShower.Instance.showSideFlag;
        set => HexMapShower.Instance.showSideFlag = value;
    }

    public string currentSideStateObjectId;

    public bool currentLogOnly = true;

    UIDocument[] allUIDocuments;

    bool _isRealtimeAdvancing;

    [CreateProperty]
    public bool isRealtimeAdvancing
    {
        get => _isRealtimeAdvancing;
        set
        {
            if (_isRealtimeAdvancing == value)
                return;

            if (value)
            {
                if (!StrategicTopTabs.Instance.TryStartRealtimeAdvance())
                    return;

                _isRealtimeAdvancing = true;
            }
            else
            {
                _isRealtimeAdvancing = false;

                if (StrategicTopTabs.Instance != null)
                {
                    StrategicTopTabs.Instance.StopRealtimeAdvance();
                }
            }
        }
    }

    [CreateProperty]
    public bool isNotRealtimeAdvancing => !isRealtimeAdvancing;

    public Transform pathLineContainerTransform;
    public GameObject pathLinePrefab;
    public WaypointController rectAreaLineController;
    public float rightClickMaxClickDistancePixels = 12f;


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
        SwitchCenter.Instance.Reset();

        allUIDocuments = FindObjectsByType<UIDocument>(FindObjectsInactive.Include, FindObjectsSortMode.None);

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
        else if(startupConfig.mode == StartupConfig.Mode.FullState)
        {
            IEnumerator Cor() // Bullshit Unity boilerplate
            {
                yield return ProcessFullState(startupConfig.fullState);

                FinishInitialization();
            }

            StartCoroutine(Cor());
        }
    }

    protected void Awake()
    {
        var gameState = StrategicGameState.Instance;

        gameState.mapRebuilt += OnMapRebuilt;
        gameState.mapCellUpdated += OnMapCellUpdated;
        
        gameState.logAdded += OnLogAdded;
        gameState.logsRefreshed += OnLogsRefreshed;

        GamePreference.Instance.shortLabelLanguageTypeChanged += OnShortLabelLanguageTypeChanged;
        GamePreference.Instance.isInEditModeChanged += OnIsInEditModeChanged;
    }

    // public List<LazyLocalizedString> displayedLogs = new();
    public List<SidedLazyLocalizedString> displayedLogs = new();

    void OnLogAdded(object sender, SidedLazyLocalizedString log)
    {
        if(isInEditMode || log.sideObjectId == null || log.sideObjectId == viewerSideId)
        {
            displayedLogs.Insert(0, log);
        }
    }

    void OnIsInEditModeChanged(object sender, bool isInEditMode)
    {
        RefreshDisplayedLogs();
    }

    void OnLogsRefreshed(object sender, EventArgs args)
    {
        RefreshDisplayedLogs();
    }

    void RefreshDisplayedLogs()
    {
        if(isInEditMode)
        {
            displayedLogs = StrategicGameState.Instance.logs.ToList();
        }
        else
        {
            displayedLogs = StrategicGameState.Instance.logs.Where(log => log.sideObjectId == null || log.sideObjectId == viewerSideId).ToList();
        }
    }

    public override void OnDestroy()
    {
        base.OnDestroy();

        var gameState = StrategicGameState.Instance;

        gameState.mapRebuilt -= OnMapRebuilt;
        gameState.mapCellUpdated -= OnMapCellUpdated;

        gameState.logAdded -= OnLogAdded;
        gameState.logsRefreshed -= OnLogsRefreshed;

        GamePreference.Instance.shortLabelLanguageTypeChanged -= OnShortLabelLanguageTypeChanged;
        GamePreference.Instance.isInEditModeChanged -= OnIsInEditModeChanged;
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
        // return ProcessFullState(fullState);
        yield return ProcessFullState(fullState);
    }

    public IEnumerator ProcessFullState(StrategicFullState fullState)
    {
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

        if(!isInEditMode && GetViewerSide() == null)
        {
            DialogRoot.Instance.PopupSideStatePickerDialog(sideState =>
            {
                viewerSideId = sideState?.objectId;
            });
        }

        RefreshDisplayedLogs();

        TempFix();

        // fullInitialized = true;
    }

    public Dictionary<string, HitArea> areaCellObjectIdToHitArea = new();

    public void FinishInitialization() // I wonder if is it better to use a dedicated method for this.
    {
        StrategicGameState.Instance.InvokeMapRebuilt();

        RefreshGridSystemAreaSystemVisibility();
        
        fullInitialized = true; // enable all independent observer (eg Update based view state controller)

        // throw new Exception("Test Exception");
        // if(!strategicWIPWarningDisplayed)
        // {
        //     strategicWIPWarningDisplayed = true;
        //     DialogRoot.Instance.PopupMessageDialog(Localize(
        //         "Note: The Strategic Mode is still far from complete. Compared to that, the Tactical Naval Combat (the left column in the main menu) is relatively more polished and playable in the current version. However, you can still explore some work-in-progress sub system here."
        //     ));
        // }

        if(!gameState.scenarioState.firstLoaded)
        {
            gameState.scenarioState.firstLoaded = true;

            DialogRoot.Instance.PopupMessageDialog(gameState.scenarioState.globalDescription.GetShortName(), "Scenario Description");
        }
    }

    // static bool strategicWIPWarningDisplayed = false;
    // static bool strategicWIPWarningDisplayed = true; // Temp disable in the dev phase

    static string Localize(string key, params object[] args) => ServiceLocator.Get<ILocalizeService>().Get(key, args);

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

        viewerSideId = viewState.viewerSideId;
    }

    [CreateProperty]
    public bool isInEditMode
    {
        get => GamePreference.Instance.isInEditMode;
        set => GamePreference.Instance.isInEditMode = value;
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

    string _viewerSideId;
    public string viewerSideId
    {
        get => _viewerSideId;
        set
        {
            if(_viewerSideId != value)
            {
                _viewerSideId = value;
                RefreshDisplayedLogs();
            }
        }
    }

    public SideState GetViewerSide() => EntityManager.Instance.Get<SideState>(viewerSideId);

    public IEnumerable<StrategicGroup> GetObserveableStrategicGroups()
    {
        // var independentStrategicGroupsOrderedByCell = StrategicGameState.Instance.IterIndependentStrategicGroupsOrderedByCell();
        var independentStrategicGroups = StrategicGameState.Instance.IterIndependentStrategicGroups();
        
        if(isInEditMode)
        {
            return independentStrategicGroups;
        }
        var viewerSide = GetViewerSide();
        // return independentStrategicGroupsOrderedByCell.Where(g => g.side == viewerSide);
        return independentStrategicGroups.Where(g => g.IsArmy() || g.side == viewerSide); // TODO: Add dedicated Naval Contact Report like to handle army unit.
    }

    void UpdateView()
    {
        var observableStrategicGroups = GetObserveableStrategicGroups();

        var observedStrategicUnits = new List<ILayableWorldSpaceGroupIconDataSource>();
        observedStrategicUnits.AddRange(observableStrategicGroups);

        // TODO: Add Contact Report
        if(!isInEditMode) // TODO: Add a toggle to show contact report in the edit mode?
        {
            var viewerSide = GetViewerSide();
            observedStrategicUnits.AddRange(
                StrategicGameState.Instance.navalContactReports.Where(r => r.GetObserverSide() == viewerSide)
            );
        }

        BindStrategicUnitIcons(counterContainerTransform, strategicGroupIconPrefab, observedStrategicUnits);

        UpdatePathLines();
        UpdateMissionWaypointLines();
        UpdateRectangleEditingLine();
    }

    void UpdateMissionWaypointLines()
    {
        var missionWaypointLines = new List<List<XY>>();

        // if (mapEditMode == StrategicMapEditMode.WaypointPlotting)
        // {
        //     var waypoints = StrategicMissionEditor.Instance.selectedObject.waypoints;
        //     if (waypoints != null)
        //         missionWaypointLines.Add(waypoints);
        // }

        if (mapEditMode == StrategicMapEditMode.WaypointPlotting && currentEditingPointList != null)
        {
            missionWaypointLines.Add(currentEditingPointList);
        }

        Utils.SyncTransformViewerLength(missionWaypointLineContainerTransform, missionWaypointLines.Count, missionWaypointLinePrefab);
        var missionWaypointLineControllers = missionWaypointLineContainerTransform.GetComponentsInChildren<WaypointController>();

        for (int i = 0; i < missionWaypointLines.Count; i++)
        {
            var missionWaypointLine = missionWaypointLines[i];
            var controller = missionWaypointLineControllers[i];
            controller.Sync(missionWaypointLine);
        }
    }

    void BindStrategicUnitIcons(Transform containerTransform, GameObject prefab, List<ILayableWorldSpaceGroupIconDataSource> strategicGroups)
    {
        // Sync Views & create mapping
        Utils.SyncTransformViewerLength(containerTransform, strategicGroups.Count, prefab);

        var worldSpaceGroupIcons = containerTransform.GetComponentsInChildren<WorldSpaceGroupIcon>();
        var groupToView = new Dictionary<IWorldSpaceGroupIconDataSource, WorldSpaceGroupIcon>();
        for (int i = 0; i < strategicGroups.Count; i++)
        {
            var strategicGroup = strategicGroups[i];
            var worldSpaceGroupIcon = worldSpaceGroupIcons[i];
            worldSpaceGroupIcon.SetDataSource(strategicGroup);

            groupToView[strategicGroup] = worldSpaceGroupIcon;
        }

        var hexMapShower = HexMapShower.Instance;

        foreach(var g in strategicGroups.GroupBy(group => group.cell))
        {
            var cell = g.Key;

            var vec = GetCellWorldCenter(cell);

            var gl = g.GroupBy(_g => _g.side).ToList();

            if (gl.Count == 1)
            {
                var _gl0 = gl[0].Select(gp => gp).ToList();
                _gl0.Sort((d1, d2) => d1.stackPriority.CompareTo(d2.stackPriority));

                Utils.LayoutStackTransform(
                    _gl0.Select(gp => groupToView[gp].transform).ToList(),
                    new Vector3(vec.x, vec.y, 0),
                    0.05f
                );
            }
            else
            {
                var (gTop, gBottom) = GetTopBottom(cell, gl);

                var _gTop = gTop.Select(gp => gp).ToList();
                _gTop.Sort((d1, d2) => d1.stackPriority.CompareTo(d2.stackPriority));

                Utils.LayoutStackTransform(
                    _gTop.Select(gp => groupToView[gp].transform).ToList(),
                    new Vector3(vec.x, vec.y + 0.25f, 0),
                    0.05f
                );

                var _gBottom = gBottom.Select(gp => gp).ToList();
                _gBottom.Sort((d1, d2) => d1.stackPriority.CompareTo(d2.stackPriority));

                // Assume 2 sides can be in the same hex at most.
                Utils.LayoutStackTransform(
                    _gBottom.Select(gp => groupToView[gp].transform).ToList(),
                    new Vector3(vec.x, vec.y - 0.25f, 0),
                    0.05f
                );
            }
        }
    }

    Vector3 GetCellWorldCenter(Cell cell)
    {
        if(cell.IsGridCell())
        {
            var x = cell.x;
            var y = cell.y;

            var (xf, yf) = HexMapShower.CellXYToLocalXY(cell.x, cell.y);
            var vec = HexMapShower.Instance.controlledRenderer.transform.TransformPoint(xf, yf, 0);
            return vec;
        }
        else// area
        {
            if(areaCellObjectIdToHitArea.TryGetValue(cell.objectId, out var hitArea))
            {
                return hitArea.transform.position;
            }
        }
        return Vector3.zero;
    }

    static Dictionary<Country, int> countryNorthScore = new()
    {
        [Country.Japan] = 0,
        [Country.China] = 1,
        [Country.Russia] = 2
    };

    (IGrouping<SideState, ILayableWorldSpaceGroupIconDataSource>, IGrouping<SideState, ILayableWorldSpaceGroupIconDataSource>) GetTopBottom(Cell cell, List<IGrouping<SideState, ILayableWorldSpaceGroupIconDataSource>> gl)
    {
        float side0yScore, side1yScore;

        if(cell.IsGridCell())
        {
            side0yScore = cell.GetMassCenterY(gl[0].Key);
            side1yScore = cell.GetMassCenterY(gl[1].Key);
        }
        else // area
        {
            side0yScore = countryNorthScore.GetValueOrDefault(gl[0].Key.countries.FirstOrDefault());
            side1yScore = countryNorthScore.GetValueOrDefault(gl[1].Key.countries.FirstOrDefault());
        }

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

        return (gTop, gBottom);
    }
    
    List<ILayableWorldSpaceGroupIconDataSource> CollectObservableStack(SideState observerSide, SideState observedSide, Cell cell)
    {
        // var groups = cell.StrategicGroupReferences.Select(r => r.Get()).Where(g => g.side == observedSide || g.type != StrategicGroup.Type.Fleet);
        // var contacts = StrategicGameState.Instance.navalContactReports.Where(r => r.observerSideId == side.objectId);
        var groups = cell.StrategicGroupReferences.Select(r => r.Get()).Where(g => g.side == observedSide);
        if(!isInEditMode && observerSide != observedSide)
        {
            groups = groups.Where(g => g.type != StrategicGroup.Type.Fleet);
        }
        var stack = groups.Select(g => g as ILayableWorldSpaceGroupIconDataSource).ToList();
        
        if(!isInEditMode)
        {
            stack.AddRange(
                StrategicGameState.Instance.navalContactReports.Where(r => r.observerSideId == observerSide.objectId && r.observedSideId == observedSide.objectId && r.cell == cell)
            );
        }

        stack.Sort((x, y) => x.stackPriority.CompareTo(y.stackPriority));
        return stack;
    }

    void ReassignStackPriority(List<ILayableWorldSpaceGroupIconDataSource> stack)
    {
        var count = stack.Count;
        for (int i = 0; i < count; i++)
        {
            stack[i].stackPriority = (float) i / count;
        }
    }

    void HandleInput()
    {
        var controlPressing = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        var altPressing = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        var shiftPressing = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        var hotKeyEnabled = IsHotKeyEnabled();

        if (!EventSystem.current.IsPointerOverGameObject())
        {
            var leftClicking = Input.GetMouseButtonDown(0);
            var rightClicking = HandleRightClickCandidateInSelectMode();

            if (leftClicking || rightClicking) // left click
            {
                var cam = PlaneCameraController.Instance.cam;

                if (mapEditMode == StrategicMapEditMode.Select && TryGetIconDataSourceAtPointer(cam, out var iconDataSource)) // click on group
                {
                    var iconSide = iconDataSource.side;
                    var iconCell = iconDataSource.cell;
                    var viewerSide = GetViewerSide();
                    
                    var observableStack = CollectObservableStack(viewerSide, iconSide, iconCell);
                    var topStackIcon = observableStack[^1];

                    lastSelectedCell = iconCell;

                    if(leftClicking)
                    {
                        if(lastSelectedObject != topStackIcon) // select
                        {
                            lastSelectedObject = topStackIcon as IObjectIdLabeled;
                        }
                        else // toggle stack
                        {
                            observableStack.RemoveAt(observableStack.Count - 1);
                            observableStack.Insert(0, topStackIcon);
                            ReassignStackPriority(observableStack);

                            lastSelectedObject = observableStack[^1] as IObjectIdLabeled;
                        }
                    }
                    else if(rightClicking)
                    {
                        if (shiftPressing)
                        {
                            TryToAppendMove(lastSelectedStrategicGroup, iconCell);
                        }
                        else if (lastSelectedStrategicGroup != iconDataSource && TryToSetNewMove(lastSelectedStrategicGroup, iconCell))
                        {
                            // handled by setting a new move target to the clicked icon cell
                        }
                        else if(lastSelectedStrategicGroup == iconDataSource && (isInEditMode || viewerSide == lastSelectedStrategicGroup.side))
                        {
                            SwitchCenter.Instance.SwitchToStrategicGroupView(lastSelectedStrategicGroup);
                        }
                    }

                    StrategicInformationPanel.Instance.BindStack(observableStack);
                }
                else if (leftClicking || rightClicking) // click on map (cell)
                {
                    if (TryGetCellAtPointer(cam, out var activeCell))
                    {
                        if (leftClicking)
                        {
                            HandleCellClick(activeCell);
                        }
                        else if (rightClicking && mapEditMode == StrategicMapEditMode.Select)
                        {
                            if (shiftPressing)
                                TryToAppendMove(lastSelectedStrategicGroup, activeCell);
                            else
                                TryToSetNewMove(lastSelectedStrategicGroup, activeCell);
                        }
                    }
                }
            }
        }

        if (hotKeyEnabled)
        {
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
            if (Input.GetKeyDown(KeyCode.Space))
            {
                isRealtimeAdvancing = !isRealtimeAdvancing;
            }
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                StrategicTopTabs.Instance.TryAdvance1Day();
            }
            if (Input.GetKeyDown(KeyCode.Tilde) || Input.GetKeyDown(KeyCode.BackQuote))
            {
                StrategicTopTabs.Instance.TryAdvance1Hour();
            }
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                mapEditMode = StrategicMapEditMode.Select;
            }
        }
    }

    public bool IsHotKeyEnabled()
    {
        if (allUIDocuments == null || allUIDocuments.Length == 0)
        {
            allUIDocuments = FindObjectsByType<UIDocument>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        }

        if (allUIDocuments != null)
        {
            foreach (var doc in allUIDocuments)
            {
                if (doc == null || !doc.isActiveAndEnabled)
                    continue;

                var root = doc.rootVisualElement;
                if (root == null)
                    continue;

                var focused = root.focusController?.focusedElement;
                if (focused == null)
                    continue;

                return false;
            }
        }

        return true;
    }

    public void TryToStartAppendMove()
    {
        if (lastSelectedStrategicGroup == null)
            return;

        ScheduleOneshotCellClickCallback(cell =>
        {
            TryToAppendMove(lastSelectedStrategicGroup, cell);
        });
    }

    public void TryToStartMakeNewMove()
    {
        if (lastSelectedStrategicGroup == null)
            return;

        ScheduleOneshotCellClickCallback(cell =>
        {
            TryToSetNewMove(lastSelectedStrategicGroup, cell);
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

    bool HandleRightClickCandidateInSelectMode()
    {
        if (mapEditMode != StrategicMapEditMode.Select)
            return false;

        if (Input.GetMouseButtonDown(1))
        {
            rightClickCandidateActive = true;
            rightClickDownPosition = Input.mousePosition;
        }

        if (!rightClickCandidateActive || !Input.GetMouseButtonUp(1))
            return false;

        rightClickCandidateActive = false;
        return Vector2.Distance(rightClickDownPosition, (Vector2)Input.mousePosition) <= rightClickMaxClickDistancePixels;
    }

    bool TryGetIconDataSourceAtPointer(Camera cam, out ILayableWorldSpaceGroupIconDataSource iconDataSource)
    {
        var ray = cam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out var hitInfo) && hitInfo.collider.CompareTag("Icon"))
        {
            Debug.Log($"hitInfo.collider={hitInfo.collider}");
            iconDataSource = hitInfo.collider.GetComponent<WorldSpaceGroupIcon>()?.currentDataSource;
            return iconDataSource != null;
        }

        iconDataSource = null;
        return false;
    }

    bool TryGetCellAtPointer(Camera cam, out Cell activeCell)
    {
        var worldPoint = cam.ScreenToWorldPoint(Input.mousePosition);
        var hit = Physics2D.Raycast(worldPoint, Vector2.zero);
        if (hit.collider != null)
        {
            if (hit.collider.CompareTag("Map"))
            {
                Debug.Log($"Map Hit: {hit.collider} {hit.point}");

                var localPoint = hit.collider.transform.InverseTransformPoint(hit.point);
                var uv = new Vector2(localPoint.x + 0.5f, localPoint.y + 0.5f);
                var cellXY = GetCellXY(uv);

                Debug.Log($"localPoint={localPoint}, cellXY={cellXY}");

                if (cellXY.x >= 0 && cellXY.x < StrategicGameState.Instance.GetMapWidth() && cellXY.y >= 0 && cellXY.y < StrategicGameState.Instance.GetMapHeight())
                {
                    activeCell = StrategicGameState.Instance.cellMatrix[cellXY.x, cellXY.y];
                    return true;
                }
            }
            else if(hit.collider.CompareTag("Hit Area"))
            {
                Debug.Log($"Hit Area Hit: {hit.collider} {hit.point}");

                var hitArea = hit.collider.GetComponent<HitArea>();
                if(hitArea != null && hitArea.hitAreaObjectId != null && hitArea.hitAreaObjectId != "")
                {
                    if(hitArea.areaCellObjectId == null || hitArea.areaCellObjectId == "")
                    {
                        Debug.Log($"(Placeholder) Create a dynamic Area Cell and bind to it: {hitArea.hitAreaObjectId}");
                        DialogRoot.Instance.PopupUnbindHitAreaDialog(hitArea);
                    }
                    else
                    {
                        Debug.Log($"(Placeholder) to handle a map click: {hitArea.hitAreaObjectId}");
                        var areaCell = EntityManager.Instance.Get<Cell>(hitArea.areaCellObjectId);
                        if(areaCell != null)
                        {
                            activeCell = areaCell;
                            return true;
                        }
                    }
                }
            }
        }

        activeCell = null;
        return false;
    }

    bool TryToSetNewMove(StrategicGroup strategicGroup, Cell dstCell)
    {
        if (strategicGroup == null)
            return false;

        var viewerSide = GetViewerSide();
        if (!isInEditMode && viewerSide != strategicGroup.side)
            return false;

        if (strategicGroup.deployState != StrategicGroup.DeployState.Independent)
            return false;

        var srcCell = strategicGroup.cell;
        IGraphEnumerable<Cell> graph = strategicGroup.IsArmy() ? new DynamicCellGraphArmy() : new DynamicCellGraphNavy();
        var pathCells = PathFinding<Cell>.AStar(graph, srcCell, dstCell);

        strategicGroup.SetPlannedPath(pathCells.Select(c => c.ToXY()).ToList());

        Debug.Log("Set path");
        return true;
    }

    bool TryToAppendMove(StrategicGroup strategicGroup, Cell dstCell)
    {
        if (strategicGroup == null)
            return false;

        var viewerSide = GetViewerSide();
        if (!isInEditMode && viewerSide != strategicGroup.side)
            return false;

        if (strategicGroup.deployState != StrategicGroup.DeployState.Independent)
            return false;

        var p = strategicGroup.plannedPath;
        var appending = p.Count >= 2;
        var srcCell = appending ? p[^1].GetCell() : strategicGroup.cell;

        IGraphEnumerable<Cell> graph = strategicGroup.IsArmy() ? new DynamicCellGraphArmy() : new DynamicCellGraphNavy();
        var pathCells = PathFinding<Cell>.AStar(graph, srcCell, dstCell);

        if (appending)
        {
            strategicGroup.plannedPath.AddRange(pathCells.Skip(1).Select(c => c.ToXY()));
        }
        else
        {
            strategicGroup.plannedPath.Clear();
            strategicGroup.plannedPath.AddRange(pathCells.Select(c => c.ToXY()));
            strategicGroup.moveProgressionKm = 0;
        }

        Debug.Log("Append path");
        return true;
    }

    [CreateProperty]
    public bool selectedCellValid => lastSelectedCell != null;

    [CreateProperty]
    public bool selectedStrategicGroupValid => lastSelectedStrategicGroup != null;

    [CreateProperty]
    public bool selectedStrategicGroupObservable
    {
        get
        {
            if(isInEditMode)
                return true;
            
            var viewerSide = GetViewerSide();
            if(viewerSide != null)
            {
                var groupCountry = lastSelectedStrategicGroup?.country ?? Country.General;
                return viewerSide.countries.Contains(groupCountry);
            }
            return false;
        }
        
    }


    [CreateProperty]
    public bool selectedNavalContactReportValid => lastSelectedNavalContactReport != null;

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
            // lastSelectedStrategicGroup = null;
            lastSelectedObject = null;
            StrategicInformationPanel.Instance.ClearStack();
        }
        else if (mapEditMode == StrategicMapEditMode.WaitOneshotCellClickCallback)
        {
            oneshotCellClickCallback(activeCell);
            mapEditMode = StrategicMapEditMode.Select;
        }
        else if (mapEditMode == StrategicMapEditMode.WaypointPlotting)
        {
            HandleCellClickWaypointPlotting(activeCell);
        }
        else if(mapEditMode == StrategicMapEditMode.RectanglePlotting)
        {
            var rect = currentEditingRect;
            if(rect != null)
            {
                if(rect.xy1 == null)
                {
                    rect.xy1 = activeCell.ToXY();
                }
                else
                {
                    rect.xy2 = activeCell.ToXY();
                }

                rectangleEditineLineDirty = true;
            }
        }
        else
        {
            HandleCellEditClick(activeCell);
        }
    }

    void HandleCellClickWaypointPlotting(Cell activeCell)
    {
        var selectedMission = StrategicMissionEditor.Instance.selectedObject;
        if (selectedMission != null)
        {
            // TODO: use currentEditingPointList
            if (selectedMission.waypoints.Count == 0 || pointListEditorMode == PointListEditorMode.Discrete)
            {
                // selectedMission.waypoints.Add(new XY() { x = activeCell.x, y = activeCell.y }); // set start
                selectedMission.waypoints.Add(activeCell.ToXY()); // set start
            }
            else if(pointListEditorMode == PointListEditorMode.Continues)
            {
                // var lastWaypoint = selectedMission.waypoints[^1];
                // var srcCell = StrategicGameState.Instance.cellMatrix[lastWaypoint.x, lastWaypoint.y];
                var srcCell = selectedMission.waypoints[^1].GetCell();
                var dstCell = activeCell;

                IGraphEnumerable<Cell> graph = pointListEditorPassabilityMode switch
                {
                    PassabilityMode.Land => new DynamicCellGraphArmy(),
                    PassabilityMode.Sea => new DynamicCellGraphNavy(),
                    _ => new DynamicCellGraphNavy(),
                };

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
            else
            {
                mapEditMode = StrategicMapEditMode.Select;
            }
        }
        else
        {
            mapEditMode = StrategicMapEditMode.Select;
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

    // Switch to isInEditor
    // [CreateProperty]
    // public bool enableFogOfWar
    // {
    //     get => StrategicGameState.Instance.scenarioState.enableFogOfWar;
    //     set => StrategicGameState.Instance.scenarioState.enableFogOfWar = value;
    // }

    [CreateProperty]
    public string fogOfWarViewerSideStateName => EntityManager.Instance.Get<SideState>(viewerSideId)?.name?.GetMergedName() ?? "[Not Specified or Invalid]";

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
            hitAreaMapRecords = hitAreaMapRecords,
            viewerSideId = viewerSideId
        };
    }

    public void RefreshGridSystemAreaSystemVisibility()
    {
        var scenarioState = gameState.scenarioState;
        gridSystemTransform.gameObject.SetActive(scenarioState.enableGridSystem);
        areaSystemTransform.gameObject.SetActive(scenarioState.enableAreaSystem);
    }

    // [CreateProperty]
    // public bool isInEditMode => GamePreference.Instance.isInEditMode;
    [CreateProperty]
    public bool isInUnityEditor => Application.isEditor;

    // Support PointListEditor
    public enum PointListEditorMode
    {
        Continues,
        Discrete
    }

    public enum PassabilityMode // Move to Core?
    {
        Land,
        Sea
    }

    public PointListEditorMode pointListEditorMode;
    public PassabilityMode pointListEditorPassabilityMode;
    public List<XY> currentEditingPointList;
    bool rightClickCandidateActive;
    Vector2 rightClickDownPosition;

    public void StartPointListEditor(List<XY> pointList, Action callback)
    {
        // Popup
        currentEditingPointList = pointList;
        var oldMapEditMode = mapEditMode;
        mapEditMode = StrategicMapEditMode.WaypointPlotting;

        DialogRoot.Instance.PopupPointListEditorDialog(() =>
        {
            callback();
            mapEditMode = oldMapEditMode;
        });
    }

    public Rectangle currentEditingRect;

    public void StartRectangleEditor(Rectangle rect, Action callback)
    {
        currentEditingRect = rect;
        var oldMapEditMode = mapEditMode;
        mapEditMode = StrategicMapEditMode.RectanglePlotting;

        DialogRoot.Instance.PopupRectangleEditorDialog(() =>
        {
            callback();
            mapEditMode = oldMapEditMode;

            rectangleEditineLineDirty = true;
        });

        rectangleEditineLineDirty = true;
    }

    public bool rectangleEditineLineDirty;

    public void UpdateRectangleEditingLine()
    {
        if(rectangleEditineLineDirty)
        {
            rectangleEditineLineDirty = false;

            // Refresh Rect Line Renderer
            var rect = currentEditingRect;
            var rectShown = rect != null && rect.IsValid();
            rectAreaLineController.gameObject.SetActive(rectShown);
            if(rectShown)
            {
                rect.GetBoundary(out int x1, out int x2, out int y1, out int y2);

                rectAreaLineController.Sync(new List<XY>()
                {
                    new XY(){x=x1, y=y1},
                    new XY(){x=x2, y=y1},
                    new XY(){x=x2, y=y2},
                    new XY(){x=x1, y=y2},
                    new XY(){x=x1, y=y1}
                });
            }
            
            // rectAreaLineController.Sync()
        }
    }
}
