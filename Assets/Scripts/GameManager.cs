using UnityEngine;
using NavalCombatCore;
using GeographicLib;
using TMPro;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Unity.Properties;
using System.IO;
using System.Linq;


public class GameManager : MonoBehaviour
{
    public NavalGameState navalGameState = NavalGameState.Instance;

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
        Debug.Log($"Persistent Path:{Application.persistentDataPath}");

#if UNITY_EDITOR

        var shipClassesPath = Path.Combine(Application.persistentDataPath, "ShipClasses.xml");
        if (File.Exists(shipClassesPath))
        {
            var shipClassXml = File.ReadAllText(shipClassesPath);
            navalGameState.ShipClassesFromXml(shipClassXml);
        }

        // temp data structure fix
        foreach (var shipClass in navalGameState.shipClasses)
        {
            foreach (var batteryReocrd in shipClass.batteryRecords)
            {
                if (batteryReocrd.fireControlType == null)
                {
                    batteryReocrd.fireControlType = new FireControlSystem();
                }
            }
        }

        var shipLogsPath = Path.Combine(Application.persistentDataPath, "ShipLogs.xml");
        if (File.Exists(shipLogsPath))
        {
            var shipLogsXml = File.ReadAllText(shipLogsPath);
            navalGameState.ShipLogsFromXml(shipLogsXml);
        }
#endif

        EntityManager.Instance.newGuidCreated += (obj, s) => Debug.LogWarning($"New guid created: {s} for {obj}");
        NavalGameState.Instance.ResetAndRegisterAll();

        var s = new RapidFiringStatus();
        var i = s.info;
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