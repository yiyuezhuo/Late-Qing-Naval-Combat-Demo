using UnityEngine;
using GeographicLib;
using TMPro;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using UnityEngine.SceneManagement;

using CoreUtils;
using YYZ;
using NavalCombatCore;
using NavalCombat;
using System.Net;
using System;

public class TopTabs : SingletonDocument<TopTabs>
{
    DropdownField playerDropdownField;
    static readonly Dictionary<ControlMode, int> formationControlModePriority = new()
    {
        { ControlMode.FollowTarget, -2 },
        { ControlMode.RelativeToTarget, -1 },
    };

    static string Localize(string key, params object[] args) => ServiceLocator.Get<ILocalizeService>().Get(key, args);

    protected override void Awake()
    {
        base.Awake();

        root.dataSource = GameManager.Instance;

        var leaderEditorButton = root.Q<Button>("LeaderEditorButton");
        leaderEditorButton.clicked += () => LeaderEditor.Instance.Show();

        var shipClassEditorButton = root.Q<Button>("ClassEditorButton");
        shipClassEditorButton.clicked += () => ShipClassEditor.Instance.Show();

        var namedShipEditorButton = root.Q<Button>("NamedShipEditorButton");
        namedShipEditorButton.clicked += NamedShipEditor.Instance.Show;

        var shipLogEditorButton = root.Q<Button>("ShipLogEditorButton");
        shipLogEditorButton.clicked += () => ShipLogEditor.Instance.Show();

        var oobEditorButton = root.Q<Button>("OOBEditorButton");
        oobEditorButton.clicked += () => OOBEditor.Instance.Show();

        var scenarioStateEditorButton = root.Q<Button>("ScenarioStateEditorButton");
        // scenarioStateEditorButton.clicked += ScenarioStateEditor.Instance.Show;
        scenarioStateEditorButton.clicked += DialogRoot.Instance.PopupScenarioStateEditor;

        var locationLabelsEditorButton = root.Q<Button>("LocationLabelsEditorButton");
        locationLabelsEditorButton.clicked += DialogRoot.Instance.PopupLocationLabelsEditorDialog;

        var launchedTorpedoEditorButton = root.Q<Button>("LaunchedTorpedoEditorButton");
        launchedTorpedoEditorButton.clicked += LaunchedTorpedoEditor.Instance.Show;

        var jsScriptConsoleButton = root.Q<Button>("JSScriptConsoleButton");
        jsScriptConsoleButton.clicked += JSScriptConsoleDialog.Instance.Show;

        var setToFormationPositionButton = root.Q<Button>("SetToFormationPositionButton");
        setToFormationPositionButton.clicked += SetToFormationPosition;

        var streamingAssetReferenceDialogButton = root.Q<Button>("StreamingAssetReferenceDialogButton");
        streamingAssetReferenceDialogButton.clicked += DialogRoot.Instance.PopupStreamingAssetReferenceDialog;

        playerDropdownField = root.Q<DropdownField>("PlayerDropdownField");

        NavalGameState.Instance.shipGroupsChanged -= OnRootShipGroupsChanged;
        NavalGameState.Instance.shipGroupsChanged += OnRootShipGroupsChanged;

        playerDropdownField.RegisterValueChangedCallback((ChangeEvent<string> evt) =>
        {
            SyncPlayerViewpoint();
        });

        var saveButton = root.Q<Button>("SaveButton");
        var loadButton = root.Q<Button>("LoadButton");

        saveButton.clicked += () => OnSaveButtonClicked(false);

        root.Q<Button>("SaveEditButton").clicked += () => OnSaveButtonClicked(true);

        loadButton.clicked += () =>
        {
            // IOManager.Instance.textLoaded += OnFullStateXMLLoaded;
            IOManager.Instance.LoadTextFile(OnFullStateXMLLoaded, "xml");
        };

        var selectionBuiltinButton = root.Q<Button>("SelectionBuiltinButton");
        selectionBuiltinButton.clicked += DialogRoot.Instance.PopupScenarioPickerDialogForScenarioSwitchInGame;

        // var gamePreferenceRoot = root.Q<VisualElement>("GamePreferenceRoot");
        // gamePreferenceRoot.dataSource = GamePreference.Instance;

        // var coreParameterRoot = root.Q<VisualElement>("CoreParameterRoot");
        // coreParameterRoot.dataSource = CoreParameter.Instance;

        var victoryStatusButton = root.Q<Button>("VictoryStatusButton");
        // var victoryStatus = VictoryStatus.Generate(NavalGameState.Instance);
        victoryStatusButton.clicked += () => DialogRoot.Instance.PopupVictoryStatusDialog(
            VictoryStatus.Generate(NavalGameState.Instance)
        );

        var runDebugScriptButton = root.Q<Button>("RunDebugScriptButton");
        runDebugScriptButton.clicked += () =>
        {
            Debug.LogWarning("RunDebugScriptButton clicked");

            // var damageEffectId = "164";
            // var damageEffectId = "101";
            foreach (var shipLog in NavalGameState.Instance.shipLogsOnMap)
            {
                var ctx = new DamageEffectContext()
                {
                    subject = shipLog,
                    baseDamagePoint = 11,
                    hitPenDetType = HitPenDetType.PenetrateWithDetonate,
                    ammunitionType = AmmunitionType.ArmorPiercing,
                    shellDiameterInch = 10,
                    addtionalDamageEffectProbility = 1
                };

                // DamageEffectChart.AddNewDamageEffect(ctx, damageEffectId);
                DamageEffectChart.AddNewDamageEffect(ctx);
            }
        };

        var advance1MinButton = root.Q<Button>("Advance1MinButton");
        var advance1PulseButton = root.Q<Button>("Advance1PulseButton");

        advance1MinButton.clicked += () =>
        {
            GameManager.Instance.SetRemainAdvanceSimulationSecondsRequestedByUserInput(60);
            // GameManager.Instance.remainAdvanceSimulationSecondsRequestedByUserInput = 60;
        };

        advance1PulseButton.clicked += () =>
        {
            GameManager.Instance.SetRemainAdvanceSimulationSecondsRequestedByUserInput(GamePreference.Instance.pulseLengthSeconds);
            // GameManager.Instance.remainAdvanceSimulationSecondsRequestedByUserInput = GamePreference.Instance.pulseLengthSeconds;
        };

        root.Q<Button>("DetachButton").clicked += () =>
        {
            var selectedShipLog = GameManager.Instance.selectedShipLog;
            if (selectedShipLog != null)
                selectedShipLog.controlMode = ControlMode.Independent;
        };

        root.Q<Button>("FollowButton").clicked += () =>
        {
            if (GameManager.Instance.selectedShipLog != null)
                GameManager.Instance.state = GameManager.State.SelectingFollowedTarget;
        };

        root.Q<Button>("RelativeButton").clicked += () =>
        {
            if (GameManager.Instance.selectedShipLog != null)
                GameManager.Instance.state = GameManager.State.SelectingRelativeToTarget;
        };

        root.Q<Button>("GoToRelativeFormationButton").clicked += GoToRelativeFormation;
        root.Q<Button>("GoToFollowFormationButton").clicked += GoToFollowFormation;

        root.Q<Button>("DistanceMeasureButton").clicked += () =>
        {
            MeasureLine.Instance.state = MeasureLine.State.ChooseStart;
        };

        root.Q<Button>("MaskMeasureButton").clicked += () =>
        {
            LOSLine.Instance.state = LOSLine.State.ChooseStart;
        };

        root.Q<Button>("HelpButton").clicked += () => DialogRoot.Instance.PopupHelpDialogDocument();

        root.Q<Button>("SetCourseButton").clicked += () =>
        {
            // GameManager.Instance.StartSetCourse();
            GameManager.Instance.state = GameManager.State.SelectingCourseTarget;
        };

        // root.Q<Button>("OpenOpenSourceRepoButton").clicked += () =>
        // {
        //     Application.OpenURL("https://github.com/yiyuezhuo/Late-Qing-Naval-Combat-Demo");
        // };
        root.Q<Button>("OpenOpenSourceRepoButton").clicked += () =>
        {
            DialogRoot.Instance.PopupConfirmDialog("Open online open resource repository link with browser?\nhttps://github.com/yiyuezhuo/Late-Qing-Naval-Combat-Demo", () =>
            {
                Application.OpenURL("https://github.com/yiyuezhuo/Late-Qing-Naval-Combat-Demo");
            });
        };

        var goToMainMenuButton = root.Q<Button>("GoToMainMenuButton");
        // goToMainMenuButton.clicked += () => SceneManager.LoadScene("Main Menu");
        goToMainMenuButton.clicked += () =>
        {
            DialogRoot.Instance.PopupConfirmDialog(Localize(
                "Confirm to return to main menu?"
            ), () =>
                {
                    SceneManager.LoadScene("Main Menu");
                }
            );
        };

        root.Q<Button>("ReturnToStrategicGameButton").clicked += () =>
        {
            // DialogRoot.Instance.PopupConfirmDialog("Confirm to conclude the battle and return to strategic game?", () =>
            DialogRoot.Instance.PopupConfirmDialog(Localize(
                "Confirm to conclude the battle and return to strategic game?"
            ), () =>
            {
                // StrategicGameManager.startupConfig.syncShipLogs = NavalGameState.Instance.shipLogs;
                // StrategicGameManager.startupConfig.victoryStatus = VictoryStatus.Generate(NavalGameState.Instance);

                // SceneManager.LoadScene("Strategic Game");

                GameManager.Instance.ReturnToStrategicGame();
            });
        };

        root.Q<Button>("GamePreferenceButton").clicked += DialogRoot.Instance.PopupGamePreferenceDialog;

        root.Q<Button>("ClearTrajectoriesButton").clicked += GameManager.Instance.ClearShipLogTrajectories;
        root.Q<Button>("ReturnTo2DViewButton").clicked += () => CameraController2.Instance?.ReturnTo2DView();
        root.Q<Button>("GoTo3DViewButton").clicked += () => CameraController2.Instance?.GoTo3DView();

        root.Q<Button>("EventEditorDialogButton").clicked += DialogRoot.Instance.PopupEventStateEditorDialog;

        root.Q<Button>("FAQButton").clicked += () => DialogRoot.Instance.PopupFAQDialogDocument();

        root.Q<Button>("AIButton").clicked += () =>
        {
            DialogRoot.Instance.PopupAIDialog();
        };

        root.Q<Button>("ManualButton").clicked += () => {
            var readmePath = Application.streamingAssetsPath + "/" + "Manuals/readme.pdf"; // This file is under version control, should be manual placed.
            DialogRoot.Instance.PopupConfirmOpenURLDialog(readmePath);
        };

        root.Q<Button>("ForceBuilderButton").clicked += () =>
        {
            DialogRoot.Instance.PopupForceBuilderDialog();
        };

        root.Q<Button>("AutoDeploymentButton").clicked += () =>
        {
            DialogRoot.Instance.PopupAutoDeploymentDialog();  
        };

        root.Q<Button>("ResetImageCacheButton").clicked += () =>
        {
            UnityWebRequestImageReader.Instance.Reset();
        };

        root.Q<Button>("EditMoveButton").clicked += () =>
        {
            if(GameManager.Instance.selectedShipLog != null)
            {
                GameManager.Instance.state = GameManager.State.MovingUnit;
            }
        };

        root.Q<Button>("InsertButton").clicked += () =>
        {
            GameManager.Instance.state = GameManager.State.SelectingInsertUnitPositionComplex;
        };

        root.Q<Button>("InsertLocationLabelButton").clicked += () =>
        {
            GameManager.Instance.state = GameManager.State.SelectingInsertLocationLabelPosition;
        };

        root.Q<Button>("DeleteButton").clicked += () =>
        {
            if(GameManager.Instance.selectedShipLog != null)
            {
                GameManager.Instance.state = GameManager.State.Idle;
                GameManager.Instance.selectedShipLog.mapState = MapState.NotDeployed;
            }
        };

        // StartAsHostButton, ConnectButton, DisconnectButton, NetworkingDetailButton
        // root.Q<Button>("StartAsHostButton").clicked += GameManager.Instance.DoStartHost;
        // root.Q<Button>("ConnectButton").clicked += GameManager.Instance.DoConnect;
        // root.Q<Button>("DisconnectButton").clicked += GameManager.Instance.DoDisconnect;
        // root.Q<Button>("SubmitTakeCommandButton").clicked += GameManager.Instance.DoSubmitTakeCommand;

        root.Q<Button>("HostButton").clicked += DialogRoot.Instance.PopupHostDialog;
        root.Q<Button>("ClientButton").clicked += DialogRoot.Instance.PopupClientDialog;
    }

