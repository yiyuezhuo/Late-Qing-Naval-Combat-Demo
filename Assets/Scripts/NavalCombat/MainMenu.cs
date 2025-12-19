using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Unity.Properties;

using NavalCombatCore;
using CoreUtils;
using System;

public class MainMenu : SingletonDocument<MainMenu>
{
    protected override void Awake()
    {
        base.Awake();

        root.dataSource = this;

        var selectScenarioButton = root.Q<Button>("SelectScenarioButton");
        var loadGameButton = root.Q<Button>("LoadGameButton");
        // var galleryButton = root.Q<Button>("GalleryButton");
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
                // StrategicGameManager.startupConfig = new();
                StrategicGameManager.startupConfig = new()
                {
                    mode = StrategicGameManager.StartupConfig.Mode.ScenPath,
                    scenSubPath = "Scenarios/StrategicGameState.xml"
                };
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

        root.Q<Button>("StartAsEmptyButton").clicked += () =>
        {
            GotoEmptyNavalGame(false);
        };

        root.Q<Button>("SkirmishButton").clicked += () =>
        {
            Debug.Log("SkirmishButton clicked");

            GotoEmptyNavalGame(true);
        };

        root.Q<Button>("VladivostokSquadronRaidingButton").clicked += () =>
        {
            Debug.Log("VladivostokSquadronRaidingButton clicked");

            DialogRoot.Instance.PopupVladivostokSquadronRaidingSideSelectorDialog();
        };
    }

    void GotoEmptyNavalGame(bool skirmish)
    {
        // popupForceBuilder
        var latitude = 37.5f;
        var longitude = 123.5f;

        var gameState = new NavalGameState();

        if(!skirmish)
        {
            gameState.shipGroups.Add(new(){name=GlobalString.redStr.Clone(), objectId=Guid.NewGuid().ToString()}); // objectId is not strictly correct here but most likely would work.
            gameState.shipGroups.Add(new(){name=GlobalString.blueStr.Clone(), objectId=Guid.NewGuid().ToString()});
        }

        GameManager.startupConfig = new()
        {
            fullState = new()
            {
                streamingAssetReference = StreamingAssetReference.Instance, // Copy?
                navalGameState = gameState,
                viewState = new()
                {
                    xRotation = latitude,
                    yRotation = 360 - longitude,
                    orthographicSize = 20
                }
            },
            mode = GameManager.StartupConfig.Mode.FullState,
            scenarioSetupGenerator = new()
            {
                anchor = new LatLon(latitude, longitude)
            },
            isFromSkirmish=skirmish
        };

        SceneManager.LoadScene("Naval Game");
    }

    // static GlobalString redStr = new()
    // {
    //     english = "Red",
    //     japanese = "赤",
    //     chineseSimplified = "红",
    //     chineseTraditional = "紅",
    // };

    // static GlobalString blueStr = new()
    // {
    //     english = "Blue",
    //     japanese = "青",
    //     chineseSimplified = "蓝",
    //     chineseTraditional = "藍",
    // };

    [CreateProperty]
    public string versionStr => $"Version: {Application.version}";

    static string mainMenuBackgroundPath = $"{Application.streamingAssetsPath}/Pictures/Backgrounds/MainMenu.jpg";

    [CreateProperty]
    public StyleBackground mainMenuBackground
    {
        get
        {
            return UnityWebRequestImageReader.Instance.FetchStyleBackground(mainMenuBackgroundPath);
        }
    }

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
