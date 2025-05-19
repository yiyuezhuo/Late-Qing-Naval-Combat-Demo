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

public class ShipClassEditor : HideableDocument<ShipClassEditor>
{
    ListView batteryRecordsListView;

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

        batteryRecordsListView = root.Q<ListView>("BatteryRecordsListView");
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

            mountsListView.makeItem = () =>
            {
                var el2 = mountsListView.itemTemplate.CloneTree();

                var mountsArcsMultiColumnsListView = el2.Q<MultiColumnListView>("MountArcsMultiColumnListView");
                mountsArcsMultiColumnsListView.itemsAdded += Utils.MakeCallbackForItemsAdded<MountArcRecord>(mountsArcsMultiColumnsListView);

                Utils.BindItemsSourceRecursive(el2);

                return el2;
            };

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

        var exportSelectedBatteryButton = root.Q<Button>("ExportSelectedBatteryButton");
        var importToSelectedBatteryButton = root.Q<Button>("ImportToSelectedBatteryButton");

        exportSelectedBatteryButton.clicked += () =>
        {
            var battryRecord = batteryRecordsListView.selectedItem as BatteryRecord;
            if (battryRecord != null)
            {
                var content = battryRecord.ToXML();
                IOManager.Instance.SaveTextFile(content, "battery", "xml");
            }
        };

        importToSelectedBatteryButton.clicked += () =>
        {
            var idx = batteryRecordsListView.selectedIndex;
            if (idx >= 0 && idx < batteryRecordsListView.itemsSource.Count) // TODO: Notify invalid 
            {
                IOManager.Instance.textLoaded += OnBatteryXMLLoaded;
                IOManager.Instance.LoadTextFile("xml");
            }
        };
    }

    public void OnBatteryXMLLoaded(object sender, string text)
    {
        IOManager.Instance.textLoaded -= OnBatteryXMLLoaded;

        var idx = batteryRecordsListView.selectedIndex;
        if (idx >= 0 && idx < batteryRecordsListView.itemsSource.Count) // TODO: Notify invalid 
        {
            var battryRecord = BatteryRecord.FromXml(text);
            batteryRecordsListView.itemsSource[idx] = battryRecord;
        }
    }

    public void OnShipClassesXMLLoaded(object sender, string text)
    {
        IOManager.Instance.textLoaded -= OnShipClassesXMLLoaded;

        GameManager.Instance.navalGameState.ShipClassesFromXml(text);
    }
}