    void OnSaveButtonClicked(bool editSave=false)
    {
        if(GameManager.Instance.detachStreamingAssets)
        {
            IOManager.Instance.StartCoroutine(StreamingAssetReference.Instance.CheckConsistence(NavalGameState.Instance, res =>
            {
                if(!res.IsAllConsistent())
                {
                    DialogRoot.Instance.PopupConfirmDialog(
                        $"Consistency check failed: ({res}), missing export or forget to use non-detached save mode? Confirm to ignore the prompt and continue to save.",
                        () => DoSave(editSave, true)
                    );
                    return;
                }
                DoSave(editSave, true);
            }));
        }
        else
        {
            DoSave(editSave, false);
        }
    }

    public static FullState CaptureFullState(bool detachGameState)
    {
        var gameState = detachGameState ? 
            DetachGameState(NavalGameState.Instance, StreamingAssetReference.Instance) : 
            XmlUtils.FromXML<NavalGameState>(XmlUtils.ToXML(NavalGameState.Instance));

        gameState.leadersBuiltin = gameState.leaders != null;
        gameState.shipClassesBuiltin = gameState.shipClasses != null;
        gameState.namedShipsBuiltin = gameState.namedShips != null;

        var fullState = new FullState()
        {
            streamingAssetReference = StreamingAssetReference.Instance,
            navalGameState = gameState,
            viewState = GameManager.Instance.CaptureViewState(),
            eventState = EventState.Instance
        };

        return fullState;
    }

