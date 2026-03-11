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
using GeographicLib;
using YYZ;

public class ScenarioPickerDialog // ScenarioPicker's root data source
{
    public List<string> scenarioNames = new();

    public string currentDescription;
    public Action<string> callbackOnceScenarioNameGet;
    public NavalGameState currentGameState;

    static string Localize(string key, params object[] args) => ServiceLocator.Get<ILocalizeService>().Get(key, args);
    static string LocalizeEnum<T>(T obj) => ServiceLocator.Get<ILocalizeService>().GetEnum(obj);

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
                                Localize("Visibility: {0}", LocalizeEnum(fullState.navalGameState.scenarioState.visibility)),
                                Localize("Sea State (Beaufort): {0}", fullState.navalGameState.scenarioState.seaStateBeaufort),
                                Localize("Description:"),
                                // fullState.navalGameState.scenarioState.description
                                fullState.navalGameState.scenarioState.globalDescription.GetShortName()
                            });
                            currentDescription = string.Join("\n", lines);

                            // currentBackground = fullState.navalGameState.scenarioState.backgroundPictureReference.pictureStyleBackground;
                            currentGameState = fullState.navalGameState;
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

    public bool plotTimestamp = true;
    public int timestampIntervalMinutes = 15;
}

public class FollowFormationDialogModel
{
    [CreateProperty]
    public float followDistanceYards { get; set; } = 500f;
}

public enum RelativeFormationMode
{
    KeepCurrentPosition,
    LineAbreast,
    LineOfBearing,
}

public class RelativeFormationDialogModel
{
    [CreateProperty]
    public int modeValue { get; set; } = (int)RelativeFormationMode.KeepCurrentPosition;

    [CreateProperty]
    public float angleDeg { get; set; } = 90f;

    [CreateProperty]
    public float distanceYards { get; set; } = 250f;

    [CreateProperty]
    public bool isSymmetric { get; set; }

    [CreateProperty]
    public bool absolute { get; set; }

    public RelativeFormationMode mode => (RelativeFormationMode)modeValue;
}


public class DialogRoot : SingletonDocument<DialogRoot>
{
    public VisualTreeAsset shipLogSelectorDocument;
    public VisualTreeAsset leaderSelectorDocument;
    public VisualTreeAsset shipClassSelectorDocument;
    public VisualTreeAsset namedShipSelectorDocument;
    public VisualTreeAsset messageDialogDocument;
    public VisualTreeAsset confirmDialogDocument;
    public VisualTreeAsset followFormationDialogDocument;
    public VisualTreeAsset shipClassPlaceholderGeneratorDialogDocument;
    public VisualTreeAsset relativeFormationDialogDocument;
    public VisualTreeAsset preScenarioDamageDialogDocument;
    public VisualTreeAsset streamingAssetReferenceDialogDocument;
    public VisualTreeAsset scenarioPickerDialogDocument;
    public VisualTreeAsset victoryStatusDocument;
    public VisualTreeAsset helpDialogDocument;
    public VisualTreeAsset faqDialogDocument;
    public VisualTreeAsset locationLabelDialogDocument;
    public VisualTreeAsset navalLocationLabelEditorDialogDocument;
    public VisualTreeAsset shipGroupRemarkDialogDocument;
    public VisualTreeAsset locationLabelsEditorDialogDocument;
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
    public VisualTreeAsset rapidFireBatteryRecordSelectorDialogDocument;
    public VisualTreeAsset torpedoSectorSelectorDialogDocument;
    public VisualTreeAsset scenarioStateEditorDialogDocument;
    public VisualTreeAsset vladivostokSquadronRaidingSideSelectorDialogDocument;
    public VisualTreeAsset strategicScenarioStateEditorDialogDocument;
    public VisualTreeAsset unbindHitAreaDialogDocument;
    public VisualTreeAsset strategicGroupDialogDocument;
    public VisualTreeAsset shipLogDialogDocument;
    public VisualTreeAsset landUnitDialogDocument;
    public VisualTreeAsset strategicMissionSelectorDialogDocument;
    public VisualTreeAsset createMissionDialogDocument;
    public VisualTreeAsset hostDialogDocument;
    public VisualTreeAsset clientDialogDocument;
    public VisualTreeAsset pointListEditorDialogDocument;
    public VisualTreeAsset rectangleEditorDialogDocument;
    public VisualTreeAsset strategicStartupDialogDocument;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {

    }

