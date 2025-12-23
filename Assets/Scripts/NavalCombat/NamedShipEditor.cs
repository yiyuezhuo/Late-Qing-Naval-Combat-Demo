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


public class NamedShipEditor : HideableDocument<NamedShipEditor>
{
    public ListView namedShipListView;

    public string selectedNamedShipObjectId;

    [CreateProperty]
    public NamedShip selectedNamedShip
    {
        get
        {
            return EntityManager.Instance.Get<NamedShip>(selectedNamedShipObjectId);
        }
    }

    // protected override void Awake()
    void OnEnable()
    {
        // base.Awake();

        // var sortingOrder = doc.sortingOrder;
        // Debug.Log($"NamedShipEditor sortingOrder={sortingOrder}");

        root.dataSource = this;

        Utils.BindItemsSourceRecursive(root);

        namedShipListView = root.Q<ListView>("NamedShipListView");
        Utils.BindItemsAddedRemoved<NamedShip>(namedShipListView, () => null);

        namedShipListView.selectionChanged += (IEnumerable<object> objects) =>
        {
            Debug.Log("namedShipListView.selectionChanged");

            var namedShip = objects.FirstOrDefault() as NamedShip;
            selectedNamedShipObjectId = namedShip?.objectId;
        };

        var confirmButton = root.Q<Button>("ConfirmButton");
        confirmButton.clicked += Hide;

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

        var gotoShipClassButton = root.Q<Button>("GotoShipClassButton");
        gotoShipClassButton.clicked += () =>
        {
            var shipClass = selectedNamedShip?.shipClass;
            // if (shipClass == null)
            //     return;

            // var gameState = SuperGameState.Instance.GetCurrentGameState();
            // var idx = gameState.shipClasses.IndexOf(shipClass);
            // if (idx != -1)
            // {
            //     Hide();
            //     ShipClassEditor.Instance.Show();
            //     // ShipClassEditor.Instance.shipClassListView.SetSelection(idx);
            //     BehaviourUtils.Instance.ScheduleToSetSelectionForListView(ShipClassEditor.Instance.shipClassListView, idx);
            // }

            SwitchCenter.Instance.SwitchToShipClassView(shipClass);
        };

        var gotoLeaderButton = root.Q<Button>("GotoLeaderButton");
        gotoLeaderButton.clicked += () =>
        {
            var leader = selectedNamedShip?.defaultLeader;
            // if (leader == null)
            //     return;

            // var gameState = SuperGameState.Instance.GetCurrentGameState();
            // var idx = gameState.leaders.IndexOf(leader);
            // if (idx != -1)
            // {
            //     Hide();
            //     LeaderEditor.Instance.Show();
            //     // LeaderEditor.Instance.leadersListView.SetSelection(idx);
            //     BehaviourUtils.Instance.ScheduleToSetSelectionForListView(LeaderEditor.Instance.leadersListView, idx);
            // }
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

}
