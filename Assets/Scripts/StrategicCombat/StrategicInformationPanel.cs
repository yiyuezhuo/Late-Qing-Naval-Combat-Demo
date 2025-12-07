using System.Linq;
using NavalCombatCore;
using StrategicCombatCore;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using System;

public class StrategicInformationPanel : SingletonDocument<StrategicInformationPanel>
{
    protected override void Awake()
    {
        base.Awake();

        var goToLocationAtTacticalModeButton = root.Q<Button>("GoToLocationAtTacticalModeButton");
        goToLocationAtTacticalModeButton.clicked += () =>
        {
            Debug.Log("GoToLocationAtTacticalModeButton clicked");

            if (Utils.TryResolveCurrentValueForBinding(goToLocationAtTacticalModeButton, out Cell cell))
            {
                GameManager.startupConfig = new()
                {
                    fullState = new()
                    {
                        streamingAssetReference = StreamingAssetReference.Instance, // Copy?
                        navalGameState = new(),
                        viewState = new()
                        {
                            xRotation = cell.latitude,
                            yRotation = 360 - cell.longitude,
                            orthographicSize = 20
                        }
                    },
                    mode = GameManager.StartupConfig.Mode.FullState,
                    scenarioSetupGenerator = new()
                    {
                        anchor = new LatLon(cell.latitude, cell.longitude)
                    },
                    isFromStrategic=true
                };

                StrategicGameManager.Instance.PrepareReturnFromNavalGame();
                SceneManager.LoadScene("Naval Game");

            }
        };

        root.Q<Button>("EditMoveButton").clicked += () =>
        {
            StrategicGameManager.Instance.StartToEditMove();
        };

        var strategicGroupNameLabel = root.Q<Label>("StrategicGroupNameLabel");
        Utils.RegisterLinkTag(strategicGroupNameLabel, new()
        {
            {"nameLink", () =>{
                var group = StrategicGameManager.Instance.lastSelectedStrategicGroup;
                var idx = StrategicGameState.Instance.strategicGroups.IndexOf(group);
                if(group != null && idx != -1)
                {
                    StrategicGroupEditor.Instance.Show();
                    BehaviourUtils.Instance.ScheduleToSetSelectionForListView(StrategicGroupEditor.Instance.objectListView, idx);
                }
            }}
        });

        var cellEditButton = root.Q<Button>("CellEditButton");
        cellEditButton.clicked += () =>
        {
            if (Utils.TryResolveCurrentValueForBinding(cellEditButton, out Cell cell))
            {
                DialogRoot.Instance.PopupCellEditorDialog(cell);
            }
        };

        var moveButton = root.Q<Button>("MoveButton");
        moveButton.clicked += StrategicGameManager.Instance.TryToStartMakeNewMove;

        root.Q<Button>("MoveAppendButton").clicked += StrategicGameManager.Instance.TryToStartAppendMove;

        var landBattleButton = root.Q<Button>("LandBattleButton");
        landBattleButton.clicked += () =>
        {
            Debug.Log("LandBattleButton clicked");

            // PopupLandBattleDialog(ce)
            if(Utils.TryResolveCurrentValueForBinding<Cell>(landBattleButton, out var cell))
            {
                var landBattle = cell.GetLandBattle();
                if(landBattle != null)
                {
                    DialogRoot.Instance.PopupLandBattleDialog(landBattle);
                }
            }
        };
    }
}