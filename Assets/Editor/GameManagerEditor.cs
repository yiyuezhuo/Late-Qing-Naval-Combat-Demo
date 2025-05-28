using UnityEditor;
using UnityEngine;
using System.Xml.Serialization;
using System.Xml;
using System.IO;
using System.Collections.Generic;
using System;
using NavalCombatCore;
using System.Linq;

public static class MenuTest
{
    [MenuItem("Custom/Generate Leaders Data Using Old Leaders Data")]
    public static void GenerateLeadersDataUsingOldLeadersData()
    {
        Func<string, string> _load = (string name) =>
        {
            var path = "Scenarios/Battle of Pungdo/" + name;
            return Resources.Load<TextAsset>(path).text;
        };

        var shipLogsXml = _load("ShipLogs");
        var shipLogs = XmlUtils.FromXML<List<ShipLog>>(shipLogsXml);

        var leaders = shipLogs.Select(shipLog => new Leader()
        {
            objectId = System.Guid.NewGuid().ToString(),
            name = shipLog.captain,
            portraitCode = shipLog.captainPortraitCode
        }).ToList();

        var leadersXml = XmlUtils.ToXML(leaders);

        string path = EditorUtility.SaveFilePanelInProject(
            "title", "leaders", "xml", "message"
        );

        File.WriteAllText(path, leadersXml);
    }

    [MenuItem("Custom/Restructure ShipLogs's captain data")]
    public static void RestructureShipLogsCaptainData()
    {
        Func<string, string> _load = (string name) =>
        {
            var path = "Scenarios/Battle of Pungdo/" + name;
            return Resources.Load<TextAsset>(path).text;
        };

        var shipLogsXml = _load("ShipLogs");
        var shipLogs = XmlUtils.FromXML<List<ShipLog>>(shipLogsXml);

        var leadersXml = _load("Leaders");
        var leaders = XmlUtils.FromXML<List<Leader>>(leadersXml);

        var leaderMap = leaders.ToDictionary(x => x.name.english, x => x);
        foreach (var shipLog in shipLogs)
        {
            var leader = leaderMap.GetValueOrDefault(shipLog.captain.english);
            if (leader != null)
            {
                shipLog.leaderObjectId = leader.objectId;
            }
        }

        var convertedShipLogsXML = XmlUtils.ToXML(shipLogs);

        string path = EditorUtility.SaveFilePanelInProject(
            "title", "ShipLogs", "xml", "message"
        );

        File.WriteAllText(path, convertedShipLogsXML);
    }
}