    public void PopupStrategicStartupDialog()
    {
        var tempDialog = new TempDialog()
        {
            root=root,
            template=strategicStartupDialogDocument,
        };

        tempDialog.onCreated += (sender, el) =>
        {
            MainMenu.RegisterStrategicStartup(el);
        };

        tempDialog.Popup();
    }

    public void PopupRectangleEditorDialog(Action callback)
    {
        var tempDialog = new TempDialog()
        {
            root=root,
            template=rectangleEditorDialogDocument,
            templateDataSource=StrategicGameManager.Instance,
            positionMode=TempDialog.PositionMode.Left
        };

        tempDialog.onCreated += (sender, el) =>
        {
            el.Q<Button>("ResetButton").clicked += () =>
            {
                // StrategicGameManager.Instance.currentEditingPointList?.Clear();
                var rect = StrategicGameManager.Instance.currentEditingRect;
                if(rect != null)
                {
                    rect.xy1 = null;
                    rect.xy2 = null;
                }
            };
        };

        tempDialog.onConfirmed += (sender, el) =>
        {
            callback();
        };

        tempDialog.Popup();
    }

    public void PopupPointListEditorDialog(Action callback)
    {
        var tempDialog = new TempDialog()
        {
            root=root,
            template=pointListEditorDialogDocument,
            templateDataSource=StrategicGameManager.Instance,
            positionMode=TempDialog.PositionMode.Left
        };

        tempDialog.onCreated += (sender, el) =>
        {
            el.Q<Button>("ResetButton").clicked += () =>
            {
                StrategicGameManager.Instance.currentEditingPointList?.Clear();
            };
        };

        tempDialog.onConfirmed += (sender, el) =>
        {
            callback();
        };

        tempDialog.Popup();
    }

    public void PopupClientDialog()
    {
        var tempDialog = new TempDialog()
        {
            root=root,
            template=clientDialogDocument,
            templateDataSource=GameManager.Instance,
            positionMode=TempDialog.PositionMode.Left
        };

        tempDialog.onCreated += (sender, el) =>
        {
            GameManager.Instance.networkingName = GameManager.Instance.networkingName == GameManager.defaultNetworkingName ? "Client" : GameManager.Instance.networkingName;
        
            el.Q<Button>("ConnectButton").clicked += GameManager.Instance.DoConnect;
            el.Q<Button>("DisconnectButton").clicked += GameManager.Instance.DoDisconnect;

            var listView = el.Q<ListView>();
            listView.makeItem = () =>
            {
                var ret = listView.itemTemplate.CloneTree();
                Utils.BindItemsSourceRecursive(ret);
                return ret;
            };
        };

        tempDialog.Popup();
    }

    public void PopupHostDialog()
    {
        var tempDialog = new TempDialog()
        {
            root=root,
            template=hostDialogDocument,
            templateDataSource=GameManager.Instance,
            positionMode=TempDialog.PositionMode.Left
        };

        tempDialog.onCreated += (sender, el) =>
        {
            GameManager.Instance.networkingName = GameManager.Instance.networkingName == GameManager.defaultNetworkingName ? "Host" : GameManager.Instance.networkingName;

            el.Q<Button>("StartHostButton").clicked += GameManager.Instance.DoStartHost;
            el.Q<Button>("StopHostButton").clicked += GameManager.Instance.DoDisconnect;

            var listView = el.Q<ListView>();
            listView.makeItem = () =>
            {
                var ret = listView.itemTemplate.CloneTree();
                Utils.BindItemsSourceRecursive(ret);
                return ret;
            };
        };

        tempDialog.Popup();
    }

