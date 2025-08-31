using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using System.Linq;
using System;
using UnityEngine.SceneManagement;
using Unity.Properties;

using NavalCombatCore;
using StrategicCombatCore;
using CoreUtils;
using NavalCombat;

public class ScenarioPickerDialog // ScenarioPicker's root data source
{
    public List<string> scenarioNames = new();

    public string currentDescription;
    public Action<string> callbackOnceScenarioNameGet;

    public void Bind(TempDialog tempDialog)
    {
        tempDialog.onCreated += (sender, root) =>
        {
            // var root = tempDialog.root;
            Utils.BindItemsSourceRecursive(root);

            var scenarioListView = root.Q<ListView>("ScenarioListView");
            scenarioListView.selectionChanged += (IEnumerable<object> objects) =>
            {
                Debug.Log("scenarioListView.selectionChanged");

                var scenarioPath = objects.FirstOrDefault() as string;
                if (scenarioPath != null)
                {
                    var scenarioName = scenarioPath.Split("/").Last();
                    // Update information
                    // GameManager.Instance.StartLoadScenarioCoroutine(scenarioName);
                    currentDescription = "Fetching Preview... " + scenarioName; // TODO: Show more informative data like side's deployed units.
                    DialogRoot.Instance.StartCoroutine(
                        StreamingAssetReference.FetchScenarioFile(scenarioName, fullStateStr =>
                        {
                            var fullState = FullState.FromXML(fullStateStr);
                            var shipCount = fullState.navalGameState.shipLogs.Count(s => s.mapState == MapState.Deployed);
                            var dateTimeUTC = fullState.navalGameState.scenarioState.dateTime;
                            // var dateTimeLocal = fullState.viewState.
                            // TODO: Fetch class to find country info

                            var centerLat = fullState.viewState.GetCenterLatitude();
                            var centerLon = fullState.viewState.GetCenterLongitude();

                            var dateTimeLocal = fullState.navalGameState.scenarioState.GetLocalDateTime(centerLon);
                            var lines = new List<string>()
                            {
                                scenarioName,
                                $"UTC DateTime: {dateTimeUTC}",
                                $"Local DateTime: {dateTimeLocal}",
                                $"Ship Count (On Map): {shipCount}",
                                $"Latitude: {centerLat}, Longtitude: {centerLon}",
                                "Description:",
                                fullState.navalGameState.scenarioState.description
                            };
                            currentDescription = string.Join("\n", lines);
                        })
                    );
                }
            };
        };
        tempDialog.onConfirmed += (obj, root) =>
        {
            var scenarioListView = root.Q<ListView>("ScenarioListView");
            var scenarioName = scenarioListView.selectedItem as string;
            if (scenarioName != null)
            {
                // GameManager.Instance.StartLoadScenarioCoroutine(scenarioName);
                callbackOnceScenarioNameGet(scenarioName);
            }
        };
    }
}

public class SectorArcIndicatorBinder
{
    // VisualElement root;
    Dictionary<MountLocation, BatteryArcIndicator> uiMap;

    public void BindUI(VisualElement root)
    {

        uiMap = new Dictionary<MountLocation, BatteryArcIndicator>()
        {
            {MountLocation.PortForward, root.Q<BatteryArcIndicator>("PortForward")},
            {MountLocation.Forward, root.Q<BatteryArcIndicator>("Forward")},
            {MountLocation.StarboardForward, root.Q<BatteryArcIndicator>("StarboardForward")},
            {MountLocation.PortMidship, root.Q<BatteryArcIndicator>("PortMidship")},
            {MountLocation.Midship, root.Q<BatteryArcIndicator>("Midship")},
            {MountLocation.StarboardMidship, root.Q<BatteryArcIndicator>("StarboardMidship")},
            {MountLocation.PortAfter, root.Q<BatteryArcIndicator>("PortAfter")},
            {MountLocation.After, root.Q<BatteryArcIndicator>("After")},
            {MountLocation.StarboardAfter, root.Q<BatteryArcIndicator>("StarboardAfter")},
        };
    }

    public void BindBatteryData(ShipClass shipClass)
    {
        var updatedSet = new HashSet<MountLocation>();
        foreach (var grouping in shipClass.batteryRecords.SelectMany(btyRec => btyRec.mountLocationRecords).GroupBy(mntRec => mntRec.mountLocation))
        {
            updatedSet.Add(grouping.Key);
            if (uiMap.TryGetValue(grouping.Key, out var ui))
            {
                var startEndTopZeroCWAngles = grouping.SelectMany(g => g.mountArcs)
                    .Select(arcRec => (arcRec.startDeg, arcRec.startDeg + arcRec.CoverageDeg))
                    .ToList();

                // ui.startEndTopZeroCWAngles = startEndTopZeroCWAngles;
                ui.UpdateStartEndTopZeroCWAngles(startEndTopZeroCWAngles);
            }
        }
        foreach (var (mntLoc, ui) in uiMap)
        {
            if (!updatedSet.Contains(mntLoc))
            {
                ui.UpdateStartEndTopZeroCWAngles(new());
            }
        }
    }

