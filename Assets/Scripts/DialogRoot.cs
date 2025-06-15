using NavalCombatCore;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

public class DialogRoot : SingletonDocument<DialogRoot>
{
    public VisualTreeAsset shipLogSelectorDocument;
    public VisualTreeAsset leaderSelectorDocument;
    public VisualTreeAsset namedShipSelectorDocument;
    public VisualTreeAsset messageDialogDocument;
    public VisualTreeAsset streamingAssetReferenceDialogDocument;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    public void PopupStreamingAssetReferenceDialog()
    {
        var tempDialog = new TempDialog()
        {
            root = root,
            template = streamingAssetReferenceDialogDocument,
            // templateDataSource = StreamingAssetReference.Instance
            templateDataSource = ReferenceManager.Instance
        };

        tempDialog.Popup();
    }

    public void PopupMessageDialog(string message, string title = null)
    {
        var tempDialog = new TempDialog()
        {
            root = root,
            template = messageDialogDocument,
            templateDataSource = null
        };

        tempDialog.onCreated += (sender, el) =>
        {
            var contentTextField = el.Q<TextField>("ContentTextField");

            contentTextField.SetValueWithoutNotify(message);
            if (title != null)
            {
                var titleLabel = el.Q<Label>("TitleLabel");
                titleLabel.text = title;
            }
        };

        tempDialog.Popup();
    }

    public void PopupLeaderSelectorDialogForSpecifyForGroup()
    {
        var tempDialog = new TempDialog()
        {
            root = root,
            template = leaderSelectorDocument,
            templateDataSource = GameManager.Instance
        };

        tempDialog.onConfirmed += (sender, el) =>
        {
            Debug.Log("tempDialog.onConfirmed");

            var leadersListView = el.Q<ListView>("LeadersListView");
            var leader = leadersListView.selectedItem as Leader;
            var selectedGroup = OOBEditor.Instance.currentSelectedShipGroup;

            if (leader != null && selectedGroup != null)
            {
                selectedGroup.leaderObjectId = leader.objectId;
            }
        };

        tempDialog.Popup();
    }

    public void PopupLeaderSelectorDialogForSpecifyForShipLog()
    {
        var tempDialog = new TempDialog()
        {
            root = root,
            template = leaderSelectorDocument,
            templateDataSource = GameManager.Instance
        };

        tempDialog.onConfirmed += (sender, el) =>
        {
            Debug.Log("tempDialog.onConfirmed");

            var leadersListView = el.Q<ListView>("LeadersListView");
            var leader = leadersListView.selectedItem as Leader;
            var selectedShipLog = GameManager.Instance.selectedShipLog;

            if (leader != null && selectedShipLog != null)
            {
                selectedShipLog.leaderObjectId = leader.objectId;
            }
        };

        tempDialog.Popup();
    }

    public void PopupLeaderSelectorDialogForNamedShip()
    {
        var tempDialog = new TempDialog()
        {
            root = root,
            template = leaderSelectorDocument,
            templateDataSource = GameManager.Instance
        };

        tempDialog.onConfirmed += (sender, el) =>
        {
            Debug.Log("tempDialog.onConfirmed");

            var leadersListView = el.Q<ListView>("LeadersListView");
            var leader = leadersListView.selectedItem as Leader;
            var selectedNamedShip = GameManager.Instance.selectedNamedShip;

            if (leader != null && selectedNamedShip != null)
            {
                selectedNamedShip.defaultLeaderObjectId = leader.objectId;
            }
        };

        tempDialog.Popup();
    }

    public void PopupShipClassSelectorDialogForNamedShip()
    {
        var tempDialog = new TempDialog()
        {
            root = root,
            template = shipLogSelectorDocument,
            templateDataSource = GameManager.Instance
        };

        tempDialog.onConfirmed += (sender, el) =>
        {
            var selectedNamedShip = GameManager.Instance.selectedNamedShip;

            var shipClassListView = el.Q<ListView>("ShipClassListView");
            var selectedShipClass = shipClassListView.selectedItem as ShipClass;
            if (selectedNamedShip != null && selectedShipClass != null)
            {
                selectedNamedShip.shipClassObjectId = selectedShipClass.objectId;
            }
        };

        tempDialog.Popup();
    }

    public void PopupNamedShipSelctorDialogForShipLog()
    {
        var tempDialog = new TempDialog()
        {
            root = root,
            template = namedShipSelectorDocument,
            templateDataSource = GameManager.Instance
        };

        tempDialog.onConfirmed += (sender, el) =>
        {
            var selectedShipLog = GameManager.Instance.selectedShipLog;

            var namedShipListView = el.Q<ListView>("NamedShipListView");
            var namedShip = namedShipListView.selectedItem as NamedShip;
            if (selectedShipLog != null && namedShip != null)
            {
                selectedShipLog.namedShipObjectId = namedShip.objectId;
            }
        };

        tempDialog.Popup();
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