    void DoSave(bool editSave, bool detachGameState)
    {
        var fullState = CaptureFullState(detachGameState);

        if (editSave)
        {
            // fullState.navalGameState.scenarioState.firstLoaded = false;
            fullState.navalGameState.scenarioState.ResetEditSaveRelatedStates();
        }

        var name = GameManager.startupConfig.mode == GameManager.StartupConfig.Mode.BuiltinScenName ? GameManager.startupConfig.builtinScenName.Replace(".scen.xml", "") : "FullState";

        // IOManager.Instance.SaveTextFile(fullState.ToXML(), "FullState", "xml");
        IOManager.Instance.SaveTextFile(fullState.ToXML(), name, "scen.xml");
    }

    static NavalGameState DetachGameState(NavalGameState _s, StreamingAssetReference sar)
    {
        // deep copy
        var s = XmlUtils.FromXML<NavalGameState>(XmlUtils.ToXML(_s));

        if (sar.leadersPath != null && sar.leadersPath != "")
        {
            // s.leadersBuiltin = false;
            s.leaders = null;
        }

        if (sar.shipClassesPath != null && sar.shipClassesPath != "")
        {
            // s.shipClassesBuiltin = false;
            s.shipClasses = null;
        }

        if (sar.namedShipsPath != null && sar.namedShipsPath != "")
        {
            // s.namedShipsBuiltin = false;
            s.namedShips = null;
        }

        return s;
    }

