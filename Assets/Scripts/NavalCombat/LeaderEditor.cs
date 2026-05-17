using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
using Unity.Properties;

// using NavalCombatCore;
using CoreUtils;

public class LeaderEditor : LeftObjectPickerRightEditor<LeaderEditor, Leader>
{
    // public ListView objectListView;

    protected override void GetFullObjects()
    {
        var gameState = SuperGameState.Instance.GetCurrentGameState();
        fullObjects = gameState.leaders;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    // protected override void Awake()
    protected override void  OnEnable()
    {
        // root.dataSource = this;

        // Utils.BindItemsSourceRecursive(root);

        // objectListView = root.Q<ListView>("ObjectListView");
        // Utils.BindItemsAddedRemoved<Leader>(objectListView, () => null);

        // objectListView.selectionChanged += (IEnumerable<object> objects) =>
        // {
        //     var leader = objects.FirstOrDefault() as Leader;
        //     selectedId = leader?.objectId;

        //     Debug.Log($"leadersListView.selectionChanged: {selectedId}");
        // };

        // var confirmButton = root.Q<Button>("ConfirmButton");
        // confirmButton.clicked += Hide;

        base.OnEnable();

        var exportButton = root.Q<Button>("ExportButton");
        exportButton.clicked += () =>
        {
            // var content = NavalGameState.Instance.LeadersToXML();
            var gameState = SuperGameState.Instance.GetCurrentGameState();
            var content = gameState.LeadersToXML();
            IOManager.Instance.SaveTextFile(content, "Leaders", "xml");
        };

        var importButton = root.Q<Button>("ImportButton");
        importButton.clicked += () =>
        {
            // IOManager.Instance.textLoaded += OnLeadersXMLLoaded;
            IOManager.Instance.LoadTextFile(OnLeadersXMLLoaded, "xml");
        };

        var setRemarkButton = root.Q<Button>("SetRemarkButton");
        setRemarkButton.clicked += () =>
        {
            var leader = selectedObject;
            if (leader == null)
                return;

            leader.remark ??= new GlobalString();
            DialogRoot.Instance.PopupGlobalStringMarkdownEditorDialog(leader.remark, "Remark");
        };

        var portraitField = root.Q<VisualElement>("PortraitField");
        PathReferenceBinder.BindPictureReference(portraitField);
    }

    void OnLeadersXMLLoaded(string text)
    {
        /// IOManager.Instance.textLoaded -= OnLeadersXMLLoaded;

        // NavalGameState.Instance.LeadersFromXML(text);
        // NavalGameState.Instance.ResetAndRegisterAll();

        var gameState = SuperGameState.Instance.GetCurrentGameState();
        gameState.LeadersFromXML(text);
        gameState.ResetAndRegisterAll();
    }

    [CreateProperty]
    public AbstractGameState currentGameState => SuperGameState.Instance.GetCurrentGameState();

    // public string selectedId;

    // [CreateProperty]
    // public Leader selectedObject
    // {
    //     get
    //     {
    //         return EntityManager.Instance.Get<Leader>(selectedId);
    //     }
    // }

    [CreateProperty]
    public bool isInEditMode => GamePreference.Instance.isInEditMode;
}
