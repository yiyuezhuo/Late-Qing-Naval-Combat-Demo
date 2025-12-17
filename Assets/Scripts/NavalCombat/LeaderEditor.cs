using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
using Unity.Properties;

// using NavalCombatCore;
using CoreUtils;

public class LeaderEditor : HideableDocument<LeaderEditor>
{
    public ListView leadersListView;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    // protected override void Awake()\
    void OnEnable()
    {
        // base.Awake();

        // root.dataSource = GameManager.Instance;
        root.dataSource = this;

        Utils.BindItemsSourceRecursive(root);

        leadersListView = root.Q<ListView>("LeadersListView");
        Utils.BindItemsAddedRemoved<Leader>(leadersListView, () => null);

        leadersListView.selectionChanged += (IEnumerable<object> objects) =>
        {
            var leader = objects.FirstOrDefault() as Leader;
            selectedLeaderObjectId = leader?.objectId;

            Debug.Log($"leadersListView.selectionChanged: {selectedLeaderObjectId}");
        };

        var confirmButton = root.Q<Button>("ConfirmButton");
        confirmButton.clicked += Hide;

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

    public string selectedLeaderObjectId;

    [CreateProperty]
    public Leader selectedLeader
    {
        get
        {
            return EntityManager.Instance.Get<Leader>(selectedLeaderObjectId);
        }
    }
}
