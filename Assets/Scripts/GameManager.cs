using UnityEngine;
using NavalCombatCore;
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
using Unity.VisualScripting;
// using SunCalcNet;

public interface IColliderRootProvider
{
    GameObject GetRoot();
}

public class GameManager : MonoBehaviour
{
    public NavalGameState navalGameState = NavalGameState.Instance;
    public GameObject shipUnitPrefab;
    public Transform earthTransform;

    public LayerMask iconLayerMask;

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
        SelectingInsertUnitPosition,
        MovingUnit,
        SelectingFollowedTarget,
        SelectingRelativeToTarget,
        SelectingFiringTarget,
        SelectingFireControlSystemTarget
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

    static GameManager _instance;
    public static GameManager Instance
    {
        get
        {
            if (_instance == null)
                _instance = FindFirstObjectByType<GameManager>();
            return _instance;
        }
    }

    // public static string scenarioSuffex = "_Pungdo"; // temp hack
    public static string scenarioSuffex = "_Yalu";

    public void Start()
    {
        iconLayerMask = LayerMask.GetMask("Icon");
        // Debug.Log($"Persistent Path:{Application.persistentDataPath}");

        EntityManager.Instance.newGuidCreated += (obj, s) => Debug.LogWarning($"New guid created: {s} for {obj}");

        Func<string, string> _load = (string name) =>
        {
            var path = "Scenarios/First Sino-Japanese War/" + name;
            return Resources.Load<TextAsset>(path).text;
        };

        var leaderXml = _load("Leaders");
        navalGameState.LeadersFromXML(leaderXml);

        var shipClassXml = _load("ShipClasses");
        navalGameState.ShipClassesFromXML(shipClassXml);

        var namedShipsXml = _load("NamedShips");
        navalGameState.NamedShipsFromXML(namedShipsXml);

        var shipLogsXml = _load("ShipLogs" + scenarioSuffex);
        navalGameState.ShipLogsFromXML(shipLogsXml);

        var rootShipGroupsXml = _load("ShipGroups" + scenarioSuffex);
        navalGameState.ShipGroupsFromXML(rootShipGroupsXml);

        OOBEditor.Instance.oobTreeView.ExpandAll();

        // var fullStateText = Resources.Load<TextAsset>("Scenarios/Battle of Pungdo/FullState").text;
        // var fullState = FullState.FromXML(fullStateText);
        // TopTabs.Instance.LoadViewState(fullState.viewState);
        // NavalGameState.Instance.UpdateTo(fullState.navalGameState);


        NavalGameState.Instance.ResetAndRegisterAll(); // Note FromXml call has call it many times.

        // Test

        // var res = Resources.LoadAll<Sprite>("Flags");
        // var res2 = Resources.LoadAll<Texture>("Flags");
        // var res3 = Resources.LoadAll<Texture2D>("Flags");
        // var res4 = Resources.LoadAll<Sprite>("Leader_Portraits");
        // var res5 = Resources.LoadAll<Texture2D>("Leader_Portraits");
        // var res6 = Resources.LoadAll<Texture>("Leader_Portraits");

        // Debug.Log(res);

        // for (int year = 1890; year < 2026; year++)
        // {
        //     // var date = new DateTime(year, 3, 5, 0, 0, 0, DateTimeKind.Utc);
        //     var date = new DateTime(year, 9, 17, 4, 30, 0, DateTimeKind.Utc);
        //     var lat = 39;
        //     var lng = 123;

        //     var sunPosition = SunCalc.GetSunPosition(date, lat, lng);
        //     var sunPosition2 = SunCalcSharp.SunCalc.GetPosition(date, lat, lng);
        //     Debug.Log($"{year} => Azi={sunPosition.Azimuth}, Alt={sunPosition.Altitude}, Azi2={sunPosition2.Azimuth}, Alt2={sunPosition2.Altitude}");
        // }

        // // get today's sun event times for Milton Keynes
        // var times = SunCalcSharp.SunCalc.GetTimes(DateTime.UtcNow, 52.0406, -0.7594);
        // var sunrise = times.Sunrise;
        // var sunset = times.Sunset;

        // // get position of the sun (azimuth and altitude) at sunrise
        // var positionAtSunrise = SunCalcSharp.SunCalc.GetPosition(times.Sunrise.Value, 52.0406, -0.7594);
        // var solarAzimuthAtSunrise = positionAtSunrise.Azimuth;
        // var solarAltitudeAtSunrise = positionAtSunrise.Altitude;

        // // get solar azimuth in degrees
        // var azimuthInDegrees = positionAtSunrise.Azimuth * 180 / Math.PI;

        // Debug.Log($"times={times}, sunrise={sunrise}, sunset={sunset}, positionAtSunrise={positionAtSunrise}, solarAzimuthAtSunrise={solarAzimuthAtSunrise}, solarAltitudeAtSunrise={solarAltitudeAtSunrise}, azimuthInDegrees={azimuthInDegrees}");
    }

