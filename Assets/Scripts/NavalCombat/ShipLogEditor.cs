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


public class ShipLogEditor : HideableDocument<ShipLogEditor>
{
    public VisualTreeAsset shipClassSelectorDialogDocument;
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

        var batteryStatusListView = root.Q<ListView>("BatteryStatusListView");
        Utils.BindItemsAddedRemoved<NavalCombatCore.BatteryStatus>(batteryStatusListView, () => selectedShipLog);
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
                            // Hide();
                            SoftHide();
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
                            // Hide();
                            SoftHide();
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
            return selectedShipLog;
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
                        SoftHide();
                    }
                }
            };

            return el;
        };

        var rapidFiringStatusListView = root.Q<ListView>("RapidFiringStatusListView");
        Utils.BindItemsAddedRemoved<RapidFiringStatus>(rapidFiringStatusListView, () => selectedShipLog);
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
                        SoftHide();
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

        var setNamedShipButton = root.Q<Button>("SetNamedShipButton");
        setNamedShipButton.clicked += DialogRoot.Instance.PopupNamedShipSelctorDialogForShipLog;

        var resetDamageExpenditureStateButton = root.Q<Button>("ResetDamageExpenditureStateButton");
        resetDamageExpenditureStateButton.clicked += () =>
        {
            if (selectedShipLog == null)
                return;
            selectedShipLog.ResetDamageExpenditureState(new());
        };

        var gotoNamedShipButton = root.Q<Button>("GotoNamedShipButton");
        gotoNamedShipButton.clicked += () =>
        {
            var namedShip = selectedShipLog?.namedShip;
            if (namedShip == null)
                return;

            var gameState = SuperGameState.Instance.GetCurrentGameState();
            var idx = gameState.namedShips.IndexOf(namedShip);
            if (idx != -1)
            {
                Hide();
                NamedShipEditor.Instance.Show();
                // NamedShipEditor.Instance.namedShipListView.Rebuild();
                // Data binding will be effective in the next frame, so we need to call the selection in the next frame.
                // StartCoroutine(SetSelectionForNamedShipListView(idx));
                BehaviourUtils.Instance.ScheduleToSetSelectionForListView(NamedShipEditor.Instance.namedShipListView, idx);
            }
        };

        var resetAllStatesButton = root.Q<Button>("ResetAllStatesButton");
        resetAllStatesButton.clicked += () =>
        {
            var gameState = SuperGameState.Instance.GetCurrentGameState();
            foreach (var shipLog in gameState.shipLogs)
            {
                shipLog.ResetDamageExpenditureState(new());
            }
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

        static string Localize(string key, params object[] args) => ServiceLocator.Get<ILocalizeService>().Get(key, args);

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

        Utils.BindIStrategicGroupMemberReferenceable(root, this);

        var loadedGroupListView = root.Q<ListView>("LoadedGroupListView");
        loadedGroupListView.makeItem = () =>
        {
            var el = loadedGroupListView.itemTemplate.CloneTree();
            Utils.BindGotoButton(el, this);
            return el;
        };

    }

    void OnShipLogsXmlLoaded(string text)
    {
        // IOManager.Instance.textLoaded -= OnShipLogsXmlLoaded;

        var gameState = SuperGameState.Instance.GetCurrentGameState();
        gameState.ShipLogsFromXML(text);
        gameState.ResetAndRegisterAll();
    }

    public void PopupWithSelection(ShipLog shipLog)
    {
        var gameState = SuperGameState.Instance.GetCurrentGameState();
        var idx = gameState.shipLogs.IndexOf(shipLog);
        if (shipLog != null && idx != -1)
        {
            Show();
            // shipLogListView.SetSelection(idx);
            BehaviourUtils.Instance.ScheduleToSetSelectionForListView(shipLogListView, idx);
        }
    }

    [CreateProperty]
    public AbstractGameState currentGameState => SuperGameState.Instance.GetCurrentGameState();

}