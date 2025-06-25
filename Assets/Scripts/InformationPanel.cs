using NavalCombatCore;
using UnityEngine;
using UnityEngine.UIElements;

public class InformationPanel : SingletonDocument<InformationPanel>
{
    protected override void Awake()
    {
        base.Awake();

        var captainLabel = root.Q<Label>("CaptainLabel");
        Utils.RegisterLinkTag(captainLabel, new()
        {
            {"captain", () => {
                Debug.Log("Captain link clicked");

                var leader = GameManager.Instance.selectedShipLog?.leader;
                if(leader == null)
                    return;

                var idx = NavalGameState.Instance.leaders.IndexOf(leader);
                if(leader != null && idx != -1)
                {
                    LeaderEditor.Instance.Show();
                    StartCoroutine(Utils.SetSelectionForListView(LeaderEditor.Instance.leadersListView, idx));
                }
            }}
        });

        var namedShipLabel = root.Q<Label>("NamedShipLabel"); // Open ShipLog or NamedShip??
        Utils.RegisterLinkTag(namedShipLabel, new()
        {
            {"namedShip", () => {
                var shipLog = GameManager.Instance.selectedShipLog;
                ShipLogEditor.Instance.PopupWithSelection(shipLog);
            } }
        });

        var classLabel = root.Q<Label>("ClassLabel");
        Utils.RegisterLinkTag(classLabel, new()
        {
            {"shipClass", () => {
                var shipClass = GameManager.Instance.selectedShipLog?.shipClass;
                var idx = NavalGameState.Instance.shipClasses.IndexOf(shipClass);
                if(shipClass != null && idx != -1)
                {
                    ShipClassEditor.Instance.Show();
                    // ShipClassEditor.Instance.shipClassListView.SetSelection(idx);
                    StartCoroutine(Utils.SetSelectionForListView(ShipClassEditor.Instance.shipClassListView, idx));
                }
            } }
        });
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
