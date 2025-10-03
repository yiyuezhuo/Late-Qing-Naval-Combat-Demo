
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

    public class StartupConfig
    {
        public enum Mode
        {
            Empty,
            ReturnFromNavalGame,
            ScenPath
        }

        public Mode mode = Mode.ScenPath;
        public Vector2 cameraPosXY;
        public float cameraZoom;
        public string scenSubPath = "Scenarios/StrategicGameState.xml";
        public List<ShipLog> syncShipLogs;
    }

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
        }
        else if (startupConfig.mode == StartupConfig.Mode.ReturnFromNavalGame)
        {
            Debug.Log("ReturnFromNavalGame mode startup");

            RestoreFromReturnFromNavalGame();
            HexMapShower.Instance.Refresh();
        }
        else if (startupConfig.mode == StartupConfig.Mode.ScenPath)
        {
            Debug.Log($"ScenPath mode startup: {startupConfig.scenSubPath}");

            // Try to fetch default scenario file and update the state
            // StartCoroutine(Utils.FetchFile(startupConfig.scenPath, initialScenText =>
            // {
            //     StartCoroutine(
            //         OnScenTextLoaded(initialScenText)
            //     );
            // }))
            var scenFullPath = Application.streamingAssetsPath + "/" + startupConfig.scenSubPath;
            StartCoroutine(StreamingTextAssetManager.Instance.FetchText(scenFullPath, initialScenText =>
            {
                StartCoroutine(
                    OnScenTextLoaded(initialScenText)
                );
            }));
        }
    }


    // public override void OnDestroy()
    // {
    //     base.OnDestroy();
    // }

    public void PrepareReturnFromNavalGame()
    {
        var pos = PlaneCameraController.Instance.transform.position;

        startupConfig = new()
        {
            mode = StartupConfig.Mode.ReturnFromNavalGame,
            cameraPosXY = new Vector2(pos.x, pos.y),
            cameraZoom = PlaneCameraController.Instance.cam.orthographicSize
        };
        // startupConfig.mode = StartupConfig.Mode.ReturnFromNavalGame;
        // startupConfig.cameraPosXY = new Vector2(pos.x, pos.y);
        // startupConfig.cameraZoom = PlaneCameraController.Instance.cam.orthographicSize;
    }

    public void RestoreFromReturnFromNavalGame()
    {
        var trans = PlaneCameraController.Instance.transform;
        trans.position = new Vector3(startupConfig.cameraPosXY.x, startupConfig.cameraPosXY.y, trans.position.z);
        PlaneCameraController.Instance.cam.orthographicSize = startupConfig.cameraZoom;

        if (startupConfig.syncShipLogs != null)
        {
            StrategicGameState.Instance.UpdatePartialShipLogs(startupConfig.syncShipLogs);
        }

        StrategicGameState.Instance.ResetAndRegisterAll();

        // Other update
        // TODO: Move to Core

        // Reset independent but empty groups (generally caused by combat) in conflict hex deploy-state to combined. So they may be "rebuilt" in the location of higher command.
        foreach (var cellGroupsGrouping in StrategicGameState.Instance.strategicGroups
            .Where(g => g.deployState == StrategicGroup.DeployState.Independent)
            .GroupBy(g => g.cell))
        {
            var sideGroupsGroupings = cellGroupsGrouping.GroupBy(g => g.side).ToList();
            if (sideGroupsGroupings.Count >= 2)
            {
                foreach (var group in cellGroupsGrouping)
                {
                    if (group.GetCombinedSubUnitSize() == 0)
                    {
                        // group.deployState = StrategicGroup.DeployState.Combined;
                        // group.RemoveFromMap();
                        group.SetDeployState(StrategicGroup.DeployState.Combined);
                    }
                }
            }
        }
    }

    IEnumerator OnScenTextLoaded(string initialScenText)
    {
        var strategicGameState = XmlUtils.FromXML<StrategicGameState>(initialScenText);
        StrategicGameState.Instance.UpdateTo(strategicGameState);

        // TODO: Save StreamingAssetReference state in the StrategicGameState?
        yield return StreamingAssetReference.Instance.TryToCompleteFromStreamingAssetReference(StrategicGameState.Instance);

        StrategicGameState.Instance.ResetAndRegisterAll();

        HexMapShower.Instance.RefreshSideFlags();
        // HexMapShower.Instance.showSideFlag = false;

        TempFix();
    }

    public static void TempFix()
    {
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
                else if(leftClicking) // click on map (cell)
                {
                    var worldPoint = cam.ScreenToWorldPoint(Input.mousePosition);

                    var hit = Physics2D.Raycast(worldPoint, Vector2.zero);
                    if (hit.collider != null)
                    {
                        if (hit.collider.CompareTag("Map"))
                        {
                            // Map Click
                            Debug.Log($"Hit: {hit.collider} {hit.point}");

                            var localPoint = hit.collider.transform.InverseTransformPoint(hit.point);
                            var uv = new Vector2(localPoint.x + 0.5f, localPoint.y + 0.5f);
                            var cellXY = GetCellXY(uv);

                            Debug.Log($"localPoint={localPoint}, cellXY={cellXY}");

                            if (cellXY.x >= 0 && cellXY.x < StrategicGameState.Instance.GetMapWidth() && cellXY.y >= 0 && cellXY.y < StrategicGameState.Instance.GetMapHeight())
                            {
                                HandleMapClick(cellXY);
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
                        group.MoveToXY(cell.x, cell.y, false);
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
                strategicGroup.plannedPath.AddRange(pathCells.Skip(1).Select(c => new XY() { x = c.x, y = c.y }));
            }
            else
            {
                strategicGroup.plannedPath.Clear();
                strategicGroup.plannedPath.AddRange(pathCells.Select(c => new XY() { x = c.x, y = c.y }));
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
                strategicGroup.plannedPath.Clear();
                strategicGroup.plannedPath.AddRange(pathCells.Select(c => new XY() { x = c.x, y = c.y }));
                strategicGroup.moveProgressionKm = 0;

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
                lastSelectedStrategicGroup.MoveToXY(cell.x, cell.y, false);
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

    void HandleMapClick(Vector2Int cellXY)
    {
        if (mapEditMode == StrategicMapEditMode.Select)
        {
            lastSelectedCell = StrategicGameState.Instance.cellMatrix[cellXY.x, cellXY.y];
            lastSelectedStrategicGroup = null;
        }
        else if (mapEditMode == StrategicMapEditMode.WaitOneshotCellClickCallback)
        {
            oneshotCellClickCallback(StrategicGameState.Instance.cellMatrix[cellXY.x, cellXY.y]);
            mapEditMode = StrategicMapEditMode.Select;
        }
        else if (mapEditMode == StrategicMapEditMode.WaypointPlotting)
        {
            var selectedMission = StrategicMissionEditor.Instance.selectedObject;
            if (selectedMission != null)
            {
                if (selectedMission.waypoints.Count == 0)
                {
                    selectedMission.waypoints.Add(new XY() { x = cellXY.x, y = cellXY.y }); // set start
                }
                else
                {
                    var lastWaypoint = selectedMission.waypoints[^1];
                    var srcCell = StrategicGameState.Instance.cellMatrix[lastWaypoint.x, lastWaypoint.y];
                    var dstCell = StrategicGameState.Instance.cellMatrix[cellXY.x, cellXY.y];

                    IGraphEnumerable<Cell> graph = new DynamicCellGraphNavy(); // TODO: Generalize to army?

                    var pathCells = PathFinding<Cell>.AStar(graph, srcCell, dstCell);
                    if (pathCells.Count < 2)
                    {
                        selectedMission.waypoints.Clear();
                    }
                    else
                    {
                        selectedMission.waypoints.AddRange(pathCells.Skip(1).Select(cell => new XY() { x = cell.x, y = cell.y }));
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
            HandleMapEditClick(cellXY);
        }
    }

    void HandleMapEditClick(Vector2Int cellXY)
    {
        if (mapEditMode == StrategicMapEditMode.PaintTerrain)
        {
            StrategicGameState.Instance.SetMapCellTerrain(cellXY.x, cellXY.y, currentTerrainType);

            Debug.Log($"SetMapCellTerrain({cellXY.x}, {cellXY.y}, {currentTerrainType})");
        }
        if (mapEditMode == StrategicMapEditMode.PaintHexControlSide)
        {
            StrategicGameState.Instance.SetMapControlSide(cellXY.x, cellXY.y, currentSideStateObjectId);

            Debug.Log($"PaintHexControlSide({cellXY.x}, {cellXY.y}, {currentTerrainType})");
        }
        if (mapEditMode == StrategicMapEditMode.ToggleCoast)
        {
            StrategicGameState.Instance.ToggleCoast(cellXY.x, cellXY.y);

            Debug.Log($"ToggleCoast({cellXY.x}, {cellXY.y})");
        }

        if (mapEditMode == StrategicMapEditMode.PaintHexPairFeatureBegin)
        {
            lastSelectedCell = StrategicGameState.Instance.cellMatrix[cellXY.x, cellXY.y];
            mapEditMode = StrategicMapEditMode.PaintHexPairFeatureEnd;
        }
        else if (mapEditMode == StrategicMapEditMode.PaintHexPairFeatureEnd)
        {
            if (lastSelectedCell != null)
            {
                var cell = StrategicGameState.Instance.cellMatrix[cellXY.x, cellXY.y];
                StrategicGameState.Instance.AddEdgeFeature(lastSelectedCell, cell, currentEdgeFeatureType);
                mapEditMode = StrategicMapEditMode.PaintHexPairFeatureBegin;
            }
        }

        if (mapEditMode == StrategicMapEditMode.DeleteHexPairFeatureBegin)
        {
            lastSelectedCell = StrategicGameState.Instance.cellMatrix[cellXY.x, cellXY.y];
            mapEditMode = StrategicMapEditMode.DeleteHexPairFeatureEnd;
        }
        else if (mapEditMode == StrategicMapEditMode.DeleteHexPairFeatureEnd)
        {
            if (lastSelectedCell != null)
            {
                var cell = StrategicGameState.Instance.cellMatrix[cellXY.x, cellXY.y];
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

    // public void Advance1Hour()
    // {
    //     StrategicGameState.Instance.Advance1Hour(1);
    // }
}