    public void PopupCreateMissionDialog(Action<StrategicMission> callback)
    {
        var createMissionDialog = new CreateMissionDialog()
        {
            callback=callback
        };

        var tempDialog = new TempDialog()
        {
            root=root,
            template=createMissionDialogDocument,
            templateDataSource=createMissionDialog
        };

        tempDialog.onConfirmed += (sender, el) =>
        {
            createMissionDialog.OnConfirm();
        };

        tempDialog.Popup();
    }


    public TempDialog PopupLandUnitDialog(LandUnit landUnit)
    {
        var tempDialog = new TempDialog()
        {
            root=root,
            template=landUnitDialogDocument,
            templateDataSource=landUnit
        };

        tempDialog.onCreated += (sender, el) =>
        {
            var binder = new LandUnitView(){root=el};
            binder.Bind();
        };

        tempDialog.Popup();

        return tempDialog;
    }

    public TempDialog PopupShipLogDialog(ShipLog shipLog)
    {
        var tempDialog = new TempDialog()
        {
            root=root,
            template=shipLogDialogDocument,
            templateDataSource=shipLog
        };

        tempDialog.onCreated += (sender, el) =>
        {
            var binder = new ShipLogView(){root=el};
            binder.Bind();
        };

        tempDialog.Popup();

        return tempDialog;
    }

    // public TempDialog BuildShipLogDialog(ShipLog shipLog)
    // {
    //     var tempDialog = new TempDialog()
    //     {
    //         root=root,
    //         template=shipLogDialogDocument,
    //         templateDataSource=shipLog
    //     };

    //     tempDialog.onCreated += (sender, el) =>
    //     {
    //         var binder = new ShipLogView(){root=el};
    //         binder.Bind();
    //     };

    //     return tempDialog;
    // }

    public TempDialog PopupStrategicGroupDialog(StrategicGroup strategicGroup)
    {
        var tempDialog = new TempDialog()
        {
            root=root,
            template=strategicGroupDialogDocument,
            templateDataSource=strategicGroup
        };

        tempDialog.onCreated += (sender, el) =>
        {
            var binder = new StrategicGroupView(){root=el};
            binder.Bind();
        };

        tempDialog.Popup();

        return tempDialog;
    }

    public void PopupUnbindHitAreaDialog(HitArea hitArea)
    {
        var unbindHitAreaDialog = new UnbindHitAreaDialog()
        {
            currentHitArea=hitArea
        };

        var tempDialog = new TempDialog()
        {
            root=root,
            template=unbindHitAreaDialogDocument,
            templateDataSource=unbindHitAreaDialog
        };

        tempDialog.onCreated += unbindHitAreaDialog.OnCreated;
        tempDialog.onConfirmed += unbindHitAreaDialog.OnConfirm;

        tempDialog.Popup();
    }

    public void PopupStrategicScenarioStateEditorDialog()
    {
        var strategicScenarioStateEditor = new StrategicScenarioStateEditor();

        var tempDialog = new TempDialog()
        {
            root=root,
            template=strategicScenarioStateEditorDialogDocument,
            templateDataSource=strategicScenarioStateEditor
        };

        tempDialog.onCreated += strategicScenarioStateEditor.OnCreated;
        tempDialog.onConfirmed += strategicScenarioStateEditor.OnConfirm;

        tempDialog.Popup();
    }

    public void PopupVladivostokSquadronRaidingSideSelectorDialog()
    {
        var vladivostokSquadronRaidingSideSelector = new VladivostokSquadronRaidingSideSelector();

        var tempDialog = new TempDialog()
        {
            root=root,
            template=vladivostokSquadronRaidingSideSelectorDialogDocument,
            templateDataSource=vladivostokSquadronRaidingSideSelector
        };

        // tempDialog.onCreated += torpedoSectorSelectorDialog.OnCreated;
        // tempDialog.onConfirmed += torpedoSectorSelectorDialog.OnConfirm;

        tempDialog.Popup();
    }

