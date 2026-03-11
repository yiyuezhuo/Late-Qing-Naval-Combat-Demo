using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Xml.Serialization;
using System.Xml;
using System.IO;
using System.Linq;
using Unity.Properties;

using NavalCombatCore;
using CoreUtils;
using System;
using YYZ;


public class ShipClassEditor : HideableDocument<ShipClassEditor>
{
    public ListView shipClassListView;
    ListView batteryRecordsListView;

    public int selectedShipClassIndex = 0;

    SectorArcIndicatorBinder sectorArcIndicatorBinder = new();
    SectorArcIndicatorBinder torpedoSectorArcIndicatorBinder = new();

    [CreateProperty]
    public ShipClass selectedShipClass
    {
        get
        {
            var gameState = SuperGameState.Instance.GetCurrentGameState();
            if (selectedShipClassIndex >= gameState.shipClasses.Count || selectedShipClassIndex < 0)
                return null;
            return gameState.shipClasses[selectedShipClassIndex];
        }
    }

    public ShipClass SelectedShipClassProvider()
    {
        return selectedShipClass;
    }

    // protected override void Awake()
    void OnEnable()
    {
        // base.Awake();

        // Always not work as expected for some reason
        // var sortingOrder = doc.sortingOrder;
        // Debug.Log($"ShipClassEditor sortingOrder={sortingOrder}");

        root.dataSource = this;

        foreach (var listView in root.Query<BaseListView>().ToList())
        {
            listView.SetBinding("itemsSource", new DataBinding());
        }

        shipClassListView = root.Q<ListView>("ShipClassListView");
        // shipClassListView.itemsAdded += Utils.MakeCallbackForItemsAdded<ShipClass>(shipClassListView);
        Utils.BindItemsAddedRemoved<ShipClass>(shipClassListView, SelectedShipClassProvider);

        shipClassListView.selectedIndicesChanged += (IEnumerable<int> ints) =>
        {
            var idx = ints.FirstOrDefault();

            // Debug.Log($"selectedIndicesChanged: {idx}");

            selectedShipClassIndex = idx;
        };

        // TODO: Switch to Data Binding from callback (though how to bind list is very poorly documented)
        sectorArcIndicatorBinder.BindUI(root.Q<VisualElement>("SectorArcIndicator"));
        torpedoSectorArcIndicatorBinder.BindUI(root.Q<VisualElement>("TorpedoSectorArcIndicator"));

        shipClassListView.selectionChanged += (objs) =>
        {
            // Debug.Log($"selectionChanged: {objs}");
            var currentShipClass = objs.FirstOrDefault() as ShipClass;
            if (currentShipClass != null)
            {
                Debug.Log($"currentShipClass: {currentShipClass}");

                sectorArcIndicatorBinder.BindBatteryData(currentShipClass);
                torpedoSectorArcIndicatorBinder.BindTorpedoData(currentShipClass);
            }
        };

        var speedIncreaseMultiColumnListView = root.Q<MultiColumnListView>("SpeedIncreaseMultiColumnListView");
        // speedIncreaseMultiColumnListView.itemsAdded += Utils.MakeCallbackForItemsAdded<SpeedIncreaseRecord>(speedIncreaseMultiColumnListView);
        Utils.BindItemsAddedRemoved<SpeedIncreaseRecord>(speedIncreaseMultiColumnListView, SelectedShipClassProvider);

        batteryRecordsListView = root.Q<ListView>("BatteryRecordsListView");
        // batteryRecordsListView.itemsAdded += Utils.MakeCallbackForItemsAdded<BatteryRecord>(batteryRecordsListView);
        Utils.BindItemsAddedRemoved<BatteryRecord>(batteryRecordsListView, SelectedShipClassProvider);
        batteryRecordsListView.makeItem = () =>
        {
            var el = batteryRecordsListView.itemTemplate.CloneTree();
            Utils.BindItemsSourceRecursive(el);

            var fireControlTableMultiColumnListView = el.Q<MultiColumnListView>("FireControlTableMultiColumnListView");
            var penetrationTableMultiColumnListView = el.Q<MultiColumnListView>("PenetrationTableMultiColumnListView");
            var mountsListView = el.Q<ListView>("MountsListView");

            // fireControlTableMultiColumnListView.itemsAdded += Utils.MakeCallbackForItemsAdded<FireControlTableRecord>(fireControlTableMultiColumnListView);
            // penetrationTableMultiColumnListView.itemsAdded += Utils.MakeCallbackForItemsAdded<PenetrationTableRecord>(penetrationTableMultiColumnListView);
            // mountsListView.itemsAdded += Utils.MakeCallbackForItemsAdded<MountLocationRecord>(mountsListView);
            Utils.BindItemsAddedRemoved<FireControlTableRecord>(fireControlTableMultiColumnListView, SelectedShipClassProvider);
            Utils.BindItemsAddedRemoved<PenetrationTableRecord>(penetrationTableMultiColumnListView, SelectedShipClassProvider);
            Utils.BindItemsAddedRemoved<MountLocationRecord>(mountsListView, SelectedShipClassProvider);

            mountsListView.makeItem = () =>
            {
                var el2 = mountsListView.itemTemplate.CloneTree();

                var mountsArcsMultiColumnsListView = el2.Q<MultiColumnListView>("MountArcsMultiColumnListView");
                // mountsArcsMultiColumnsListView.itemsAdded += Utils.MakeCallbackForItemsAdded<MountArcRecord>(mountsArcsMultiColumnsListView);
                Utils.BindItemsAddedRemoved<MountArcRecord>(mountsArcsMultiColumnsListView, SelectedShipClassProvider);

                Utils.BindItemsSourceRecursive(el2);

                return el2;
            };

            return el;
        };

        var torpedoSettingsMultiColumnListView = root.Q<MultiColumnListView>("TorpedoSettingsMultiColumnListView");
        // torpedoSettingsMultiColumnListView.itemsAdded += Utils.MakeCallbackForItemsAdded<TorpedoSetting>(torpedoSettingsMultiColumnListView);
        Utils.BindItemsAddedRemoved<TorpedoSetting>(torpedoSettingsMultiColumnListView, SelectedShipClassProvider);

        var torpedoMountsListView = root.Q<ListView>("TorpedoMountsListView");
        // torpedoMountsListView.itemsAdded += Utils.MakeCallbackForItemsAdded<MountLocationRecord>(torpedoMountsListView);
        Utils.BindItemsAddedRemoved<MountLocationRecord>(torpedoMountsListView, SelectedShipClassProvider);
        torpedoMountsListView.makeItem = () =>
        {
            var el = torpedoMountsListView.itemTemplate.CloneTree();

            Utils.BindItemsSourceRecursive(el);
            var mountArcsMultiColumnListView = el.Q<MultiColumnListView>("MountArcsMultiColumnListView");
            // mountArcsMultiColumnListView.itemsAdded += Utils.MakeCallbackForItemsAdded<MountArcRecord>(mountArcsMultiColumnListView);
            Utils.BindItemsAddedRemoved<MountArcRecord>(mountArcsMultiColumnListView, SelectedShipClassProvider);

            return el;
        };

        var rapidFireBatteryListView = root.Q<ListView>("RapidFireBatteryListView");
        // rapidFireBatteryListView.itemsAdded += Utils.MakeCallbackForItemsAdded<RapidFireBatteryRecord>(rapidFireBatteryListView);
        Utils.BindItemsAddedRemoved<RapidFireBatteryRecord>(rapidFireBatteryListView, SelectedShipClassProvider);

        rapidFireBatteryListView.makeItem = () =>
        {
            var el = rapidFireBatteryListView.itemTemplate.CloneTree();

            Utils.BindItemsSourceRecursive(el);
            var fireControlLevelMultiColumnListView = el.Q<MultiColumnListView>("FireControlLevelMultiColumnListView");
            // fireControlLevelMultiColumnListView.itemsAdded += Utils.MakeCallbackForItemsAdded<RapidFireBatteryFireControlLevelRecord>(fireControlLevelMultiColumnListView);
            Utils.BindItemsAddedRemoved<RapidFireBatteryFireControlLevelRecord>(fireControlLevelMultiColumnListView, SelectedShipClassProvider);

            return el;
        };

        var confirmButton = root.Q<Button>("ConfirmButton");
        confirmButton.clicked += Hide;

        var exportButton = root.Q<Button>("ExportButton");
        exportButton.clicked += () =>
        {
            var gameState = SuperGameState.Instance.GetCurrentGameState();
            var content = gameState.ShipClassesToXML();
            IOManager.Instance.SaveTextFile(content, "ShipClasses", "xml");
        };

        var importButton = root.Q<Button>("ImportButton");
        importButton.clicked += () =>
        {
            // IOManager.Instance.textLoaded += OnShipClassesXMLLoaded;
            IOManager.Instance.LoadTextFile(OnShipClassesXMLLoaded, "xml");
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
                // IOManager.Instance.textLoaded += OnBatteryXMLLoaded;
                IOManager.Instance.LoadTextFile(OnBatteryXMLLoaded, "xml");
            }
        };

