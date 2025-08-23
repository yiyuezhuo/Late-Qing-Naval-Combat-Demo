using NavalCombatCore;
using StrategicCombatCore;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

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
                StrategicGameManager.Instance.PrepareReturnFromNavalGame();

                // GameManager.startupConfig.mode = GameManager.StartupConfig.Mode.LocalizedCameraOnly;
                // GameManager.startupConfig.cameraLocation = new LatLon(cell.latitude, cell.longitude);
                GameManager.startupConfig = new()
                {
                    mode = GameManager.StartupConfig.Mode.LocalizedCameraOnly,
                    cameraLocation = new LatLon(cell.latitude, cell.longitude)
                };
                SceneManager.LoadScene("Naval Game");
            }
        };

        root.Q<Button>("ResolveNavalCombatButton").clicked += () =>
        {
            Debug.Log("ResolveNavalCombatButton clicked");
            TryGotoTacticalNavalCombat(StrategicGameManager.Instance.lastSelectedCell);
            // var builder = new LocalNavalCombatBuilder();
            // builder.TryToSwitch(StrategicGameManager.Instance.lastSelectedCell);
            // builder.TryGotoTacticalNavalCombat(StrategicGameManager.Instance.lastSelectedCell);
        };

        root.Q<Button>("EditMoveButton").clicked += () =>
        {
            StrategicGameManager.Instance.StartToEditMove();
        };
    }

    public void TryGotoTacticalNavalCombat(Cell cell)
    {
        var builder = new LocalNavalCombatBuilder();
        // builder.TryToSwitch(StrategicGameManager.Instance.lastSelectedCell);
        // builder.TryGotoTacticalNavalCombat(StrategicGameManager.Instance.lastSelectedCell);

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