    public void PopupStrategicMissionSelectorDialogDocument(Action<StrategicMission> callback, StrategicMission parentMission)
    {
        var selectorDialog = new NamedSelector<StrategicMission>()
        {
            fullObjects = StrategicGameState.Instance.missions.Where(m => m.parentMissionRef.Get() == null && parentMission != m).ToList(),
            callback = callback
        };
        selectorDialog.RefreshFilter();

        var tempDialog = new TempDialog()
        {
            root=root,
            template=strategicMissionSelectorDialogDocument,
            templateDataSource=selectorDialog
        };

        // tempDialog.onCreated += selectorDialog.OnCreated;
        tempDialog.onConfirmed += selectorDialog.OnConfirm;

        tempDialog.Popup();
    }


    public void PopupTorpedoSectorSelectorDialog(Action<ShipClass> callback)
    {
        var torpedoSectorSelectorDialog = new TorpedoSectorSelectorDialog()
        {
            callback=callback
        };

        var tempDialog = new TempDialog()
        {
            root=root,
            template=torpedoSectorSelectorDialogDocument,
            templateDataSource=torpedoSectorSelectorDialog
        };

        tempDialog.onCreated += torpedoSectorSelectorDialog.OnCreated;
        tempDialog.onConfirmed += torpedoSectorSelectorDialog.OnConfirm;

        tempDialog.Popup();
    }

    public void PopupRapidFireBatteryRecordSelectorDialog(Action<RapidFireBatteryRecord> callback)
    {
        var rapidFireBatteryRecordSelectorDialog = new RapidFireBatteryRecordSelectorDialog()
        {
            callback=callback
        };

        var tempDialog = new TempDialog()
        {
            root=root,
            template=rapidFireBatteryRecordSelectorDialogDocument,
            templateDataSource=rapidFireBatteryRecordSelectorDialog
        };

        tempDialog.onCreated += rapidFireBatteryRecordSelectorDialog.OnCreated;
        tempDialog.onConfirmed += rapidFireBatteryRecordSelectorDialog.OnConfirm;

        tempDialog.Popup();
    }

