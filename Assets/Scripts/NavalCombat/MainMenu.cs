using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Unity.Properties;

public class MainMenu : SingletonDocument<MainMenu>
{
    protected override void Awake()
    {
        base.Awake();

        root.dataSource = this;

        var selectScenarioButton = root.Q<Button>("SelectScenarioButton");
        var loadGameButton = root.Q<Button>("LoadGameButton");
        var galleryButton = root.Q<Button>("GalleryButton");
        var openSourceRepositoryButton = root.Q<Button>("OpenSourceRepositoryButton");
        var exitButton = root.Q<Button>("ExitButton");

        selectScenarioButton.clicked += DialogRoot.Instance.PopupScenarioPickerDialogForSwitchingSceneWithSelectedScenario;

        loadGameButton.clicked += () =>
        {
            // IOManager.Instance.textLoaded += OnFullStateXMLLoaded;
            IOManager.Instance.LoadTextFile(OnFullStateXMLLoaded, "xml");
        };


        // openSourceRepositoryButton.clicked += () => Application.OpenURL("https://github.com/yiyuezhuo/Late-Qing-Naval-Combat-Demo");
        openSourceRepositoryButton.clicked += () =>
        {
            // DialogRoot.Instance.PopupConfirmDialog("Open online open resource repository link with browser?\nhttps://github.com/yiyuezhuo/Late-Qing-Naval-Combat-Demo", () =>
            // {
            //     Application.OpenURL("https://github.com/yiyuezhuo/Late-Qing-Naval-Combat-Demo");
            // });
            DialogRoot.Instance.PopupConfirmOpenURLDialog("https://github.com/yiyuezhuo/Late-Qing-Naval-Combat-Demo");
        };

        exitButton.clicked += Application.Quit;

        root.Q<Button>("HelpButton").clicked += () => DialogRoot.Instance.PopupHelpDialogDocument();
        root.Q<Button>("FAQButton").clicked += () => DialogRoot.Instance.PopupFAQDialogDocument();

        var strategicModeTestButton = root.Q<Button>("StrategicModeTestButton");

        if (Utils.SceneInBuildSettings("Strategic Game"))
        {
            strategicModeTestButton.clicked += () =>
            {
                StrategicGameManager.startupConfig = new();
                SceneManager.LoadScene("Strategic Game");
            };
        }
        else
        {
            strategicModeTestButton.style.display = DisplayStyle.None;
        }

        root.Q<Button>("SettingButton").clicked += DialogRoot.Instance.PopupGamePreferenceDialog;

        root.Q<Button>("ManualButton").clicked += () => {
            var readmePath = Application.streamingAssetsPath + "/" + "Manuals/readme.pdf"; // This file is under version control, should be manual placed.
            DialogRoot.Instance.PopupConfirmOpenURLDialog(readmePath);
        };
    }

    [CreateProperty]
    public string versionStr => $"Version: {Application.version}";

    void OnFullStateXMLLoaded(string text)
    {
        // IOManager.Instance.textLoaded -= OnFullStateXMLLoaded;

        var fullState = FullState.FromXML(text);
        // GameManager.startupConfig.fullState = fullState;
        // GameManager.startupConfig.mode = GameManager.StartupConfig.Mode.FullState;
        GameManager.startupConfig = new()
        {
            fullState = fullState,
            mode = GameManager.StartupConfig.Mode.FullState
        };
        SceneManager.LoadScene("Naval Game");
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
