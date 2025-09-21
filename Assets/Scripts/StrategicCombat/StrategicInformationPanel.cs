using System.Linq;
using NavalCombatCore;
using StrategicCombatCore;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using YYZ.PathFinding;

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
                // StrategicGameManager.Instance.PrepareReturnFromNavalGame();

                // GameManager.startupConfig = new()
                // {
                //     mode = GameManager.StartupConfig.Mode.LocalizedCameraOnly,
                //     cameraLocation = new LatLon(cell.latitude, cell.longitude)
                // };
                // SceneManager.LoadScene("Naval Game");

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
                    }
                };

                StrategicGameManager.Instance.PrepareReturnFromNavalGame();
                SceneManager.LoadScene("Naval Game");

            }
        };

        root.Q<Button>("ResolveNavalCombatButton").clicked += () =>
        {
            Debug.Log("ResolveNavalCombatButton clicked");
            TryGotoTacticalNavalCombat(StrategicGameManager.Instance.lastSelectedCell);
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
                if(group!=null && idx != -1)
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
    }

    public void TryGotoTacticalNavalCombat(Cell cell)
    {
        var builder = new LocalNavalCombatBuilder();

        var fullState = builder.BuildFullState(cell);
        if (fullState != null)
        {
            GameManager.startupConfig = new()
            {
                fullState = fullState,
                mode = GameManager.StartupConfig.Mode.FullState,
                scenarioSetupGenerator = new()
                {
                    anchor=new LatLon(cell.latitude, cell.longitude)
                }
            };

            StrategicGameManager.Instance.PrepareReturnFromNavalGame();
            SceneManager.LoadScene("Naval Game");
        }
    }

}