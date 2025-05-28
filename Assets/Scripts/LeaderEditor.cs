using UnityEngine;
using NavalCombatCore;
using GeographicLib;
using TMPro;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Xml.Serialization;
using System.Xml;
using System.IO;
using System.Linq;

public class LeaderEditor : HideableDocument<LeaderEditor>
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    protected override void Awake()
    {
        base.Awake();

        root.dataSource = GameManager.Instance;

        Utils.BindItemsSourceRecursive(root);

        var leadersListView = root.Q<ListView>("LeadersListView");
        Utils.BindItemsAddedRemoved<Leader>(leadersListView, () => null);

        leadersListView.selectionChanged += (IEnumerable<object> objects) =>
        {
            Debug.Log("leadersListView.selectionChanged");

            var leader = objects.FirstOrDefault() as Leader;
            GameManager.Instance.selectedLeaderObjectId = leader?.objectId;
        };

        var confirmButton = root.Q<Button>("ConfirmButton");
        confirmButton.clicked += Hide;

        var exportButton = root.Q<Button>("ExportButton");
        exportButton.clicked += () =>
        {
            var content = GameManager.Instance.navalGameState.LeadersToXML();
            IOManager.Instance.SaveTextFile(content, "Leaders", "xml");
        };

        var importButton = root.Q<Button>("ImportButton");
        importButton.clicked += () =>
        {
            IOManager.Instance.textLoaded += OnLeadersXMLLoaded;
            IOManager.Instance.LoadTextFile("xml");
        };
    }

    void OnLeadersXMLLoaded(object sender, string text)
    {
        IOManager.Instance.textLoaded -= OnLeadersXMLLoaded;

        GameManager.Instance.navalGameState.LeadersFromXML(text);
    }
}
