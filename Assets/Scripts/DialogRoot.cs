using NavalCombatCore;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

public class DialogRoot : SingletonDocument<DialogRoot>
{
    public VisualTreeAsset shipLogSelectorDocument;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    public void PopupShipLogSelectorDialogForRedeploy()
    {
        var tempDialog = new TempDialog()
        {
            root = root,
            template = shipLogSelectorDocument,
            templateDataSource = GameManager.Instance
        };

        tempDialog.onConfirmed += (sender, el) =>
        {
            Debug.Log("tempDialog.onConfirmed");

            var shipLogMultiColumnListView = el.Q<MultiColumnListView>("ShipLogMultiColumnListView");
            var selectedShipLog = shipLogMultiColumnListView.selectedItem as ShipLog;
            var latLon = GameManager.Instance.lastSelectedLatLon;
            if (selectedShipLog != null && latLon != null)
            {
                selectedShipLog.mapState = MapState.Deployed;
                selectedShipLog.position = latLon;
                // Set Default heading?
            }
        };

        tempDialog.Popup();
    }

    public void PopupShipLogSelectorDialogForAddShipLogToOOBItem()
    {
        var tempDialog = new TempDialog()
        {
            root = root,
            template = shipLogSelectorDocument,
            templateDataSource = GameManager.Instance
        };

        tempDialog.onConfirmed += (sender, el) =>
        {
            var addToShipGroup = OOBEditor.Instance.currentSelectedShipGroup;
            var shipLogMultiColumnListView = el.Q<MultiColumnListView>("ShipLogMultiColumnListView");
            var selectedShipLog = shipLogMultiColumnListView.selectedItem as ShipLog;

            if (addToShipGroup != null && selectedShipLog != null)
            {
                if (((IShipGroupMember)selectedShipLog).TryAttachTo(addToShipGroup))
                {
                    OOBEditor.Instance.Sync();
                }
                else
                {
                    Debug.LogWarning("Not attachable");
                }
            }
        };

        tempDialog.Popup();
    }

}