    public Dictionary<string, PortraitViewer> objectId2Viewer = new();

    // public LatLon hoveringLatLon = new();
    // public float hoveringTimeZoneOffset;
    // public DateTime hoveringLocalDateTime = new();
    // public SunState hoveringSunState = new();

    // [CreateProperty]
    // public DayNightLevel hoveringDayNightLevel
    // {
    //     get => hoveringSunState?.GetDayNightLevel() ?? DayNightLevel.Day;
    // }

    public string hoveringLocationInfo;

    // float viewAccTime;
    void UpdateLocationInfoLabel()
    {
        var ray = CameraController2.Instance.cam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            var hitPoint = hit.point;

            var latLon = Utils.Vector3ToLatLon(hitPoint);

            var scenarioState = NavalGameState.Instance.scenarioState;

            var timeZoneOffset = scenarioState.GetTimeZoneOffset(latLon.LonDeg);
            var timeZoneOffsetF = timeZoneOffset.ToString("+#;-#;0");

            var localDT = scenarioState.GetLocalDateTime(latLon.LonDeg);
            var sunState = scenarioState.GetSunPosition(latLon);

            var latF = latLon.LatDeg.ToString("0.000");
            var lonF = latLon.LonDeg.ToString("0.000");
            var utcDT = scenarioState.dateTime;

            var sunAziF = sunState.azimuthDeg.ToString("0.0");
            var sunAltF = sunState.altitudeDeg.ToString("0.0");

            var dayNightLevel = sunState.GetDayNightLevel();

            hoveringLocationInfo = $"Lat: {latF} Lon: {lonF} UTC: {utcDT} Local: {localDT} ({dayNightLevel},{timeZoneOffsetF}) Sun Alt: {sunAltF} Azi: {sunAziF}";
        }
    }

    public float remainAdvanceSimulationSeconds;
    // public float simulationRateRaio = 30;
    float simulationRateRaio = 120;
    public float pulseLengthSeconds = 1;

    public void UpdateSimulation()
    {
        var realSeconds = Time.deltaTime;
        var advanceSimulationSeconds = realSeconds * simulationRateRaio;
        while (remainAdvanceSimulationSeconds > 0 && advanceSimulationSeconds > 0)
        {
            var pulseSeconds = Math.Min(pulseLengthSeconds, Math.Min(remainAdvanceSimulationSeconds, advanceSimulationSeconds));
            NavalGameState.Instance.Step(pulseSeconds);
            remainAdvanceSimulationSeconds -= pulseSeconds;
            advanceSimulationSeconds -= pulseSeconds;
        }
    }

    static Dictionary<KeyCode, float> simulationSecondsAdvanceMap = new()
    {
        {KeyCode.Alpha1, 60 * 1},
        {KeyCode.Alpha2, 60 * 2},
        {KeyCode.Alpha3, 60 * 3},
        {KeyCode.Alpha4, 60 * 4},
        {KeyCode.Alpha5, 60 * 5},
        {KeyCode.Alpha6, 60 * 6},
        {KeyCode.Alpha7, 60 * 7},
        {KeyCode.Alpha8, 60 * 8},
        {KeyCode.Alpha9, 60 * 9},
    };

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
        foreach (var shipLog in NavalGameState.Instance.shipLogs)
        {
            if (shipLog.IsOnMap() && !objectId2Viewer.ContainsKey(shipLog.objectId))
            {
                var obj = Instantiate(shipUnitPrefab, earthTransform);

                var portraitView = obj.GetComponent<PortraitViewer>();
                portraitView.modelObjectId = shipLog.objectId;
                objectId2Viewer[shipLog.objectId] = portraitView;
            }
        }

        var shouldRemoved = objectId2Viewer.Where(kv => !EntityManager.Instance.Get<ShipLog>(kv.Key)?.IsOnMap() ?? false).ToList();

        foreach ((var shipLog, var viewer) in shouldRemoved)
        {
            Destroy(viewer); // Or Set Inactive only?
            objectId2Viewer.Remove(shipLog);
        }

        // sync Line renderer to show firing line, fire control line, fired line etc.

        SyncDynamicLines();

        // location browser: current latitude, longitude, time zone, local time, sun altitude, day/night discrete value
        UpdateLocationInfoLabel();

        // Handle Events

        if (!EventSystem.current.IsPointerOverGameObject())
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                state = State.Idle;
                selectedShipLogObjectId = null;
                return;
            }

            var isPressingShift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            if (state == State.Idle) // unit left click chosen
            {
                // handle events
                if (Input.GetKeyDown(KeyCode.Insert))
                {
                    state = State.SelectingInsertUnitPosition;
                }

                if (Input.GetKeyDown(KeyCode.M) && selectedShipLog != null)
                {
                    state = State.MovingUnit;
                }

                if (Input.GetMouseButtonDown(0) && !isPressingShift) // try select a unit
                {
                    var portraitViewer = TryToRaycastViewer();
                    if (portraitViewer != null)
                    {
                        var shipLog = portraitViewer.shipLog;
                        selectedShipLogObjectId = shipLog.objectId;
                    }
                }

                if (Input.GetMouseButtonDown(0) && isPressingShift) // RTW-like course setting
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

                // simulationSecondsAdvanceMap
                foreach ((var keyCode, var advanceSimulationSeconds) in simulationSecondsAdvanceMap)
                {
                    if (Input.GetKeyDown(keyCode))
                    {
                        remainAdvanceSimulationSeconds = advanceSimulationSeconds;
                    }
                }

                if (Input.GetKeyDown(KeyCode.F) && selectedShipLog != null) // Follow
                {
                    state = State.SelectingFollowedTarget;
                }

                if (Input.GetKeyDown(KeyCode.R) && selectedShipLog != null)
                {
                    state = State.SelectingRelativeToTarget;
                }

                if (Input.GetKeyDown(KeyCode.L) && selectedShipLog != null) // ship Log
                {
                    ShipLogEditor.Instance.PopupWithSelection(selectedShipLog);
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
                        var portraitViewer = TryToRaycastViewer();
                        if (portraitViewer != null)
                        {
                            var targetShipLog = portraitViewer.shipLog;
                            if (selectedShipLog != targetShipLog)
                            {
                                selectedShipLog.followedTargetObjectId = targetShipLog.objectId;
                                selectedShipLog.controlMode = ControlMode.FollowTarget;
                                Debug.Log($"Set Followed Object ID: {selectedShipLog.objectId} -> {targetShipLog.objectId}");
                            }
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
                        if (targetShipLog != null && selectedShipLog != targetShipLog)
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
                    ShipLogEditor.Instance.Show();
                    if (selectedMountStatusRecord != null)
                    {
                        var targetShipLog = TryToRaycastShipLog();
                        if (targetShipLog != null)
                        {
                            selectedMountStatusRecord.firingTargetObjectId = targetShipLog.objectId;
                            Debug.Log($"Set Firing Target: {selectedMountStatusRecord.objectId} -> {targetShipLog.objectId}");
                        }
                    }
                }
            }
            else if (state == State.SelectingFireControlSystemTarget)
            {
                if (Input.GetMouseButtonDown(0))
                {
                    state = State.Idle;
                    ShipLogEditor.Instance.Show();
                    if (selectedFireControlSystemStatusRecord != null)
                    {
                        var targetShipLog = TryToRaycastShipLog();
                        if (targetShipLog != null)
                        {
                            selectedFireControlSystemStatusRecord.targetObjectId = targetShipLog.objectId;
                            Debug.Log($"Set Fire Control System Target: {selectedFireControlSystemStatusRecord.objectId} -> {targetShipLog.objectId}");
                        }
                    }
                }
            }
        }
    }

    public PortraitViewer TryToRaycastViewer()
    {
        var cam = CameraController2.Instance.cam;
        var ray = cam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out var hit, Mathf.Infinity, iconLayerMask))
        {
            Debug.Log($"Hit: {hit.collider}");
            var colliderRootProvider = hit.collider.GetComponent<IColliderRootProvider>();
            if (colliderRootProvider != null)
            {
                var root = colliderRootProvider.GetRoot();
                var portraitViewer = root.GetComponent<PortraitViewer>();
                return portraitViewer;
            }
        }
        return null;
    }

    public ShipLog TryToRaycastShipLog()
    {
        return TryToRaycastViewer()?.shipLog;
    }

    public void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    public int selectedShipClassIndex = 0;

    [CreateProperty]
    public ShipClass selectedShipClass
    {
        get
        {
            if (selectedShipClassIndex >= navalGameState.shipClasses.Count || selectedShipClassIndex < 0)
                return null;
            return navalGameState.shipClasses[selectedShipClassIndex];
        }
    }

    public ShipClass SelectedShipClassProvider()
    {
        return selectedShipClass;
    }

    public string selectedShipLogObjectId;

    [CreateProperty]
    public ShipLog selectedShipLog
    {
        get
        {
            return EntityManager.Instance.Get<ShipLog>(selectedShipLogObjectId);
        }
    }

    public string selectedLeaderObjectId;

    [CreateProperty]
    public Leader selectedLeader
    {
        get
        {
            return EntityManager.Instance.Get<Leader>(selectedLeaderObjectId);
        }
    }

    public string selectedNamedShipObjectId;

    [CreateProperty]
    public NamedShip selectedNamedShip
    {
        get
        {
            return EntityManager.Instance.Get<NamedShip>(selectedNamedShipObjectId);
        }
    }

    public LanguageType iconLanuageType;
    // public int selected

    public string selectedMountStatusRecordObjectId;
    public MountStatusRecord selectedMountStatusRecord => EntityManager.Instance.Get<MountStatusRecord>(selectedMountStatusRecordObjectId);
    public string selectedFireControlSystemStatusRecordObjectId;
    public FireControlSystemStatusRecord selectedFireControlSystemStatusRecord => EntityManager.Instance.Get<FireControlSystemStatusRecord>(selectedFireControlSystemStatusRecordObjectId);

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

    public IEnumerable<ShipLog> GetShipsRequiringFiringLineRendering()
    {
        switch (GamePreference.Instance.firingLineDisplayMode)
        {
            case GamePreference.FiringLineDisplayMode.None:
                break;
            case GamePreference.FiringLineDisplayMode.SelectedShip:
                if (selectedShipLog != null)
                    yield return selectedShipLog;
                break;
            // Support other modes
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
}