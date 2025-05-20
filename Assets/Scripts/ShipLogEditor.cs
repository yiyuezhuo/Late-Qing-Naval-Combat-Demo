using UnityEngine;
using NavalCombatCore;
using GeographicLib;
using TMPro;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Xml.Serialization;
using System.Xml;
using System.IO;
using System.Linq;
using Unity.Properties;

namespace NavalCombatCore
{
    public partial class ShipLog
    {
        [CreateProperty]
        public ShipClass shipClassProperty
        {
            get => shipClass;
        }
    }
}


public class ShipLogEditor : HideableDocument<ShipLogEditor>
{
    protected override void Awake()
    {
        base.Awake();

        root.dataSource = GameManager.Instance;

        foreach (var listView in root.Query<BaseListView>().ToList())
        {
            listView.SetBinding("itemsSource", new DataBinding());
        }

        var shipLogListView = root.Q<ListView>("ShipLogListView");
        shipLogListView.itemsAdded += Utils.MakeCallbackForItemsAdded<ShipLog>(shipLogListView);

        shipLogListView.selectedIndicesChanged += (IEnumerable<int> ints) =>
        {
            var idx = ints.FirstOrDefault();
            GameManager.Instance.selectedShipLogIndex = idx;
        };

        var confirmButton = root.Q<Button>("ConfirmButton");
        confirmButton.clicked += Hide;

        var exportButton = root.Q<Button>("ExportButton");
        exportButton.clicked += () =>
        {
            var content = GameManager.Instance.navalGameState.ShipLogsToXML();
            IOManager.Instance.SaveTextFile(content, "ShipLogs", "xml");
        };

        var importButton = root.Q<Button>("ImportButton");
        importButton.clicked += () =>
        {
            IOManager.Instance.textLoaded += OnShipLogsXMLLoaded;
            IOManager.Instance.LoadTextFile("xml");
        };
    }

    void OnShipLogsXMLLoaded(object sender, string text)
    {
        IOManager.Instance.textLoaded -= OnShipLogsXMLLoaded;

        GameManager.Instance.navalGameState.ShipLogsFromXml(text);
    }
}