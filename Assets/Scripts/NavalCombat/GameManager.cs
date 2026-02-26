using UnityEngine;
using GeographicLib;
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
// using SunCalcNet;

using NavalCombatCore;
using CoreUtils;
using NavalCombat;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.SceneManagement;
using YYZ;

public interface IColliderRootProvider
{
    GameObject GetRoot();
}

public class GameManager : SingletonMonoBehaviour<GameManager>
{
    [CreateProperty]
    public NavalGameState navalGameState => NavalGameState.Instance;

    public GameObject shipUnitPrefab;
    public Transform earthTransform;

    public LayerMask iconLayerMask;

    public LineRenderer primaryBatteryRangeLine;
    public LineRenderer secondaryBatteryRangeLine;
    public LineRenderer tertiaryBatteryRangeLine;
    public LineRenderer rapidFireBatteryRangeLine;
    public LineRenderer torpedoRangeLine;
    public LineRenderer visibilityRangeLine;
    readonly Dictionary<LineRenderer, List<LineRenderer>> rangeArcLinePool = new();

    public GameObject shipLogTrajectoryPrefab;
    public Transform shipLogTrajectoriesTransform;
    public Transform shipLogTrajectoryLabelsTransform;
    public GameObject shipLogTrajectoryLabelPrefab;
    [Header("Gunnery Shell Visual")]
    public bool enableGunneryShellVisual = true;
    [Min(1f)]
    public float gunneryShellSpeedMps = 760f;
    [Min(0f)]
    public float gunneryShellAltitudeFoot = 300f;
    [Min(1f)]
    public float gunneryShellRadiusScaleCoef = 100f;
    public Transform gunneryShellVisualContainer;

    [Serializable]
    public class StateText2DConfig
    {
        public State state;
        public Texture2D texture;
        public Vector2 hotSpot;
    }

    public List<StateText2DConfig> stateIconMap = new();

    [Serializable]
    public class PostureMaterialConfig
    {
        public PostureType postureType;
        public Material material;
    }

    public List<PostureMaterialConfig> postureMaterialMap = new();

    public LatLon lastSelectedLatLon;

    public enum State
    {
        Idle,
        SelectingInsertUnitPosition, // Insert + Alt
        SelectingInsertUnitPositionComplex, // Insert
        MovingUnit,
        SelectingFollowedTarget,
        SelectingRelativeToTarget,
        SelectingFiringTarget,
        SelectingFireControlSystemTarget,
        SelectingRapidFiringTarget,
        SelectingTorpedoFiringTarget,
        SelectingTargetMisc,
        SelectingCourseTarget,
        SelectingShipLevelFiringTarget
    }

    State _state = State.Idle;
    public State state
    {
        get
        {
            return _state;
        }
        set
        {
            if (_state != value)
            {
                _state = value;

                var r = stateIconMap.FirstOrDefault(p => p.state == value);
                var icon = r?.texture;
                var hotSpot = r?.hotSpot ?? Vector2.zero;

                UnityEngine.Cursor.SetCursor(icon, hotSpot, CursorMode.Auto);
            }
        }
    }

    [CreateProperty]
    public string stateDesc
    {
        get => state.ToString();
    }

    public class StartupConfig
    {
        public enum Mode
        {
            Empty, // Obstacle
            LocalizedCameraOnly, // Obstacle
            BuiltinScenName,
            FullState
        }

        public Mode mode = Mode.BuiltinScenName;
        public FullState fullState = null;
        // public string builtinScenName = "Battle of Yalu River.scen.xml";
        // public string builtinScenName = "Tutorial 1 - Single Ship.scen.xml";
        // public string builtinScenName = "Tutorial 2 - Ship Group.scen.xml";
        // public string builtinScenName = "Tutorial 3 - Combat.scen.xml";
        // public string builtinScenName = "SJS - Ting Yuen vs Three View.scen.xml";
        public string builtinScenName = "RJH - Battle of Ulsan.scen.xml";
        public LatLon cameraLocation;
        // public bool requireAutoDeployAll = false;
        public ScenarioDynamicSetupGenerator scenarioSetupGenerator; // TODO: switch to AutoDeployment
        public AutoDeployment autoDeployment;
        public bool isFromStrategic;
        public bool isFromSkirmish;

        // public bool IsFromStrategic() => scenarioSetupGenerator != null;
        public bool IsFromStrategic() => isFromStrategic;
    }

    public static StartupConfig startupConfig = new();

    // public static FullState oneShotStartupFullState = null; // one-shot config
    // public static string scenarioSuffix = "_Yalu_Torpedo.xml";
    // public static string initialScenName = "Battle of Yalu River.scen.xml";

    public bool fullInitialized = false;

    public AudioClip oceanWaveSound;
    public AudioClip shipBellSound;
    AudioSource audioSource;

    UIDocument[] allUIDocuments;

    public void Start()
    {
        allUIDocuments = FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
        audioSource = GetComponent<AudioSource>();

        SwitchCenter.Instance.Reset();

        GamePreference.Instance.SetShortLabelLanguageTypeByLocale(LocalizationSettings.SelectedLocale);
        enableGunneryShellVisual = GamePreference.Instance.enableGunneryShellVisual;
        gunneryShellRadiusScaleCoef = GamePreference.Instance.gunneryShellRadiusScaleCoef;

        iconLayerMask = LayerMask.GetMask("Icon");
        if (gunneryShellVisualContainer == null)
        {
            var root = new GameObject("GunneryShellVisualContainer");
            root.transform.SetParent(null, false);
            gunneryShellVisualContainer = root.transform;
        }
        // Debug.Log($"Persistent Path:{Application.persistentDataPath}");

        SuperGameState.Instance.currentGameMode = GameMode.Naval;
        wasInMultiplayerLastFrame = isHostOrClient;

        // EntityManager.Instance.newGuidCreated += (obj, s) => Debug.LogWarning($"New guid created: {s} for {obj}");

        if (startupConfig.mode == StartupConfig.Mode.Empty)
        {
            Debug.Log("Empty Startup");
        }
        if (startupConfig.mode == StartupConfig.Mode.LocalizedCameraOnly)
        {
            Debug.Log("LocalizedCameraOnly Startup");
            CameraController2.Instance.SetCameraState(startupConfig.cameraLocation, 40);
        }
        else if (startupConfig.mode == StartupConfig.Mode.BuiltinScenName)
        {
            Debug.Log($"BuiltinScenName Startup: {startupConfig.builtinScenName}");

            // StartLoadScenarioCoroutine(initialScenName);
            StartLoadScenarioCoroutine(startupConfig.builtinScenName);
        }
        else if (startupConfig.mode == StartupConfig.Mode.FullState)
        {
            Debug.Log($"FullState Startup");

            // StartCoroutine(CompleteFullStateAndUpdateCoroutine(oneShotStartupFullState));
            StartCoroutine(CompleteFullStateAndUpdateCoroutine(startupConfig.fullState));
            // oneShotStartupFullState = null; // one-shot
        }

        RefreshClockState();
    }

    public void StartLoadScenarioCoroutine(string scenName)
    {
        fullInitialized = false;
        StartCoroutine(LoadScenario(scenName));
    }

    public IEnumerator LoadScenario(string scenName)
    {
        yield return StreamingAssetReference.Instance.FetchScenarioFile(scenName, s =>
        {
            var fullState = FullState.FromXML(s);
            StartCoroutine(CompleteFullStateAndUpdateCoroutine(fullState));
        });

        // OOBEditor.Instance.oobTreeView.ExpandAll(); // ??? NullReferenceException: Object reference not set to an instance of an object?
        // NavalGameState.Instance.ResetAndRegisterAll(); // Note FromXml call has call it many times.

        Debug.Log("LoadScenario Corountine Completed");
    }

    public IEnumerator CompleteFullStateAndUpdateCoroutine(FullState fullState)
    {
        fullInitialized = false;
        ClearAllGunneryShellVisuals();

        // Loading
        yield return fullState.streamingAssetReference.TryToCompleteFromStreamingAssetReference(fullState.navalGameState);
        StreamingAssetReference.UpdateInstance(fullState.streamingAssetReference);

        EventState.UpdateTo(fullState.eventState);
        yield return EventState.Instance.SyncAndRegister();

        LoadViewState(fullState.viewState);
        NavalGameState.UpdateInstance(fullState.navalGameState);

        NavalGameState.Instance.ResetAndRegisterAll();
        InitializeGunneryLogProcessingBaseline();

        if (startupConfig.scenarioSetupGenerator != null)
        {
            startupConfig.scenarioSetupGenerator.Setup();
        }

        if(startupConfig.autoDeployment != null)
        {
            // TODO: Process AutoDeployment
            DialogRoot.Instance.PopupAutoDeploymentDialog();
        }

        Debug.Log("OnFullStateXMLLoadedCoroutine");

        // Other Initialization
        ScriptEngine.Instance.Reset(); // Better location to invoke reset of Script Engine?

        loaded?.Invoke(this, EventArgs.Empty);

        if (!NavalGameState.Instance.scenarioState.firstLoaded)
        {
            NavalGameState.Instance.scenarioState.firstLoaded = true;
            firstLoaded?.Invoke(this, EventArgs.Empty);

            if(GamePreference.Instance.showAIDialog && navalGameState.shipGroups.Count > 0) // > 0 filter out "view only" mode implicitly
            {
                DialogRoot.Instance.PopupAIDialog();
            }
        }

        if(startupConfig.isFromSkirmish)
        {
            DialogRoot.Instance.PopupForceBuilderDialog();
        }

        TempFix();

        fullInitialized = true;

        audioSource.clip = oceanWaveSound;
        audioSource.loop = true;
        audioSource.volume = 0.5f;
        audioSource.Play();

        audioSource.PlayOneShot(shipBellSound);
    }

    public EventHandler firstLoaded;
    public EventHandler loaded;
    public EventHandler minuteChanged;
    public EventHandler shipLogClicked;

    void TempFix()
    {
        // foreach (var shipGroup in navalGameState.shipGroups)
        // {
        //     shipGroup.leaderReference.referenceObjectId = shipGroup.leaderObjectId;
        // }

        // foreach(var shipClass in NavalGameState.Instance.shipClasses)
        // {
        //     foreach(var btyRec in shipClass.batteryRecords)
        //     {
        //         var prevCode = btyRec.fireControlType.code;
        //         btyRec.fireControlType.SyncCodeByStates();
        //         var currentCode = btyRec.fireControlType.code;
        //         if(currentCode == FCSCode.Custom)
        //         {
        //             Debug.LogWarning($"{btyRec.labelName} => {btyRec.fireControlType}");
        //         }
        //     }
        // }

        // foreach(var shipClass in NavalGameState.Instance.shipClasses)
        // {
        //     shipClass.armorRating.TryInferArmorType();
        // }

        // foreach(var shipClass in NavalGameState.Instance.shipClasses)
        // {
        //     foreach(var btyRec in shipClass.batteryRecords)
        //     {
        //         foreach(var mntRec in btyRec.mountLocationRecords)
        //         {
        //             if(mntRec.defaultNarrow)
        //             {
        //                 mntRec.mountArcsPattern = MountArcsPattern.Casemate;
        //                 mntRec.SyncDefaultMountArcs();
        //             }
        //         }
        //     }

        //     foreach(var mntLoc in shipClass.torpedoSector.mountLocationRecords)
        //     {
        //         if(mntLoc.defaultNarrow)
        //         {
        //             mntLoc.mountArcsPattern = MountArcsPattern.Narrow;
        //         }
        //     }
        // }

        // foreach(var shipClass in NavalGameState.Instance.shipClasses)
        // {
        //     foreach(var mntLoc in shipClass.torpedoSector.mountLocationRecords)
        //     {
        //         if(mntLoc.mountArcs.Count == 1)
        //         {
        //             var arc = mntLoc.mountArcs.First();
        //             if(arc.CoverageDeg == 30)
        //             {
        //                 mntLoc.mountArcsPattern = MountArcsPattern.Narrow;
        //             }
        //         }
        //     }
        // }

        // foreach(var shipClass in NavalGameState.Instance.shipClasses)
        // {
        //     if(shipClass.batteryRecords.Count >=3 && shipClass.rapidFireBatteryRecords.Count >= 2)
        //     {
        //         Debug.Log(shipClass.name);
        //     }
        // }

        // foreach(var shipClass in NavalGameState.Instance.shipClasses)
        // {
        //     foreach(var btyRec in shipClass.batteryRecords)
        //     {
        //         foreach(var mntRec in btyRec.mountLocationRecords)
        //         {
        //             if(mntRec.mountArcsPattern == MountArcsPattern.Casemate)
        //             {
        //                 mntRec.SyncDefaultMountArcs();
        //             }
        //         }
        //     }
        // }
    }

