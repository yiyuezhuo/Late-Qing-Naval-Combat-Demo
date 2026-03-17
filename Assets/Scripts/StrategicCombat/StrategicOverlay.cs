using UnityEngine;
using UnityEngine.UIElements;
using StrategicCombatCore;
using CoreUtils;
using YYZ;

public class StrategicOverlay : SingletonDocument<StrategicOverlay>
{
    VisualElement strategicTimePanel;
    Label strategicTimeValueLabel;
    Label strategicTimeStatusLabel;
    Button strategicTimePlayPauseButton;
    Button strategicTimeSlowButton;
    Button strategicTimeNormalButton;
    Button strategicTimeFastButton;

    protected override void Awake()
    {
        base.Awake();

        root.dataSource = StrategicGameManager.Instance;
        Utils.BindItemsSourceRecursive(root);

        root.Q<Button>("ClearLogButton").clicked += () => 
        {
            StrategicGameState.Instance.ClearLogs();
            // StrategicGameState.Instance.Refresh
        };

        strategicTimePanel = root.Q<VisualElement>("StrategicTimePanel");
        strategicTimeValueLabel = root.Q<Label>("StrategicTimeValueLabel");
        strategicTimeStatusLabel = root.Q<Label>("StrategicTimeStatusLabel");
        strategicTimePlayPauseButton = root.Q<Button>("StrategicTimePlayPauseButton");
        strategicTimeSlowButton = root.Q<Button>("StrategicTimeSlowButton");
        strategicTimeNormalButton = root.Q<Button>("StrategicTimeNormalButton");
        strategicTimeFastButton = root.Q<Button>("StrategicTimeFastButton");

        strategicTimePlayPauseButton.clicked += StrategicGameManager.Instance.ToggleRealtimeAdvance;
        strategicTimeSlowButton.clicked += () => StrategicGameManager.Instance.SetStrategicTimeAdvanceSpeed(StrategicTimeAdvanceSpeed.Slow);
        strategicTimeNormalButton.clicked += () => StrategicGameManager.Instance.SetStrategicTimeAdvanceSpeed(StrategicTimeAdvanceSpeed.Normal);
        strategicTimeFastButton.clicked += () => StrategicGameManager.Instance.SetStrategicTimeAdvanceSpeed(StrategicTimeAdvanceSpeed.Fast);
    }

    static string Localize(string key, params object[] args) => ServiceLocator.Get<ILocalizeService>().Get(key, args);

    void RefreshStrategicTimeHud()
    {
        var gameManager = StrategicGameManager.Instance;
        if (gameManager == null)
            return;

        strategicTimeValueLabel.text = gameManager.strategicCurrentTimeString;
        strategicTimeStatusLabel.text = $"{Localize(gameManager.strategicTimeStatusKey)} | {Localize(gameManager.strategicTimeSpeedKey)}";
        strategicTimePlayPauseButton.text = gameManager.isRealtimeAdvancing ? "||" : ">";

        strategicTimeSlowButton.text = Localize("Slow");
        strategicTimeNormalButton.text = Localize("Normal");
        strategicTimeFastButton.text = Localize("Fast");

        UpdateSpeedButtonState(strategicTimeSlowButton, gameManager.strategicTimeAdvanceSpeed == StrategicTimeAdvanceSpeed.Slow);
        UpdateSpeedButtonState(strategicTimeNormalButton, gameManager.strategicTimeAdvanceSpeed == StrategicTimeAdvanceSpeed.Normal);
        UpdateSpeedButtonState(strategicTimeFastButton, gameManager.strategicTimeAdvanceSpeed == StrategicTimeAdvanceSpeed.Fast);
        strategicTimePanel?.EnableInClassList("strategic-time-panel-paused", gameManager.isStrategicTimePaused);
    }

    static void UpdateSpeedButtonState(Button button, bool isSelected)
    {
        button.EnableInClassList("strategic-time-speed-button-selected", isSelected);
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        RefreshStrategicTimeHud();
    }

    // Update is called once per frame
    void Update()
    {
        RefreshStrategicTimeHud();
    }
}
