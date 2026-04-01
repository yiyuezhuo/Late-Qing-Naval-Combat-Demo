using NavalCombatCore;
using UnityEngine;
using UnityEngine.UIElements;
using System;
using System.Collections;

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
                // if(leader == null)
                //     return;

                // var idx = NavalGameState.Instance.leaders.IndexOf(leader);
                // if(leader != null && idx != -1)
                // {
                //     LeaderEditor.Instance.Show();
                //     BehaviourUtils.Instance.ScheduleToSetSelectionForListView(LeaderEditor.Instance.leadersListView, idx);
                // }

                SwitchCenter.Instance.SwitchToLeaderView(leader);
            }}
        });

        var namedShipLabel = root.Q<Label>("NamedShipLabel"); // Open ShipLog or NamedShip??
        Utils.RegisterLinkTag(namedShipLabel, new()
        {
            {"namedShip", () => {
                var shipLog = GameManager.Instance.selectedShipLog;
                // ShipLogEditor.Instance.PopupWithSelection(shipLog);
                SwitchCenter.Instance.SwitchToShipLogView(shipLog);
            } }
        });

        var classLabel = root.Q<Label>("ClassLabel");
        Utils.RegisterLinkTag(classLabel, new()
        {
            {"shipClass", () => {
                var shipClass = GameManager.Instance.selectedShipLog?.shipClass;
                // var idx = NavalGameState.Instance.shipClasses.IndexOf(shipClass);
                // if(shipClass != null && idx != -1)
                // {
                //     ShipClassEditor.Instance.Show();
                //     // ShipClassEditor.Instance.shipClassListView.SetSelection(idx);
                //     BehaviourUtils.Instance.ScheduleToSetSelectionForListView(ShipClassEditor.Instance.shipClassListView, idx);
                // }

                SwitchCenter.Instance.SwitchToShipClassView(shipClass);
            } }
        });

        var oobParentLabel = root.Q<Label>("OOBParentLabel");
        Utils.RegisterLinkTag(oobParentLabel, new()
        {
            {"group", () =>{
                var member = GameManager.Instance.selectedShipLog as IShipGroupMember;
                var parentGroup = member.GetParentGroup();
                if(parentGroup != null)
                {
                    OOBEditor.Instance.Show();
                    BehaviourUtils.Instance.StartCoroutine(SetSelectionForOOBEditorTreeViewNextFrame(parentGroup.objectId));
                }
            }}
        });

        var setAttackTargetButton = root.Q<Button>("SetAttackTargetButton");
        setAttackTargetButton.clicked += () =>
        {
            var selectedShipLog = GameManager.Instance.selectedShipLog;
            if (selectedShipLog == null || selectedShipLog.IsSurpriseCommandChangeBlocked())
                return;
            GameManager.Instance.state = GameManager.State.SelectingShipLevelFiringTarget;
        };

    }

    static IEnumerator SetSelectionForOOBEditorTreeViewNextFrame(string objectId)
    {
        // yield return new WaitForNextFrameUnit();
        yield return null;
        OOBEditor.Instance.TrySetSelection(objectId);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
