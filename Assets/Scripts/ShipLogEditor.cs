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
using System;



public class ShipLogEditor : HideableDocument<ShipLogEditor>
{
    public VisualTreeAsset shipClassSelectorDialogDocument;
    public ListView shipLogListView;

    protected override void Awake()
    {
        base.Awake();

        // var sortingOrder = doc.sortingOrder;
        // Debug.Log($"ShipLogEditor sortingOrder={sortingOrder}");

        root.dataSource = GameManager.Instance;

        // foreach (var listView in root.Query<BaseListView>().ToList())
        // {
        //     listView.SetBinding("itemsSource", new DataBinding());
        // }
        Utils.BindItemsSourceRecursive(root);

        shipLogListView = root.Q<ListView>("ShipLogListView");
        // shipLogListView.itemsAdded += Utils.MakeCallbackForItemsAdded<ShipLog>(shipLogListView);
        Utils.BindItemsAddedRemoved<ShipLog>(shipLogListView, () => null);

        // shipLogListView.selectedIndicesChanged += (IEnumerable<int> ints) =>
        // {
        //     var idx = ints.FirstOrDefault();
        //     GameManager.Instance.selectedShipLogIndex = idx;
        // };

        shipLogListView.selectionChanged += (IEnumerable<object> objs) =>
        {
            var shipLog = objs.FirstOrDefault() as ShipLog;
            GameManager.Instance.selectedShipLogObjectId = shipLog.objectId;
        };

        var batteryStatusListView = root.Q<ListView>("BatteryStatusListView");
        Utils.BindItemsAddedRemoved<NavalCombatCore.BatteryStatus>(batteryStatusListView, () => GameManager.Instance.selectedShipLog);
        // MountStatusMultiColumnListView
        batteryStatusListView.makeItem = () =>
        {
            var batteryStatusElement = batteryStatusListView.itemTemplate.CloneTree();

            Utils.BindItemsSourceRecursive(batteryStatusElement);

            var mountStatusMultiColumnListView = batteryStatusElement.Q<MultiColumnListView>("MountStatusMultiColumnListView");
            Utils.BindItemsAddedRemoved<MountStatusRecord>(mountStatusMultiColumnListView, () =>
            {
                var ctx = batteryStatusElement.GetHierarchicalDataSourceContext(); // 
                var isSucc = PropertyContainer.TryGetValue(ctx.dataSource, ctx.dataSourcePath, out NavalCombatCore.BatteryStatus bs);

                return bs;
            }); // TODO: Not always valid?

            var firingTargetColumn = mountStatusMultiColumnListView.columns["firingTarget"];
            firingTargetColumn.makeCell = () =>
            {
                var el = firingTargetColumn.cellTemplate.CloneTree();

                var setButton = el.Q<Button>("SetButton");
                setButton.clicked += () =>
                {
                    var ctx = setButton.GetHierarchicalDataSourceContext();
                    if (PropertyContainer.TryGetValue(ctx.dataSource, ctx.dataSourcePath, out MountStatusRecord mountStatus))
                    {
                        GameManager.Instance.selectedMountStatusRecordObjectId = mountStatus.objectId;
                        GameManager.Instance.state = GameManager.State.SelectingFiringTarget;
                        Hide();
                    }
                };

                return el;
            };

            var detailColumn = mountStatusMultiColumnListView.columns["detail"];
            detailColumn.makeCell = () =>
            {
                var el = detailColumn.cellTemplate.CloneTree();

                var detailButton = el.Q<Button>("DetailButton");
                detailButton.clicked += () =>
                {
                    var ctx = detailButton.GetHierarchicalDataSourceContext();
                    if (PropertyContainer.TryGetValue(ctx.dataSource, ctx.dataSourcePath, out MountStatusRecord mountStatus))
                    {
                        // Debug.Log($"Detail Invoke: {mountStatus.objectId}");

                        DialogRoot.Instance.PopupMessageDialog(mountStatus.DescribeDetail(), "Mount Detail");
                    }
                };

                return el;
            };

            var fireControlSystemMultiColumnListView = batteryStatusElement.Q<MultiColumnListView>("FireControlSystemMultiColumnListView");
            Utils.BindItemsAddedRemoved<FireControlSystemStatusRecord>(
                fireControlSystemMultiColumnListView,
                Utils.MakeDynamicResolveProvider<NavalCombatCore.BatteryStatus>(batteryStatusElement)
            );

            var targetColumn = fireControlSystemMultiColumnListView.columns["target"];
            targetColumn.makeCell = () =>
            {
                var el = targetColumn.cellTemplate.CloneTree();
                var setButton = el.Q<Button>("SetButton");
                setButton.clicked += () =>
                {
                    if (Utils.TryResolveCurrentValueForBinding(el, out FireControlSystemStatusRecord r))
                    {
                        GameManager.Instance.selectedFireControlSystemStatusRecordObjectId = r.objectId;
                        GameManager.Instance.state = GameManager.State.SelectingFireControlSystemTarget;
                        Hide();
                    }
                };
                return el;
            };

            var batteryDetailButton = batteryStatusElement.Q<Button>("BatteryDetailButton");
            batteryDetailButton.clicked += () =>
            {
                if (Utils.TryResolveCurrentValueForBinding(batteryDetailButton, out NavalCombatCore.BatteryStatus batteryStatus))
                {
                    DialogRoot.Instance.PopupMessageDialog(batteryStatus.DescribeDetail(), "Battery Detail");
                }
            };

            return batteryStatusElement;
        };

        var torpedoMountStatusMultiColumnListView = root.Q<MultiColumnListView>("TorpedoMountStatusMultiColumnListView");
        Utils.BindItemsAddedRemoved<MountStatusRecord>(torpedoMountStatusMultiColumnListView, () =>
        {
            return GameManager.Instance.selectedShipLog;
        });
        var torpedoMountStatusFiringTargetColumn = torpedoMountStatusMultiColumnListView.columns["firingTarget"];
        torpedoMountStatusFiringTargetColumn.makeCell = () =>
        {
            var el = torpedoMountStatusFiringTargetColumn.cellTemplate.CloneTree();

            var setButton = el.Q<Button>("SetButton");
            setButton.clicked += () =>
            {
                var ctx = setButton.GetHierarchicalDataSourceContext();
                if (PropertyContainer.TryGetValue(ctx.dataSource, ctx.dataSourcePath, out TorpedoMountStatusRecord torpedoMountStatusRecord))
                {
                    // Debug.Log(torpedoMountStatusRecord);
                    GameManager.Instance.selectedTorpedoMountStatusRecord = torpedoMountStatusRecord;
                    GameManager.Instance.state = GameManager.State.SelectingTorpedoFiringTarget;
                    Hide();
                }

            };

            return el;
        };

        // var rapidFiringStatusMultiColumnListView = root.Q<MultiColumnListView>("RapidFiringStatusMultiColumnListView");
        // Utils.BindItemsAddedRemoved<RapidFiringStatus>(rapidFiringStatusMultiColumnListView, () =>
        // {
        //     return GameManager.Instance.selectedShipLog;
        // });

        var rapidFiringStatusListView = root.Q<ListView>("RapidFiringStatusListView");
        Utils.BindItemsAddedRemoved<RapidFiringStatus>(rapidFiringStatusListView, () => GameManager.Instance.selectedShipLog);
        rapidFiringStatusListView.makeItem = () =>
        {
            var el = rapidFiringStatusListView.itemTemplate.CloneTree();

            Utils.BindItemsSourceRecursive(el);

            var detailButton = el.Q<Button>("DetailButton");
            detailButton.clicked += () =>
            {
                if (Utils.TryResolveCurrentValueForBinding(el, out RapidFiringStatus r))
                {
                    DialogRoot.Instance.PopupMessageDialog(r.DescribeDetail());
                }
            };

            var rapidFiringTargettingStatusMultiColumnListView = el.Q<MultiColumnListView>("RapidFiringTargettingStatusMultiColumnListView");

            Utils.BindItemsAddedRemoved<RapidFiringTargettingStatus>(
                rapidFiringTargettingStatusMultiColumnListView,
                Utils.MakeDynamicResolveProvider<RapidFiringStatus>(el)
            );

            var targetColumn = rapidFiringTargettingStatusMultiColumnListView.columns["target"];
            targetColumn.makeCell = () =>
            {
                var el = targetColumn.cellTemplate.CloneTree();

                var setButton = el.Q<Button>("SetButton");
                setButton.clicked += () =>
                {
                    if (Utils.TryResolveCurrentValueForBinding(el, out RapidFiringTargettingStatus r))
                    {
                        GameManager.Instance.selectedRapidFiringTargettingStatus = r;
                        GameManager.Instance.state = GameManager.State.SelectingRapidFiringTarget;
                        Hide();
                    }
                };

                return el;
            };

            return el;
        };

        var confirmButton = root.Q<Button>("ConfirmButton");
        confirmButton.clicked += Hide;

        var exportButton = root.Q<Button>("ExportButton");
        exportButton.clicked += () =>
        {
            // var content = GameManager.Instance.navalGameState.ShipLogsToXML();
            var content = NavalGameState.Instance.ShipLogsToXML();
            IOManager.Instance.SaveTextFile(content, "ShipLogs" + GameManager.scenarioSuffix, "xml");
        };

        var importButton = root.Q<Button>("ImportButton");
        importButton.clicked += () =>
        {
            IOManager.Instance.textLoaded += OnShipLogsXmlLoaded;
            IOManager.Instance.LoadTextFile("xml");
        };

        var setNamedShipButton = root.Q<Button>("SetNamedShipButton");
        setNamedShipButton.clicked += DialogRoot.Instance.PopupNamedShipSelctorDialogForShipLog;

        var resetDamageExpenditureStateButton = root.Q<Button>("ResetDamageExpenditureStateButton");
        resetDamageExpenditureStateButton.clicked += () =>
        {
            var selectedShipLog = GameManager.Instance.selectedShipLog;
            if (selectedShipLog == null)
                return;
            selectedShipLog.ResetDamageExpenditureState();
        };

        var gotoNamedShipButton = root.Q<Button>("GotoNamedShipButton");
        gotoNamedShipButton.clicked += () =>
        {
            var namedShip = GameManager.Instance.selectedShipLog?.namedShip;
            if (namedShip == null)
                return;
            var idx = NavalGameState.Instance.namedShips.IndexOf(namedShip);
            if (idx != -1)
            {
                Hide();
                NamedShipEditor.Instance.Show();
                NamedShipEditor.Instance.namedShipListView.SetSelection(idx);
            }
        };

        var resetAllStatesButton = root.Q<Button>("ResetAllStatesButton");
        resetAllStatesButton.clicked += () =>
        {
            foreach (var shipLog in NavalGameState.Instance.shipLogs)
            {
                shipLog.ResetDamageExpenditureState();
            }
        };

        var shipLogDetailButton = root.Q<Button>("ShipLogDetailButton");
        shipLogDetailButton.clicked += () =>
        {
            var ctx = shipLogDetailButton.GetHierarchicalDataSourceContext();
            if (PropertyContainer.TryGetValue(ctx.dataSource, ctx.dataSourcePath, out ShipLog shipLog))
            {
                // Debug.Log($"Detail Invoke: {mountStatus.objectId}");

                DialogRoot.Instance.PopupMessageDialog(shipLog.DescribeDetail(), "ShipLog Detail");
            }
        };

    }

    void OnShipLogsXmlLoaded(object sender, string text)
    {
        IOManager.Instance.textLoaded -= OnShipLogsXmlLoaded;

        // GameManager.Instance.navalGameState.ShipLogsFromXML(text);
        NavalGameState.Instance.ShipLogsFromXML(text);
        NavalGameState.Instance.ResetAndRegisterAll();
    }

    public void PopupWithSelection(ShipLog shipLog)
    {
        var idx = NavalGameState.Instance.shipLogs.IndexOf(shipLog);
        if(shipLog != null && idx != -1)
        {
            Show();
            shipLogListView.SetSelection(idx);
        }
    }
}