    public void PopupScenarioStateEditor()
    {
        var scenarioStateEditor = new ScenarioStateEditor()
        {
            timeZoneOffset = GameManager.Instance.GetTimeZoneOffsetByLatestHoveringLocation()
        };

        var tempDialog = new TempDialog()
        {
            root=root,
            template=scenarioStateEditorDialogDocument,
            templateDataSource=scenarioStateEditor
        };

        tempDialog.onCreated += scenarioStateEditor.OnCreated;

        tempDialog.Popup();
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
        tempDialog.confirmCheck = forceBuilder.ConfirmCheck;
        
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

    public void PopupOOBTreeDialog(List<StrategicGroup> viewableGroups)
    {
        var tempDialog = new TempDialog()
        {
            root = root,
            template = oobTreeDialogDocument,
            templateDataSource = null,
            // draggable = false
        };

        tempDialog.onCreated += (sender, el) =>
        {
            var oobTreeView = el.Q<TreeView>("OOBTreeView");

            // var tree = new FullGroupTree();
            // var treeViewerBuilder = new UITKTreeViewBuilder<IStrategicGroupMemberReferenceable, string>()
            // {
            //     tree=tree
            // };

            // var viewableGroups = StrategicGameState.Instance.strategicGroups;

            var tree = new FullGroupTreeNameLink();
            var treeViewerBuilder = new UITKTreeViewBuilder<IStrategicGroupMemberReferenceable, IStrategicGroupMemberReferenceable>()
            {
                tree=tree
            };
            var rootItems = treeViewerBuilder.CreateTreeViewRootItems(viewableGroups);
            oobTreeView.SetRootItems(rootItems);

            tree.BindMakeItemBindItem(oobTreeView);

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
            // draggable = false
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
            // draggable = false
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
            // draggable = false
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

            var cellConnectionsMultiColumnListView = el.Q<MultiColumnListView>("CellConnectionsMultiColumnListView");
            
            // It's easier to write following compared to "Data Binding Gymnastics" and hack Add Removed callback
            var addConnectionButton = el.Q<Button>("AddConnectionButton");
            var deleteConnectionButton = el.Q<Button>("DeleteConnectionButton");

            addConnectionButton.clicked += () =>
            {
                StrategicGameManager.Instance.ScheduleOneshotCellClickCallback(otherCell =>
                {
                    StrategicGameManager.Instance.mapEditMode = StrategicMapEditMode.Select;

                    // otherCell.CellConnections.FirstOrDefault(c => c.GetOther() == cell);
                    var selfMatched = cell.CellConnections.FirstOrDefault(c => c.GetOther() == otherCell);
                    if(selfMatched == null)
                    {
                        cell.CellConnections.Add(new()
                        {
                            self=cell.ToXY(),
                            other=otherCell.ToXY(),
                        });
                        otherCell.CellConnections.Add(new()
                        {
                            self=otherCell.ToXY(),
                            other=cell.ToXY(),
                        });
                    }
                });
            };

            deleteConnectionButton.clicked += () =>
            {
                if(cellConnectionsMultiColumnListView.selectedItem is CellConnection cellConnection && cellConnection != null)
                {
                    cell.CellConnections.Remove(cellConnection);
                    var otherCell = cellConnection.GetOther();
                    var otherConnection = otherCell.CellConnections.FirstOrDefault(conn => conn.GetOther() == cell);
                    // var otherConnection = cellConnection.GetOtherConnectionToSelf();
                    if(otherConnection != null)
                    {
                        otherCell.CellConnections.Remove(otherConnection);
                    }
                }
            };

            el.Q<Button>("RecalculateCostButton").clicked += () =>
            {
                // TODO: Add grid system's calculation
                foreach(var areaCell in StrategicGameState.Instance.areaCells)
                {
                    foreach(var conn in areaCell.CellConnections)
                    {
                        // conn.costCoef = 1;
                        var otherCell = conn.GetOther();

                        Geodesic.WGS84.Inverse(areaCell.latitude, areaCell.longitude, otherCell.latitude, otherCell.longitude, out double distanceM);
                        var distanceKm = (float)distanceM / 1000; // Consistent with 50km/hex scale.
                        //  * MeasureUtils.kilometerToNavalMile;
                        conn.cost = distanceKm * conn.costCoef;
                    }
                }
            };

            var cellSideInforsListView = el.Q<ListView>("CellSideInforsListView");
            Utils.BindItemsAddedRemoved<CellSideInfo>(cellSideInforsListView, () => null);
            cellSideInforsListView.makeItem = () =>
            {
                var item = cellSideInforsListView.itemTemplate.CloneTree();
                var setButton = item.Q<Button>("SetButton");
                setButton.clicked += () =>
                {
                    if(Utils.TryResolveCurrentValueForBinding<CellSideInfo>(setButton, out var cellSideInfo))
                    {
                        PopupSideStatePickerDialog(side =>
                        {
                            cellSideInfo.sideObjectId = side.objectId; 
                        });
                    }
                };
                return item;
            };
            // PopupSideStatePickerDialog
        };

        // tempDialog.onConfirmed += (sender, args) => StrategicGameState.Instance.InvokeMapCellUpdated(cell.x, cell.y);
        tempDialog.onConfirmed += (sender, args) => StrategicGameState.Instance.InvokeMapCellUpdated(cell);

        tempDialog.Popup();
    }

    public void PopupSubStrategicCombatDialog(SubStrategicCombat combat)
    {
        var tempDialog = new TempDialog()
        {
            root = root,
            template = subStrategicCombatDialogDocument,
            templateDataSource = combat,
            // draggable = false
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

                    // Utils.BindGotoButton(item, null); // TODO: Remove strange reference of StrategicGroupEditor
                    Utils.BindGotoButton(item);

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
            // draggable = false
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
            // color = shipLog.shipClass.country == Country.China ? Color.red : Color.blue,
            color = shipLog.shipClass.country == Country.Japan ? Color.blue : Color.red, // support Russo-Japanese War scenario
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

            GameManager.Instance.PlotShipLogTrajectory(EntityManager.Instance.Get<ShipLog>(model.shipLogObjectId), model.color, model.plotTimestamp, model.timestampIntervalMinutes);
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

    public void PopupNavalLocationLabelEditorDialogForCreate(LatLon latLon)
    {
        var model = LocationLabelEditDialogModel.ForCreate(latLon, label =>
        {
            NavalGameState.Instance.scenarioState.locationLabels ??= new();
            NavalGameState.Instance.scenarioState.locationLabels.Add(label);
        });

        var tempDialog = new TempDialog()
        {
            root = root,
            template = navalLocationLabelEditorDialogDocument,
            templateDataSource = model
        };

        tempDialog.onConfirmed += model.OnConfirm;
        tempDialog.Popup();
    }

    public void PopupNavalLocationLabelEditorDialog(LocationLabel label, Action afterConfirm = null)
    {
        var model = LocationLabelEditDialogModel.ForEdit(label, afterConfirm);

        var tempDialog = new TempDialog()
        {
            root = root,
            template = navalLocationLabelEditorDialogDocument,
            templateDataSource = model
        };

        tempDialog.onConfirmed += model.OnConfirm;
        tempDialog.Popup();
    }

    public void PopupShipGroupRemarkDialog(ShipGroup shipGroup, Action onClosed = null)
    {
        if (shipGroup == null)
            return;

        var tempDialog = new TempDialog()
        {
            root = root,
            template = shipGroupRemarkDialogDocument,
            templateDataSource = shipGroup.remark
        };

        if (onClosed != null)
        {
            tempDialog.onClosed += (sender, root) => onClosed();
        }

        tempDialog.Popup();
    }

    public void PopupLocationLabelsEditorDialog()
    {
        var dialog = new LocationLabelsEditorDialog();

        var tempDialog = new TempDialog()
        {
            root = root,
            template = locationLabelsEditorDialogDocument,
            templateDataSource = dialog
        };

        tempDialog.onCreated += dialog.OnCreated;
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
                positionMode = TempDialog.PositionMode.None,
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
            // draggable = true
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
            // draggable = true
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

    public void PopupFollowFormationDialog(Action<float> confirmCallback, float initialFollowDistanceYards = 500f)
    {
        if (followFormationDialogDocument == null)
        {
            PopupMessageDialog("FollowFormationDialog is not configured.");
            return;
        }

        var model = new FollowFormationDialogModel()
        {
            followDistanceYards = initialFollowDistanceYards
        };

        var tempDialog = new TempDialog()
        {
            root = root,
            template = followFormationDialogDocument,
            templateDataSource = model,
        };

        tempDialog.confirmCheck = _ =>
        {
            if (float.IsNaN(model.followDistanceYards) || float.IsInfinity(model.followDistanceYards) || model.followDistanceYards <= 0f)
            {
                PopupMessageDialog("Follow distance must be greater than 0 yards.");
                return false;
            }
            return true;
        };

        tempDialog.onConfirmed += (sender, el) =>
        {
            confirmCallback?.Invoke(model.followDistanceYards);
        };

        tempDialog.Popup();
    }

    public void PopupShipClassPlaceholderGeneratorDialog(ShipClass shipClass)
    {
        if (shipClassPlaceholderGeneratorDialogDocument == null)
        {
            PopupMessageDialog("ShipClassPlaceholderGeneratorDialog is not configured.");
            return;
        }

        var model = new ShipClassPlaceholderGeneratorDialogModel()
        {
            shipClass = shipClass
        };

        var tempDialog = new TempDialog()
        {
            root = root,
            template = shipClassPlaceholderGeneratorDialogDocument,
            templateDataSource = model,
        };

        tempDialog.onCreated += (sender, el) =>
        {
            var titleLabel = el.Q<Label>("TitleLabel");
            var previewImage = el.Q<Image>("PreviewImage");
            var statusLabel = el.Q<Label>("StatusLabel");
            var generateButton = el.Q<Button>("GenerateButton");
            var saveTopButton = el.Q<Button>("SaveTopButton");
            var saveIconButton = el.Q<Button>("SaveIconButton");

            if (titleLabel != null && shipClass != null)
            {
                titleLabel.text = $"Generate Placeholder Image - {shipClass.name.GetMergedName()}";
            }

            void RefreshUi()
            {
                if (previewImage != null)
                {
                    previewImage.image = model.previewTexture;
                }
                if (statusLabel != null)
                {
                    statusLabel.text = model.statusText;
                }
                if (saveTopButton != null)
                {
                    saveTopButton.SetEnabled(model.hasGenerated);
                }
                if (saveIconButton != null)
                {
                    saveIconButton.SetEnabled(model.hasGenerated);
                }
            }

            RefreshUi();

            if (generateButton != null)
            {
                generateButton.clicked += () =>
                {
                    model.TryGenerate();
                    RefreshUi();
                };
            }

            if (saveTopButton != null)
            {
                saveTopButton.clicked += () =>
                {
                    model.SaveTopImage();
                    RefreshUi();
                };
            }

            if (saveIconButton != null)
            {
                saveIconButton.clicked += () =>
                {
                    model.SaveIconImage();
                    RefreshUi();
                };
            }
        };

        tempDialog.onClosed += (_, _) => model.Dispose();
        tempDialog.Popup();
    }

    public void PopupRelativeFormationDialog(Action<RelativeFormationDialogModel> confirmCallback)
    {
        if (relativeFormationDialogDocument == null)
        {
            PopupMessageDialog("RelativeFormationDialog is not configured.");
            return;
        }

        var model = new RelativeFormationDialogModel();

        var tempDialog = new TempDialog()
        {
            root = root,
            template = relativeFormationDialogDocument,
            templateDataSource = model,
        };

        tempDialog.onCreated += (sender, el) =>
        {
            var modeField = el.Q<LocalizedEnumField>("ModeField");
            var angleField = el.Q<FloatField>("AngleField");
            modeField?.RegisterValueChangedCallback(evt =>
            {
                var mode = (RelativeFormationMode)evt.newValue;
                if (mode == RelativeFormationMode.LineAbreast)
                {
                    model.angleDeg = 90f;
                    angleField?.SetValueWithoutNotify(model.angleDeg);
                }
                else if (mode == RelativeFormationMode.LineOfBearing)
                {
                    model.angleDeg = 135f;
                    angleField?.SetValueWithoutNotify(model.angleDeg);
                }
            });
        };

        tempDialog.confirmCheck = _ =>
        {
            if (float.IsNaN(model.distanceYards) || float.IsInfinity(model.distanceYards) || model.distanceYards <= 0f)
            {
                PopupMessageDialog("Relative formation distance must be greater than 0 yards.");
                return false;
            }

            if (float.IsNaN(model.angleDeg) || float.IsInfinity(model.angleDeg))
            {
                PopupMessageDialog("Relative formation angle must be a valid number.");
                return false;
            }
            return true;
        };

        tempDialog.onConfirmed += (sender, el) =>
        {
            model.angleDeg = MeasureUtils.NormalizeAngle(model.angleDeg);
            confirmCallback?.Invoke(model);
        };

        tempDialog.Popup();
    }

    public void PopupPreScenarioDamageDialog(float initialDamageRatioPercent, Action<float> confirmCallback)
    {
        if (preScenarioDamageDialogDocument == null)
        {
            PopupMessageDialog("PreScenarioDamageDialog is not configured.");
            return;
        }

        var tempDialog = new TempDialog()
        {
            root = root,
            template = preScenarioDamageDialogDocument,
            templateDataSource = null,
        };

        tempDialog.onCreated += (sender, el) =>
        {
            var ratioSlider = el.Q<SliderInt>("TargetDamageRatioPercentSlider");
            ratioSlider?.SetValueWithoutNotify((int)Math.Round(Math.Clamp(initialDamageRatioPercent, 0, 100)));
        };

        tempDialog.onConfirmed += (sender, el) =>
        {
            var ratioSlider = el.Q<SliderInt>("TargetDamageRatioPercentSlider");
            var targetRatioPercent = Math.Clamp(ratioSlider?.value ?? 0, 0, 100);
            confirmCallback?.Invoke(targetRatioPercent);
        };

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
        var leaderSelector = new NamedSelector<Leader>()
        {
            fullObjects = SuperGameState.Instance.GetCurrentGameState().leaders,
            callback = callback
        };

        leaderSelector.RefreshFilter();

        var tempDialog = new TempDialog()
        {
            root = root,
            template = leaderSelectorDocument,
            templateDataSource = leaderSelector
        };

        // tempDialog.onConfirmed += (sender, el) =>
        // {
        //     Debug.Log("tempDialog.onConfirmed");

        //     var leadersListView = el.Q<ListView>("LeadersListView");
        //     var leader = leadersListView.selectedItem as Leader;

        //     callback(leader);
        // };

        tempDialog.onConfirmed += leaderSelector.OnConfirm;

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
            var selectedNamedShip = NamedShipEditor.Instance.selectedObject;

            // if (leader != null && selectedNamedShip != null)
            // {
            //     selectedNamedShip.defaultLeaderReference.referenceObjectId = leader.objectId;
            // }
            if (selectedNamedShip != null)
            {
                selectedNamedShip.defaultLeaderReference.referenceObjectId = leader?.objectId;
            }
        });
    }

    public void PopupShipClassSelectorDialogForNamedShip()
    {
        var shipClassSelector = new ShipClassSelector()
        {
            fullShipClasses = SuperGameState.Instance.GetCurrentGameState().shipClasses
        };
        shipClassSelector.Refresh();

        var tempDialog = new TempDialog()
        {
            root = root,
            template = shipClassSelectorDocument,
            templateDataSource = shipClassSelector
        };

        tempDialog.onConfirmed += shipClassSelector.OnConfirm;

        tempDialog.Popup();
    }

    public void PopupNamedShipSelctorDialogForShipLog()
    {
        var namedShipSelector = new NamedShipSelector()
        {
            fullNamedShips = SuperGameState.Instance.GetCurrentGameState().namedShips
        };
        namedShipSelector.Refresh();

        var tempDialog = new TempDialog()
        {
            root = root,
            template = namedShipSelectorDocument,
            templateDataSource = namedShipSelector // GameManager.Instance
        };

        tempDialog.onConfirmed += namedShipSelector.OnConfirm;

        // tempDialog.onConfirmed += (sender, el) =>
        // {
        //     // var selectedShipLog = GameManager.Instance.selectedShipLog;
        //     var selectedShipLog = ShipLogEditor.Instance.selectedShipLog;

        //     var namedShipListView = el.Q<ListView>("NamedShipListView");
        //     var namedShip = namedShipListView.selectedItem as NamedShip;
        //     if (selectedShipLog != null && namedShip != null)
        //     {
        //         selectedShipLog.namedShipObjectId = namedShip.objectId;
        //     }
        // };

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
            positionMode = TempDialog.PositionMode.None,
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
            positionMode = TempDialog.PositionMode.None,
        };

        tempDialog.Popup();
    }
}
