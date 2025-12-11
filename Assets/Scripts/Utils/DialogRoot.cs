using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using System.Linq;
using System;
using UnityEngine.SceneManagement;
using Unity.Properties;
using UnityEngine.Localization.Settings;
using System.Collections;

using NavalCombatCore;
using StrategicCombatCore;
using CoreUtils;
using NavalCombat;
using UnityEngine.Localization;

public class ScenarioPickerDialog // ScenarioPicker's root data source
{
    public List<string> scenarioNames = new();

    public string currentDescription;
    public Action<string> callbackOnceScenarioNameGet;

    static string Localize(string key, params object[] args) => ServiceLocator.Get<ILocalizeService>().Get(key, args);

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
                        StreamingAssetReference.Instance.FetchScenarioFile(scenarioName, fullStateStr =>
                        {
                            var fullState = FullState.FromXML(fullStateStr);
                            var shipCount = fullState.navalGameState.shipLogs.Count(s => s.mapState == MapState.Deployed);
                            var dateTimeUTC = fullState.navalGameState.scenarioState.dateTime;
                            // var dateTimeLocal = fullState.viewState.
                            // TODO: Fetch class to find country info

                            var centerLat = fullState.viewState.GetCenterLatitude();
                            var centerLon = fullState.viewState.GetCenterLongitude();

                            // var dateTimeLocal = fullState.navalGameState.scenarioState.GetLocalDateTime(centerLon);
                            var lines = new List<string>()
                            {
                                scenarioName,
                                Localize("Begin UTC Time: {0}", dateTimeUTC),
                                Localize("Begin Local DateTime: {0}", ScenarioState.GetLocalDateTimeOffset(centerLon, dateTimeUTC)),
                            };
                            if(fullState.navalGameState.scenarioState.hasEndDateTime)
                            {
                                var endDateTimeUTC = fullState.navalGameState.scenarioState.endDateTime;
                                lines.AddRange(new List<string>()
                                {
                                    Localize("End UTC DateTime: {0}", endDateTimeUTC),
                                    Localize("End Local DateTime: {0}", ScenarioState.GetLocalDateTimeOffset(centerLon, endDateTimeUTC)),
                                });
                            }
                            lines.AddRange(new List<string>()
                            {
                                Localize("Ship Count (On Map): {0}", shipCount),
                                Localize("Latitude: {0}, Longtitude: {1}", centerLat, centerLon),
                                Localize("Description:"),
                                // fullState.navalGameState.scenarioState.description
                                fullState.navalGameState.scenarioState.globalDescription.GetShortName()
                            });
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
    public VisualTreeAsset confirmDialogDocument;
    public VisualTreeAsset streamingAssetReferenceDialogDocument;
    public VisualTreeAsset scenarioPickerDialogDocument;
    public VisualTreeAsset victoryStatusDocument;
    public VisualTreeAsset helpDialogDocument;
    public VisualTreeAsset faqDialogDocument;
    public VisualTreeAsset locationLabelDialogDocument;
    public VisualTreeAsset subordinatePickerDialogDocument;
    public VisualTreeAsset strategicGroupPickerDialogDocument;
    public VisualTreeAsset gamePreferenceDialogDocument;
    public VisualTreeAsset batteryArcIndicatorDialogDocument;
    public VisualTreeAsset plotTrajectoryDialogDocument;
    public VisualTreeAsset eventStateEditorDialogDocument;
    public VisualTreeAsset weaponPickerDialogDocument;
    public VisualTreeAsset sideStatePickerDialogDocument;
    public VisualTreeAsset landUnitTemplateDialogDocument;
    public VisualTreeAsset subStrategicCombatDialogDocument;
    public VisualTreeAsset cellEditorDialogDocument;
    public VisualTreeAsset pendingNavalCombatDialogDocument;
    public VisualTreeAsset navalCombatResolverDialogDocument;
    public VisualTreeAsset oobTreeDialogDocument;
    public VisualTreeAsset landBattleDialogDocument;
    public VisualTreeAsset aiDialogDocument;
    public VisualTreeAsset insertShipComplexDialogDocument;
    public VisualTreeAsset forceBuilderDialogDocument;
    public VisualTreeAsset autoDeploymentDialogDocument;
    public VisualTreeAsset batteryRecordSelectorDialogDocument;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {

    }

    public void PopupBatteryRecordSelectorDialog(Action<BatteryRecord> callback)
    {
        var batteryRecordSelectorDialog = new BatteryRecordSelectorDialog()
        {
            callback=callback
        };

        var tempDialog = new TempDialog()
        {
            root=root,
            template=batteryRecordSelectorDialogDocument,
            templateDataSource=batteryRecordSelectorDialog
        };

        tempDialog.onCreated += batteryRecordSelectorDialog.OnCreated;
        tempDialog.onConfirmed += batteryRecordSelectorDialog.OnConfirm;

        tempDialog.Popup();

    }

    public void PopupAutoDeploymentDialog()
    {
        var autoDeploymentDialog = new AutoDeploymentDialog();

        var tempDialog = new TempDialog()
        {
            root=root,
            template=autoDeploymentDialogDocument,
            templateDataSource=autoDeploymentDialog
        };

        tempDialog.onCreated += autoDeploymentDialog.OnCreated;
        tempDialog.onConfirmed += autoDeploymentDialog.OnConfirm;

        tempDialog.Popup();
    }

    public void PopupForceBuilderDialog()
    {
        var forceBuilder = new ForceBuilder();

        var tempDialog = new TempDialog()
        {
            root=root,
            template=forceBuilderDialogDocument,
            templateDataSource=forceBuilder,
        };

        tempDialog.onCreated += forceBuilder.OnCreated;
        tempDialog.onConfirmed += forceBuilder.OnConfirm;
        
        tempDialog.Popup();
    }

    public void PopupAIDialog()
    {
        var topShipGroups = NavalGameState.Instance.shipGroups.Where(g => g.parentObjectId == null);
        var items = topShipGroups.Select(g => new AIDialogItem(){topGroup=g}).ToList();
        var aiDialog = new AIDialog()
        {
            items = items
        };
        var tempDialog = new TempDialog()
        {
            root=root,
            template=aiDialogDocument,
            templateDataSource=aiDialog,
        };
        // tempDialog.onCreated += aiDialog.OnCreated;
        
        tempDialog.Popup();
    }

    public void PopupLandBattleDialog(LandBattle landBattle)
    {
        // var landBattleDialog = new LandBattleDialog()
        // {
        //     landBattle = landBattle,
        //     attacker = landBattle.GetAttackerDynamic(),
        //     defender = landBattle.GetDefenderDynamic(),
        // };
        var landBattleDialogDynamic = new LandBattleDialogLazy()
        {
            landBattleId = landBattle.objectId
        };

        var tempDialog = new TempDialog()
        {
            root = root,
            template = landBattleDialogDocument,
            templateDataSource = landBattleDialogDynamic,
        };

        // tempDialog.onCreated += LandBattleDialog.OnCreated;

        // FIXME: Code smell

        var attacker = landBattle.GetAttackerDynamic();
        var defender = landBattle.GetDefenderDynamic();

        tempDialog.onCreated += (sender, root) => {

            LandBattleDialog.OnCreated(sender, root);

            attacker.battleLeader.portraitReference.RequestIfNotRequestedYetOtherwiseExecuteDirectly(styleBackground =>
            {
                var el = root.Q<VisualElement>("AttackerState").Q<VisualElement>("LeaderPortrait");
                el.style.backgroundImage = styleBackground;
            });

            defender.battleLeader.portraitReference.RequestIfNotRequestedYetOtherwiseExecuteDirectly(styleBackground =>
            {
                var el = root.Q<VisualElement>("DefenderState").Q<VisualElement>("LeaderPortrait");
                el.style.backgroundImage = styleBackground;
            });
        };

        tempDialog.Popup();
    }

    public void PopupOOBTreeDialog()
    {
        var tempDialog = new TempDialog()
        {
            root = root,
            template = oobTreeDialogDocument,
            templateDataSource = null,
            draggable = false
        };

        tempDialog.onCreated += (sender, el) =>
        {
            var oobTreeView = el.Q<TreeView>("OOBTreeView");

            var tree = new FullGroupTree();
            // var treeViewRootItems = 
            var treeViewerBuilder = new UITKTreeViewBuilder<IStrategicGroupMemberReferenceable, string>()
            {
                tree=tree
            };
            var rootItems = treeViewerBuilder.CreateTreeViewRootItems(StrategicGameState.Instance.strategicGroups);
            oobTreeView.SetRootItems(rootItems);
            oobTreeView.Rebuild();
            // oobTreeView.ExpandAll();
        };

        tempDialog.Popup();
    }

    public TempDialog PopupNavalCombatResolverDialog(PendingNavalCombat pendingNavalCombat)
    {
        // TODO: Very bad code smell (tangle) here, try to improve when I have enough spare time
        var resolver = new NavalCombatResolver()
        {
            root = null, // defer
            // cell = StrategicGameState.Instance.cellMatrix[pendingNavalCombat.xy.x, pendingNavalCombat.xy.y]
            pendingNavalCombat = pendingNavalCombat,
        };

        var tempDialog = new TempDialog()
        {
            root = root,
            template = navalCombatResolverDialogDocument,
            templateDataSource = resolver,
            draggable = false
        };

        resolver.closed += (sender, args) => tempDialog.Close();

        tempDialog.onCreated += (sender, el) =>
        {
            resolver.root = el;
            resolver.Bind();
        };

        tempDialog.Popup();

        return tempDialog;
    }

    public void PopupPendingNavalCombatDialog()
    {
        var tempDialog = new TempDialog()
        {
            root = root,
            template = pendingNavalCombatDialogDocument,
            templateDataSource = StrategicGameState.Instance,
            draggable = false
        };

        tempDialog.onCreated += (sender, el) =>
        {
            el.Q<Button>("ClearButton").clicked += () =>
            {
                StrategicGameState.Instance.pendingNavalCombats.Clear();
                tempDialog.root.Remove(el);
            };

            var pendingNavalCombatsListView = el.Q<ListView>("PendingNavalCombatsListView");
            pendingNavalCombatsListView.makeItem = () =>
            {
                var el = pendingNavalCombatsListView.itemTemplate.CloneTree();

                el.Q<Button>().clicked += () =>
                {
                    if (Utils.TryResolveCurrentValueForBinding(el, out PendingNavalCombat pendingNavalCombat))
                    {
                        var resolverDialog = PopupNavalCombatResolverDialog(pendingNavalCombat);
                        resolverDialog.onClosed += (sender, resolverEl) =>
                        {
                            if(StrategicGameState.Instance.pendingNavalCombats.Count == 0)
                            {
                                tempDialog.Close();
                            }
                        };
                    }
                };

                return el;
            };
        };
        
        tempDialog.Popup();
    }

    public void PopupCellEditorDialog(Cell cell)
    {
        var tempDialog = new TempDialog()
        {
            root = root,
            template = cellEditorDialogDocument,
            templateDataSource = cell,
            draggable = false
        };

        tempDialog.onCreated += (sender, el) =>
        {
            el.Q<Button>("SideObjectIdHexButton").clicked += () => PopupSideStatePickerDialog(sideState => cell.sideObjectIdHex = sideState.objectId);
            el.Q<Button>("SideObjectIdTopButton").clicked += () => PopupSideStatePickerDialog(sideState => cell.sideObjectIdTop = sideState.objectId);
            el.Q<Button>("SideObjectIdTopRightButton").clicked += () => PopupSideStatePickerDialog(sideState => cell.sideObjectIdTopRight = sideState.objectId);
            el.Q<Button>("SideObjectIdBottomRightButton").clicked += () => PopupSideStatePickerDialog(sideState => cell.sideObjectIdBottomRight = sideState.objectId);
            el.Q<Button>("SideObjectIdBottomButton").clicked += () => PopupSideStatePickerDialog(sideState => cell.sideObjectIdBottom = sideState.objectId);
            el.Q<Button>("SideObjectIdBottomLeftButton").clicked += () => PopupSideStatePickerDialog(sideState => cell.sideObjectIdBottomLeft = sideState.objectId);
            el.Q<Button>("SideObjectIdTopLeftButton").clicked += () => PopupSideStatePickerDialog(sideState => cell.sideObjectIdTopLeft = sideState.objectId);
        };

        tempDialog.onConfirmed += (sender, args) => StrategicGameState.Instance.InvokeMapCellUpdated(cell.x, cell.y);

        tempDialog.Popup();
    }

    public void PopupSubStrategicCombatDialog(SubStrategicCombat combat)
    {
        var tempDialog = new TempDialog()
        {
            root = root,
            template = subStrategicCombatDialogDocument,
            templateDataSource = combat,
            draggable = false
        };

        tempDialog.onCreated += (sender, el) =>
        {
            // var listViews = new ListView[] { el.Q<ListView>("AttackerListView"), el.Q<ListView>("DefenderListView") };
            var listViews = el.Query<ListView>("CombatItemListView").ToList();
            foreach (var listView in listViews)
            {
                Utils.BindItemsAddedRemoved<SubStrategicCombatItem>(listView, () => null);
                listView.makeItem = () =>
                {
                    var item = listView.itemTemplate.CloneTree();

                    var setButton = item.Q<Button>("SetButton");
                    setButton.clicked += () =>
                    {
                        if (Utils.TryResolveCurrentValueForBinding(setButton, out StrategicGroupMemberReference fieldReference))
                        {
                            PopupSubordinatePickerDialog(selectedReferenceables =>
                            {
                                var selectedReferenceable = selectedReferenceables.FirstOrDefault();
                                if (selectedReferenceable != null)
                                {
                                    fieldReference.referenceId = selectedReferenceable.objectId;
                                }
                            }, SubordinatePickerDialog.Mode.Free);
                        }
                    };

                    // StrategicGroupEditor.Instance.BindGotoButton(item);
                    Utils.BindGotoButton(item, null); // TODO: Remove strange reference of StrategicGroupEditor

                    return item;
                };
            }
        };

        tempDialog.Popup();
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
            draggable = false
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
            PathReferenceBinder.AddCallback(pathField, () =>
            {
                if (Utils.TryResolveCurrentValueForBinding(refreshButton, out EventItem eventItem)) // TODO: Temp Hack
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

        tempDialog.onCreated += (sender, el) =>
        {
            var localeDropdownField = el.Q<DropdownField>("LocaleDropdownField");
            StartCoroutine(GamePreference.Instance.SetupLocale(localeDropdownField));
        };

        tempDialog.onConfirmed += (sender, el) =>
        {
            GamePreference.Instance.SaveToPlayerPrefs();
        };

        tempDialog.Popup();
    }

    // IEnumerator SetupLocale(DropdownField localeDropdownField)
    // {
    //     yield return LocalizationSettings.InitializationOperation;

    //     localeDropdownField.choices = LocalizationSettings.AvailableLocales.Locales.Select(LocaleToNativeName).ToList();

    //     // LocalizationSettings.SelectedLocale.Identifier.CultureInfo.NativeName
    //     // en
    //     // ja
    //     // zh-Hans
    //     // zh-Hant

    //     var locales = LocalizationSettings.AvailableLocales.Locales;
    //     for (var i = 0; i < locales.Count; i++)
    //         if (LocaleToNativeName(locales[i]) == LocaleToNativeName(LocalizationSettings.SelectedLocale))
    //             localeDropdownField.index = i;

    //     localeDropdownField.RegisterValueChangedCallback(evt => GamePreference.Instance.SwitchToLocaleByName(evt.newValue));
    // }

    // static string LocaleToNativeName(UnityEngine.Localization.Locale locale) => locale.Identifier.CultureInfo.NativeName;
    // static string LocaleToNativeName(UnityEngine.Localization.Locale locale)
    // {
    //     var name = locale.Identifier.CultureInfo.Name;
    //     switch (name)
    //     {
    //         case "en":
    //             return "English";
    //         case "ja":
    //             return "日本語";
    //         case "zh-Hans":
    //             return "简体中文";
    //         case "zh-Hant":
    //             return "繁體中文";
    //         default:
    //             return name;
    //     }
    // }

    // static void SwitchToLocaleByName(string s)
    // {
    //     var selectedLocale = LocalizationSettings.AvailableLocales.Locales.FirstOrDefault(locale => LocaleToNativeName(locale) == s);

    //     if (selectedLocale != null)
    //     {
    //         LocalizationSettings.SelectedLocale = selectedLocale;
    //         GamePreference.Instance.SetShortLabelLanguageTypeByLocale(selectedLocale);
    //     }
    // }

    // static void SetShortLabelLanguageTypeByLocale(Locale locale)
    // {
    //     GamePreference.Instance.shortLabelLanguageType = locale.Identifier.CultureInfo.Name switch
    //     {
    //         "en" => LanguageType.English,
    //         "ja" => LanguageType.Japanese,
    //         "zh-Hans" => LanguageType.ChineseSimplified,
    //         "zh-Hant" => LanguageType.ChineseTraditional,
    //         _ =>LanguageType.English
    //     };
    // }

    public void PopupLandUnitTemplatePickerDialog(Action<LandUnitTemplate> callback)
    {
        var tempDialog = new TempDialog()
        {
            root = root,
            template = landUnitTemplateDialogDocument,
            templateDataSource = StrategicGameManager.Instance,
        };

        tempDialog.onConfirmed += (sender, el) =>
        {
            var objectListView = el.Q<ListView>("ObjectListView");
            var landUnitTemplate = objectListView.selectedItem as LandUnitTemplate;
            callback(landUnitTemplate);
        };

        tempDialog.Popup();
    }

    public void PopupWeaponPickerDialog(Action<Weapon> callback)
    {
        var tempDialog = new TempDialog()
        {
            root = root,
            template = weaponPickerDialogDocument,
            templateDataSource = StrategicGameManager.Instance,
        };

        tempDialog.onConfirmed += (sender, el) =>
        {
            var objectListView = el.Q<ListView>("ObjectListView");
            var weapon = objectListView.selectedItem as Weapon;
            callback(weapon);
        };

        tempDialog.Popup();
    }

    public void PopupSideStatePickerDialog(Action<SideState> callback)
    {
        var tempDialog = new TempDialog()
        {
            root = root,
            template = sideStatePickerDialogDocument,
            templateDataSource = StrategicGameManager.Instance,
        };

        tempDialog.onConfirmed += (sender, el) =>
        {
            var objectListView = el.Q<ListView>("ObjectListView");
            var sideState = objectListView.selectedItem as SideState;
            callback(sideState);
        };

        tempDialog.Popup();
    }

    public void PopupStrategicGroupPickerDialog(Action<StrategicGroup> callback, Func<StrategicGroup, bool> filter = null)
    {
        var strategicGroups = StrategicGameManager.Instance.gameState.strategicGroups;
        if (filter != null)
        {
            strategicGroups = strategicGroups.Where(filter).ToList();
        }

        var tempDialog = new TempDialog()
        {
            root = root,
            template = strategicGroupPickerDialogDocument,
            // templateDataSource = StrategicGameManager.Instance,
            templateDataSource = strategicGroups
        };

        tempDialog.onConfirmed += (sender, el) =>
        {
            var objectListView = el.Q<ListView>("ObjectListView");
            var strategicGroup = objectListView.selectedItem as StrategicGroup;
            callback(strategicGroup);
        };

        tempDialog.Popup();
    }

    public void PopupSubordinatePickerDialog(Action<List<IStrategicGroupMemberReferenceable>> confirmCallback, SubordinatePickerDialog.Mode mode)
    {
        var subordinatePickerDialog = new SubordinatePickerDialog()
        {
            confirmCallback = confirmCallback,
            mode = mode
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

    public void PopupConfirmDialog(string message, Action confirmCallback, string title = null)
    {
        var tempDialog = new TempDialog()
        {
            root = root,
            template = confirmDialogDocument,
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

        tempDialog.onConfirmed += (sender, el) => confirmCallback();

        tempDialog.Popup();
    }

    static string Localize(string key, params object[] args) => ServiceLocator.Get<ILocalizeService>().Get(key, args);

    public void PopupConfirmOpenURLDialog(string url, string title = null)
    {
        PopupConfirmDialog(
            Localize("Confirm to open url {0} ?", url),
            () => Application.OpenURL(url),
            title
        );
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

    public void PopupInsertShipComplexDialog()
    {
        var insertShipComplexDialog = new InsertShipComplexDialog();

        var tempDialog = new TempDialog()
        {
            root = root,
            template = insertShipComplexDialogDocument,
            templateDataSource = insertShipComplexDialog
        };

        tempDialog.onConfirmed += insertShipComplexDialog.OnConfirm;
        tempDialog.onCreated += insertShipComplexDialog.OnCreated;
        tempDialog.confirmCheck = insertShipComplexDialog.ConfirmCheck;

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

    public void PopupVictoryStatusDialog(VictoryStatus victoryStatus)
    {
        // StrategicGameManager.startupConfig.victoryStatus
        // var victoryStatus = VictoryStatus.Generate(NavalGameState.Instance);

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
    
    public void PopupFAQDialogDocument()
    {
        var tempDialog = new TempDialog()
        {
            root = root,
            template = faqDialogDocument,
            templateDataSource = null,
            centering = false
        };

        tempDialog.Popup();
    }
}
