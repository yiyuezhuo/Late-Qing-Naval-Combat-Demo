using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class MainMenu : SingletonDocument<MainMenu>
{
    protected override void Awake()
    {
        base.Awake();

        var selectScenarioButton = root.Q<Button>("SelectScenarioButton");
        var loadGameButton = root.Q<Button>("LoadGameButton");
        var galleryButton = root.Q<Button>("GalleryButton");
        var openSourceRepositoryButton = root.Q<Button>("OpenSourceRepositoryButton");
        var exitButton = root.Q<Button>("ExitButton");

        selectScenarioButton.clicked += DialogRoot.Instance.PopupScenarioPickerDialogForSwitchingSceneWithSelectedScenario;

        loadGameButton.clicked += () =>
        {
            IOManager.Instance.textLoaded += OnFullStateXMLLoaded;
            IOManager.Instance.LoadTextFile("xml");
        };


        openSourceRepositoryButton.clicked += () => Application.OpenURL("https://github.com/yiyuezhuo/Late-Qing-Naval-Combat-Demo");

        exitButton.clicked += Application.Quit;

        root.Q<Button>("HelpButton").clicked += () => DialogRoot.Instance.PopupHelpDialogDocument();
    }

    void OnFullStateXMLLoaded(object sender, string text)
    {
        IOManager.Instance.textLoaded -= OnFullStateXMLLoaded;

        var fullState = FullState.FromXML(text);
        GameManager.oneShotStartupFullState = fullState;
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