    void SetToFormationPosition()
    {
        var resolvedSet = NavalGameState.Instance.shipLogsOnMap.Where(s => s.GetEffectiveControlMode() == ControlMode.Independent).ToHashSet();
        
        foreach(var initialResolved in resolvedSet)
        {
            if (initialResolved.IsLandBattery())
            {
                initialResolved.EnforceLandBatteryFixedKinematics();
                continue;
            }

            initialResolved.speedKnots = initialResolved.desiredSpeedKnots;
            initialResolved.headingDeg = initialResolved.desiredHeadingDeg;
        }
        
        var waitingSet = NavalGameState.Instance.shipLogsOnMap.Where(s => s.GetEffectiveControlMode() != ControlMode.Independent).ToHashSet();
        while (waitingSet.Count > 0)
        {
            var picked = waitingSet.FirstOrDefault(s =>
            {
                var controlMode = s.GetEffectiveControlMode();
                return (controlMode == ControlMode.FollowTarget && resolvedSet.Contains(s.followedTarget)) ||
                    (controlMode == ControlMode.RelativeToTarget && resolvedSet.Contains(s.relativeToTarget));
            });
            if (picked == null)
            {
                Debug.LogWarning("Potential looping control refernece");
                break;
            }
            resolvedSet.Add(picked);
            waitingSet.Remove(picked);

            // Move ship to their "ideal" formation position            
            switch (picked.GetEffectiveControlMode())
            {
                case ControlMode.FollowTarget:
                    if (picked.IsLandBattery())
                    {
                        picked.EnforceLandBatteryFixedKinematics();
                        break;
                    }

                    var target = picked.followedTarget;
                    var distM = picked.followDistanceYards * MeasureUtils.yardToMeter;
                    Geodesic.WGS84.Direct(target.position.LatDeg, target.position.LonDeg,
                        MeasureUtils.NormalizeAngle(target.headingDeg + 180), distM, out var lat2, out var lon2);
                    picked.position = new LatLon((float)lat2, (float)lon2);
                    picked.headingDeg = picked.desiredHeadingDeg = target.headingDeg;
                    picked.speedKnots = picked.desiredSpeedKnots = target.speedKnots;

                    break;
                case ControlMode.RelativeToTarget:
                    if (picked.IsLandBattery())
                    {
                        picked.EnforceLandBatteryFixedKinematics();
                        break;
                    }

                    target = picked.relativeToTarget;
                    distM = picked.relativeToTargetDistanceYards * MeasureUtils.yardToMeter;
                    var angle = picked.relativeToAbsolute
                        ? MeasureUtils.NormalizeAngle(picked.relativeToTargetAzimuth)
                        : MeasureUtils.NormalizeAngle(target.headingDeg + picked.relativeToTargetAzimuth);
                    Geodesic.WGS84.Direct(target.position.LatDeg, target.position.LonDeg,
                        angle, distM, out lat2, out lon2);
                    picked.position = new LatLon((float)lat2, (float)lon2);
                    picked.headingDeg = picked.desiredHeadingDeg = target.headingDeg;
                    picked.speedKnots = picked.desiredSpeedKnots = target.speedKnots;

                    break;
            }
        }
    }

