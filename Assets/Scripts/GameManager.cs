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



public class GameManager : MonoBehaviour
{
    public NavalGameState navalGameState = NavalGameState.Instance;
    public GameObject natoIconPrefab;
    public Transform earthTransform;

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
        SelectingInsertUnitPosition
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

    public void Start()
    {
        // Debug.Log($"Persistent Path:{Application.persistentDataPath}");

        EntityManager.Instance.newGuidCreated += (obj, s) => Debug.LogWarning($"New guid created: {s} for {obj}");

        // #if UNITY_EDITOR

        //         var shipClassesPath = Path.Combine(Application.persistentDataPath, "ShipClasses.xml");
        //         if (File.Exists(shipClassesPath))
        //         {
        //             var shipClassXml = File.ReadAllText(shipClassesPath);
        //             navalGameState.ShipClassesFromXml(shipClassXml);
        //         }

        //         // temp data structure fix
        //         foreach (var shipClass in navalGameState.shipClasses)
        //         {
        //             foreach (var batteryReocrd in shipClass.batteryRecords)
        //             {
        //                 if (batteryReocrd.fireControlType == null)
        //                 {
        //                     batteryReocrd.fireControlType = new FireControlSystem();
        //                 }
        //             }
        //         }

        //         var shipLogsPath = Path.Combine(Application.persistentDataPath, "ShipLogs.xml");
        //         if (File.Exists(shipLogsPath))
        //         {
        //             var shipLogsXml = File.ReadAllText(shipLogsPath);
        //             navalGameState.ShipLogsFromXml(shipLogsXml);
        //         }

        //         // RootShipGroups
        //         var rootShipGroupsPath = Path.Combine(Application.persistentDataPath, "RootShipGroups.xml");
        //         if (File.Exists(rootShipGroupsPath))
        //         {
        //             var rootShipGroupsXml = File.ReadAllText(rootShipGroupsPath);
        //             navalGameState.RootShipGroupsFromXml(rootShipGroupsXml);
        //         }

        //         OOBEditor.Instance.oobTreeView.ExpandAll();
        // #endif

        Func<string, string> _load = (string name) =>
        {
            var path = "Scenarios/Battle of Pungdo/" + name;
            return Resources.Load<TextAsset>(path).text;
        };

        var shipClassXml = _load("ShipClasses");
        navalGameState.ShipClassesFromXml(shipClassXml);

        var shipLogsXml = _load("ShipLogs");
        navalGameState.ShipLogsFromXml(shipLogsXml);

        var rootShipGroupsXml = _load("ShipGroups");
        navalGameState.ShipGroupsFromXml(rootShipGroupsXml);

        OOBEditor.Instance.oobTreeView.ExpandAll();

        // var fullStateText = Resources.Load<TextAsset>("Scenarios/Battle of Pungdo/FullState").text;
        // var fullState = FullState.FromXML(fullStateText);
        // TopTabs.Instance.LoadViewState(fullState.viewState);
        // NavalGameState.Instance.UpdateTo(fullState.navalGameState);


        NavalGameState.Instance.ResetAndRegisterAll(); // Note FromXml call has call it many times.

        // Test
        // var s = new RapidFiringStatus();
        // var i = s.info;

        // var j = 0;
        // foreach (var shipLog in NavalGameState.Instance.shipLogs)
        // {
        //     // var icon = Instantiate(natoIconPrefab, earthTransform);
        //     // var viewer = icon.GetComponent<IDF3ModelViewer>();
        //     // viewer.model = shipLog;
        //     shipLog.mapState = MapState.Deployed;
        //     shipLog.position.LatDeg = 37 + j * 0.1f;
        //     shipLog.position.LonDeg = 123 + j * 0.1f;
        //     shipLog.headingDeg = j * 10f;
        //     shipLog.speedKnots = 2 * j;

        //     j++;
        // }
    }

    public Dictionary<string, IDF3ModelViewer> objectId2Viewer = new();

    // float viewAccTime;

    public void Update()
    {
        // viewAccTime += Time.deltaTime;

        // if (viewAccTime > 2)
        // {
        //     viewAccTime -= 2;
        //     Debug.Log("2s Tick");
        // }

        // sync ShipView and ShipLog mapping
            foreach (var shipLog in NavalGameState.Instance.shipLogs)
            {
                if (shipLog.IsOnMap() && !objectId2Viewer.ContainsKey(shipLog.objectId))
                {
                    var obj = Instantiate(natoIconPrefab, earthTransform);

                    var df3viewer = obj.GetComponent<IDF3ModelViewer>();
                    df3viewer.modelObjectId = shipLog.objectId;
                    objectId2Viewer[shipLog.objectId] = df3viewer;

                    var iconViewer = obj.GetComponent<NATOIconViewer>();
                    iconViewer.shipLogObjectId = shipLog.objectId;
                }
            }
        var shouldRemoved = objectId2Viewer.Where(kv => !EntityManager.Instance.Get<ShipLog>(kv.Key)?.IsOnMap() ?? false).ToList();
        foreach ((var shipLog, var viewer) in shouldRemoved)
        {
            Destroy(viewer);
            objectId2Viewer.Remove(shipLog);
        }

        // Handle Events

        if (!EventSystem.current.IsPointerOverGameObject())
        {
            if (state == State.SelectingInsertUnitPosition)
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

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                state = State.Idle;
            }

            // handle events
            if (Input.GetKeyDown(KeyCode.Insert))
            {
                state = State.SelectingInsertUnitPosition;
            }
        }

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

    public int selectedShipLogIndex = 0;

    [CreateProperty]
    public ShipLog selectedShipLog
    {
        get
        {
            if (selectedShipLogIndex >= navalGameState.shipLogs.Count || selectedShipLogIndex < 0)
                return null;
            return navalGameState.shipLogs[selectedShipLogIndex];
        }
    }

    // [CreateProperty]
    // public ShipClass shipClassOfSelectedShipLog
    // {
    //     get
    //     {
    //         var shipLog = selectedShipLog;
    //         if (shipLog != null)
    //         {
    //             return navalGameState.shipClasses.FirstOrDefault(x => x.name.english == shipLog.shipClassStr); // TODO: Use formal ID?
    //         }
    //         return null;
    //     }
    // }


}