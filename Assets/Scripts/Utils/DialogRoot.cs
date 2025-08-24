using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using System.Linq;
using System;
using UnityEngine.SceneManagement;

using NavalCombatCore;
using StrategicCombatCore;
using CoreUtils;

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

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

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
            templateDataSource = null
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