    void GoToRelativeFormation()
    {
        var anchorShip = GameManager.Instance.selectedShipLog;
        if (anchorShip == null)
            return;

        var controlTree = BuildFormationControlTree(anchorShip);
        if (controlTree.edges.Count == 0)
            return;

        DialogRoot.Instance.PopupRelativeFormationDialog(model =>
        {
            switch (model.mode)
            {
                case RelativeFormationMode.KeepCurrentPosition:
                    ApplyKeepCurrentRelativeFormation(controlTree.edges, model.absolute);
                    break;
                case RelativeFormationMode.LineAbreast:
                case RelativeFormationMode.LineOfBearing:
                    ApplyPatternRelativeFormation(anchorShip, controlTree.childrenMap, controlTree.oobOrderIndex, model);
                    break;
            }
        });
    }

    void GoToFollowFormation()
    {
        var anchorShip = GameManager.Instance.selectedShipLog;
        if (anchorShip == null)
            return;

        var controlTree = BuildFormationControlTree(anchorShip);
        if (controlTree.edges.Count == 0)
            return;

        DialogRoot.Instance.PopupFollowFormationDialog(followDistanceYards =>
        {
            var chain = FlattenFormationTreeForFollow(anchorShip, controlTree.childrenMap, controlTree.oobOrderIndex);
            var previousShip = anchorShip;
            foreach (var ship in chain)
            {
                ship.controlMode = ControlMode.FollowTarget;
                ship.followedTargetObjectId = previousShip.objectId;
                ship.followDistanceYards = followDistanceYards;
                ship.relativeTargetObjectId = null;
                previousShip = ship;
            }
        });
    }