    public void BindTorpedoData(ShipClass shipClass)
    {
        var updatedSet = new HashSet<MountLocation>();
        foreach (var grouping in shipClass.torpedoSector.mountLocationRecords.GroupBy(mntRec => mntRec.mountLocation))
        {
            if (uiMap.TryGetValue(grouping.Key, out var ui))
            {
                updatedSet.Add(grouping.Key);
                var startEndTopZeroCWAngles = grouping.SelectMany(g => g.mountArcs)
                    .Select(arcRec => (arcRec.startDeg, arcRec.startDeg + arcRec.CoverageDeg))
                    .ToList();
                ui.UpdateStartEndTopZeroCWAngles(startEndTopZeroCWAngles);
            }
        }
        foreach (var (mntLoc, ui) in uiMap)
        {
            if (!updatedSet.Contains(mntLoc))
            {
                ui.UpdateStartEndTopZeroCWAngles(new());
            }
        }
    }
}

public class PlotTrajectoryViewModel
{
    public string shipLogObjectId;

    [CreateProperty]
    public string shipLogName => EntityManager.Instance.Get<ShipLog>(shipLogObjectId)?.namedShip.name.GetMergedName();

    public Color32 color;

    [CreateProperty]
    public int red
    {
        get => color.r;
        set => color = new Color32((byte)value, color.g, color.b, 255);
    }

    [CreateProperty]
    public int green
    {
        get => color.g;
        set => color = new Color32(color.r, (byte)value, color.b, 255);
    }

    [CreateProperty]
    public int blue
    {
        get => color.b;
        set => color = new Color32(color.r, color.g, (byte)value, 255);
    }
}


public class DialogRoot : SingletonDocument<DialogRoot>
{
    public VisualTreeAsset shipLogSelectorDocument;
    public VisualTreeAsset leaderSelectorDocument;
    public VisualTreeAsset shipClassSelectorDocument;
    public VisualTreeAsset namedShipSelectorDocument;
    public VisualTreeAsset messageDialogDocument;
    public VisualTreeAsset streamingAssetReferenceDialogDocument;
    public VisualTreeAsset scenarioPickerDialogDocument;
    public VisualTreeAsset victoryStatusDocument;
    public VisualTreeAsset helpDialogDocument;
    public VisualTreeAsset locationLabelDialogDocument;
    public VisualTreeAsset subordinatePickerDialogDocument;
    public VisualTreeAsset strategicGroupPickerDialogDocument;
    public VisualTreeAsset gamePreferenceDialogDocument;
    public VisualTreeAsset batteryArcIndicatorDialogDocument;
    public VisualTreeAsset plotTrajectoryDialogDocument;
    public VisualTreeAsset eventStateEditorDialogDocument;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    public class EventStateEditorDialogDataSource
    {
        public EventState eventState;
        public EventItem currentEventItem;

        [CreateProperty]
        public bool isCurrentSelectionValid => currentEventItem != null;
    }

    public void PopupEventStateEditorDialog()
    {
        var dataSource = new EventStateEditorDialogDataSource()
        {
            eventState = EventState.Instance,
            currentEventItem = null
        };

        var tempDialog = new TempDialog()
        {
            root = root,
            template = eventStateEditorDialogDocument,
            templateDataSource = dataSource,
            draggable = true
        };

        tempDialog.onCreated += (sender, el) =>
        {
            Utils.BindItemsSourceRecursive(el);

            var objectListView = el.Q<ListView>("ObjectListView");
            Utils.BindItemsAddedRemoved<EventItem>(objectListView, () => null);

            objectListView.selectionChanged += (IEnumerable<object> objects) =>
            {
                Debug.Log("LeftObjectPickerRightEditorStrategic.selectionChanged");

                var obj = objects.FirstOrDefault() as EventItem;
                dataSource.currentEventItem = obj;
                // selectedId = obj?.objectId;
            };

            // el.Q<Button>("RefreshButton").clicked += TextReference.ClearCache; // TODO: switch local
            el.Q<Button>("RefreshAllButton").clicked += () =>
            {
                EventState.Instance.RefreshAll();
            };

            var refreshButton = el.Q<Button>("RefreshButton");
            refreshButton.clicked += () =>
            {
                Debug.Log(EventState.Instance.eventItems);
                if (Utils.TryResolveCurrentValueForBinding(refreshButton, out EventItem eventItem))
                {
                    BehaviourUtils.Instance.StartCoroutine(eventItem.Refresh());
                }
            };

            var pathField = el.Q<VisualElement>("PathField");
            PathReferenceBinder.BindJSReference(pathField);
            PathReferenceBinder.AddCallback(pathField, () =>{
                if (Utils.TryResolveCurrentValueForBinding(refreshButton, out EventItem eventItem))
                {
                    BehaviourUtils.Instance.StartCoroutine(eventItem.Refresh());
                }
            });
        };

        tempDialog.onConfirmed += (sender, el) =>
        {
            BehaviourUtils.Instance.StartCoroutine(EventState.Instance.SyncAndRegister());
        };

        tempDialog.Popup();
    }

