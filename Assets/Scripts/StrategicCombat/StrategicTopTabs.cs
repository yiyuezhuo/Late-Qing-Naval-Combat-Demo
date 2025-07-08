using UnityEngine;
using GeographicLib;
using TMPro;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using UnityEngine.SceneManagement;

using StrategicCombatCore;
using CoreUtils;

public class StrategicTopTabs : SingletonDocument<StrategicTopTabs>
{
    protected override void Awake()
    {
        base.Awake();

        root.dataSource = StrategicGameManager.Instance;

        root.Q<Button>("ExportMapButton").clicked += () =>
        {
            Debug.Log("ExportMapButton clicked");

            IOManager.Instance.SaveTextFile(
                XmlUtils.ToXML(StrategicGameState.Instance),
                "StrategicGameState", "xml"
            );
        };

        root.Q<Button>("ImportMapButton").clicked += () =>
        {
            Debug.Log("ImportMapButton clicked");

            IOManager.Instance.textLoaded += OnMapXMLLoaded;
            IOManager.Instance.LoadTextFile("xml");
        };

        root.Q<Button>("GenerateMapButton").clicked += () =>
        {
            Debug.Log("GenerateMapButton clicked");

            var width = StrategicGameManager.Instance.tempMapWidth;
            var height = StrategicGameManager.Instance.tempMapHeight;

            StrategicGameState.Instance.GenerateTerrainMatrix(width, height);
        };
    }

    void OnMapXMLLoaded(object sender, string text)
    {
        IOManager.Instance.textLoaded -= OnMapXMLLoaded;

        var strategicGameState = XmlUtils.FromXML<StrategicGameState>(text);
        StrategicGameState.Instance.UpdateTo(strategicGameState);
    }
}