    (List<(ShipLog parent, ShipLog child)> edges, Dictionary<ShipLog, List<ShipLog>> childrenMap, Dictionary<string, int> oobOrderIndex) BuildFormationControlTree(ShipLog anchorShip)
    {
        var allShips = NavalGameState.Instance.shipLogsOnMap.ToList();
        var predecessorToChildren = allShips
            .Where(ship => ship != anchorShip)
            .GroupBy(ship => ship.GetControlPredecessor())
            .Where(group => group.Key != null)
            .ToDictionary(group => group.Key, group => group.ToList());

        var edges = new List<(ShipLog parent, ShipLog child)>();
        var childrenMap = new Dictionary<ShipLog, List<ShipLog>>();
        var visited = new HashSet<string>() { anchorShip.objectId };
        var queue = new Queue<ShipLog>();
        queue.Enqueue(anchorShip);

        while (queue.Count > 0)
        {
            var parent = queue.Dequeue();
            if (!predecessorToChildren.TryGetValue(parent, out var directChildren))
                continue;

            childrenMap[parent] = directChildren;
            foreach (var child in directChildren)
            {
                if (!visited.Add(child.objectId))
                    continue;

                edges.Add((parent, child));
                queue.Enqueue(child);
            }
        }

        return (edges, childrenMap, BuildOobOrderIndex(anchorShip));
    }

    Dictionary<string, int> BuildOobOrderIndex(ShipLog anchorShip)
    {
        var oobOrderIndex = new Dictionary<string, int>();
        var rootParent = ((IShipGroupMember)anchorShip).GetRootParent();
        var nextIndex = 0;

        void Visit(IShipGroupMember member)
        {
            if (member == null)
                return;

            oobOrderIndex[member.objectId] = nextIndex++;
            if (member is ShipGroup shipGroup)
            {
                foreach (var child in shipGroup.GetChildren())
                {
                    Visit(child);
                }
            }
        }

        Visit(rootParent);
        return oobOrderIndex;
    }

    List<ShipLog> FlattenFormationTreeForFollow(
        ShipLog parent,
        Dictionary<ShipLog, List<ShipLog>> childrenMap,
        Dictionary<string, int> oobOrderIndex)
    {
        var orderedChildren = GetOrderedFormationChildren(parent, childrenMap, oobOrderIndex);
        var result = new List<ShipLog>();
        foreach (var child in orderedChildren)
        {
            result.Add(child);
            result.AddRange(FlattenFormationTreeForFollow(child, childrenMap, oobOrderIndex));
        }
        return result;
    }

    List<ShipLog> GetOrderedFormationChildren(
        ShipLog parent,
        Dictionary<ShipLog, List<ShipLog>> childrenMap,
        Dictionary<string, int> oobOrderIndex)
    {
        if (!childrenMap.TryGetValue(parent, out var children))
            return new List<ShipLog>();

        return children
            .OrderBy(ship => formationControlModePriority.GetValueOrDefault(ship.controlMode, 0))
            .ThenBy(ship => oobOrderIndex.GetValueOrDefault(ship.objectId, int.MaxValue))
            .ToList();
    }