    public void PopupPlotTrajectoryDialog(ShipLog shipLog)
    {
        var model = new PlotTrajectoryViewModel()
        {
            shipLogObjectId = shipLog.objectId,
            color = shipLog.shipClass.country == Country.China ? Color.red : Color.blue,
        };

        var tempDialog = new TempDialog()
        {
            root = root,
            template = plotTrajectoryDialogDocument,
            templateDataSource = model
        };

        tempDialog.onConfirmed += (sender, root) =>
        {
            Debug.Log("PopupPlotTrajectoryDialog Confirm");

            GameManager.Instance.AddShipLogTrajectory(EntityManager.Instance.Get<ShipLog>(model.shipLogObjectId), model.color);
        };

        tempDialog.Popup();
    }

    public void PopupBatteryArcIndicatorDialog(ShipClass shipClass)
    {
        var tempDialog = new TempDialog()
        {
            root = root,
            template = batteryArcIndicatorDialogDocument,
        };

        tempDialog.onCreated += (sender, root) =>
        {
            var binder = new SectorArcIndicatorBinder();
            binder.BindUI(root);
            binder.BindBatteryData(shipClass);
        };

        tempDialog.Popup();
    }

    public void PopupGamePreferenceDialog()
    {
        var tempDialog = new TempDialog()
        {
            root = root,
            template = gamePreferenceDialogDocument,
            templateDataSource = GamePreference.Instance,
        };

        tempDialog.Popup();
    }

    public void PopupStrategicGroupPickerDialog(Action<StrategicGroup> callback)
    {
        var tempDialog = new TempDialog()
        {
            root = root,
            template = strategicGroupPickerDialogDocument,
            templateDataSource = StrategicGameManager.Instance,
        };

        tempDialog.onConfirmed += (sender, el) =>
        {
            var objectListView = el.Q<ListView>("ObjectListView");
            var strategicGroup = objectListView.selectedItem as StrategicGroup;
            callback(strategicGroup);
        };

        tempDialog.Popup();
    }

    public void PopupSubordinatePickerDialog(Action<List<IStrategicGroupMemberReferenceable>> confirmCallback)
    {
        var subordinatePickerDialog = new SubordinatePickerDialog()
        {
            confirmCallback = confirmCallback
        };

        var tempDialog = new TempDialog()
        {
            root = root,
            template = subordinatePickerDialogDocument,
            templateDataSource = subordinatePickerDialog,
        };
        tempDialog.onCreated += subordinatePickerDialog.OnCreated;
        tempDialog.onConfirmed += subordinatePickerDialog.OnConfirmed;

        tempDialog.Popup();
    }

    public void PopupLocationLabelDialog(StrategicLocationLabel label)
    {
        var tempDialog = new TempDialog()
        {
            root = root,
            template = locationLabelDialogDocument,
            // templateDataSource = StreamingAssetReference.Instance
            templateDataSource = label
        };

        tempDialog.Popup();
    }

    public void PopupScenarioPickerDialogForScenarioSwitchInGame()
    {
        ManifestModelCache.Instance.CommitTask(manifestModel =>
        {
            var scenarioNames = manifestModel.scenarioFiles.Select(path => path.Split("/").Last()).ToList();
            var scenarioPickerDialog = new ScenarioPickerDialog()
            {
                scenarioNames = scenarioNames,
                callbackOnceScenarioNameGet = GameManager.Instance.StartLoadScenarioCoroutine
            };
            var tempDialog = new TempDialog()
            {
                root = root,
                template = scenarioPickerDialogDocument,
                templateDataSource = scenarioPickerDialog
            };
            scenarioPickerDialog.Bind(tempDialog);

            tempDialog.Popup();
        });
    }