    public ViewState CaptureViewState()
    {
        var t = CameraController2.Instance.transform;
        return new()
        {
            xRotation = t.eulerAngles.x,
            yRotation = t.eulerAngles.y,
            orthographicSize = CameraController2.Instance.cam.orthographicSize
        };
    }

    public void LoadViewState(ViewState viewState)
    {
        var c = CameraController2.Instance;
        foreach (var cam in c.cameras)
            cam.orthographicSize = viewState.orthographicSize;
        c.transform.rotation = Quaternion.Euler(viewState.xRotation, viewState.yRotation, 0);
    }


    public Dictionary<string, PortraitViewer> objectId2Viewer = new();
    readonly Dictionary<string, int> processedMountFiringLogCount = new();
    readonly Dictionary<string, int> processedRapidFiringLogCount = new();
    readonly List<IPortraitViewerObservable> viewerObservablesBuffer = new();
    readonly HashSet<string> viewerObjectIdSet = new();
    readonly List<string> viewerRemovalBuffer = new();
    readonly List<DynamicLine> dynamicLinePool = new();
    readonly Queue<BallController> gunneryShellPool = new();
    readonly List<BallController> activeGunneryShells = new();
    bool wasInMultiplayerLastFrame;
    float nextLocationInfoRefreshUnscaledTime;
    static readonly float locationInfoRefreshIntervalSeconds = 0.1f;
    RangeLineRenderSignature lastRangeLineRenderSignature;
    static Mesh gunneryShellConeMesh;

    public string hoveringLocationInfo;
    public bool currentLogOnly = true;

    public void ReturnToStrategicGame()
    {
        StrategicGameManager.startupConfig.syncShipLogs = NavalGameState.Instance.shipLogs;
        StrategicGameManager.startupConfig.victoryStatus = VictoryStatus.Generate(NavalGameState.Instance);

        SceneManager.LoadScene("Strategic Game");
    }

    LatLon latestHoveringLatLon = new();

    struct RangeLineRenderSignature
    {
        public string shipObjectId;
        public float latDeg;
        public float lonDeg;
        public float headingDeg;
        public GamePreference.RangeRingDisplayMode rangeMode;
    }

