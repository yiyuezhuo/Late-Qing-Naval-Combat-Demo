using System.Linq;
using NavalCombatCore;
using StrategicCombatCore;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using System;
using System.Collections.Generic;
using CoreUtils;

public class StrategicInformationPanel : SingletonDocument<StrategicInformationPanel>
{
    public VisualTreeAsset smallIconAsset;

    VisualElement stackContainer;

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

                SwitchCenter.Instance.SwitchToStrategicGroupView(group);
            }}
        });

        var leaderNameLabel = root.Q<Label>("LeaderNameLabel");
        Utils.RegisterLinkTag(leaderNameLabel, new()
        {
            {"nameLink", () =>
                {
                    var group = StrategicGameManager.Instance.lastSelectedStrategicGroup;
                    var leader = group.leaderReference.Get();

                    SwitchCenter.Instance.SwitchToLeaderView(leader);
                }
            }
        });

        var missionNameLabel = root.Q<Label>("MissionNameLabel");
        Utils.RegisterLinkTag(missionNameLabel, new()
        {
            {"nameLink", () =>
                {
                    var group = StrategicGameManager.Instance.lastSelectedStrategicGroup;
                    var mission = group.GetAssignedMission();
                    SwitchCenter.Instance.SwitchToMissionView(mission);
                }
            }
        });

        var cellEditButton = root.Q<Button>("CellEditButton");
        cellEditButton.clicked += () =>
        {
            if (Utils.TryResolveCurrentValueForBinding(cellEditButton, out Cell cell))
            {
                DialogRoot.Instance.PopupCellEditorDialog(cell);
            }
        };

        var cellTheaterNameLabel = root.Q<Label>("CellTheaterNameLabel");
        Utils.RegisterLinkTag(cellTheaterNameLabel, new()
        {
            {
                "theaterLink", () =>
                {
                    if (Utils.TryResolveCurrentValueForBinding(cellTheaterNameLabel, out Cell cell))
                    {
                        var theater = cell.currentTheater;
                        if (theater != null)
                        {
                            DialogRoot.Instance.PopupTheaterDetailDialog(theater);
                        }
                    }
                }
            }
        });

        var moveButton = root.Q<Button>("MoveButton");
        moveButton.clicked += StrategicGameManager.Instance.TryToStartMakeNewMove;

        root.Q<Button>("MoveAppendButton").clicked += StrategicGameManager.Instance.TryToStartAppendMove;
        root.Q<Button>("TransferButton").clicked += () =>
        {
            DialogRoot.Instance.PopupStrategicGroupTransferDialog(StrategicGameManager.Instance.lastSelectedStrategicGroup);
        };
        root.Q<Button>("DetachDamagedButton").clicked += () =>
        {
            DialogRoot.Instance.PopupStrategicGroupDetachDamagedDialog(StrategicGameManager.Instance.lastSelectedStrategicGroup);
        };

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

        stackContainer = root.Q<VisualElement>("StackContainer");
    }

    public void BindStack<T>(List<T> stack) where T: IWorldSpaceGroupIconDataSource
    {
        stackContainer.Clear();

        for (int i = stack.Count - 1; i >= 0; i--)
        {
            var icon = stack[i];
            // var iconInstance = smallIconAsset.Instantiate();
            var iconInstance = smallIconAsset.CloneTree();
            iconInstance.dataSource = icon;
            stackContainer.Add(iconInstance);

            iconInstance.RegisterCallback<ClickEvent>(evt =>
            {
                var ve = (VisualElement)evt.currentTarget;
                Debug.Log($"ve.dataSource={ve.dataSource}");

                if(ve.dataSource is IObjectIdLabeled obj)
                {
                    StrategicGameManager.Instance.lastSelectedObject = obj;
                }
            });
        }
    }

    public void ClearStack()
    {
        stackContainer.Clear();
    }
}