    public void PopupScenarioPickerDialogForSwitchingSceneWithSelectedScenario()
    {
        ManifestModelCache.Instance.CommitTask(manifestModel =>
        {
            var scenarioNames = manifestModel.scenarioFiles.Select(path => path.Split("/").Last()).ToList();
            var scenarioPickerDialog = new ScenarioPickerDialog()
            {
                scenarioNames = scenarioNames,
                callbackOnceScenarioNameGet = scenarioName =>
                {
                    // GameManager.startupConfig.builtinScenName = scenarioName;
                    // GameManager.startupConfig.mode = GameManager.StartupConfig.Mode.BuiltinScenName;
                    GameManager.startupConfig = new()
                    {
                        builtinScenName = scenarioName,
                        mode = GameManager.StartupConfig.Mode.BuiltinScenName
                    };
                    SceneManager.LoadScene("Naval Game");
                }
            };
            var tempDialog = new TempDialog()
            {
                root = root,
                template = scenarioPickerDialogDocument,
                templateDataSource = scenarioPickerDialog,
                centering = false,
                fullScreen = true
            };
            scenarioPickerDialog.Bind(tempDialog);

            tempDialog.Popup();
        });
    }

    public void PopupStreamingAssetReferenceDialog()
    {
        var tempDialog = new TempDialog()
        {
            root = root,
            template = streamingAssetReferenceDialogDocument,
            // templateDataSource = StreamingAssetReference.Instance
            templateDataSource = ReferenceManager.Instance
        };

        tempDialog.Popup();
    }

    public void PopupMessageDialog(string message, string title = null)
    {
        var tempDialog = new TempDialog()
        {
            root = root,
            template = messageDialogDocument,
            templateDataSource = null,
            draggable = true
        };

        tempDialog.onCreated += (sender, el) =>
        {
            var contentTextField = el.Q<TextField>("ContentTextField");

            contentTextField.SetValueWithoutNotify(message);
            if (title != null)
            {
                var titleLabel = el.Q<Label>("TitleLabel");
                titleLabel.text = title;
            }
        };

        tempDialog.Popup();
    }

    public void PopupLeaderSelectorDialogForCallback(Action<Leader> callback)
    {
        var tempDialog = new TempDialog()
        {
            root = root,
            template = leaderSelectorDocument,
            templateDataSource = SuperGameState.Instance // GameManager.Instance
        };

        tempDialog.onConfirmed += (sender, el) =>
        {
            Debug.Log("tempDialog.onConfirmed");

            var leadersListView = el.Q<ListView>("LeadersListView");
            var leader = leadersListView.selectedItem as Leader;

            callback(leader);
        };

        tempDialog.Popup();
    }

    public void PopupLeaderSelectorDialogForSpecifyForGroup()
    {
        // var tempDialog = new TempDialog()
        // {
        //     root = root,
        //     template = leaderSelectorDocument,
        //     templateDataSource = SuperGameState.Instance // GameManager.Instance
        // };

        // tempDialog.onConfirmed += (sender, el) =>
        // {
        //     Debug.Log("tempDialog.onConfirmed");

        //     var leadersListView = el.Q<ListView>("LeadersListView");
        //     var leader = leadersListView.selectedItem as Leader;
        //     var selectedGroup = OOBEditor.Instance.currentSelectedShipGroup;

        //     if (leader != null && selectedGroup != null)
        //     {
        //         selectedGroup.leaderObjectId = leader.objectId;
        //     }
        // };

        // tempDialog.Popup();

        PopupLeaderSelectorDialogForCallback(leader =>
        {
            var selectedGroup = OOBEditor.Instance.currentSelectedShipGroup;

            if (leader != null && selectedGroup != null)
            {
                // selectedGroup.leaderObjectId = leader.objectId;
                selectedGroup.leaderReference.referenceObjectId = leader.objectId;
            }
        });
    }

    // public void PopupLeaderSelectorDialogForSpecifyForShipLog()
    // {
    //     PopupLeaderSelectorDialogForCallback(leader =>
    //     {
    //         var selectedShipLog = GameManager.Instance.selectedShipLog;

    //         if (leader != null && selectedShipLog != null)
    //         {
    //             selectedShipLog.leaderObjectId = leader.objectId;
    //         }
    //     });
    // }

    public void PopupLeaderSelectorDialogForNamedShip()
    {
        PopupLeaderSelectorDialogForCallback(leader =>
        {
            var selectedNamedShip = NamedShipEditor.Instance.selectedNamedShip;

            if (leader != null && selectedNamedShip != null)
            {
                // selectedNamedShip.defaultLeaderObjectId = leader.objectId;
                selectedNamedShip.defaultLeaderReference.referenceObjectId = leader.objectId;
            }
        });
    }

