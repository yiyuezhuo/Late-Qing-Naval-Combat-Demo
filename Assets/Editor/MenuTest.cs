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
    // [MenuItem("Custom/Generate Leaders Data Using Old Leaders Data")]
    // public static void GenerateLeadersDataUsingOldLeadersData()
    // {
    //     Func<string, string> _load = (string name) =>
    //     {
    //         var path = "Scenarios/Battle of Pungdo/" + name;
    //         return Resources.Load<TextAsset>(path).text;
    //     };

    //     var shipLogsXml = _load("ShipLogs");
    //     var shipLogs = XmlUtils.FromXML<List<ShipLog>>(shipLogsXml);

    //     var leaders = shipLogs.Select(shipLog => new Leader()
    //     {
    //         objectId = System.Guid.NewGuid().ToString(),
    //         name = shipLog.captain,
    //         portraitCode = shipLog.captainPortraitCode
    //     }).ToList();

    //     var leadersXml = XmlUtils.ToXML(leaders);

    //     string path = EditorUtility.SaveFilePanelInProject(
    //         "title", "leaders", "xml", "message"
    //     );

    //     File.WriteAllText(path, leadersXml);
    // }

    // [MenuItem("Custom/Restructure ShipLogs's captain data")]
    // public static void RestructureShipLogsCaptainData()
    // {
    //     Func<string, string> _load = (string name) =>
    //     {
    //         var path = "Scenarios/Battle of Pungdo/" + name;
    //         return Resources.Load<TextAsset>(path).text;
    //     };

    //     var shipLogsXml = _load("ShipLogs");
    //     var shipLogs = XmlUtils.FromXML<List<ShipLog>>(shipLogsXml);

    //     var leadersXml = _load("Leaders");
    //     var leaders = XmlUtils.FromXML<List<Leader>>(leadersXml);

    //     var leaderMap = leaders.ToDictionary(x => x.name.english, x => x);
    //     foreach (var shipLog in shipLogs)
    //     {
    //         var leader = leaderMap.GetValueOrDefault(shipLog.captain.english);
    //         if (leader != null)
    //         {
    //             shipLog.leaderObjectId = leader.objectId;
    //         }
    //     }

    //     var convertedShipLogsXML = XmlUtils.ToXML(shipLogs);

    //     string path = EditorUtility.SaveFilePanelInProject(
    //         "title", "ShipLogs", "xml", "message"
    //     );

    //     File.WriteAllText(path, convertedShipLogsXML);
    // }

    [MenuItem("Custom/Generate Name Ships")]
    public static void GenerateNameShips()
    {
        Func<string, string> _load = (string name) =>
        {
            var path = "Scenarios/Battle of Pungdo/" + name;
            return Resources.Load<TextAsset>(path).text;
        };

        var shipLogsXml = _load("ShipLogs");
        var shipLogs = XmlUtils.FromXML<List<ShipLog>>(shipLogsXml);

        var shipClassesXml = _load("ShipClasses");
        var shipClasses = XmlUtils.FromXML<List<ShipClass>>(shipClassesXml);

        var namedShips = shipLogs.Select(shipLog =>
        {
            var shipClass = shipClasses.FirstOrDefault(shipClass => shipClass.objectId == shipLog.shipClassObjectId);

            return new NamedShip()
            {
                objectId = System.Guid.NewGuid().ToString(),
                shipClassObjectId = shipLog.shipClassObjectId,
                name = shipLog.name,
                builderDesc = shipClass.builderDesc,
                launchedDate = shipLog.launchedDate,
                completedDate = shipLog.completedDate,
                fateDesc = shipLog.fateDesc,
                applicableYearBegin = shipClass.applicableYearBegin,
                applicableYearEnd = shipClass.applicableYearEnd,
                defaultLeaderObjectId = shipLog.leaderObjectId
            };
        }).ToList();

        var convertedShipLogsXML = XmlUtils.ToXML(namedShips);

        string path = EditorUtility.SaveFilePanelInProject(
            "title", "NamedShips", "xml", "message"
        );

        File.WriteAllText(path, convertedShipLogsXML);
    }

    [MenuItem("Custom/Rebase ShipLog to NamedShip")]
    public static void RebaseShipLogToNamedShip()
    {
        Func<string, string> _load = (string name) =>
        {
            var path = "Scenarios/Battle of Pungdo/" + name;
            return Resources.Load<TextAsset>(path).text;
        };

        var shipLogs = XmlUtils.FromXML<List<ShipLog>>(_load("ShipLogs"));
        var namedShips = XmlUtils.FromXML<List<NamedShip>>(_load("NamedShips"));

        foreach (var shipLog in shipLogs)
        {
            var namedShip = namedShips.FirstOrDefault(x => x.name.english == shipLog.name.english);
            shipLog.namedShipObjectId = namedShip.objectId;
        }

        var convertedShipLogsXML = XmlUtils.ToXML(shipLogs);

        string path = EditorUtility.SaveFilePanelInProject(
            "title", "ShipLogs", "xml", "message"
        );

        File.WriteAllText(path, convertedShipLogsXML);
    }
}