using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Xml.Serialization;
using System.Xml;
using System.IO;
using System.Linq;
using Unity.Properties;
using System;
using System.Collections;

using NavalCombatCore;
using CoreUtils;


public class NamedShipEditor : LeftObjectPickerRightEditor<NamedShipEditor, NamedShip> // HideableDocument<NamedShipEditor>
// public class NamedShipEditor : HideableDocument<NamedShipEditor>
{
    // public ListView objectListView;

    // public string selectedId;

    // [CreateProperty]
    // public NamedShip selectedObject
    // {
    //     get
    //     {
    //         return EntityManager.Instance.Get<NamedShip>(selectedId);
    //     }
    // }
    protected override void GetFullObjects()
    {
        fullObjects = SuperGameState.Instance.GetCurrentGameState().namedShips;
    }

    // protected override void Awake()
    protected override void OnEnable()
    {
        base.OnEnable();

        var exportButton = root.Q<Button>("ExportButton");
        exportButton.clicked += () =>
        {
            var gameState = SuperGameState.Instance.GetCurrentGameState();
            var content = gameState.NamedShipsToXML();
            IOManager.Instance.SaveTextFile(content, "NamedShips", "xml");
        };

        var importButton = root.Q<Button>("ImportButton");
        importButton.clicked += () =>
        {
            // IOManager.Instance.textLoaded += OnNamedShipsXMLLoaded;
            IOManager.Instance.LoadTextFile(OnNamedShipsXMLLoaded, "xml");
        };

        var selectShipClassButton = root.Q<Button>("SelectShipClassButton");
        selectShipClassButton.clicked += DialogRoot.Instance.PopupShipClassSelectorDialogForNamedShip;

        var selectDefaultLeaderButton = root.Q<Button>("SelectDefaultLeaderButton");
        selectDefaultLeaderButton.clicked += DialogRoot.Instance.PopupLeaderSelectorDialogForNamedShip;

        var setRemarkButton = root.Q<Button>("SetRemarkButton");
        setRemarkButton.clicked += () =>
        {
            var namedShip = selectedObject;
            if (namedShip == null)
                return;

            namedShip.remark ??= new GlobalString();
            DialogRoot.Instance.PopupGlobalStringMarkdownEditorDialog(namedShip.remark, "Remark");
        };

        var gotoShipClassButton = root.Q<Button>("GotoShipClassButton");
        gotoShipClassButton.clicked += () =>
        {
            var shipClass = selectedObject?.shipClass;

            SwitchCenter.Instance.SwitchToShipClassView(shipClass);
        };

        var gotoLeaderButton = root.Q<Button>("GotoLeaderButton");
        gotoLeaderButton.clicked += () =>
        {
            var leader = selectedObject?.defaultLeader;
            SwitchCenter.Instance.SwitchToLeaderView(leader);
        };
    }

    public EventHandler shown;

    protected override void OnShow()
    {
        shown?.Invoke(this, EventArgs.Empty);
    }

    void OnNamedShipsXMLLoaded(string text)
    {
        // IOManager.Instance.textLoaded -= OnNamedShipsXMLLoaded;

        var gameState = SuperGameState.Instance.GetCurrentGameState();
        gameState.NamedShipsFromXML(text);
        gameState.ResetAndRegisterAll();
    }

    [CreateProperty]
    public AbstractGameState currentGameState => SuperGameState.Instance.GetCurrentGameState();

    [CreateProperty]
    public bool isInEditMode => GamePreference.Instance.isInEditMode;

}