        var setSelectedByBatterySelectorButton = root.Q<Button>("SetSelectedByBatterySelectorButton");
        setSelectedByBatterySelectorButton.clicked += () =>
        {
            // Debug.Log("setSelectedByBatterySelectorButton clicked");

            DialogRoot.Instance.PopupBatteryRecordSelectorDialog(_batteryRecord =>
            {
                var batteryRecord = XmlUtils.FromXML<BatteryRecord>(XmlUtils.ToXML(_batteryRecord));
                ((IObjectIdLabeled)batteryRecord).ResetObjectId();

                var idx = batteryRecordsListView.selectedIndex;
                if (idx >= 0 && idx < batteryRecordsListView.itemsSource.Count) // TODO: Notify invalid 
                {
                    batteryRecordsListView.itemsSource[idx] = batteryRecord;
                }
                else
                {
                    batteryRecordsListView.itemsSource.Add(batteryRecord);
                }

                var gameState = SuperGameState.Instance.GetCurrentGameState();
                gameState.ResetAndRegisterAll(); // Assign a new guid to new copied battery record
            });
        };

        PathReferenceBinder.BindPictureReference(root.Q<VisualElement>("PortraitTopReferenceField"));
        PathReferenceBinder.BindPictureReference(root.Q<VisualElement>("PortraitReferenceField"));
        PathReferenceBinder.BindPictureReference(root.Q<VisualElement>("PortraitIconReferenceField"));