    // float viewAccTime;
    void UpdateLocationInfoLabel()
    {
        if (Time.unscaledTime < nextLocationInfoRefreshUnscaledTime)
            return;
        nextLocationInfoRefreshUnscaledTime = Time.unscaledTime + locationInfoRefreshIntervalSeconds;

        var ray = CameraController2.Instance.cam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            var hitPoint = hit.point;

            var latLon = Utils.Vector3ToLatLon(hitPoint);

            latestHoveringLatLon = latLon;

            var scenarioState = NavalGameState.Instance.scenarioState;

            var timeZoneOffset = ScenarioState.GetTimeZoneOffset(latLon.LonDeg);
            // var timeZoneOffsetF = timeZoneOffset.ToString("+#;-#;0");

            var sunState = scenarioState.GetSunPosition(latLon);

            var latF = latLon.LatDeg.ToString("0.000");
            var lonF = latLon.LonDeg.ToString("0.000");
            var localCurrentDateTimeOffset = ScenarioState.GetLocalDateTimeOffset(latLon.LonDeg, scenarioState.dateTime);

            var sunAziF = sunState.azimuthDeg.ToString("0.0");
            var sunAltF = sunState.altitudeDeg.ToString("0.0");

            var dayNightLevel = sunState.GetDayNightLevel();

            // hoveringLocationInfo = string.Format(
            //     "Lat: {0} Lon: {1} Local: {2} ({3},{4}) Sun Alt: {5} Sun Azi: {6}",
            //     latF, lonF, localCurrentDateTimeOffset, LocalizeEnum(dayNightLevel), timeZoneOffsetF, sunAltF, sunAziF
            // );

            // if (scenarioState.hasEndDateTime)
            // {
            //     var localEndDateTimeOffset = ScenarioState.GetLocalDateTimeOffset(latLon.LonDeg, scenarioState.endDateTime);
            //     var scenarioStart = scenarioState.beginDateTime;
            //     var totalSeconds = (scenarioState.endDateTime - scenarioStart).TotalSeconds;
            //     var elapsedSeconds = (scenarioState.dateTime - scenarioStart).TotalSeconds;
            //     var elapsedRatio = totalSeconds > 0 ? Math.Clamp(elapsedSeconds / totalSeconds, 0d, 1d) : 1d;

            //     hoveringLocationInfo += string.Format(
            //         " | End Local: {0} | Elapsed: {1:0.0}%",
            //         localEndDateTimeOffset,
            //         elapsedRatio * 100d
            //     );
            // }

            // var hoveringLocationInfoTime = scenarioState.hasEndDateTime ?
            //     $"{localCurrentDateTimeOffset} -> {ScenarioState.GetLocalDateTimeOffset(latLon.LonDeg, scenarioState.endDateTime)} {:P0}"
            string hoveringLocationInfoTime;
            if(!scenarioState.hasEndDateTime)
            {
                hoveringLocationInfoTime = $"{localCurrentDateTimeOffset}";
            }
            else
            {
                var localEndDateTimeOffset = ScenarioState.GetLocalDateTimeOffset(latLon.LonDeg, scenarioState.endDateTime);
                var scenarioStart = scenarioState.beginDateTime;
                var totalSeconds = (scenarioState.endDateTime - scenarioStart).TotalSeconds;
                var elapsedSeconds = (scenarioState.dateTime - scenarioStart).TotalSeconds;
                var elapsedRatio = totalSeconds > 0 ? Math.Clamp(elapsedSeconds / totalSeconds, 0d, 1d) : 1d;

                var totalMinutes = (int)Math.Floor(totalSeconds / 60);
                var elapsedMinutes = (int)Math.Floor(elapsedSeconds / 60);

                hoveringLocationInfoTime = $"{localCurrentDateTimeOffset} -> {localEndDateTimeOffset} ({elapsedMinutes}/{totalMinutes}, {elapsedRatio:P0})";
            }

            hoveringLocationInfo = Localize(
                "Lat: {0} Lon: {1} {2}, {3} (Sun Alt: {4}, Azi: {5})",
                latF, lonF, hoveringLocationInfoTime, LocalizeEnum(dayNightLevel), sunAltF, sunAziF
            );
        }
    }

    public DateTimeOffset GetDateTimeOffsetByLatestHoveringLatLon(DateTime time)
    {
        return ScenarioState.GetDateTimeOffset(time, latestHoveringLatLon.LonDeg);
    }

    public float GetTimeZoneOffsetByLatestHoveringLocation() => ScenarioState.GetTimeZoneOffset(latestHoveringLatLon.LonDeg);

    static string Localize(string key, params object[] args) => ServiceLocator.Get<ILocalizeService>().Get(key, args);
    protected static string LocalizeEnum<T>(T obj) => ServiceLocator.Get<ILocalizeService>().GetEnum(obj);

    public float remainAdvanceSimulationSecondsRequestedByUserInput; // Requested by KeyCode 1-9 (1-9 min) and BackQuote (`) (1s)
    public float remainAdvanceSimulationSecondsRequestedByUpdate;
    float projectileVisualAdvanceSecondsThisFrame;
    public bool isAutoPlaying = false;
    float _savedTimeScaleBeforePause = 1f;
    bool _timeScalePausedByGameManager = false;

    // public float simulationRateRaio = 30;
    // float simulationRateRaio = 120;
    // float pulseLengthSeconds = 1;

    bool IsSimulationAdvancing()
    {
        var pulseLengthSeconds = GamePreference.Instance.pulseLengthSeconds;
        return isAutoPlaying || remainAdvanceSimulationSecondsRequestedByUserInput >= pulseLengthSeconds;
    }

    public float GetCurrentSimulationAdvanceRatio()
    {
        if (isAutoPlaying)
            return GamePreference.Instance.simulationRateRatioAuto;

        var pulseLengthSeconds = GamePreference.Instance.pulseLengthSeconds;
        if (remainAdvanceSimulationSecondsRequestedByUserInput >= pulseLengthSeconds)
            return GamePreference.Instance.simulationRateRatio;

        return 0f;
    }

    void PauseUnityClock()
    {
        if (_timeScalePausedByGameManager)
            return;
        _savedTimeScaleBeforePause = Time.timeScale;
        Time.timeScale = 0f;
        _timeScalePausedByGameManager = true;

        Physics.SyncTransforms();
    }

    void ResumeUnityClock()
    {
        if (!_timeScalePausedByGameManager)
            return;
        var recover = _savedTimeScaleBeforePause > 0 ? _savedTimeScaleBeforePause : 1f;
        Time.timeScale = recover;
        _timeScalePausedByGameManager = false;
    }

    public void RefreshClockState()
    {
        if (IsSimulationAdvancing())
            ResumeUnityClock();
        else
            PauseUnityClock();
    }

    public void UpdateSimulation()
    {
        projectileVisualAdvanceSecondsThisFrame = 0f;
        RefreshClockState();

        var pulseLengthSeconds = GamePreference.Instance.pulseLengthSeconds;
        var simulationRateRatio = GamePreference.Instance.simulationRateRatio;
        var simulationRateRatioAuto = GamePreference.Instance.simulationRateRatioAuto;

        var realSeconds = Time.deltaTime;

        if(isAutoPlaying) // auto adavance mode
        {
            var visualAdvance = realSeconds * simulationRateRatioAuto;
            remainAdvanceSimulationSecondsRequestedByUpdate += visualAdvance;
            projectileVisualAdvanceSecondsThisFrame += visualAdvance;
        }
        else // manual advance mode
        {
            if (remainAdvanceSimulationSecondsRequestedByUserInput >= pulseLengthSeconds)
            {
                var visualAdvance = realSeconds * simulationRateRatio;
                remainAdvanceSimulationSecondsRequestedByUpdate += visualAdvance;
                projectileVisualAdvanceSecondsThisFrame += visualAdvance;
            }
        }
        
        // var advanceAny = false;
        var minuteAdvanced = false;

        if(isAutoPlaying) // auto adavance mode
        {
            while (remainAdvanceSimulationSecondsRequestedByUpdate >= pulseLengthSeconds)
            {
                var lastMin = NavalGameState.Instance.scenarioState.dateTime.Minute;

                NavalGameState.Instance.Step(pulseLengthSeconds);
                remainAdvanceSimulationSecondsRequestedByUpdate -= pulseLengthSeconds;

                if (NavalGameState.Instance.scenarioState.dateTime.Minute != lastMin)
                {
                    minuteChanged?.Invoke(this, EventArgs.Empty);
                    
                    minuteAdvanced = true;
                }
            }
        }
        else // manual advance mode
        {
            var manualAdvanceAny = false;
            while (remainAdvanceSimulationSecondsRequestedByUserInput >= pulseLengthSeconds && remainAdvanceSimulationSecondsRequestedByUpdate >= pulseLengthSeconds)
            {
                manualAdvanceAny = true;

                var lastMin = NavalGameState.Instance.scenarioState.dateTime.Minute;

                NavalGameState.Instance.Step(pulseLengthSeconds);
                remainAdvanceSimulationSecondsRequestedByUserInput -= pulseLengthSeconds;
                remainAdvanceSimulationSecondsRequestedByUpdate -= pulseLengthSeconds;

                if (NavalGameState.Instance.scenarioState.dateTime.Minute != lastMin)
                {
                    minuteChanged?.Invoke(this, EventArgs.Empty);
                    
                    minuteAdvanced = true;
                }
            }

            var advanceEnded = remainAdvanceSimulationSecondsRequestedByUserInput < pulseLengthSeconds;

            if(manualAdvanceAny && 
                (
                    (hostSyncMode == HostSyncMode.EveryUnityUpdate) || 
                    (hostSyncMode == HostSyncMode.AdvanceEnd && advanceEnded)
                )
            )
            {
                // Send GameState sync command to all clients in the host mode
                if(networkingManager is NetworkingHostManager hostManager)
                {
                    var command = new NavalNetworkingCommands.GameStateSync()
                    {
                        gameState = NavalGameState.Instance // TODO: Detach?
                    };
                    foreach(var connection in hostManager.connections)
                    {
                        hostManager.SendCommand(connection, command);
                    }
                }
            }
        }

        if(minuteAdvanced) // When control is return to player (active advancing is completed)
        {
            HandleAutoEnd();
        }

        RefreshClockState();
    }

    public void HandleAutoEnd()
    {
        var scenarioState = navalGameState.scenarioState;

        if(!scenarioState.firstRemainOneOperationalFleet)
        {
            var rootGroups = navalGameState.shipGroups.Where(g => g.parentObjectId == null).ToList();
            var rootGroupShips = rootGroups.Select(g => g.Walk<ShipLog>().ToList()).ToList();
            // var test = rootGroupShips.Select(shipLogs => shipLogs.Select(s => (s.mapState, s.operationalState, s.GetMaxSpeedKnots())).ToList()).ToList();
            var operationalGroupCounts = rootGroupShips.Count(shipLogs => 
                shipLogs.Any(shipLog =>
                    shipLog.mapState == MapState.Deployed
                    && shipLog.operationalState == ShipOperationalState.Operational
                    && (
                        shipLog.GetMaxSpeedKnots() > 0
                        || (shipLog.IsLandBattery() && shipLog.isLandTarget)
                    )
                )
            );

            if(operationalGroupCounts <= 1) // Effective Completed (only one side has effective ships)
            {
                scenarioState.firstRemainOneOperationalFleet = true;

                if(!scenarioState.disableFirstRemainOneOperationalFleetPrompt)
                {
                    if(startupConfig.IsFromStrategic())
                    {
                        DialogRoot.Instance.PopupConfirmDialog(Localize(
                            "The battle field has only a operational fleet now. You can return to the strategic game now, or use the button on the top menu bar to return at any time."
                        ), () =>
                        {
                            ReturnToStrategicGame();
                        });
                    }
                    else
                    {
                        DialogRoot.Instance.PopupConfirmDialog(Localize(
                            "The battle field has only a operational fleet now. Check the victory status now?"
                            ), () =>
                            {
                                DialogRoot.Instance.PopupVictoryStatusDialog(
                                    VictoryStatus.Generate(NavalGameState.Instance)
                                );
                            }
                        );
                    }
                }
            }
            else if(!navalGameState.scenarioState.firstDisengaged) // So operationalGroupCounts.Count >= 2
            {
                var latLonAverages = rootGroupShips.Select(shipLogs => new LatLon(
                    shipLogs.Average(s => s.GetLatitudeDeg()), // FIXME: separated retreat?
                    shipLogs.Average(s => s.GetLongitudeDeg())
                )).ToList();

                var disengagedAny = false;
                for(int i=0; i<latLonAverages.Count; i++)
                {
                    for(int j=i+1; j<latLonAverages.Count; j++)
                    {
                        var distIJYards = MeasureStats.Approximation.HaversineDistanceYards(latLonAverages[i], latLonAverages[j]);
                        if(distIJYards > 48000) // 48000 yards to determine disengaged
                        {
                            disengagedAny = true;
                            break;
                        }
                    }
                }
                if(disengagedAny)
                {
                    scenarioState.firstDisengaged = true;

                    if(startupConfig.IsFromStrategic())
                    {
                        DialogRoot.Instance.PopupConfirmDialog(Localize(
                            "Fleets appears to be disengaged. You can return to the strategic game now, or use the button on the top menu bar to exit at any time."
                        ), () =>
                        {
                            ReturnToStrategicGame();
                        });
                    }
                    else
                    {
                        DialogRoot.Instance.PopupConfirmDialog(Localize(
                            "Fleets appears to be disengaged. Check the victory status now?"
                            ), () =>
                            {
                                DialogRoot.Instance.PopupVictoryStatusDialog(
                                    VictoryStatus.Generate(NavalGameState.Instance)
                                );
                            }
                        );
                    }
                }
            }
        }

        if(scenarioState.hasEndDateTime && scenarioState.dateTime > scenarioState.endDateTime && !scenarioState.firstReachEndDateTime)
        {
            scenarioState.firstReachEndDateTime = true;

            if(startupConfig.IsFromStrategic())
            {
                DialogRoot.Instance.PopupConfirmDialog(Localize(
                    "Scenario End time is reached. You can return to the strategic game now, or use the button on the top menu bar to return at any time."
                ), () =>
                {
                    ReturnToStrategicGame();
                });
            }
            else
            {
                DialogRoot.Instance.PopupConfirmDialog(Localize(
                    "Scenario End time is reached. Check the victory status now?"
                    ), () =>
                    {
                        DialogRoot.Instance.PopupVictoryStatusDialog(
                            VictoryStatus.Generate(NavalGameState.Instance)
                        );
                    }
                );
            }
        }
    }

    static Dictionary<KeyCode, float> simulationSecondsAdvanceMap = new()
    {
        // {KeyCode.Tilde, 1}, // 1s, Note Tilde, BackQuote may be blocked by input method. So it's recommended to disable input method when playing.
        // {KeyCode.BackQuote, 1},
        {KeyCode.Alpha1, 60 * 1}, // 1 min
        {KeyCode.Alpha2, 60 * 2}, // 2 min
        {KeyCode.Alpha3, 60 * 3},
        {KeyCode.Alpha4, 60 * 4},
        {KeyCode.Alpha5, 60 * 5},
        {KeyCode.Alpha6, 60 * 6},
        {KeyCode.Alpha7, 60 * 7},
        {KeyCode.Alpha8, 60 * 8},
        {KeyCode.Alpha9, 60 * 9},
    };

    public void SetSelectedShipCourseTowardPointer()
    {
        SetSelectedShipCourseTowardScreenPoint((Vector2)Input.mousePosition);
    }

    public void SetSelectedShipCourseTowardScreenPoint(Vector2 screenPoint)
    {
        if (selectedShipLog != null)
        {
            if (selectedShipLog.IsLandBattery())
                return;

            var cameraController = CameraController2.Instance;
            if (cameraController == null || cameraController.cam == null)
                return;

            var ray = cameraController.cam.ScreenPointToRay(screenPoint);
            if (!Physics.Raycast(ray, out var hit))
                return;

            var hitPoint = hit.point;
            var dstPos = Utils.Vector3ToLatLon(hitPoint);

            var currentPos = selectedShipLog.position;
            var inverseLine = Geodesic.WGS84.InverseLine(
                currentPos.LatDeg, currentPos.LonDeg,
                dstPos.LatDeg, dstPos.LonDeg
            );

            selectedShipLog.desiredHeadingDeg = MeasureUtils.NormalizeAngle((float)inverseLine.Azimuth);
        }
    }

    void ClearPendingRightClickAction()
    {
        rightClickPendingExecution = false;
        rightClickPendingAction = RightClickPendingAction.None;
    }

    void HandleRightClickCandidateInIdle()
    {
        if (Input.GetMouseButtonDown(1))
        {
            rightClickCandidateActive = true;
            rightClickDownPosition = (Vector2)Input.mousePosition;
            ClearPendingRightClickAction();
        }

        if (rightClickCandidateActive && Input.GetMouseButtonUp(1))
        {
            rightClickCandidateActive = false;
            var releasePosition = (Vector2)Input.mousePosition;
            var clickDistance = Vector2.Distance(rightClickDownPosition, releasePosition);
            if (clickDistance > rightClickMaxClickDistancePixels)
            {
                ClearPendingRightClickAction();
                return;
            }

            var shipLog = TryToRaycastShipLog();
            if (shipLog != null && selectedShipLogObjectId == shipLog.objectId)
                rightClickPendingAction = RightClickPendingAction.OpenSelectedShipView;
            else if (selectedShipLog != null)
                rightClickPendingAction = RightClickPendingAction.SetSelectedShipCourse;
            else
                rightClickPendingAction = RightClickPendingAction.None;

            if (rightClickPendingAction == RightClickPendingAction.None)
            {
                ClearPendingRightClickAction();
                return;
            }

            rightClickPendingExecution = true;
            rightClickReleasePosition = releasePosition;
            rightClickReleaseTime = Time.unscaledTime;
        }
    }

    void TryExecutePendingRightClickActionInIdle()
    {
        if (!rightClickPendingExecution)
            return;

        if (state != State.Idle)
        {
            ClearPendingRightClickAction();
            return;
        }

        if (Input.GetMouseButton(1))
        {
            ClearPendingRightClickAction();
            return;
        }

        var stayDistance = Vector2.Distance((Vector2)Input.mousePosition, rightClickReleasePosition);
        if (stayDistance > rightClickMaxClickDistancePixels)
        {
            ClearPendingRightClickAction();
            return;
        }

        if (Time.unscaledTime - rightClickReleaseTime < rightClickPostReleaseHoldSeconds)
            return;

        if (rightClickPendingAction == RightClickPendingAction.OpenSelectedShipView)
        {
            var shipLog = TryToRaycastShipLog();
            if (shipLog != null && selectedShipLogObjectId == shipLog.objectId)
                SwitchCenter.Instance.SwitchToShipLogView(shipLog);
        }
        else if (rightClickPendingAction == RightClickPendingAction.SetSelectedShipCourse)
        {
            SetSelectedShipCourseTowardScreenPoint(rightClickReleasePosition);
        }

        ClearPendingRightClickAction();
    }

    public void SetRemainAdvanceSimulationSecondsRequestedByUserInput(float value)
    {
        if(networkingManager is NetworkingHostManager hostManager && hostManager != null)
        {
            readyToAdvanceAsHost = true;
            remainAdvanceSimulationSecondsRequestedByUserInputPending = value;
            RefreshClockState();
        }
        else if(networkingManager is NetworkingClientManager clientManager && clientManager != null)
        {
            // TODO: Extract Take Command related states and Send MergeRequest to the host.
            var host = GetHostConnectionOrNull();
            if(host != null)
            {
                clientManager.SendCommand(host, CreateMergeRequestByExtraction());
            }
            else
            {
                DialogRoot.Instance.PopupMessageDialog("Invalid Host");
            }
            RefreshClockState();
        }
        else // single player advance
        {
            if (currentLogOnly)
                NavalGameState.Instance.tempSubjectLogs.Clear();

            remainAdvanceSimulationSecondsRequestedByUserInput = value;
            RefreshClockState();
        }
    }

    // bool GetValidToAdvanceByHand()
    // {
    //     if(networkingManager == null)
    //         return true;
    //     if(networkingManager is NetworkingHostManager hostManager)
    //     {
    //         return readyToAdvanceAsHost && connectionInfoMap.Values.All(info => info.takeCommandIds.Count == 0 || info.mergeRequest != null);
    //     }
    //     return false;
    // }

    public static bool showSunkShips = true;

    public bool IsHotKeyEnabled()
    {
        // if(EventSystem.current.IsPointerOverGameObject())
        //     return false;

        if(allUIDocuments != null)
        {
            foreach (var doc in allUIDocuments)
            {
                var root = doc.rootVisualElement;
                if (root == null) continue;

                var focused = root.focusController?.focusedElement;
                if (focused == null) continue;

                return false; // UITK focus => block hot keys
                // if (IsTextInputElement(focused))
                //     return true;
            }
        }

        return true;
    }

    [Header("Right Click Course Setting")]
    public float rightClickMaxClickDistancePixels = 8f;
    public float rightClickPostReleaseHoldSeconds = 0.05f;
    bool rightClickCandidateActive;
    Vector2 rightClickDownPosition;
    bool rightClickPendingExecution;
    Vector2 rightClickReleasePosition;
    float rightClickReleaseTime;
    enum RightClickPendingAction
    {
        None,
        OpenSelectedShipView,
        SetSelectedShipCourse
    }
    RightClickPendingAction rightClickPendingAction = RightClickPendingAction.None;

    public void Update()
    {
        if(networkingManager is NetworkingHostManager hostManager && hostManager != null)
        {
            // if(readyToAdvanceAsHost && connectionInfoMap.Values.All(info => info.takeCommandIds.Count == 0 || info.mergeRequest != null))
            if(readyToAdvanceAsHost && hostManager.connections.Select(conn => GetConnectionInfo(conn)).All(info => info.takeCommandIds.Count == 0 || info.mergeRequest != null))
            {
                if (currentLogOnly)
                    NavalGameState.Instance.tempSubjectLogs.Clear();

                foreach(var connInfo in hostManager.connections.Select(conn => GetConnectionInfo(conn)))
                {
                    if(connInfo.mergeRequest != null)
                    {
                        connInfo.mergeRequest.DoMerge();
                    }
                }
                // Send Advance Command
                // var advanceCommand = new NavalNetworkingCommands.AdvanceSimulation();
                remainAdvanceSimulationSecondsRequestedByUserInput = remainAdvanceSimulationSecondsRequestedByUserInputPending;
                remainAdvanceSimulationSecondsRequestedByUserInputPending = 0;
                readyToAdvanceAsHost = false;
                RefreshClockState();
            }
        }

        // Networking
        networkingManager?.Update();

        var isInMultiplayerNow = isHostOrClient;
        if (isInMultiplayerNow != wasInMultiplayerLastFrame)
        {
            HandleMultiplayerVisualModeTransition(isInMultiplayerNow);
            wasInMultiplayerLastFrame = isInMultiplayerNow;
        }

        UpdateSimulation();
        if (fullInitialized)
        {
            SyncGunneryShellVisualsFromLogs();
            ForceAdvanceGunneryShellVisualsThisFrame();
        }
        // viewAccTime += Time.deltaTime;

        // if (viewAccTime > 2)
        // {
        //     viewAccTime -= 2;
        //     Debug.Log("2s Tick");
        // }

        // sync Ship's Viewer and ShipLog mapping
        viewerObservablesBuffer.Clear();
        viewerObjectIdSet.Clear();
        viewerRemovalBuffer.Clear();

        var displayedShipLogs = showSunkShips ? NavalGameState.Instance.shipLogsOnMapOrDestroyed : NavalGameState.Instance.shipLogsOnMap;
        viewerObservablesBuffer.AddRange(displayedShipLogs);
        viewerObservablesBuffer.AddRange(NavalGameState.Instance.launchedTorpedosOnMap);

        foreach (var observable in viewerObservablesBuffer)
        {
            viewerObjectIdSet.Add(observable.objectId);
            if (!objectId2Viewer.ContainsKey(observable.objectId))
            {
                var obj = Instantiate(shipUnitPrefab, earthTransform);

                var portraitView = obj.GetComponent<PortraitViewer>();
                portraitView.modelObjectId = observable.objectId;
                objectId2Viewer[observable.objectId] = portraitView;
            }
        }

        foreach (var objectId in objectId2Viewer.Keys)
        {
            if (!viewerObjectIdSet.Contains(objectId))
            {
                viewerRemovalBuffer.Add(objectId);
            }
        }

        foreach (var objectId in viewerRemovalBuffer)
        {
            var viewer = objectId2Viewer[objectId];
            Destroy(viewer.gameObject); // Or Set Inactive only?
            objectId2Viewer.Remove(objectId);
        }

        // sync Line renderer to show firing line, fire control line, fired line etc.

        SyncDynamicLines();

        SyncRangeLine();

        // location browser: current latitude, longitude, time zone, local time, sun altitude, day/night discrete value
        UpdateLocationInfoLabel();

        // Handle Events
        if (CameraController2.Instance != null && CameraController2.Instance.HandleMiddleMouseCameraRotation())
        {
            return;
        }

        if (!IsHotKeyEnabled())
        {
            rightClickCandidateActive = false;
            ClearPendingRightClickAction();
        }

        if (IsHotKeyEnabled())
        {
            if (Input.GetKeyDown(KeyCode.H))
            {
                CameraController2.Instance?.ReturnTo2DView();
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                state = State.Idle;
                selectedShipLogObjectId = null;
                rightClickCandidateActive = false;
                ClearPendingRightClickAction();
                return;
            }

            var isPressingShift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            var isPressingAlt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

            if (state != State.Idle && (rightClickCandidateActive || rightClickPendingExecution))
            {
                rightClickCandidateActive = false;
                ClearPendingRightClickAction();
            }

            if (state == State.Idle) // unit left click chosen
            {
                HandleRightClickCandidateInIdle();
                TryExecutePendingRightClickActionInIdle();

                // handle events
                if (Input.GetKeyDown(KeyCode.Insert) && isPressingAlt) // Insert(Deploy) Unit (traditional) TODO: Remove it?
                {
                    state = State.SelectingInsertUnitPosition;
                }
                if (Input.GetKeyDown(KeyCode.Insert) && !isPressingAlt) // Insert(Deploy) Unit
                {
                    state = State.SelectingInsertUnitPositionComplex;
                }


                if (Input.GetKeyDown(KeyCode.M) && selectedShipLog != null) // Move Unit
                {
                    state = State.MovingUnit;
                }

                if (Input.GetMouseButtonDown(0) && !isPressingShift) // try select a unit
                {
                    var shipLog = TryToRaycastShipLog(); // TODO: Handle other click? (like land target?)
                    if (shipLog != null)
                    {
                        selectedShipLogObjectId = shipLog.objectId;

                        ShipLogEditor.Instance.selectedShipLogObjectId = selectedShipLogObjectId;

                        shipLogClicked?.Invoke(this, EventArgs.Empty);
                    }
                }

                if (Input.GetMouseButtonDown(0) && isPressingShift) // RTW-like course setting
                {
                    SetSelectedShipCourseTowardPointer();
                }

                foreach ((var keyCode, var advanceSimulationSeconds) in simulationSecondsAdvanceMap)
                {
                    if (Input.GetKeyDown(keyCode))
                    {

                        // remainAdvanceSimulationSecondsRequestedByUserInput = advanceSimulationSeconds;
                        SetRemainAdvanceSimulationSecondsRequestedByUserInput(advanceSimulationSeconds);
                    }
                }

                if (Input.GetKeyDown(KeyCode.Tilde) || Input.GetKeyDown(KeyCode.BackQuote))
                {
                    // remainAdvanceSimulationSecondsRequestedByUserInput = GamePreference.Instance.pulseLengthSeconds;
                    SetRemainAdvanceSimulationSecondsRequestedByUserInput(GamePreference.Instance.pulseLengthSeconds);
                }

                if(Input.GetKeyDown(KeyCode.Space))
                {
                    if(networkingManager == null)
                    {
                        isAutoPlaying = !isAutoPlaying;
                        if(isAutoPlaying) // Clear logs if current only and clear potential "leaked" seconds requested by input.
                        {
                            SetRemainAdvanceSimulationSecondsRequestedByUserInput(0);
                        }
                        RefreshClockState();
                    }
                    else
                    {
                        DialogRoot.Instance.PopupMessageDialog("Auto play can't be used in the multiplayer mode");
                    }
                }

                if (Input.GetKeyDown(KeyCode.I) && selectedShipLog != null)
                {
                    selectedShipLog.controlMode = ControlMode.Independent;
                }

                if (Input.GetKeyDown(KeyCode.F) && selectedShipLog != null) // Set "Follow" Control
                {
                    state = State.SelectingFollowedTarget;
                }

                if (Input.GetKeyDown(KeyCode.R) && selectedShipLog != null) // Set "Relative To" Control
                {
                    state = State.SelectingRelativeToTarget;
                }

                if (Input.GetKeyDown(KeyCode.L) && selectedShipLog != null) // open ship Log editor
                {
                    // ShipLogEditor.Instance.PopupWithSelection(selectedShipLog);
                    SwitchCenter.Instance.SwitchToShipLogView(selectedShipLog);
                }
                if (Input.GetKeyDown(KeyCode.Delete) && selectedShipLog != null) // toggle ship on map back up undeployed
                {
                    selectedShipLog.mapState = MapState.NotDeployed;
                }
                if(Input.GetKeyDown(KeyCode.A) && selectedShipLog != null) // Set Ship-level attack target
                {
                    state = State.SelectingShipLevelFiringTarget;
                }
            }
            else if (state == State.SelectingInsertUnitPosition)
            {
                if (Input.GetMouseButtonDown(0))
                {
                    state = State.Idle;

                    var ray = CameraController2.Instance.cam.ScreenPointToRay(Input.mousePosition);
                    if (Physics.Raycast(ray, out RaycastHit hit))
                    {
                        var hitPoint = hit.point;

                        lastSelectedLatLon = Utils.Vector3ToLatLon(hitPoint);
                    }

                    // lastSelectedLatLon

                    DialogRoot.Instance.PopupShipLogSelectorDialogForRedeploy();
                }
            }
            else if(state == State.SelectingInsertUnitPositionComplex)
            {
                if (Input.GetMouseButtonDown(0))
                {
                    state = State.Idle;

                    var ray = CameraController2.Instance.cam.ScreenPointToRay(Input.mousePosition);
                    if (Physics.Raycast(ray, out RaycastHit hit))
                    {
                        var hitPoint = hit.point;

                        lastSelectedLatLon = Utils.Vector3ToLatLon(hitPoint);
                    }

                    // lastSelectedLatLon

                    DialogRoot.Instance.PopupInsertShipComplexDialog();
                }
            }
            else if (state == State.MovingUnit)
            {
                if (Input.GetMouseButtonDown(0))
                {
                    state = State.Idle;
                    if (selectedShipLog != null)
                    {
                        var hitPoint = CameraController2.Instance.GetHitPoint();
                        selectedShipLog.position = Utils.Vector3ToLatLon(hitPoint);
                    }
                }
            }
            else if (state == State.SelectingFollowedTarget)
            {
                if (Input.GetMouseButtonDown(0))
                {
                    state = State.Idle;
                    if (selectedShipLog != null)
                    {
                        var targetShipLog = TryToRaycastShipLog();
                        if (targetShipLog != null && CheckGiveControlTo(selectedShipLog, targetShipLog))
                        {
                            selectedShipLog.followedTargetObjectId = targetShipLog.objectId;
                            selectedShipLog.controlMode = ControlMode.FollowTarget;
                            Debug.Log($"Set Followed Object ID: {selectedShipLog.objectId} -> {targetShipLog.objectId}");
                        }
                    }
                }
            }
            else if (state == State.SelectingRelativeToTarget)
            {
                if (Input.GetMouseButtonDown(0))
                {
                    state = State.Idle;
                    if (selectedShipLog != null)
                    {
                        var targetShipLog = TryToRaycastShipLog();
                        if (targetShipLog != null && CheckGiveControlTo(selectedShipLog, targetShipLog))
                        {
                            selectedShipLog.relativeTargetObjectId = targetShipLog.objectId;
                            selectedShipLog.controlMode = ControlMode.RelativeToTarget;
                            Debug.Log($"Set Relative To Object ID: {selectedShipLog.objectId} -> {targetShipLog.objectId}");
                        }
                    }
                }
            }
            else if (state == State.SelectingFiringTarget)
            {
                if (Input.GetMouseButtonDown(0))
                {
                    state = State.Idle;
                    // ShipLogEditor.Instance.Show();
                    
                    // TODO: Fix broken soft-close or devise a new way to select target.
                    // SwitchCenter.Instance.SwitchToShipLogView(selectedShipLog);
                    SwitchCenter.Instance.RetoreCurrentSoftHide();
                    
                    if (selectedMountStatusRecord != null)
                    {
                        var targetShipLog = TryToRaycastShipLog();
                        selectedMountStatusRecord.firingTargetObjectId = targetShipLog?.objectId;
                        Debug.Log($"Set Firing Target: {selectedMountStatusRecord.objectId} -> {targetShipLog?.objectId}");
                    }
                }
            }
            else if (state == State.SelectingFireControlSystemTarget)
            {
                if (Input.GetMouseButtonDown(0))
                {
                    state = State.Idle;
                    // TODO: Fix broken soft-close or devise a new way to select target.
                    // ShipLogEditor.Instance.Show();
                    
                    // SwitchCenter.Instance.SwitchToShipLogView(selectedShipLog);
                    SwitchCenter.Instance.RetoreCurrentSoftHide();

                    if (selectedFireControlSystemStatusRecord != null)
                    {
                        var targetShipLog = TryToRaycastShipLog();
                        selectedFireControlSystemStatusRecord.targetObjectId = targetShipLog?.objectId;
                        Debug.Log($"Set Fire Control System Target: {selectedFireControlSystemStatusRecord.objectId} -> {targetShipLog?.objectId}");
                    }
                }
            }
            else if (state == State.SelectingRapidFiringTarget)
            {
                if (Input.GetMouseButtonDown(0))
                {
                    state = State.Idle;
                    // TODO: Fix broken soft-close or devise a new way to select target.
                    // ShipLogEditor.Instance.Show();
                    
                    // SwitchCenter.Instance.SwitchToShipLogView(selectedShipLog);
                    SwitchCenter.Instance.RetoreCurrentSoftHide();
                    
                    if (selectedRapidFiringTargettingStatus != null)
                    {
                        var targetShipLog = TryToRaycastShipLog();
                        selectedRapidFiringTargettingStatus.targetObjectId = targetShipLog?.objectId;
                        Debug.Log($"Set Rapid Firing Battery Target: {selectedRapidFiringTargettingStatus} -> {targetShipLog?.objectId}");
                    }
                }
            }
            else if (state == State.SelectingTorpedoFiringTarget)
            {
                if (Input.GetMouseButtonDown(0))
                {
                    state = State.Idle;
                    // TODO: Fix broken soft-close or devise a new way to select target.
                    // ShipLogEditor.Instance.Show();

                    // SwitchCenter.Instance.SwitchToShipLogView(selectedShipLog);
                    SwitchCenter.Instance.RetoreCurrentSoftHide();

                    if (selectedTorpedoMountStatusRecord != null)
                    {
                        var targetShipLog = TryToRaycastShipLog();
                        selectedTorpedoMountStatusRecord.firingTargetObjectId = targetShipLog?.objectId;
                        Debug.Log($"Set Torpedo Tube Target: {selectedTorpedoMountStatusRecord} -> {targetShipLog?.objectId}");
                    }
                }
            }
            else if (state == State.SelectingCourseTarget)
            {
                if (Input.GetMouseButtonDown(0))
                {
                    state = State.Idle;
                    SetSelectedShipCourseTowardPointer();
                }
            }
            else if(state == State.SelectingShipLevelFiringTarget)
            {
                if(Input.GetMouseButtonDown(0))
                {
                    state = State.Idle;
                    // set Ship-level target
                    var targetShipLog = TryToRaycastShipLog();
                    selectedShipLog.shipLevelFiringTargetObjectId = targetShipLog?.objectId;
                    
                    Debug.Log($"Set Ship-Level Target: {selectedShipLog} -> {targetShipLog}");
                }
            }
        }
    }

    public bool CheckGiveControlTo(ShipLog newControlledShipLog, ShipLog newControlShipLog)
    {
        var result = newControlledShipLog.CheckGiveControlTo(newControlShipLog);
        if (result == ShipLog.CheckGiveControlToResult.Ok)
        {
            return true;
        }
        else if(result == ShipLog.CheckGiveControlToResult.LoopingDetected)
        {
            DialogRoot.Instance.PopupMessageDialog("Invalid: Looping Detected");
        }
        else if(result == ShipLog.CheckGiveControlToResult.DifferentGroupRootParent)
        {
            DialogRoot.Instance.PopupMessageDialog("Invalid: Different Group Root Parent");
        }
        return false;
    }

    public PortraitViewer TryToRaycastViewer()
    {
        var cam = CameraController2.Instance.cam;
        var ray = cam.ScreenPointToRay(Input.mousePosition);

        // In paused timeScale mode with Auto Sync Transforms off, ensure colliders match latest transforms
        // before any immediate raycast-based picking.
        Physics.SyncTransforms();
        var hits = Physics.RaycastAll(ray, Mathf.Infinity, iconLayerMask);
        if (hits.Length == 0)
            return null;

        var dists = hits.Select(hit =>
        {
            var colliderScreenPos = cam.WorldToScreenPoint(hit.collider.bounds.center);
            var colliderScreenPos2D = new Vector2(colliderScreenPos.x, colliderScreenPos.y);
            return Vector2.Distance(colliderScreenPos2D, Input.mousePosition);
        }).ToList();
        var minDist = dists.Min();
        var idx = dists.IndexOf(minDist);
        var colliderRootProvider = hits[idx].collider.GetComponent<IColliderRootProvider>();
        if (colliderRootProvider != null)
        {
            var root = colliderRootProvider.GetRoot();
            var portraitViewer = root.GetComponent<PortraitViewer>();
            return portraitViewer;
        }
        return null;

        // if (Physics.Raycast(ray, out var hit, Mathf.Infinity, iconLayerMask))
        // {
        //     Debug.Log($"Hit: {hit.collider}");
        //     var colliderRootProvider = hit.collider.GetComponent<IColliderRootProvider>();
        //     if (colliderRootProvider != null)
        //     {
        //         var root = colliderRootProvider.GetRoot();
        //         var portraitViewer = root.GetComponent<PortraitViewer>();
        //         return portraitViewer;
        //     }
        // }
        // return null;
    }

    public ShipLog TryToRaycastShipLog()
    {
        return TryToRaycastViewer()?.model as ShipLog;
    }

    // public void OnDestroy()
    // {
    //     if (_instance == this)
    //         _instance = null;
    // }

    // public int selectedShipClassIndex = 0;

    // [CreateProperty]
    // public ShipClass selectedShipClass
    // {
    //     get
    //     {
    //         if (selectedShipClassIndex >= navalGameState.shipClasses.Count || selectedShipClassIndex < 0)
    //             return null;
    //         return navalGameState.shipClasses[selectedShipClassIndex];
    //     }
    // }

    // public ShipClass SelectedShipClassProvider()
    // {
    //     return selectedShipClass;
    // }

    public string selectedShipLogObjectId;

    [CreateProperty]
    public ShipLog selectedShipLog
    {
        get
        {
            return EntityManager.Instance.Get<ShipLog>(selectedShipLogObjectId);
        }
    }

    // public string selectedLeaderObjectId;

    // [CreateProperty]
    // public Leader selectedLeader
    // {
    //     get
    //     {
    //         return EntityManager.Instance.Get<Leader>(selectedLeaderObjectId);
    //     }
    // }

    // public string selectedNamedShipObjectId;

    // [CreateProperty]
    // public NamedShip selectedNamedShip
    // {
    //     get
    //     {
    //         return EntityManager.Instance.Get<NamedShip>(selectedNamedShipObjectId);
    //     }
    // }

    // public LanguageType iconLanuageType;
    // public int selected

    public string selectedMountStatusRecordObjectId;
    public MountStatusRecord selectedMountStatusRecord => EntityManager.Instance.Get<MountStatusRecord>(selectedMountStatusRecordObjectId);
    public string selectedFireControlSystemStatusRecordObjectId;
    public FireControlSystemStatusRecord selectedFireControlSystemStatusRecord => EntityManager.Instance.Get<FireControlSystemStatusRecord>(selectedFireControlSystemStatusRecordObjectId);

    public RapidFiringTargettingStatus selectedRapidFiringTargettingStatus; // TODO: Use object id to reference?
    public TorpedoMountStatusRecord selectedTorpedoMountStatusRecord;

    public GameObject dynamicLinePrefab;
    public Transform dynamicLineContainer;
    public void SyncDynamicLines()
    {
        var firingLinePairs = GetFiringLinePairs().ToList();
        if (dynamicLinePool.Count < firingLinePairs.Count)
        {
            for (int i = dynamicLinePool.Count; i < firingLinePairs.Count; i++)
            {
                var dynamicLine = Instantiate(dynamicLinePrefab, dynamicLineContainer).GetComponent<DynamicLine>();
                dynamicLinePool.Add(dynamicLine);
            }
        }
        else if (dynamicLinePool.Count > firingLinePairs.Count)
        {
            for (int i = firingLinePairs.Count; i < dynamicLinePool.Count; i++)
            {
                var dynamicLine = dynamicLinePool[i];
                dynamicLine.gameObject.SetActive(false);
            }
        }

        for (var i = 0; i < firingLinePairs.Count; i++)
        {
            (var firingShip, var target) = firingLinePairs[i];
            var dynamicLine = dynamicLinePool[i];
            dynamicLine.gameObject.SetActive(true);

            dynamicLine.SetBeginEndByLatLon(firingShip.position, target.position);
            // dynamicLine.SetColor(Color.black);
            dynamicLine.SetColor(Color.red);
        }
    }

    void SyncGunneryShellVisualsFromLogs()
    {
        if (isHostOrClient)
            return;

        var navalState = NavalGameState.Instance;
        if (navalState == null)
            return;

        var activeMountIds = new HashSet<string>();
        var activeRapidIds = new HashSet<string>();

        foreach (var shooter in navalState.shipLogsOnMap)
        {
            foreach (var batteryStatus in shooter.batteryStatus)
            {
                foreach (var mount in batteryStatus.mountStatus)
                {
                    var mountId = mount.objectId;
                    if (string.IsNullOrEmpty(mountId))
                        continue;

                    activeMountIds.Add(mountId);

                    var logCount = mount.logs.Count;
                    var beginIdx = processedMountFiringLogCount.GetValueOrDefault(mountId, 0);
                    if (beginIdx > logCount)
                        beginIdx = logCount;

                    for (var i = beginIdx; i < logCount; i++)
                    {
                        var shellDiameterInch = mount.GetFullContext()?.batteryRecord?.shellSizeInch ?? 0f;
                        TrySpawnGunneryShellVisual(shooter, mount.logs[i].firingTargetObjectId, shellDiameterInch);
                    }

                    processedMountFiringLogCount[mountId] = logCount;
                }
            }

            foreach (var rapidFiringStatus in shooter.rapidFiringStatus)
            {
                var rapidId = rapidFiringStatus.objectId;
                if (string.IsNullOrEmpty(rapidId))
                    continue;

                activeRapidIds.Add(rapidId);

                var logCount = rapidFiringStatus.logs.Count;
                var beginIdx = processedRapidFiringLogCount.GetValueOrDefault(rapidId, 0);
                if (beginIdx > logCount)
                    beginIdx = logCount;

                for (var i = beginIdx; i < logCount; i++)
                {
                    // var shellDiameterInch = rapidFiringStatus.GetRapidFireBatteryRecord()?.shellSizeInch ?? 0f;
                    var shellDiameterInch = RapidFireBatteryRecord.shellSizeInch;
                    TrySpawnGunneryShellVisual(shooter, rapidFiringStatus.logs[i].firingTargetObjectId, shellDiameterInch);
                }

                processedRapidFiringLogCount[rapidId] = logCount;
            }
        }

        foreach (var staleId in processedMountFiringLogCount.Keys.Where(k => !activeMountIds.Contains(k)).ToList())
        {
            processedMountFiringLogCount.Remove(staleId);
        }

        foreach (var staleId in processedRapidFiringLogCount.Keys.Where(k => !activeRapidIds.Contains(k)).ToList())
        {
            processedRapidFiringLogCount.Remove(staleId);
        }
    }

    void HandleMultiplayerVisualModeTransition(bool isInMultiplayerNow)
    {
        ClearAllGunneryShellVisuals();
        if (!isInMultiplayerNow)
        {
            InitializeGunneryLogProcessingBaseline();
        }
    }

    void ClearAllGunneryShellVisuals()
    {
        if (gunneryShellVisualContainer != null)
        {
            for (var i = gunneryShellVisualContainer.childCount - 1; i >= 0; i--)
            {
                var child = gunneryShellVisualContainer.GetChild(i);
                if (child != null)
                    Destroy(child.gameObject);
            }
        }

        gunneryShellPool.Clear();
        activeGunneryShells.Clear();
        processedMountFiringLogCount.Clear();
        processedRapidFiringLogCount.Clear();
    }

    void InitializeGunneryLogProcessingBaseline()
    {
        processedMountFiringLogCount.Clear();
        processedRapidFiringLogCount.Clear();

        var navalState = NavalGameState.Instance;
        if (navalState == null)
            return;

        foreach (var shooter in navalState.shipLogsOnMap)
        {
            foreach (var batteryStatus in shooter.batteryStatus)
            {
                foreach (var mount in batteryStatus.mountStatus)
                {
                    var mountId = mount.objectId;
                    if (string.IsNullOrEmpty(mountId))
                        continue;

                    processedMountFiringLogCount[mountId] = mount.logs.Count;
                }
            }

            foreach (var rapidFiringStatus in shooter.rapidFiringStatus)
            {
                var rapidId = rapidFiringStatus.objectId;
                if (string.IsNullOrEmpty(rapidId))
                    continue;

                processedRapidFiringLogCount[rapidId] = rapidFiringStatus.logs.Count;
            }
        }
    }

    void TrySpawnGunneryShellVisual(ShipLog shooter, string targetObjectId, float shellDiameterInch)
    {
        if (!enableGunneryShellVisual || shooter == null || string.IsNullOrEmpty(targetObjectId))
            return;

        var target = EntityManager.Instance.Get<ShipLog>(targetObjectId);
        if (target == null)
            return;

        var shellRadiusFoot = Mathf.Max(0.05f, shellDiameterInch * (1f / 24f));
        var shellRadiusWu = shellRadiusFoot * gunneryShellRadiusScaleCoef * Utils.footToWu;

        var startPos = Utils.LatitudeLongitudeDegHeightFootToVector3(
            shooter.position.LatDeg,
            shooter.position.LonDeg,
            gunneryShellAltitudeFoot
        );
        var endPos = Utils.LatitudeLongitudeDegHeightFootToVector3(
            target.position.LatDeg,
            target.position.LonDeg,
            gunneryShellAltitudeFoot
        );

        var controller = AcquireGunneryShell(shellRadiusWu);
        var shell = controller.gameObject;
        shell.transform.SetParent(gunneryShellVisualContainer, true);
        shell.transform.position = startPos;

        controller.Setup(
            startPos,
            endPos,
            gunneryShellSpeedMps * MeasureUtils.meterToFoot * Utils.footToWu,
            targetObjectId,
            gunneryShellAltitudeFoot,
            ReleaseGunneryShell
        );

        if (!activeGunneryShells.Contains(controller))
            activeGunneryShells.Add(controller);
    }

    BallController AcquireGunneryShell(float shellRadiusWu)
    {
        BallController controller = null;
        while (gunneryShellPool.Count > 0 && controller == null)
        {
            controller = gunneryShellPool.Dequeue();
        }

        if (controller == null)
        {
            var shell = CreateGunneryShellVisualObject(shellRadiusWu);
            controller = shell.AddComponent<BallController>();
        }
        else
        {
            var shell = controller.gameObject;
            shell.transform.localScale = Vector3.one;
            shell.SetActive(true);
            ResizeGunneryShellVisual(shell, shellRadiusWu);
        }

        return controller;
    }

    void ReleaseGunneryShell(BallController controller)
    {
        if (controller == null)
            return;

        activeGunneryShells.Remove(controller);
        controller.gameObject.SetActive(false);
        gunneryShellPool.Enqueue(controller);
    }

    void ForceAdvanceGunneryShellVisualsThisFrame()
    {
        if (projectileVisualAdvanceSecondsThisFrame <= 0f || activeGunneryShells.Count == 0)
            return;

        for (var i = activeGunneryShells.Count - 1; i >= 0; i--)
        {
            var controller = activeGunneryShells[i];
            if (controller == null)
            {
                activeGunneryShells.RemoveAt(i);
                continue;
            }

            var advanceSeconds = controller.IsSpawnedThisFrame
                ? projectileVisualAdvanceSecondsThisFrame * 0.1f
                : projectileVisualAdvanceSecondsThisFrame;
            controller.AdvanceBySimulationSeconds(advanceSeconds);
        }
    }

    static void ResizeGunneryShellVisual(GameObject root, float shellRadiusWu)
    {
        var body = root.transform.Find("Body");
        var head = root.transform.Find("Head");
        if (body == null || head == null)
            return;

        var bodyLengthWu = shellRadiusWu * 6f;
        var headLengthWu = shellRadiusWu * 2f;

        body.localRotation = Quaternion.Euler(90f, 0f, 0f);
        body.localScale = new Vector3(shellRadiusWu * 2f, bodyLengthWu * 0.5f, shellRadiusWu * 2f);
        body.localPosition = new Vector3(0f, 0f, bodyLengthWu * 0.5f);

        head.localRotation = Quaternion.Euler(90f, 0f, 0f);
        head.localScale = new Vector3(shellRadiusWu * 2f, headLengthWu, shellRadiusWu * 2f);
        head.localPosition = new Vector3(0f, 0f, bodyLengthWu + headLengthWu * 0.5f);
    }

    GameObject CreateGunneryShellVisualObject(float shellRadiusWu)
    {
        var root = new GameObject("GunneryShellVisual");
        var bodyLengthWu = shellRadiusWu * 6f;
        var headLengthWu = shellRadiusWu * 2f;

        var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        body.name = "Body";
        body.transform.SetParent(root.transform, false);
        body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        body.transform.localScale = new Vector3(shellRadiusWu * 2f, bodyLengthWu * 0.5f, shellRadiusWu * 2f);
        body.transform.localPosition = new Vector3(0f, 0f, bodyLengthWu * 0.5f);

        var bodyCollider = body.GetComponent<Collider>();
        if (bodyCollider != null)
            Destroy(bodyCollider);

        var bodyRenderer = body.GetComponent<Renderer>();

        var head = new GameObject("Head");
        head.transform.SetParent(root.transform, false);
        head.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        head.transform.localScale = new Vector3(shellRadiusWu * 2f, headLengthWu, shellRadiusWu * 2f);
        head.transform.localPosition = new Vector3(0f, 0f, bodyLengthWu + headLengthWu * 0.5f);

        var headFilter = head.AddComponent<MeshFilter>();
        headFilter.sharedMesh = GetOrCreateGunneryConeMesh();

        var headRenderer = head.AddComponent<MeshRenderer>();
        headRenderer.sharedMaterial = bodyRenderer != null ? bodyRenderer.sharedMaterial : null;

        return root;
    }

    static Mesh GetOrCreateGunneryConeMesh()
    {
        if (gunneryShellConeMesh != null)
            return gunneryShellConeMesh;

        const int segments = 16;
        var vertices = new List<Vector3>();
        var triangles = new List<int>();

        var apexIndex = vertices.Count;
        vertices.Add(new Vector3(0f, 0.5f, 0f));

        var baseCenterIndex = vertices.Count;
        vertices.Add(new Vector3(0f, -0.5f, 0f));

        var ringStart = vertices.Count;
        for (var i = 0; i < segments; i++)
        {
            var t = (float)i / segments;
            var angle = t * Mathf.PI * 2f;
            vertices.Add(new Vector3(Mathf.Cos(angle) * 0.5f, -0.5f, Mathf.Sin(angle) * 0.5f));
        }

        for (var i = 0; i < segments; i++)
        {
            var curr = ringStart + i;
            var next = ringStart + (i + 1) % segments;

            triangles.Add(apexIndex);
            triangles.Add(next);
            triangles.Add(curr);

            triangles.Add(baseCenterIndex);
            triangles.Add(curr);
            triangles.Add(next);
        }

        gunneryShellConeMesh = new Mesh
        {
            name = "GunneryShellConeMesh"
        };
        gunneryShellConeMesh.SetVertices(vertices);
        gunneryShellConeMesh.SetTriangles(triangles, 0);
        gunneryShellConeMesh.RecalculateNormals();
        gunneryShellConeMesh.RecalculateBounds();
        return gunneryShellConeMesh;
    }

    void SyncRangeLine()
    {
        var shipLog = selectedShipLog;
        if (shipLog != null && shipLog.mapState == MapState.Destroyed)
            shipLog = null;

        var currentSignature = new RangeLineRenderSignature
        {
            shipObjectId = shipLog?.objectId,
            latDeg = shipLog?.position?.LatDeg ?? 0f,
            lonDeg = shipLog?.position?.LonDeg ?? 0f,
            headingDeg = shipLog?.headingDeg ?? 0f,
            rangeMode = GamePreference.Instance.rangeRingDisplayMode
        };
        if (IsSameRangeLineRenderSignature(lastRangeLineRenderSignature, currentSignature))
            return;
        lastRangeLineRenderSignature = currentSignature;

        var shipClass = shipLog?.shipClass;

        var hasPrimaryBattery = shipClass != null && shipClass.batteryRecords.Count >= 1;
        if (hasPrimaryBattery)
        {
            var primaryBatteryRecord = shipClass.batteryRecords[0];
            var rangeM = MeasureUtils.yardToMeter * primaryBatteryRecord.rangeYards;
            var arcSegmentsRaw = GetRawArcSegmentsForBattery(primaryBatteryRecord, shipLog.headingDeg);
            SyncRangeLineByMode(primaryBatteryRangeLine, shipLog.position.LatDeg, shipLog.position.LonDeg, rangeM, arcSegmentsRaw);
        }
        else
        {
            SetRangeLineInactive(primaryBatteryRangeLine);
        }

        var hasSecondBattery = shipClass != null && shipClass.batteryRecords.Count >= 2;
        if (hasSecondBattery)
        {
            var secondaryBatteryRecord = shipClass.batteryRecords[1];
            var rangeM = secondaryBatteryRecord.rangeYards * MeasureUtils.yardToMeter;
            var arcSegmentsRaw = GetRawArcSegmentsForBattery(secondaryBatteryRecord, shipLog.headingDeg);
            SyncRangeLineByMode(secondaryBatteryRangeLine, shipLog.position.LatDeg, shipLog.position.LonDeg, rangeM, arcSegmentsRaw);
        }
        else
        {
            SetRangeLineInactive(secondaryBatteryRangeLine);
        }

        var hasTertiaryBattery = shipClass != null && shipClass.batteryRecords.Count >= 3;
        if (hasTertiaryBattery)
        {
            var tertiaryBatteryRecord = shipClass.batteryRecords[2];
            var rangeM = tertiaryBatteryRecord.rangeYards * MeasureUtils.yardToMeter;
            var arcSegmentsRaw = GetRawArcSegmentsForBattery(tertiaryBatteryRecord, shipLog.headingDeg);
            SyncRangeLineByMode(tertiaryBatteryRangeLine, shipLog.position.LatDeg, shipLog.position.LonDeg, rangeM, arcSegmentsRaw);
        }
        else
        {
            SetRangeLineInactive(tertiaryBatteryRangeLine);
        }

        var hasOneRapidFiringBattery = shipClass != null && shipClass.rapidFireBatteryRecords.Count >= 1;
        if (hasOneRapidFiringBattery)
        {
            var rapidFireBatteryRecord = shipClass.rapidFireBatteryRecords[0];
            var rangeM = rapidFireBatteryRecord.maxRangeYards * MeasureUtils.yardToMeter;
            var arcSegmentsRaw = GetRawArcSegmentsForRapidFire(rapidFireBatteryRecord, shipLog.headingDeg);
            SyncRangeLineByMode(rapidFireBatteryRangeLine, shipLog.position.LatDeg, shipLog.position.LonDeg, rangeM, arcSegmentsRaw);
        }
        else
        {
            SetRangeLineInactive(rapidFireBatteryRangeLine);
        }

        var hasTorpedo = shipClass != null && shipClass.torpedoSector.torpedoSettings.Count >= 1;
        if (hasTorpedo)
        {
            var rangeM = shipClass.torpedoSector.torpedoSettings[0].rangeYards * MeasureUtils.yardToMeter;
            var arcSegmentsRaw = GetRawArcSegmentsForTorpedo(shipClass.torpedoSector, shipLog.headingDeg);
            SyncRangeLineByMode(torpedoRangeLine, shipLog.position.LatDeg, shipLog.position.LonDeg, rangeM, arcSegmentsRaw);
        }
        else
        {
            SetRangeLineInactive(torpedoRangeLine);
        }

        var hasVisibilityCap = shipClass != null;
        visibilityRangeLine.gameObject.SetActive(hasVisibilityCap);
        if (hasVisibilityCap)
        {
            // Utils.DrawCircleForLineRenderer(visibilityRangeLine, shipLog.position.LatDeg, shipLog.position.LonDeg,
            //     32900 * MeasureUtils.yardToMeter); // 32900 yards: D1 Surface Visibility, 4 and up -> 4 in Exceptionally Clear (smoke is not considered)
            // TODO: Handle observer's target size, and visibility condition.
            // Night change can be used to detect day/night error.

            var scenarioState = NavalGameState.Instance.scenarioState;
            var dayNightLevel = scenarioState.GetSunPosition(shipLog.position).GetDayNightLevel();
            var refObsTargetSize = 1;
            // var refObsTargetSize = shipClass.targetSizeModifier;
            var visibilityRangeYards = RuleChart.GetVisibilityRangeYards(
                shipClass.targetSizeModifier, refObsTargetSize, scenarioState.visibility, dayNightLevel,
                noMoonlight: !scenarioState.hasMoonlight, searchVesselSpeedKnots: shipLog.speedKnots, searchVesselIsNotAWarship: !shipClass.IsCombatShip()
            );

            Utils.DrawCircleForLineRenderer(
                visibilityRangeLine, shipLog.position.LatDeg, shipLog.position.LonDeg,
                visibilityRangeYards * MeasureUtils.yardToMeter
            );
        }
    }

    static bool IsSameRangeLineRenderSignature(RangeLineRenderSignature a, RangeLineRenderSignature b)
    {
        if (a.shipObjectId != b.shipObjectId)
            return false;
        if (a.rangeMode != b.rangeMode)
            return false;
        if (Mathf.Abs(a.latDeg - b.latDeg) > 0.0001f)
            return false;
        if (Mathf.Abs(a.lonDeg - b.lonDeg) > 0.0001f)
            return false;
        if (Mathf.Abs(a.headingDeg - b.headingDeg) > 0.001f)
            return false;
        return true;
    }

    void SyncRangeLineByMode(LineRenderer baseLineRenderer, float latDeg, float lonDeg, float rangeM, List<Utils.ArcSegmentDeg> rawArcSegments)
    {
        var mode = GamePreference.Instance.rangeRingDisplayMode;
        if (mode == GamePreference.RangeRingDisplayMode.Circle)
        {
            baseLineRenderer.gameObject.SetActive(true);
            Utils.DrawCircleForLineRenderer(baseLineRenderer, latDeg, lonDeg, rangeM);
            HideVariantRangeLines(baseLineRenderer);
            return;
        }

        var arcSegments = mode switch
        {
            GamePreference.RangeRingDisplayMode.MergedArcs => Utils.MergeArcSegments(rawArcSegments),
            GamePreference.RangeRingDisplayMode.DistinctArcs => Utils.DistinctArcSegments(rawArcSegments),
            _ => Utils.MergeArcSegments(rawArcSegments)
        };
        SyncArcRangeLines(baseLineRenderer, latDeg, lonDeg, rangeM, arcSegments);
    }

    void SetRangeLineInactive(LineRenderer baseLineRenderer)
    {
        baseLineRenderer.gameObject.SetActive(false);
        HideVariantRangeLines(baseLineRenderer);
    }

    void HideVariantRangeLines(LineRenderer baseLineRenderer)
    {
        if (!rangeArcLinePool.TryGetValue(baseLineRenderer, out var variantLineRenderers))
            return;
        foreach (var lineRenderer in variantLineRenderers)
        {
            lineRenderer.gameObject.SetActive(false);
        }
    }

    List<Utils.ArcSegmentDeg> GetRawArcSegmentsForBattery(BatteryRecord batteryRecord, float shipHeadingDeg)
    {
        var arcSegments = batteryRecord.mountLocationRecords
            .SelectMany(mountLocationRecord => mountLocationRecord.mountArcs)
            .Select(arc => new Utils.ArcSegmentDeg
            {
                startDeg = MeasureUtils.NormalizeAngle(shipHeadingDeg + arc.startDeg),
                sweepDeg = arc.CoverageDeg
            })
            .ToList();
        if (arcSegments.Count == 0)
        {
            arcSegments.Add(new Utils.ArcSegmentDeg { startDeg = 0, sweepDeg = 360 });
        }
        return arcSegments;
    }

    List<Utils.ArcSegmentDeg> GetRawArcSegmentsForTorpedo(TorpedoSector torpedoSector, float shipHeadingDeg)
    {
        var arcSegments = torpedoSector.mountLocationRecords
            .SelectMany(mountLocationRecord => mountLocationRecord.mountArcs)
            .Select(arc => new Utils.ArcSegmentDeg
            {
                startDeg = MeasureUtils.NormalizeAngle(shipHeadingDeg + arc.startDeg),
                sweepDeg = arc.CoverageDeg
            })
            .ToList();
        if (arcSegments.Count == 0)
        {
            arcSegments.Add(new Utils.ArcSegmentDeg { startDeg = 0, sweepDeg = 360 });
        }
        return arcSegments;
    }

    List<Utils.ArcSegmentDeg> GetRawArcSegmentsForRapidFire(RapidFireBatteryRecord rapidFireBatteryRecord, float shipHeadingDeg)
    {
        var arcSegments = new List<Utils.ArcSegmentDeg>();
        if (rapidFireBatteryRecord.barrelsLevelStarboard.FirstOrDefault() > 0)
        {
            arcSegments.Add(new Utils.ArcSegmentDeg
            {
                startDeg = MeasureUtils.NormalizeAngle(shipHeadingDeg + 0),
                sweepDeg = 180
            });
        }
        if (rapidFireBatteryRecord.barrelsLevelPort.FirstOrDefault() > 0)
        {
            arcSegments.Add(new Utils.ArcSegmentDeg
            {
                startDeg = MeasureUtils.NormalizeAngle(shipHeadingDeg + 180),
                sweepDeg = 180
            });
        }
        if (arcSegments.Count == 0)
        {
            arcSegments.Add(new Utils.ArcSegmentDeg { startDeg = 0, sweepDeg = 360 });
        }
        return arcSegments;
    }

    void SyncArcRangeLines(LineRenderer baseLineRenderer, float latDeg, float lonDeg, float rangeM, List<Utils.ArcSegmentDeg> arcSegments)
    {
        if (!rangeArcLinePool.TryGetValue(baseLineRenderer, out var variantLineRenderers))
        {
            variantLineRenderers = new List<LineRenderer>();
            rangeArcLinePool[baseLineRenderer] = variantLineRenderers;
        }

        if (arcSegments == null || arcSegments.Count == 0)
        {
            baseLineRenderer.gameObject.SetActive(false);
            foreach (var lineRenderer in variantLineRenderers)
            {
                lineRenderer.gameObject.SetActive(false);
            }
            return;
        }

        baseLineRenderer.gameObject.SetActive(true);
        Utils.DrawArcForLineRenderer(baseLineRenderer, latDeg, lonDeg, rangeM, arcSegments[0].startDeg, arcSegments[0].sweepDeg);

        for (int i = 1; i < arcSegments.Count; i++)
        {
            var variantIdx = i - 1;
            if (variantIdx >= variantLineRenderers.Count)
            {
                var variantObj = Instantiate(baseLineRenderer.gameObject, baseLineRenderer.transform.parent);
                variantObj.name = $"{baseLineRenderer.gameObject.name}_Arc_{variantIdx + 1}";
                var variantLineRenderer = variantObj.GetComponent<LineRenderer>();
                variantLineRenderers.Add(variantLineRenderer);
            }

            var lineRenderer = variantLineRenderers[variantIdx];
            lineRenderer.gameObject.SetActive(true);
            Utils.DrawArcForLineRenderer(lineRenderer, latDeg, lonDeg, rangeM, arcSegments[i].startDeg, arcSegments[i].sweepDeg);
        }

        for (int i = arcSegments.Count - 1; i < variantLineRenderers.Count; i++)
        {
            variantLineRenderers[i].gameObject.SetActive(false);
        }
    }

    public void PlotShipLogTrajectory(ShipLog shipLog, Color color, bool plotTimestamp, int timestampIntervalMinutes)
    {
        if (shipLog == null)
            return;

        var obj = Instantiate(shipLogTrajectoryPrefab, shipLogTrajectoriesTransform);
        var lineRenderer = obj.GetComponent<LineRenderer>();
        lineRenderer.startColor = color;
        lineRenderer.endColor = color;
        lineRenderer.positionCount = shipLog.timeLocLogs.Count;
        lineRenderer.SetPositions(shipLog.timeLocLogs.Select(r => Utils.LatitudeLongitudeDegHeightFootToVector3(r.latDeg, r.lonDeg, 30)).ToArray());
        // shipLog.timeLocLogs

        if(plotTimestamp)
        {
            foreach(var log in shipLog.timeLocLogs)
            {
                if(log.time.Minute % timestampIntervalMinutes == 0) // So only 60,30,20,15,12,10,6,5,4,3,2,1 generate uniform interval
                {
                    var label = Instantiate(shipLogTrajectoryLabelPrefab, shipLogTrajectoryLabelsTransform);
                    // var pos = Utils.LatitudeLongitudeDegHeightFootToVector3(log.latDeg, log.lonDeg, 100);
                    var pos = Utils.LatitudeLongitudeDegHeightFootToVector3(log.latDeg, log.lonDeg, 10000);
                    label.transform.position = pos;

                    var dateTimeOffset = GetDateTimeOffsetByLatestHoveringLatLon(log.time);
                    label.GetComponent<TMP_Text>().text = dateTimeOffset.ToString("HH:mm");
                }
            }
        }
    }

    public void ClearShipLogTrajectories()
    {
        Utils.DestroyChildrensFor(shipLogTrajectoriesTransform);
        // var parent = shipLogTrajectoriesTransform;
        // for (int i = parent.childCount - 1; i >= 0; i--)
        // {
        //     Destroy(parent.GetChild(i).gameObject);
        // }

        Utils.DestroyChildrensFor(shipLogTrajectoryLabelsTransform);
        // parent = shipLogTrajectoryLabelsTransform;
        // for (int i = parent.childCount - 1; i >= 0; i--)
        // {
        //     Destroy(parent.GetChild(i).gameObject);
        // }
    }

    public IEnumerable<ShipLog> GetShipsRequiringFiringLineRendering()
    {
        if (selectedShipLog == null || selectedShipLog.mapState != MapState.Deployed)
            yield break;

        switch (GamePreference.Instance.firingLineDisplayMode)
        {
            case GamePreference.FiringLineDisplayMode.None:
                break;

            case GamePreference.FiringLineDisplayMode.SelectedShip:
                if (selectedShipLog != null)
                    yield return selectedShipLog;
                break;

            case GamePreference.FiringLineDisplayMode.SelectedGroup:
                if (selectedShipLog == null)
                    break;

                foreach (var shipLog in NavalGameState.Instance.GetSameLevel1GroupShipLogs(selectedShipLog))
                {
                    yield return shipLog;
                }
                break;

            case GamePreference.FiringLineDisplayMode.SelectedControlRoot:
                if (selectedShipLog == null)
                    break;

                var selectedControlRoot = selectedShipLog.GetControlRoot();
                foreach (var shipLog in NavalGameState.Instance.shipLogsOnMap)
                {
                    if (shipLog.GetControlRoot() == selectedControlRoot)
                        yield return shipLog;
                }
                break;

            case GamePreference.FiringLineDisplayMode.SelectedRootGroup:
                if (selectedShipLog == null)
                    break;

                foreach (var shipLog in NavalGameState.Instance.GetSameRootGroupShipLogs(selectedShipLog))
                {
                    yield return shipLog;
                }

                break;

            case GamePreference.FiringLineDisplayMode.All:
                foreach (var shipLog in NavalGameState.Instance.shipLogs)
                {
                    if (shipLog.mapState == MapState.Deployed)
                        yield return shipLog;
                }
                break;
        }
    }

    public IEnumerable<(ShipLog, ShipLog)> GetFiringLinePairs()
    {
        foreach (var shipLog in GetShipsRequiringFiringLineRendering())
        {
            foreach (var firingTarget in shipLog.GetFiringToTargets())
            {
                yield return (shipLog, firingTarget);
            }
        }
    }

    public bool detachStreamingAssets = true;

    public string selectedLaunchedTorpedoObjectId;

    [CreateProperty]
    public LaunchedTorpedo selectedLaunchedTorpedo
    {
        get => EntityManager.Instance.Get<LaunchedTorpedo>(selectedLaunchedTorpedoObjectId);
    }


    // public void ScheduleToSetSelectionForListView(ListView listView, int idx)
    // {
    //     StartCoroutine(Utils.SetSelectionForListView(listView, idx));
    // }

    [CreateProperty]
    public bool isInEditMode
    {
        get => GamePreference.Instance.isInEditMode;
        set => GamePreference.Instance.isInEditMode = value;
    }

    [CreateProperty]
    public bool isInUnityEditor => Application.isEditor;


    [CreateProperty]
    public bool isFromStrategic => startupConfig.IsFromStrategic();

    public NetworkingManager networkingManager;

    [CreateProperty]
    public bool isNetworkingHost => networkingManager is NetworkingHostManager;

    [CreateProperty]
    public bool isNetworkingClient => networkingManager is NetworkingClientManager;

    [CreateProperty]
    public bool isHostOrClient => networkingManager != null;

    [CreateProperty]
    public bool isNotHostAndClient => networkingManager == null;

    public static string defaultNetworkingName = "Name";
    string _networkingName = defaultNetworkingName;

    [CreateProperty]
    public string networkingName
    {
        get => _networkingName;
        set
        {
            if(value != _networkingName)
            {
                _networkingName = value;
                if(networkingManager is NetworkingHostManager hostManager)
                {
                    hostManager.myName = value;
                    RefreshConnectionViewStatesAsHost(); // Refresh and send command to all the clients.
                }
                if(networkingManager is NetworkingClientManager clientManager)
                {
                    clientManager.myName = value;
                    clientManager.SendCommandToHost(new NavalNetworkingCommands.ConnectionViewStatesSyncRequest());
                    // The name would be sync into connection and refresh into connection view states, and dispatch to all client
                }
            }
        }
    }

    public int networkingPort = 18947;
    public string connectToIp = "127.0.0.1";
    public string hostIp = "0.0.0.0";

    // [CreateProperty]
    // public string networkingDescription
    // {
    //     get
    //     {
    //         if(networkingManager == null)
    //             return "No Networking";
    //         if(networkingManager is NetworkingHostManager hostManager)
    //         {
    //             var namesStr = string.Join(", ", hostManager.connections.Select(c => $"{c.name} ({c.client?.Client.RemoteEndPoint})"));
    //             return $"Connected by {hostManager.connections.Count} clients: {namesStr}";
    //         }
    //         if(networkingManager is NetworkingClientManager clientManager)
    //         {
    //             var connections = clientManager.connections;
    //             if(connections.Count != 1)
    //             {
    //                 return $"Connected to {clientManager.connections.Count} Hosts???";
    //             }
    //             var connection = connections[0];
    //             return $"Connected to {connection.name} ({connection.client?.Client.RemoteEndPoint})";
    //         }
    //         return "Invalid";
    //     }
    // }

    public HashSet<string> takeCommandIdSet = new(); // Client
    public Dictionary<NetworkingManager.Connection, ConnectionInfo> connectionInfoMap = new(); // Host, currently this dictionary may list some outdated object so it should not be iterated directly, instead "join" it withb NetworkingManager.connections
    public ConnectionInfo GetConnectionInfo(NetworkingManager.Connection connection)
    {
        if(!connectionInfoMap.TryGetValue(connection, out var info))
            info = connectionInfoMap[connection] = new();
        return info;
    }
    bool _readyToAdvanceAsHost; // Host
    public bool readyToAdvanceAsHost
    {
        get => _readyToAdvanceAsHost;
        set
        {
            if(value != _readyToAdvanceAsHost)
            {
                _readyToAdvanceAsHost = value;
                RefreshConnectionViewStatesAsHost();
            }
        }
    }
    public float remainAdvanceSimulationSecondsRequestedByUserInputPending;

    public NavalNetworkingCommands.MergeRequest CreateMergeRequestByExtraction()
    {
        var syncShipLogSet = new HashSet<ShipLog>();
        var syncShipGroupSet = new HashSet<ShipGroup>();

        var otherTakeCommandIdSet = new HashSet<string>();
        foreach(var connViewState in connectionViewStates)
        {
            foreach(var takeCommandId in connViewState.takeCommandIds)
                otherTakeCommandIdSet.Add(takeCommandId);
        }
        otherTakeCommandIdSet.ExceptWith(takeCommandIdSet);

        foreach(var takeCommandId in takeCommandIdSet)
        {
            var obj = EntityManager.Instance.Get<IObjectIdLabeled>(takeCommandId);
            if(obj is ShipLog shipLog)
            {
                syncShipLogSet.Add(shipLog);
            }
            else if(obj is ShipGroup shipGroup)
            {
                syncShipGroupSet.Add(shipGroup);
                // Recursive 
                // Exclude elements which is explicitly assigned to other client
                // foreach(var _shipLog in shipGroup.Walk<ShipLog>())
                foreach(var _shipLog in shipGroup.Walk<ShipLog>(e => !otherTakeCommandIdSet.Contains(e.objectId)))
                    syncShipLogSet.Add(_shipLog);
                
                // foreach(var _shipGroup in shipGroup.Walk<ShipGroup>())
                foreach(var _shipGroup in shipGroup.Walk<ShipGroup>(e => !otherTakeCommandIdSet.Contains(e.objectId)))
                    syncShipGroupSet.Add(_shipGroup);
            }
        }

        var command = new NavalNetworkingCommands.MergeRequest()
        {
            syncShipGroups = syncShipGroupSet.ToList(),
            syncShipLogs = syncShipLogSet.ToList(),
        };
        return command;
    }

    public NetworkingManager.Connection GetHostConnectionOrNull()
    {
        if(networkingManager is NetworkingClientManager clientManager && clientManager != null && clientManager.connections.Count > 0)
        {
            return clientManager.connections[0];
        }
        return null;
    }

    public void RefreshConnectionViewStatesAsHost() // by Host
    {
        if(networkingManager is NetworkingHostManager hostManager)
        {
            var ret = new List<ConnectionViewState>();
            var hostViewState = new ConnectionViewState()
            {
                name = hostManager.myName,
                passed = readyToAdvanceAsHost,
                takeCommandIds = new(), // Host is "Otherwise" in the take command meaning
                // takeCommandIds = takeCommandIdSet.ToList(),
            };
            ret.Add(hostViewState);
            ret.AddRange(hostManager.connections.Select(conn =>
            {
                var connInfo = GetConnectionInfo(conn);
                return new ConnectionViewState()
                {
                    name = conn.name,
                    passed = connInfo.takeCommandIds.Count == 0 || connInfo.mergeRequest != null,
                    takeCommandIds = connInfo.takeCommandIds,
                };
            }));

            // return ret;
            connectionViewStates = ret;

            // Send ConnectionViewStates sync command to all clients
            var command = new NavalNetworkingCommands.ConnectionViewStatesSync()
            {
                connectionViewStates = connectionViewStates,
            };
            hostManager.SendCommandToAll(command);

            return;
        }

        connectionViewStates = new();
    }

    public void OnDisable()
    {
        ResumeUnityClock();
        ClearAllGunneryShellVisuals();
    }

    public override void OnDestroy()
    {
        ClearAllGunneryShellVisuals();
        base.OnDestroy();
    }

    public List<ConnectionViewState> connectionViewStates = new();

    public enum HostSyncMode
    {
        AdvanceEnd,
        EveryUnityUpdate
    }

    // public HostSyncMode hostSyncMode;
    public HostSyncMode hostSyncMode = HostSyncMode.EveryUnityUpdate;

    public void DoStartHost()
    {
        var networkingHostManager = new NetworkingHostManager(){myName=networkingName};
        networkingHostManager.connectionsChanged += (sender, args) => RefreshConnectionViewStatesAsHost();
        
        networkingManager = networkingHostManager;
        networkingHostManager.StartHostServer(hostIp, networkingPort);

        RefreshConnectionViewStatesAsHost();
    }

    public void DoConnect()
    {
        var networkingClientManager = new NetworkingClientManager(){myName=networkingName};
        networkingClientManager.connectionsChanged += (sender, args) =>
        {
            if(networkingClientManager.connections.Count == 0)  // When disconnected from host (if it was connected)
            {
                networkingManager = null;
                connectionViewStates = new();
            }
        };
        
        networkingManager = networkingClientManager;
        var client = networkingClientManager.ConnectTo($"{connectToIp}:{networkingPort}");
    
        // TODO: Send to a full sync request command
        if(client != null)
        {
            networkingClientManager.SendCommand(client, new NavalNetworkingCommands.RequestFullStateSync());
        }
        else
        {
            DialogRoot.Instance.PopupMessageDialog("Can't connect to the host.");
        }
    }

    public void DoDisconnect()
    {
        if(networkingManager != null)
        {
            networkingManager.Close();
            networkingManager = null;
        }

        connectionViewStates = new();
    }

    public void DoSubmitTakeCommand()
    {
        var clientManager = networkingManager as NetworkingClientManager;
        if(clientManager != null && clientManager.connections.Count > 0)
        {
            var hostConnection = clientManager.connections.First();
            clientManager.SendCommand(hostConnection, new NavalNetworkingCommands.UpdateTakeCommand()
            {
                takeCommandIds=takeCommandIdSet.ToList()
            });
        }
    }
}

