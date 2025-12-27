using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Collections;
using System.Xml.Serialization;
using System.Xml;
using System.IO;
using System.Linq;
using Unity.Properties;
using System;

using NavalCombatCore;
using CoreUtils;

public class ShipLogView
{
    public VisualElement root;

    VisualElement shipLogView;

    ShipLog GetSelectedShipLog()
    {
        if(Utils.TryResolveCurrentValueForBinding<ShipLog>(shipLogView, out var shipLog))
        {
            return shipLog;
        }
        return null;
    }

    public void Bind()
    {
        shipLogView = root.Q<VisualElement>("ShipLogView"); // selectedShipLog Provider
        // ShipLog GetSelectedShipLog()
        // {
        //     return _GetSelectedShipLog(shipLogView);
        // }

        var batteryStatusListView = root.Q<ListView>("BatteryStatusListView");
        Utils.BindItemsAddedRemoved<NavalCombatCore.BatteryStatus>(batteryStatusListView, () => GetSelectedShipLog());
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
                    if (SuperGameState.Instance.IsInNavalGame())
                    {
                        var ctx = setButton.GetHierarchicalDataSourceContext();
                        if (PropertyContainer.TryGetValue(ctx.dataSource, ctx.dataSourcePath, out MountStatusRecord mountStatus))
                        {
                            GameManager.Instance.selectedMountStatusRecordObjectId = mountStatus.objectId;
                            GameManager.Instance.state = GameManager.State.SelectingFiringTarget;
                            // SoftHide();
                            SwitchCenter.Instance.TryToSoftHideCurrent(); // Temp Hack
                        }
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
                    if (SuperGameState.Instance.IsInNavalGame())
                    {
                        if (Utils.TryResolveCurrentValueForBinding(el, out FireControlSystemStatusRecord r))
                        {
                            GameManager.Instance.selectedFireControlSystemStatusRecordObjectId = r.objectId;
                            GameManager.Instance.state = GameManager.State.SelectingFireControlSystemTarget;
                            // SoftHide();
                            SwitchCenter.Instance.TryToSoftHideCurrent(); // Temp Hack
                        }
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
            return GetSelectedShipLog();
        });
        var torpedoMountStatusFiringTargetColumn = torpedoMountStatusMultiColumnListView.columns["firingTarget"];
        torpedoMountStatusFiringTargetColumn.makeCell = () =>
        {
            var el = torpedoMountStatusFiringTargetColumn.cellTemplate.CloneTree();

            var setButton = el.Q<Button>("SetButton");
            setButton.clicked += () =>
            {
                if (SuperGameState.Instance.IsInNavalGame())
                {
                    var ctx = setButton.GetHierarchicalDataSourceContext();
                    if (PropertyContainer.TryGetValue(ctx.dataSource, ctx.dataSourcePath, out TorpedoMountStatusRecord torpedoMountStatusRecord))
                    {
                        // Debug.Log(torpedoMountStatusRecord);
                        GameManager.Instance.selectedTorpedoMountStatusRecord = torpedoMountStatusRecord;
                        GameManager.Instance.state = GameManager.State.SelectingTorpedoFiringTarget;
                        // SoftHide();
                        SwitchCenter.Instance.TryToSoftHideCurrent(); // Temp Hack
                    }
                }
            };

            return el;
        };

        var rapidFiringStatusListView = root.Q<ListView>("RapidFiringStatusListView");
        Utils.BindItemsAddedRemoved<RapidFiringStatus>(rapidFiringStatusListView, () => GetSelectedShipLog());
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
                        // SoftHide();
                        SwitchCenter.Instance.TryToSoftHideCurrent(); // Temp Hack
                    }
                };

                return el;
            };

            return el;
        };

        var resetDamageExpenditureStateButton = root.Q<Button>("ResetDamageExpenditureStateButton");
        resetDamageExpenditureStateButton.clicked += () =>
        {
            var selectedShipLog = GetSelectedShipLog();
            if (selectedShipLog == null)
                return;
            selectedShipLog.ResetDamageExpenditureState(new());
        };

        var setNamedShipButton = root.Q<Button>("SetNamedShipButton");
        setNamedShipButton.clicked += DialogRoot.Instance.PopupNamedShipSelctorDialogForShipLog;

        var gotoNamedShipButton = root.Q<Button>("GotoNamedShipButton");
        gotoNamedShipButton.clicked += () =>
        {
            var selectedShipLog = GetSelectedShipLog();
            var namedShip = selectedShipLog?.namedShip;
            SwitchCenter.Instance.SwitchToNamedShipView(namedShip);
        };

        var shipLogDetailButton = root.Q<Button>("ShipLogDetailButton");
        shipLogDetailButton.clicked += () =>
        {
            var ctx = shipLogDetailButton.GetHierarchicalDataSourceContext();
            if (PropertyContainer.TryGetValue(ctx.dataSource, ctx.dataSourcePath, out ShipLog shipLog))
            {
                // Debug.Log($"Detail Invoke: {mountStatus.objectId}");

                DialogRoot.Instance.PopupMessageDialog(shipLog.DescribeDetail(), Localize("ShipLog Detail"));
            }
        };

        var plotTrajectoryOnMapButton = root.Q<Button>("PlotTrajectoryOnMapButton");
        plotTrajectoryOnMapButton.clicked += () =>
        {
            if (SuperGameState.Instance.currentGameMode == GameMode.Naval)
            {
                Debug.Log("plot trajectory on map");

                if (Utils.TryResolveCurrentValueForBinding(plotTrajectoryOnMapButton, out ShipLog shipLog))
                {
                    DialogRoot.Instance.PopupPlotTrajectoryDialog(shipLog);
                }
            }
        };

        // Utils.BindIStrategicGroupMemberReferenceable(root, this);
        Utils.BindIStrategicGroupMemberReferenceable(root);

        var loadedGroupListView = root.Q<ListView>("LoadedGroupListView");
        loadedGroupListView.makeItem = () =>
        {
            var el = loadedGroupListView.itemTemplate.CloneTree();
            // Utils.BindGotoButton(el, this);
            Utils.BindGotoButton(el);
            return el;
        };
    }

    static string Localize(string key, params object[] args) => ServiceLocator.Get<ILocalizeService>().Get(key, args);
}

