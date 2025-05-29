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
using Unity.Properties;
using System.Threading.Tasks;
using System;

public class NamedShipEditor : HideableDocument<NamedShipEditor>
{
    protected override void Awake()
    {
        base.Awake();

        root.dataSource = GameManager.Instance;

        Utils.BindItemsSourceRecursive(root);

        var namedShipListView = root.Q<ListView>("NamedShipListView");
        Utils.BindItemsAddedRemoved<NamedShip>(namedShipListView, () => null);

        namedShipListView.selectionChanged += (IEnumerable<object> objects) =>
        {
            Debug.Log("namedShipListView.selectionChanged");

            var namedShip = objects.FirstOrDefault() as NamedShip;
            GameManager.Instance.selectedNamedShipObjectId = namedShip?.objectId;
        };

        var confirmButton = root.Q<Button>("ConfirmButton");
        confirmButton.clicked += Hide;

        var exportButton = root.Q<Button>("ExportButton");
        exportButton.clicked += () =>
        {
            var content = GameManager.Instance.navalGameState.NamedShipsToXML();
            IOManager.Instance.SaveTextFile(content, "NamedShips", "xml");
        };

        var importButton = root.Q<Button>("ImportButton");
        importButton.clicked += () =>
        {
            IOManager.Instance.textLoaded += OnNamedShipsXMLLoaded;
            IOManager.Instance.LoadTextFile("xml");
        };

        var selectShipClassButton = root.Q<Button>("SelectShipClassButton");
        selectShipClassButton.clicked += DialogRoot.Instance.PopupShipClassSelectorDialogForNamedShip;

        var selectDefaultLeaderButton = root.Q<Button>("SelectDefaultLeaderButton");
        selectDefaultLeaderButton.clicked += DialogRoot.Instance.PopupLeaderSelectorDialogForNamedShip;
    }

    void OnNamedShipsXMLLoaded(object sender, string text)
    {
        IOManager.Instance.textLoaded -= OnNamedShipsXMLLoaded;

        GameManager.Instance.navalGameState.NamedShipsFromXML(text);
    }

}
