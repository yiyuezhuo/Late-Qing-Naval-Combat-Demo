using UnityEngine;
using NavalCombatCore;
using GeographicLib;
using TMPro;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Unity.Properties;
using System.IO;


public class GameManager : MonoBehaviour
{
    public NavalGameState navalGameState = new();

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

        var shipClassesPath = Path.Combine(Application.persistentDataPath, "shipClasses.xml");
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
#endif

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
            if (selectedShipClassIndex >= navalGameState.shipClasses.Count)
                return null;
            return navalGameState.shipClasses[selectedShipClassIndex];
        }
    }
}