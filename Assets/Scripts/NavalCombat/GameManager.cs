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

    public GameObject shipLogTrajectoryPrefab;
    public Transform shipLogTrajectoriesTransform;
    public Transform shipLogTrajectoryLabelsTransform;
    public GameObject shipLogTrajectoryLabelPrefab;

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
        SelectingCourseTarget
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
        public string builtinScenName = "SJS - Ting Yuen vs Three View.scen.xml";
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

    public void Start()
    {
        audioSource = GetComponent<AudioSource>();

        SwitchCenter.Instance.Reset();

        GamePreference.Instance.SetShortLabelLanguageTypeByLocale(LocalizationSettings.SelectedLocale);

        iconLayerMask = LayerMask.GetMask("Icon");
        // Debug.Log($"Persistent Path:{Application.persistentDataPath}");

        SuperGameState.Instance.currentGameMode = GameMode.Naval;

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
    }

    public void StartLoadScenarioCoroutine(string scenName)
    {
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
        // Loading
        yield return fullState.streamingAssetReference.TryToCompleteFromStreamingAssetReference(fullState.navalGameState);
        StreamingAssetReference.UpdateInstance(fullState.streamingAssetReference);

        EventState.UpdateTo(fullState.eventState);
        yield return EventState.Instance.SyncAndRegister();

        LoadViewState(fullState.viewState);
        NavalGameState.UpdateInstance(fullState.navalGameState);

        NavalGameState.Instance.ResetAndRegisterAll();

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

    public string hoveringLocationInfo;
    public bool currentLogOnly = true;

    public void ReturnToStrategicGame()
    {
        StrategicGameManager.startupConfig.syncShipLogs = NavalGameState.Instance.shipLogs;
        StrategicGameManager.startupConfig.victoryStatus = VictoryStatus.Generate(NavalGameState.Instance);

        SceneManager.LoadScene("Strategic Game");
    }

    LatLon latestHoveringLatLon = new();

    // float viewAccTime;
    void UpdateLocationInfoLabel()
    {
        var ray = CameraController2.Instance.cam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            var hitPoint = hit.point;

            var latLon = Utils.Vector3ToLatLon(hitPoint);

            latestHoveringLatLon = latLon;

            var scenarioState = NavalGameState.Instance.scenarioState;

            var timeZoneOffset = ScenarioState.GetTimeZoneOffset(latLon.LonDeg);
            var timeZoneOffsetF = timeZoneOffset.ToString("+#;-#;0");

            var localDT = scenarioState.GetLocalDateTime(latLon.LonDeg);
            var sunState = scenarioState.GetSunPosition(latLon);

            var latF = latLon.LatDeg.ToString("0.000");
            var lonF = latLon.LonDeg.ToString("0.000");
            var utcDT = scenarioState.dateTime;

            var sunAziF = sunState.azimuthDeg.ToString("0.0");
            var sunAltF = sunState.altitudeDeg.ToString("0.0");

            var dayNightLevel = sunState.GetDayNightLevel();

            // hoveringLocationInfo = $"Lat: {latF} Lon: {lonF} UTC: {utcDT} Local: {localDT} ({dayNightLevel},{timeZoneOffsetF}) Sun Alt: {sunAltF} Azi: {sunAziF}";
            hoveringLocationInfo = Localize(
                "Lat: {0} Lon: {1} UTC: {2} Local: {3} ({4},{5}) Sun Alt: {6} Sun Azi: {7}",
                latF, lonF, utcDT, localDT, LocalizeEnum(dayNightLevel), timeZoneOffsetF, sunAltF, sunAziF
            );
        }
    }

    public DateTimeOffset GetDateTimeOffsetByLatestHoveringLatLon(DateTime time)
    {
        return ScenarioState.GetDateTimeOffset(time, latestHoveringLatLon.LonDeg);
    }

    static string Localize(string key, params object[] args) => ServiceLocator.Get<ILocalizeService>().Get(key, args);
    protected static string LocalizeEnum<T>(T obj) => ServiceLocator.Get<ILocalizeService>().GetEnum(obj);

    public float remainAdvanceSimulationSecondsRequestedByUserInput; // Requested by KeyCode 1-9 (1-9 min) and BackQuote (`) (1s)
    public float remainAdvanceSimulationSecondsRequestedByUpdate;
    public bool isAutoPlaying = false;

    // public float simulationRateRaio = 30;
    // float simulationRateRaio = 120;
    // float pulseLengthSeconds = 1;

    public void UpdateSimulation()
    {
        var pulseLengthSeconds = GamePreference.Instance.pulseLengthSeconds;
        var simulationRateRatio = GamePreference.Instance.simulationRateRatio;
        var simulationRateRatioAuto = GamePreference.Instance.simulationRateRatioAuto;

        var realSeconds = Time.deltaTime;

        if(isAutoPlaying) // auto adavance mode
        {
            remainAdvanceSimulationSecondsRequestedByUpdate += realSeconds * simulationRateRatioAuto;
        }
        else // manual advance mode
        {
            if (remainAdvanceSimulationSecondsRequestedByUserInput >= pulseLengthSeconds)
            {
                remainAdvanceSimulationSecondsRequestedByUpdate += realSeconds * simulationRateRatio;
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
            while (remainAdvanceSimulationSecondsRequestedByUserInput >= pulseLengthSeconds && remainAdvanceSimulationSecondsRequestedByUpdate >= pulseLengthSeconds)
            {
                // advanceAny = true;

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
        }

        if(minuteAdvanced) // When control is return to player (active advancing is completed)
        {
            HandleAutoEnd();
        }
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
                shipLogs.Any(shipLog => shipLog.mapState == MapState.Deployed && shipLog.operationalState == ShipOperationalState.Operational && shipLog.GetMaxSpeedKnots() > 0)
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
        if (selectedShipLog != null)
        {
            var hitPoint = CameraController2.Instance.GetHitPoint();
            var dstPos = Utils.Vector3ToLatLon(hitPoint);

            var currentPos = selectedShipLog.position;
            var inverseLine = Geodesic.WGS84.InverseLine(
                currentPos.LatDeg, currentPos.LonDeg,
                dstPos.LatDeg, dstPos.LonDeg
            );

            selectedShipLog.desiredHeadingDeg = MeasureUtils.NormalizeAngle((float)inverseLine.Azimuth);
        }
    }

    void SetRemainAdvanceSimulationSecondsRequestedByUserInput(float value)
    {
        if (currentLogOnly)
            NavalGameState.Instance.tempSubjectLogs.Clear();

        remainAdvanceSimulationSecondsRequestedByUserInput = value;
    }

    public void Update()
    {
        UpdateSimulation();
        // viewAccTime += Time.deltaTime;

        // if (viewAccTime > 2)
        // {
        //     viewAccTime -= 2;
        //     Debug.Log("2s Tick");
        // }

        // sync Ship's Viewer and ShipLog mapping
        List<IPortraitViewerObservable> viewerObservables = new();
        viewerObservables.AddRange(NavalGameState.Instance.shipLogsOnMap);
        viewerObservables.AddRange(NavalGameState.Instance.launchedTorpedosOnMap);

        foreach (var observable in viewerObservables)
        {
            if (!objectId2Viewer.ContainsKey(observable.objectId))
            {
                var obj = Instantiate(shipUnitPrefab, earthTransform);

                var portraitView = obj.GetComponent<PortraitViewer>();
                portraitView.modelObjectId = observable.objectId;
                objectId2Viewer[observable.objectId] = portraitView;
            }
        }

        var objectIdSet = viewerObservables.Select(obs => obs.objectId).ToHashSet();

        var shouldRemoved = objectId2Viewer.Where(kv => !objectIdSet.Contains(kv.Key)).ToList();

        foreach ((var objectId, var viewer) in shouldRemoved)
        {
            Destroy(viewer.gameObject); // Or Set Inactive only?
            objectId2Viewer.Remove(objectId);
        }

        // sync Line renderer to show firing line, fire control line, fired line etc.

        SyncDynamicLines();

        SyncRangeLine();

        // location browser: current latitude, longitude, time zone, local time, sun altitude, day/night discrete value
        UpdateLocationInfoLabel();

        // Handle Events

        if (EventSystem.current.IsPointerOverGameObject())
        {
            // Works on UI as well, debugging purpose.
            if (Input.GetKey(KeyCode.LeftAlt) && Input.GetKey(KeyCode.LeftShift))
            {
                // Alt + Shift + 1/2/3/...
                foreach ((var keyCode, var advanceSimulationSeconds) in simulationSecondsAdvanceMap)
                {
                    if (Input.GetKeyDown(keyCode))
                    {
                        // remainAdvanceSimulationSecondsRequestedByUserInput = advanceSimulationSeconds;
                        SetRemainAdvanceSimulationSecondsRequestedByUserInput(advanceSimulationSeconds);
                    }
                }
            }
        }

        if (!EventSystem.current.IsPointerOverGameObject())
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                state = State.Idle;
                selectedShipLogObjectId = null;
                return;
            }

            var isPressingShift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            var isPressingAlt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

            if (state == State.Idle) // unit left click chosen
            {
                // handle events
                if (Input.GetKeyDown(KeyCode.Insert) && isPressingAlt) // Insert(Deploy) Unit
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

                if (Input.GetMouseButtonDown(1)) // try select unit and open ShipLog Editor for it
                {
                    var shipLog = TryToRaycastShipLog(); // TODO: Handle other click?
                    // if (shipLog != null)
                    if (shipLog != null && selectedShipLogObjectId == shipLog.objectId)
                    {
                        // ShipLogEditor.Instance.selectedShipLogObjectId = selectedShipLogObjectId;
                        // ShipLogEditor.Instance.Show();

                        SwitchCenter.Instance.SwitchToShipLogView(shipLog);
                    }
                }

                if (Input.GetMouseButtonDown(0) && isPressingShift) // RTW-like course setting
                {
                    SetSelectedShipCourseTowardPointer();
                }

                // simulationSecondsAdvanceMap
                // Debug.Log($"Input.inputString={Input.inputString}");
                // foreach(KeyCode keyCode in System.Enum.GetValues(typeof(KeyCode)))
                // {
                //     if (Input.GetKeyDown(keyCode))
                //     {
                //         Debug.Log("Pressed: " + keyCode);
                //     }
                // }

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
                    isAutoPlaying = !isAutoPlaying;
                    if(isAutoPlaying) // Clear logs if current only and clear potential "leaked" seconds requested by input.
                    {
                        SetRemainAdvanceSimulationSecondsRequestedByUserInput(0);
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
                    SwitchCenter.Instance.SwitchToShipLogView(selectedShipLog);
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
                    SwitchCenter.Instance.SwitchToShipLogView(selectedShipLog);
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
                    SwitchCenter.Instance.SwitchToShipLogView(selectedShipLog);
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
                    SwitchCenter.Instance.SwitchToShipLogView(selectedShipLog);
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
        // TODO: Maintain
        // dynamicLineContainer.GetChild
        var dynamicLines = dynamicLineContainer.GetComponentsInChildren<DynamicLine>().ToList();
        if (dynamicLines.Count < firingLinePairs.Count)
        {
            for (int i = dynamicLines.Count; i < firingLinePairs.Count; i++)
            {
                var dynamicLine = Instantiate(dynamicLinePrefab, dynamicLineContainer).GetComponent<DynamicLine>();
                // dynamicLine.gameObject.SetActive(true);
            }
            dynamicLines = dynamicLineContainer.GetComponentsInChildren<DynamicLine>().ToList();
        }
        else if (dynamicLines.Count > firingLinePairs.Count)
        {
            for (int i = firingLinePairs.Count; i < dynamicLines.Count; i++)
            {
                var dynamicLine = dynamicLines[i];
                dynamicLine.gameObject.SetActive(false);
            }
        }

        for (var i = 0; i < firingLinePairs.Count; i++)
        {
            (var firingShip, var target) = firingLinePairs[i];
            var dynamicLine = dynamicLines[i];
            dynamicLine.gameObject.SetActive(true);

            dynamicLine.SetBeginEndByLatLon(firingShip.position, target.position);
            // dynamicLine.SetColor(Color.black);
            dynamicLine.SetColor(Color.red);
        }
    }

    void SyncRangeLine()
    {
        var shipLog = selectedShipLog;
        if (shipLog != null && shipLog.mapState == MapState.Destroyed)
            shipLog = null;
        var shipClass = shipLog?.shipClass;

        var hasPrimaryBattery = shipClass != null && shipClass.batteryRecords.Count >= 1;
        primaryBatteryRangeLine.gameObject.SetActive(hasPrimaryBattery);
        if (hasPrimaryBattery)
        {
            var primaryBatteryRecord = shipClass.batteryRecords[0];
            var rangeM = MeasureUtils.yardToMeter * primaryBatteryRecord.rangeYards;
            Utils.DrawCircleForLineRenderer(primaryBatteryRangeLine, shipLog.position.LatDeg, shipLog.position.LonDeg, rangeM);
        }

        var hasSecondBattery = shipClass != null && shipClass.batteryRecords.Count >= 2;
        secondaryBatteryRangeLine.gameObject.SetActive(hasSecondBattery);
        if (hasSecondBattery)
        {
            Utils.DrawCircleForLineRenderer(secondaryBatteryRangeLine, shipLog.position.LatDeg, shipLog.position.LonDeg,
                shipClass.batteryRecords[1].rangeYards * MeasureUtils.yardToMeter);
        }

        var hasTertiaryBattery = shipClass != null && shipClass.batteryRecords.Count >= 3;
        tertiaryBatteryRangeLine.gameObject.SetActive(hasTertiaryBattery);
        if (hasTertiaryBattery)
        {
            Utils.DrawCircleForLineRenderer(tertiaryBatteryRangeLine, shipLog.position.LatDeg, shipLog.position.LonDeg,
                shipClass.batteryRecords[2].rangeYards * MeasureUtils.yardToMeter);
        }

        var hasOneRapidFiringBattery = shipClass != null && shipClass.rapidFireBatteryRecords.Count >= 1;
        rapidFireBatteryRangeLine.gameObject.SetActive(hasOneRapidFiringBattery);
        if (hasOneRapidFiringBattery)
        {
            Utils.DrawCircleForLineRenderer(rapidFireBatteryRangeLine, shipLog.position.LatDeg, shipLog.position.LonDeg,
                shipClass.rapidFireBatteryRecords[0].maxRangeYards * MeasureUtils.yardToMeter);
        }

        var hasTorpedo = shipClass != null && shipClass.torpedoSector.torpedoSettings.Count >= 1;
        torpedoRangeLine.gameObject.SetActive(hasTorpedo);
        if (hasTorpedo)
        {
            Utils.DrawCircleForLineRenderer(torpedoRangeLine, shipLog.position.LatDeg, shipLog.position.LonDeg,
                shipClass.torpedoSector.torpedoSettings[0].rangeYards * MeasureUtils.yardToMeter);
        }

        var hasVisibilityCap = shipClass != null;
        visibilityRangeLine.gameObject.SetActive(hasVisibilityCap);
        if (hasVisibilityCap)
        {
            Utils.DrawCircleForLineRenderer(visibilityRangeLine, shipLog.position.LatDeg, shipLog.position.LonDeg,
                32900 * MeasureUtils.yardToMeter); // 32900 yards: D1 Surface Visibility, 4 and up -> 4 in Exceptionally Clear (smoke is not considered)
            // TODO: Handle observer's target size, and visibility condition.
            // Night change can be used to detect day/night error.
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
                if(log.time.Minute % timestampIntervalMinutes == 0) // So only 60，30，20，15，12，10，6，5，4，3，2，1 generate uniform interval
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
}