    void ApplyKeepCurrentRelativeFormation(List<(ShipLog parent, ShipLog child)> edges, bool absolute)
    {
        foreach (var edge in edges)
        {
            Geodesic.WGS84.Inverse(
                edge.parent.position.LatDeg,
                edge.parent.position.LonDeg,
                edge.child.position.LatDeg,
                edge.child.position.LonDeg,
                out var distanceM,
                out var azimuthDeg,
                out _
            );

            edge.child.relativeTargetObjectId = edge.parent.objectId;
            edge.child.relativeToTargetDistanceYards = (float)distanceM * MeasureUtils.meterToYard;
            edge.child.relativeToTargetAzimuth = absolute
                ? MeasureUtils.NormalizeAngle((float)azimuthDeg)
                : MeasureUtils.NormalizeAngle((float)azimuthDeg - edge.parent.headingDeg);
            edge.child.relativeToAbsolute = absolute;
            edge.child.followedTargetObjectId = null;
            edge.child.controlMode = ControlMode.RelativeToTarget;
        }
    }

    void ApplyPatternRelativeFormation(
        ShipLog anchorShip,
        Dictionary<ShipLog, List<ShipLog>> childrenMap,
        Dictionary<string, int> oobOrderIndex,
        RelativeFormationDialogModel model)
    {
        var chain = FlattenFormationTreeForFollow(anchorShip, childrenMap, oobOrderIndex);
        if (chain.Count == 0)
            return;

        if (!model.isSymmetric)
        {
            ShipLog previousShip = anchorShip;
            foreach (var ship in chain)
            {
                SetRelativeFormationLink(ship, previousShip, model.distanceYards, model.angleDeg, model.absolute);
                previousShip = ship;
            }
            return;
        }

        ShipLog rightPreviousShip = anchorShip;
        ShipLog leftPreviousShip = anchorShip;
        var mirroredAngle = MeasureUtils.NormalizeAngle(360f - model.angleDeg);

        for (var i = 0; i < chain.Count; i++)
        {
            var ship = chain[i];
            if (i % 2 == 0)
            {
                SetRelativeFormationLink(ship, rightPreviousShip, model.distanceYards, model.angleDeg, model.absolute);
                rightPreviousShip = ship;
            }
            else
            {
                SetRelativeFormationLink(ship, leftPreviousShip, model.distanceYards, mirroredAngle, model.absolute);
                leftPreviousShip = ship;
            }
        }
    }

    void SetRelativeFormationLink(ShipLog ship, ShipLog targetShip, float distanceYards, float azimuthDeg, bool absolute)
    {
        ship.controlMode = ControlMode.RelativeToTarget;
        ship.relativeTargetObjectId = targetShip.objectId;
        ship.relativeToTargetDistanceYards = distanceYards;
        ship.relativeToTargetAzimuth = MeasureUtils.NormalizeAngle(azimuthDeg);
        ship.relativeToAbsolute = absolute;
        ship.followedTargetObjectId = null;
    }

    void OnFullStateXMLLoaded(string text)
    {
        // IOManager.Instance.textLoaded -= OnFullStateXMLLoaded;

        var fullState = FullState.FromXML(text);
        StartCoroutine(GameManager.Instance.CompleteFullStateAndUpdateCoroutine(fullState));
    }

    public void OnRootShipGroupsChanged(object sender, List<ShipGroup> groups)
    {
        SyncPlayerDropdownField();
    }

    public void SyncPlayerDropdownField()
    {
        // foreach (var shipMember in NavalGameState.Instance.GetShipGroupMembersRecursive())
        // {

        // }
        var members = NavalGameState.Instance.GetShipGroupMembersRecursive().ToList();
        var names = members.Select(m => m.GetMemberName()).ToList();
        playerDropdownField.choices = names;
        playerDropdownField.userData = members;
    }

    public void SyncPlayerViewpoint()
    {
        var refGroup = playerDropdownField.index == -1 ? null : (playerDropdownField.userData as List<IShipGroupMember>)[playerDropdownField.index];
        var postureTypeMap = NavalGameState.Instance.CalcualtePostureMap(refGroup);
        foreach ((var objectId, var viewer) in GameManager.Instance.objectId2Viewer)
        {
            // var model = EntityManager.Instance.Get<ShipLog>(objectId);
            // var postureType = postureTypeMap.GetValueOrDefault(model);
            // Sync shader parameter for PortraitViewer?
            // var natoViewer = viewer.GetComponent<NATOIconViewer>();
            // natoViewer.SyncPostureType(postureType);
        }
    }
}
