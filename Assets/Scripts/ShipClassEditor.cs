using UnityEngine;
using NavalCombatCore;
using GeographicLib;
using TMPro;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Xml.Serialization;
using System.Xml;
using System.IO;

public class ShipClassEditor : HideableDocument<ShipClassEditor>
{
    protected override void Awake()
    {
        base.Awake();

        root.dataSource = GameManager.Instance;

        var shipClassListView = root.Q<ListView>("ShipClassListView");
        shipClassListView.SetBinding("itemsSource", new DataBinding());
        shipClassListView.itemsAdded += (IEnumerable<int> index) =>
        {
            foreach (var i in index)
            {
                var v = shipClassListView.itemsSource[i];
                if (v == null)
                {
                    shipClassListView.itemsSource[i] = new ShipClass();
                }
            }
        };

        var confirmButton = root.Q<Button>("ConfirmButton");
        confirmButton.clicked += Hide;

        var exportButton = root.Q<Button>("ExportButton");
        exportButton.clicked += () =>
        {
            var content = GameManager.Instance.navalGameState.ShipClassesToXML();
            IOManager.Instance.SaveTextFile(content, "ShipClasses", "xml");
        };
    }
}