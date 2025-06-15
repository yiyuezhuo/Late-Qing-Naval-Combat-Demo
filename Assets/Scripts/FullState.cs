using NavalCombatCore;
using UnityEngine;
using System.Xml.Serialization;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Xml;
using UnityEngine.Networking;
using System.Collections;
using System;

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

    IEnumerator FetchScenarioFile(string name, Action<string> callback)
    {
        var root = Application.streamingAssetsPath + "/Scenarios/First Sino-Japanese War/";
        var request = UnityWebRequest.Get(root + name);
        yield return request.SendWebRequest();
        if (request.result == UnityWebRequest.Result.Success)
        {
            callback(request.downloadHandler.text);
        }
        else
        {
            Debug.LogError($"failed to fetch and setup: {name}");
        }
    }

    public IEnumerator FetchScenarioFileIfApplicable(object obj, string name, Action<string> callback)
    {
        if (obj == null && name != null && name != "")
        {
            yield return FetchScenarioFile(name, callback);
        }
    }

    public IEnumerator TryToCompleteFromStreamingAssetReference()
    {
        var s = NavalGameState.Instance;

        yield return FetchScenarioFileIfApplicable(s.leaders, leadersPath, s.LeadersFromXML);
        yield return FetchScenarioFileIfApplicable(s.shipClasses, shipClassesPath, s.ShipClassesFromXML);
        yield return FetchScenarioFileIfApplicable(s.namedShips, namedShipsPath, s.NamedShipsFromXML);
    }

    public NavalGameState Detach()
    {
        // deep copy
        var s = XmlUtils.FromXML<NavalGameState>(XmlUtils.ToXML<NavalGameState>(NavalGameState.Instance));

        if (leadersPath != null && leadersPath != "")
            s.leaders = null;

        if(shipClassesPath != null && shipClassesPath != "")
            s.shipClasses = null;

        if (namedShipsPath != null && namedShipsPath != "")
            s.namedShips = null;

        return s;
    }

}

public class FullState
{
    public StreamingAssetReference streamingAssetReference;
    public NavalGameState navalGameState;
    public ViewState viewState;


    // static XmlSerializer fullStateSerializer = new XmlSerializer(typeof(FullState));

    public string ToXML()
    {
        // using (var textWriter = new StringWriter())
        // {
        //     using (XmlWriter xmlWriter = XmlWriter.Create(textWriter))
        //     {
        //         fullStateSerializer.Serialize(xmlWriter, this);
        //         string serializedXml = textWriter.ToString();

        //         return serializedXml;
        //     }
        // }

        return XmlUtils.ToXML(this);
    }

    public static FullState FromXML(string xml)
    {
        // using (var reader = new StringReader(xml))
        // {
        //     return (FullState)fullStateSerializer.Deserialize(reader);
        // }
        return XmlUtils.FromXML<FullState>(xml);
    }
}