public class ShipLogEditor : HideableDocument<ShipLogEditor>
{
    // public VisualTreeAsset shipClassSelectorDialogDocument;
    public ListView shipLogListView;

    // protected override void Awake()
    // {
    //     base.Awake();
    //     Bind();
    // }

    public string selectedShipLogObjectId;

    [CreateProperty]
    public ShipLog selectedShipLog
    {
        get
        {
            return EntityManager.Instance.Get<ShipLog>(selectedShipLogObjectId);
        }
    }

    void OnEnable()
    {
        // Debug.LogWarning("ShipLogEditor OnEnable");
        Bind();

        var shipLogView = new ShipLogView()
        {
            root = root.Q<VisualElement>("ShipLogView")
        };
        shipLogView.Bind();
    }

    public EventHandler shown;

    protected override void OnShow()
    {
        shown?.Invoke(this, EventArgs.Empty);
    }

    // protected override void Awake()
    void Bind()
    {
        // base.Awake();

        // var sortingOrder = doc.sortingOrder;
        // Debug.Log($"ShipLogEditor sortingOrder={sortingOrder}");

        root.dataSource = this;

        // foreach (var listView in root.Query<BaseListView>().ToList())
        // {
        //     listView.SetBinding("itemsSource", new DataBinding());
        // }
        Utils.BindItemsSourceRecursive(root);

        shipLogListView = root.Q<ListView>("ShipLogListView");
        // shipLogListView.itemsAdded += Utils.MakeCallbackForItemsAdded<ShipLog>(shipLogListView);
        Utils.BindItemsAddedRemoved<ShipLog>(shipLogListView, () => null);

        shipLogListView.selectionChanged += (IEnumerable<object> objs) =>
        {
            var shipLog = objs.FirstOrDefault() as ShipLog;
            if (shipLog != null)
            {
                selectedShipLogObjectId = shipLog.objectId;
            }
        };

        var confirmButton = root.Q<Button>("ConfirmButton");
        confirmButton.clicked += Hide;

        var exportButton = root.Q<Button>("ExportButton");
        exportButton.clicked += () =>
        {
            var gameState = SuperGameState.Instance.GetCurrentGameState();
            var content = gameState.ShipLogsToXML();
            // IOManager.Instance.SaveTextFile(content, "ShipLogs" + GameManager.scenarioSuffix, "xml");
            IOManager.Instance.SaveTextFile(content, "ShipLogs.xml", "xml");
        };

        var importButton = root.Q<Button>("ImportButton");
        importButton.clicked += () =>
        {
            // IOManager.Instance.textLoaded += OnShipLogsXmlLoaded;
            IOManager.Instance.LoadTextFile(OnShipLogsXmlLoaded, "xml");
        };

        var resetAllStatesButton = root.Q<Button>("ResetAllStatesButton");
        resetAllStatesButton.clicked += () =>
        {
            var gameState = SuperGameState.Instance.GetCurrentGameState();
            foreach (var shipLog in gameState.shipLogs)
            {
                shipLog.ResetDamageExpenditureState(new());
                shipLog.logs.Clear();
            }
        };
    }

    static string Localize(string key, params object[] args) => ServiceLocator.Get<ILocalizeService>().Get(key, args);

    void OnShipLogsXmlLoaded(string text)
    {
        // IOManager.Instance.textLoaded -= OnShipLogsXmlLoaded;

        var gameState = SuperGameState.Instance.GetCurrentGameState();
        gameState.ShipLogsFromXML(text);
        gameState.ResetAndRegisterAll();
    }

    // public void PopupWithSelection(ShipLog shipLog)
    // {
    //     var gameState = SuperGameState.Instance.GetCurrentGameState();
    //     var idx = gameState.shipLogs.IndexOf(shipLog);
    //     if (shipLog != null && idx != -1)
    //     {
    //         Show();
    //         // shipLogListView.SetSelection(idx);
    //         BehaviourUtils.Instance.ScheduleToSetSelectionForListView(shipLogListView, idx);
    //     }
    // }

    [CreateProperty]
    public AbstractGameState currentGameState => SuperGameState.Instance.GetCurrentGameState();

}