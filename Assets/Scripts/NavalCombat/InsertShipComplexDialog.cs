using System.Collections.Generic;
using UnityEngine.UIElements;

using NavalCombatCore;
using System.Linq;
using UnityEngine;
using CoreUtils;

public class InsertShipComplexDialog
{
    // bind to dialog
    public List<ShipLog> validShipLogs;
    public List<NamedShip> validNamedShips;
    public List<ShipClass> validShipClasses;

    public enum Mode
    {
        None,
        ShipLog,
        NamedShip,
        ShipClass
    }

    public Mode mode;

    ListView validShipLogListView;
    ListView validNamedShipListView;
    ListView validShipClassListView;

    public void OnCreated(object sender, VisualElement root)
    {
        var gameState = NavalGameState.Instance;

        validShipLogs = gameState.shipLogs.Where(s => s.mapState != MapState.Deployed).ToList();
        
        var deployedShipLogs = gameState.shipLogs.Where(s => s.mapState == MapState.Deployed).ToList();
        var deployedNamedShipSet = deployedShipLogs.Select(s => s.namedShip).ToHashSet();
        
        validNamedShips = gameState.namedShips.Where(s => !deployedNamedShipSet.Contains(s)).ToList();
        validShipClasses = gameState.shipClasses.ToList();

        var shipGroupDropdownField = root.Q<DropdownField>("ShipGroupDropdownField");
        shipGroupDropdownField.choices = gameState.shipGroups.Select(g => g.name.GetMergedName()).ToList();

        validShipLogListView = root.Q<ListView>("ValidShipLogListView");
        validShipLogListView.selectionChanged += (IEnumerable<object> objects) =>
        {
            mode = Mode.ShipLog;
        };

        validNamedShipListView = root.Q<ListView>("ValidNamedShipListView");
        validNamedShipListView.selectionChanged += (IEnumerable<object> objects) =>
        {
            mode = Mode.NamedShip;
        };

        validShipClassListView = root.Q<ListView>("ValidShipClassListView");
        validShipClassListView.selectionChanged += (IEnumerable<object> objects) =>
        {
            mode = Mode.ShipClass;
        };
    }

    public void OnConfirm(object sender, VisualElement root)
    {
        Debug.Log("InsertShipComplexDialog confirmed");

        var gameState = NavalGameState.Instance;


        var latLon = GameManager.Instance.lastSelectedLatLon;
        if(latLon == null)
        {
            DialogRoot.Instance.PopupMessageDialog("Position is not selected");
            return;
        }

        ShipLog deployedShipLog = null;
        if(mode == Mode.None)
        {
            DialogRoot.Instance.PopupMessageDialog("Ship is not selected");
            return;
        }
        else if(mode == Mode.ShipLog)
        {
            var selectedShipLog = validShipLogListView.selectedItem as ShipLog;
            if (selectedShipLog != null)
            {
                deployedShipLog = selectedShipLog;
                // selectedShipLog.mapState = MapState.Deployed;
                // selectedShipLog.position = latLon;
                // Set Default heading?
            }
        }
        else if(mode == Mode.NamedShip)
        {
            var selectedNamedShip = validNamedShipListView.selectedItem as NamedShip;
            if(selectedNamedShip != null)
            {
                var shipLog = new ShipLog()
                {
                    namedShipObjectId = selectedNamedShip.objectId
                };
                shipLog.ResetDamageExpenditureState(new());
                
                gameState.shipLogs.Add(shipLog);
                EntityManager.Instance.Register(shipLog, null);

                deployedShipLog = shipLog;
                // shipLog.mapState = MapState.Deployed;
                // shipLog.position = latLon;
            }
        }
        else if(mode == Mode.ShipClass)
        {
            var selectedShipClass = validShipClassListView.selectedItem as ShipClass;
            if(selectedShipClass != null)
            {
                var namedShip = new NamedShip()
                {
                    name = gameState.GetNameForNewShipClass(selectedShipClass),
                    shipClassObjectId = selectedShipClass.objectId
                };
                
                gameState.namedShips.Add(namedShip);
                EntityManager.Instance.Register(namedShip, null);

                var shipLog = new ShipLog()
                {
                    namedShipObjectId = namedShip.objectId
                };
                shipLog.ResetDamageExpenditureState(new());
                
                gameState.shipLogs.Add(shipLog);
                EntityManager.Instance.Register(shipLog, null);

                deployedShipLog = shipLog;
                // shipLog.mapState = MapState.Deployed;
                // shipLog.position = latLon;
            }
        }

        if(deployedShipLog != null)
        {
            deployedShipLog.mapState = MapState.Deployed;
            deployedShipLog.position = latLon;

            var shipGroupDropdownField = root.Q<DropdownField>("ShipGroupDropdownField");

            var isShipGroupValid = shipGroupDropdownField.index >= 0 && shipGroupDropdownField.index < gameState.shipGroups.Count;
            if(isShipGroupValid)
            {
                var selectedShipGroup = gameState.shipGroups[shipGroupDropdownField.index];
                
                selectedShipGroup.childrenObjectIds.Add(deployedShipLog.objectId);
                deployedShipLog.parentObjectId = selectedShipGroup.objectId;
            }
        }
    }
}
