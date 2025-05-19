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
using TreeEditor;

public class ShipClassEditor : HideableDocument<ShipClassEditor>
{
    protected override void Awake()
    {
        base.Awake();

        root.dataSource = GameManager.Instance;

        foreach (var listView in root.Query<BaseListView>().ToList())
        {
            listView.SetBinding("itemsSource", new DataBinding());
        }

        var shipClassListView = root.Q<ListView>("ShipClassListView");
        // shipClassListView.SetBinding("itemsSource", new DataBinding());
        shipClassListView.itemsAdded += Utils.MakeCallbackForItemsAdded<ShipClass>(shipClassListView);
        // shipClassListView.itemsAdded += (IEnumerable<int> index) =>
        // {
        //     foreach (var i in index)
        //     {
        //         var v = shipClassListView.itemsSource[i];
        //         if (v == null)
        //         {
        //             shipClassListView.itemsSource[i] = new ShipClass();
        //         }
        //     }
        // };

        // shipClassListView.selectionChanged += (IEnumerable<object> objs) =>
        // {
        //     var shipClass = objs.FirstOrDefault() as ShipClass;
        //     if (shipClass != null)
        //     {
        //         Debug.Log(shipClass.name.GetMergedName());
        //     }
        // };

        shipClassListView.selectedIndicesChanged += (IEnumerable<int> ints) =>
        {
            var idx = ints.FirstOrDefault();
            GameManager.Instance.selectedShipClassIndex = idx;
        };

        var speedIncreaseMultiColumnListView = root.Q<MultiColumnListView>("SpeedIncreaseMultiColumnListView");
        speedIncreaseMultiColumnListView.itemsAdded += Utils.MakeCallbackForItemsAdded<SpeedIncreaseRecord>(speedIncreaseMultiColumnListView);
        // speedIncreaseMultiColumnListView.itemsAdded += (IEnumerable<int> index) =>
        // {
        //     foreach (var i in index)
        //     {
        //         var v = speedIncreaseMultiColumnListView.itemsSource[i];
        //         if (v == null)
        //         {
        //             speedIncreaseMultiColumnListView.itemsSource[i] = new SpeedIncreaseRecord();
        //         }
        //     }
        // };
        // speedIncreaseMultiColumnListView.SetBinding("itemsSource", new DataBinding());

        var batteryRecordsListView = root.Q<ListView>("BatteryRecordsListView");
        batteryRecordsListView.itemsAdded += Utils.MakeCallbackForItemsAdded<BatteryRecord>(batteryRecordsListView);
        batteryRecordsListView.makeItem = () =>
        {
            var el = batteryRecordsListView.itemTemplate.CloneTree();
            Utils.BindItemsSourceRecursive(el);

            var fireControlTableMultiColumnListView = el.Q<MultiColumnListView>("FireControlTableMultiColumnListView");
            var penetrationTableMultiColumnListView = el.Q<MultiColumnListView>("PenetrationTableMultiColumnListView");
            var mountsListView = el.Q<ListView>("MountsListView");

            fireControlTableMultiColumnListView.itemsAdded += Utils.MakeCallbackForItemsAdded<FireControlTableRecord>(fireControlTableMultiColumnListView);
            penetrationTableMultiColumnListView.itemsAdded += Utils.MakeCallbackForItemsAdded<PenetrationTableRecord>(penetrationTableMultiColumnListView);
            mountsListView.itemsAdded += Utils.MakeCallbackForItemsAdded<MountLocationRecord>(mountsListView);
            
            return el;
        };

        var confirmButton = root.Q<Button>("ConfirmButton");
        confirmButton.clicked += Hide;

        var exportButton = root.Q<Button>("ExportButton");
        exportButton.clicked += () =>
        {
            var content = GameManager.Instance.navalGameState.ShipClassesToXML();
            IOManager.Instance.SaveTextFile(content, "ShipClasses", "xml");
        };

        var importButton = root.Q<Button>("ImportButton");
        importButton.clicked += () =>
        {
            IOManager.Instance.textLoaded += OnShipClassesXMLLoaded;
            IOManager.Instance.LoadTextFile("xml");
        };
    }

    public void OnShipClassesXMLLoaded(object sender, string text)
    {
        GameManager.Instance.navalGameState.ShipClassesFromXml(text);
        IOManager.Instance.textLoaded -= OnShipClassesXMLLoaded;
    }
}