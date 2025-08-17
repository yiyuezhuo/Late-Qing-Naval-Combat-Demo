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

                GameManager.startupConfig.mode = GameManager.StartupConfig.Mode.LocalizedCameraOnly;
                GameManager.startupConfig.cameraLocation = new LatLon(cell.latitude, cell.longitude);
                SceneManager.LoadScene("Naval Game");
            }
        };
    }
}