    public void PopupShipClassSelectorDialogForNamedShip()
    {
        var tempDialog = new TempDialog()
        {
            root = root,
            template = shipClassSelectorDocument,
            templateDataSource = SuperGameState.Instance // GameManager.Instance
        };

        tempDialog.onConfirmed += (sender, el) =>
        {
            // var selectedNamedShip = GameManager.Instance.selectedNamedShip;
            var selectedNamedShip = NamedShipEditor.Instance.selectedNamedShip;

            var shipClassListView = el.Q<ListView>("ShipClassListView");
            var selectedShipClass = shipClassListView.selectedItem as ShipClass;
            if (selectedNamedShip != null && selectedShipClass != null)
            {
                selectedNamedShip.shipClassObjectId = selectedShipClass.objectId;
            }
        };

        tempDialog.Popup();
    }

    public void PopupNamedShipSelctorDialogForShipLog()
    {
        var tempDialog = new TempDialog()
        {
            root = root,
            template = namedShipSelectorDocument,
            templateDataSource = SuperGameState.Instance // GameManager.Instance
        };

        tempDialog.onConfirmed += (sender, el) =>
        {
            // var selectedShipLog = GameManager.Instance.selectedShipLog;
            var selectedShipLog = ShipLogEditor.Instance.selectedShipLog;

            var namedShipListView = el.Q<ListView>("NamedShipListView");
            var namedShip = namedShipListView.selectedItem as NamedShip;
            if (selectedShipLog != null && namedShip != null)
            {
                selectedShipLog.namedShipObjectId = namedShip.objectId;
            }
        };

        tempDialog.Popup();
    }

    public void PopupShipLogSelectorDialogForRedeploy()
    {
        var tempDialog = new TempDialog()
        {
            root = root,
            template = shipLogSelectorDocument,
            templateDataSource = GameManager.Instance
        };

        tempDialog.onConfirmed += (sender, el) =>
        {
            Debug.Log("tempDialog.onConfirmed");

            var shipLogMultiColumnListView = el.Q<MultiColumnListView>("ShipLogMultiColumnListView");
            var selectedShipLog = shipLogMultiColumnListView.selectedItem as ShipLog;
            var latLon = GameManager.Instance.lastSelectedLatLon;
            if (selectedShipLog != null && latLon != null)
            {
                selectedShipLog.mapState = MapState.Deployed;
                selectedShipLog.position = latLon;
                // Set Default heading?
            }
        };

        tempDialog.Popup();
    }

    public void PopupShipLogSelectorDialogForAddShipLogToOOBItem()
    {
        var tempDialog = new TempDialog()
        {
            root = root,
            template = shipLogSelectorDocument,
            templateDataSource = GameManager.Instance
        };

        tempDialog.onConfirmed += (sender, el) =>
        {
            var addToShipGroup = OOBEditor.Instance.currentSelectedShipGroup;
            var shipLogMultiColumnListView = el.Q<MultiColumnListView>("ShipLogMultiColumnListView");
            var selectedShipLog = shipLogMultiColumnListView.selectedItem as ShipLog;

            if (addToShipGroup != null && selectedShipLog != null)
            {
                if (((IShipGroupMember)selectedShipLog).TryAttachTo(addToShipGroup))
                {
                    OOBEditor.Instance.Sync();
                }
                else
                {
                    Debug.LogWarning("Not attachable");
                }
            }
        };

        tempDialog.Popup();
    }

    public void PopupVictoryStatusDialog()
    {
        var victoryStatus = VictoryStatus.Generate(NavalGameState.Instance);

        var tempDialog = new TempDialog()
        {
            root = root,
            template = victoryStatusDocument,
            templateDataSource = victoryStatus
        };

        tempDialog.onCreated += (sender, root) =>
        {
            // SideVictoryStatusesListView
            // ShipTypeLossItemsMultiColumnListView

            Utils.BindItemsSourceRecursive(root);

            var sideVictoryStatusesListView = root.Q<ListView>("SideVictoryStatusesListView");
            sideVictoryStatusesListView.makeItem = () =>
            {
                var el = sideVictoryStatusesListView.itemTemplate.CloneTree();

                Utils.BindItemsSourceRecursive(el);

                return el;
            };
        };

        tempDialog.Popup();
    }

    public void PopupHelpDialogDocument()
    {
        var tempDialog = new TempDialog()
        {
            root = root,
            template = helpDialogDocument,
            templateDataSource = null,
            centering = false
        };

        tempDialog.Popup();
    }
}
