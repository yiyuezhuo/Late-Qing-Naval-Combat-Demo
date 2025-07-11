using UnityEngine;
using NavalCombatCore;
using GeographicLib;
using TMPro;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using UnityEngine.SceneManagement;


public class TopTabs : SingletonDocument<TopTabs>
{
    DropdownField playerDropdownField;

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
        scenarioStateEditorButton.clicked += ScenarioStateEditor.Instance.Show;

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

        saveButton.clicked += () =>
        {

            var fullState = new FullState()
            {
                // NavalGameState = NavalGameState.Instance,
                streamingAssetReference = StreamingAssetReference.Instance,
                navalGameState = StreamingAssetReference.Instance.Detach(NavalGameState.Instance),
                viewState = GameManager.Instance.CaptureViewState(),
            };

            IOManager.Instance.SaveTextFile(fullState.ToXML(), "FullState", "xml");
        };

        loadButton.clicked += () =>
        {
            IOManager.Instance.textLoaded += OnFullStateXMLLoaded;
            IOManager.Instance.LoadTextFile("xml");
        };

        var selectionBuiltinButton = root.Q<Button>("SelectionBuiltinButton");
        selectionBuiltinButton.clicked += DialogRoot.Instance.PopupScenarioPickerDialogForScenarioSwitchInGame;

        var goToMainMenuButton = root.Q<Button>("GoToMainMenuButton");
        goToMainMenuButton.clicked += () => SceneManager.LoadScene("Main Menu");

        var gamePreferenceRoot = root.Q<VisualElement>("GamePreferenceRoot");
        gamePreferenceRoot.dataSource = GamePreference.Instance;

        var coreParameterRoot = root.Q<VisualElement>("CoreParameterRoot");
        coreParameterRoot.dataSource = CoreParameter.Instance;

        var victoryStatusButton = root.Q<Button>("VictoryStatusButton");
        victoryStatusButton.clicked += DialogRoot.Instance.PopupVictoryStatusDialog;

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
            GameManager.Instance.remainAdvanceSimulationSecondsRequestedByUserInput = 60;
        };

        advance1PulseButton.clicked += () =>
        {
            GameManager.Instance.remainAdvanceSimulationSecondsRequestedByUserInput = GamePreference.Instance.pulseLengthSeconds;
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

        root.Q<Button>("DistanceMeasureButton").clicked += () =>
        {
            MeasureLine.Instance.state = MeasureLine.State.ChooseStart;
        };

        root.Q<Button>("MaskMeasureButton").clicked += () =>
        {
            LOSLine.Instance.state = LOSLine.State.ChooseStart;
        };

        root.Q<Button>("HelpButton").clicked += () => DialogRoot.Instance.PopupHelpDialogDocument();
    }

    void SetToFormationPosition()
    {
        var resolvedSet = NavalGameState.Instance.shipLogsOnMap.Where(s => s.GetEffectiveControlMode() == ControlMode.Independent).ToHashSet();
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
                    var target = picked.followedTarget;
                    var distM = picked.followDistanceYards * MeasureUtils.yardToMeter;
                    Geodesic.WGS84.Direct(target.position.LatDeg, target.position.LonDeg,
                        MeasureUtils.NormalizeAngle(target.headingDeg + 180), distM, out var lat2, out var lon2);
                    picked.position = new LatLon((float)lat2, (float)lon2);
                    picked.headingDeg = target.headingDeg;
                    picked.speedKnots = target.speedKnots;

                    break;
                case ControlMode.RelativeToTarget:
                    target = picked.relativeToTarget;
                    distM = picked.relativeToTargetDistanceYards * MeasureUtils.yardToMeter;
                    var angle = MeasureUtils.NormalizeAngle(target.headingDeg + picked.relativeToTargetAzimuth);
                    Geodesic.WGS84.Direct(target.position.LatDeg, target.position.LonDeg,
                        angle, distM, out lat2, out lon2);
                    picked.position = new LatLon((float)lat2, (float)lon2);
                    picked.headingDeg = target.headingDeg;
                    picked.speedKnots = target.speedKnots;

                    break;
            }
        }
    }

    void OnFullStateXMLLoaded(object sender, string text)
    {
        IOManager.Instance.textLoaded -= OnFullStateXMLLoaded;

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