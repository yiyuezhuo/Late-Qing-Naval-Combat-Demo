using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System;

using CoreUtils;
using System.Collections.Generic;
using System.Xml.Serialization;

public class StreamingTextAssetManager
{
    static StreamingTextAssetManager instance = new();
    public static StreamingTextAssetManager Instance => instance;

    public List<UnityWebRequest> busyUnityWebRequests = new();

    public IEnumerator FetchText(string path, Action<string> callback)
    {
        var request = UnityWebRequest.Get(path);

        busyUnityWebRequests.Add(request);

        yield return request.SendWebRequest();
        if (request.result == UnityWebRequest.Result.Success)
        {
            Debug.Log($"Success: {path}");
            callback(request.downloadHandler.text);

            busyUnityWebRequests.Remove(request);
        }
        else
        {
            Debug.LogError($"failed to fetch and setup: {path}");
        }
    }
}

public class StreamingAssetReference
{
    static StreamingAssetReference instance = new();
    public static StreamingAssetReference Instance => instance;

    public string leadersPath = "Leaders.xml";
    public string shipClassesPath = "ShipClasses.xml";
    public string namedShipsPath = "NamedShips.xml";
    // public string shipLogsPath;
    // public string shipGroupsPath;
    // scenarioState, launchedTorpedos, weaponSimulationAssignmentClock has little reusability so it's directly tracked by NavalGameState and cannot be replaced by external file.

    // [XmlIgnore]
    // public List<UnityWebRequest> busyUnityWebRequests = new();

    public static void UpdateInstance(StreamingAssetReference newInstance)
    {
        instance = newInstance;
    }

    public IEnumerator FetchScenarioFile(string name, Action<string> callback)
    {
        var root = Application.streamingAssetsPath + "/Scenarios/";
        var path = root + name;
        return StreamingTextAssetManager.Instance.FetchText(path, callback);

        // var root = Application.streamingAssetsPath + "/Scenarios/";
        // var path = root + name;
        // var request = UnityWebRequest.Get(path);

        // busyUnityWebRequests.Add(request);

        // yield return request.SendWebRequest();
        // if (request.result == UnityWebRequest.Result.Success)
        // {
        //     Debug.Log($"Success: {path}");
        //     callback(request.downloadHandler.text);

        //     busyUnityWebRequests.Remove(request);
        // }
        // else
        // {
        //     Debug.LogError($"failed to fetch and setup: {name}");
        // }
    }

    public IEnumerator FetchScenarioFileIfApplicable(object obj, string name, Action<string> callback)
    {
        // Debug.Log($"obj={obj}, obj==null={obj==null} name={name}");

        var objCanFill = obj == null;
        if (!objCanFill)
        {
            var list = obj as IList;
            if (list != null && list.Count == 0)
            {
                objCanFill = true;
            }
        }

        if (objCanFill && name != null && name != "")
        {
            // Debug.Log($"FetchScenarioFile Before: {name} {callback}");
            yield return FetchScenarioFile(name, callback);
            // Debug.Log($"FetchScenarioFile After: {name} {callback}");
        }
    }

    public IEnumerator TryToCompleteFromStreamingAssetReference(AbstractGameState s)
    {
        yield return FetchScenarioFileIfApplicable(s.leaders, leadersPath, s.LeadersFromXML);
        yield return FetchScenarioFileIfApplicable(s.shipClasses, shipClassesPath, s.ShipClassesFromXML);
        yield return FetchScenarioFileIfApplicable(s.namedShips, namedShipsPath, s.NamedShipsFromXML);
        // Debug.Log("TryToCompleteFromStreamingAssetReference End");
    }

    // public NavalGameState Detach(NavalGameState _s)
    // {
    //     // deep copy
    //     var s = XmlUtils.FromXML<NavalGameState>(XmlUtils.ToXML(_s));

    //     if (leadersPath != null && leadersPath != "")
    //         s.leaders = null;

    //     if(shipClassesPath != null && shipClassesPath != "")
    //         s.shipClasses = null;

    //     if (namedShipsPath != null && namedShipsPath != "")
    //         s.namedShips = null;

    //     return s;
    // }
}