        root.Q<Button>("GeneratePlaceholderImageButton").clicked += () =>
        {
            if (selectedShipClass != null)
            {
                DialogRoot.Instance.PopupShipClassPlaceholderGeneratorDialog(selectedShipClass);
            }
        };

        var batteryArcIndicatorDialogButton = root.Q<Button>("BatteryArcIndicatorDialogButton");
        batteryArcIndicatorDialogButton.clicked += () =>
        {
            if (Utils.TryResolveCurrentValueForBinding(batteryArcIndicatorDialogButton, out ShipClass shipClass))
            {
                DialogRoot.Instance.PopupBatteryArcIndicatorDialog(shipClass);
            }
        };

        root.Q<Button>("SetSelectedByRapidFireBatterySelectorButton").clicked += () =>
        {
            Debug.Log("SetSelectedByRapidFireBatterySelectorButton clicked");

            DialogRoot.Instance.PopupRapidFireBatteryRecordSelectorDialog(_rapidFireBatteryRecord =>
            {
                var rapidFireBatteryRecord = XmlUtils.FromXML<RapidFireBatteryRecord>(XmlUtils.ToXML(_rapidFireBatteryRecord));
                // ((IObjectIdLabeled)rapidFireBatteryRecord).ResetObjectId();

                var idx = rapidFireBatteryListView.selectedIndex;
                if (idx >= 0 && idx < rapidFireBatteryListView.itemsSource.Count) // TODO: Notify invalid 
                {
                    rapidFireBatteryListView.itemsSource[idx] = rapidFireBatteryRecord;
                }
                else
                {
                    rapidFireBatteryListView.itemsSource.Add(rapidFireBatteryRecord);
                }

                // var gameState = SuperGameState.Instance.GetCurrentGameState();
                // gameState.ResetAndRegisterAll(); // Assign a new guid to new copied battery record
            });
        };
        
        var setByTorpedoSelectorButton = root.Q<Button>("SetByTorpedoSelectorButton");
        setByTorpedoSelectorButton.clicked += () =>
        {
            Debug.Log("SetByTorpedoSelectorButton clicked");

            DialogRoot.Instance.PopupTorpedoSectorSelectorDialog(_shipClass =>
            {
                var _torpedoSector = _shipClass.torpedoSector;
                var torpedoSector = XmlUtils.FromXML<TorpedoSector>(XmlUtils.ToXML(_torpedoSector));
                // ((IObjectIdLabeled)rapidFireBatteryRecord).ResetObjectId();

                if(Utils.TryResolveCurrentValueForBinding<ShipClass>(setByTorpedoSelectorButton, out var shipClass))
                {
                    shipClass.torpedoSector = torpedoSector;
                }
            });
        };
    }

    public EventHandler shown;
    public EventHandler hidden;

    protected override void OnShow()
    {
        shown?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnHidden()
    {
        hidden?.Invoke(this, EventArgs.Empty);
    }

    public void OnBatteryXMLLoaded(string text)
    {
        // IOManager.Instance.textLoaded -= OnBatteryXMLLoaded;

        var idx = batteryRecordsListView.selectedIndex;
        if (idx >= 0 && idx < batteryRecordsListView.itemsSource.Count) // TODO: Notify invalid 
        {
            var battryRecord = BatteryRecord.FromXml(text);
            batteryRecordsListView.itemsSource[idx] = battryRecord;
        }

        var gameState = SuperGameState.Instance.GetCurrentGameState();
        gameState.ResetAndRegisterAll(); // re-duplicate object id // FIXME: Correctness is questionable though
    }

    public void OnShipClassesXMLLoaded(string text)
    {
        // IOManager.Instance.textLoaded -= OnShipClassesXMLLoaded;

        var gameState = SuperGameState.Instance.GetCurrentGameState();
        gameState.ShipClassesFromXML(text);
        gameState.ResetAndRegisterAll();
    }

    [CreateProperty]
    public AbstractGameState currentGameState => SuperGameState.Instance.GetCurrentGameState();
}
