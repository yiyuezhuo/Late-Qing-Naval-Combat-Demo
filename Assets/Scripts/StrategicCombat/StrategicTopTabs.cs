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

        root.Q<Button>("SaveButton").clicked += () =>
        {
            Debug.Log("SaveButton clicked");

            var gameState = DetachGameState(StrategicGameState.Instance, StreamingAssetReference.Instance);

            IOManager.Instance.SaveTextFile(
                XmlUtils.ToXML(gameState),
                "StrategicGameState", "xml"
            );
        };

        root.Q<Button>("LoadButton").clicked += () =>
        {
            Debug.Log("LoadButton clicked");

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

        root.Q<Button>("LeaderEditorButton").clicked += () =>
        {
            LeaderEditor.Instance.Show();
        };

        var shipClassEditorButton = root.Q<Button>("ClassEditorButton");
        shipClassEditorButton.clicked += () => ShipClassEditor.Instance.Show();

        var namedShipEditorButton = root.Q<Button>("NamedShipEditorButton");
        namedShipEditorButton.clicked += NamedShipEditor.Instance.Show;

        var shipLogEditorButton = root.Q<Button>("ShipLogEditorButton");
        shipLogEditorButton.clicked += () => ShipLogEditor.Instance.Show();
    }

    StrategicGameState DetachGameState(StrategicGameState _s, StreamingAssetReference sar)
    {
        // deep copy
        var s = XmlUtils.FromXML<StrategicGameState>(XmlUtils.ToXML(_s));

        if (sar.leadersPath != null && sar.leadersPath != "")
            s.leaders = null;

        if (sar.shipClassesPath != null && sar.shipClassesPath != "")
            s.shipClasses = null;

        if (sar.namedShipsPath != null && sar.namedShipsPath != "")
            s.namedShips = null;

        return s;
    }


    void OnMapXMLLoaded(object sender, string text)
    {
        IOManager.Instance.textLoaded -= OnMapXMLLoaded;

        var strategicGameState = XmlUtils.FromXML<StrategicGameState>(text);
        StrategicGameState.Instance.UpdateTo(strategicGameState);
    }
}