using CoreUtils;
using StrategicCombatCore;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
using NavalCombatCore;


public class StrategicGroupView // 
{
    public VisualElement root;

    ListView subordinatesCombinedListView;

    public void Bind()
    {
        var contentContainer = root.Q<VisualElement>("StrategicGroupView");
        subordinatesCombinedListView = root.Q<ListView>("SubordinatesCombinedListView");
        Utils.BindStrategicGroupMemberReferenceListView(subordinatesCombinedListView, contentContainer);

        var setLeaderButton = root.Q<Button>("SetLeaderButton");
        setLeaderButton.clicked += () =>
        {
            DialogRoot.Instance.PopupLeaderSelectorDialogForCallback(leader =>
            {
                if (Utils.TryResolveCurrentValueForBinding(setLeaderButton, out StrategicGroup group))
                {
                    group.leaderReference.referenceObjectId = leader.objectId;
                }
            });
        };

        var gotoLeaderButton = root.Q<Button>("GotoLeaderButton");
        gotoLeaderButton.clicked += () =>
        {
            if (Utils.TryResolveCurrentValueForBinding<StrategicGroup>(gotoLeaderButton, out var group))
            {
                var leader = group.leaderReference.Get();
                // if (leader != null)
                // {
                //     var idx = StrategicGameState.Instance.leaders.IndexOf(leader);
                //     if (idx != -1)
                //     {
                //         Hide();
                //         LeaderEditor.Instance.Show();
                //         BehaviourUtils.Instance.ScheduleToSetSelectionForListView(LeaderEditor.Instance.leadersListView, idx);
                //     }
                // }
                SwitchCenter.Instance.SwitchToLeaderView(leader);
            }
        };
        
        Utils.BindIStrategicGroupMemberReferenceable(root);

        var gotoAssignedMissionButton = root.Q<Button>("GotoAssignedMissionButton");
        gotoAssignedMissionButton.clicked += () =>
        {
            if (Utils.TryResolveCurrentValueForBinding<StrategicGroup>(gotoAssignedMissionButton, out var group))
            {
                var mission = EntityManager.Instance.Get<StrategicMission>(group.assignedMissionObjectId);
                // if (mission != null)
                // {
                //     var idx = StrategicGameState.Instance.missions.IndexOf(mission);
                //     if (idx != -1)
                //     {
                //         Hide();
                //         StrategicMissionEditor.Instance.Show();
                //         BehaviourUtils.Instance.ScheduleToSetSelectionForListView(StrategicMissionEditor.Instance.objectListView, idx);
                //     }
                // }
                SwitchCenter.Instance.SwitchToMissionView(mission);
            }
        };


        var splitButton = root.Q<Button>("SplitButton");
        splitButton.clicked += () =>
        {
            if (Utils.TryResolveCurrentValueForBinding(splitButton, out StrategicGroup group))
            {
                group.Split();
            }
        };

        var gotoContainerButton = root.Q<Button>("GotoContainerButton");
        gotoContainerButton.clicked += () =>
        {
            if(Utils.TryResolveCurrentValueForBinding(gotoContainerButton, out StrategicGroup group))
            {
                var container = EntityManager.Instance.Get<ShipLog>(group.containerObjectId);
                // if(container != null)
                // {
                //     var idx = StrategicGameState.Instance.shipLogs.IndexOf(container);
                //     if (idx != -1)
                //     {
                //         Hide();
                //         ShipLogEditor.Instance.Show();
                //         BehaviourUtils.Instance.ScheduleToSetSelectionForListView(ShipLogEditor.Instance.shipLogListView, idx);
                //     }
                // }

                SwitchCenter.Instance.SwitchToShipLogView(container);
            }
        };

        var setArriveToButton = root.Q<Button>("SetArriveToButton");
        setArriveToButton.clicked += () =>
        {
            SwitchCenter.Instance.TryToSoftHideCurrent();

            if(Utils.TryResolveCurrentValueForBinding(setArriveToButton, out StrategicGroup.ArriveState arriveState))
            {
                StrategicGameManager.Instance.ScheduleOneshotCellClickCallback(cell =>
                {
                    arriveState.arriveTo = cell.ToXY();

                    // SwitchCenter.Instance.SwitchToStrategicGroupView(group);
                    SwitchCenter.Instance.RetoreCurrentSoftHide();
                });
            }
        };
    }
}

public class StrategicGroupEditor : LeftObjectPickerRightEditorStrategic<StrategicGroupEditor, StrategicGroup>
{
    // ListView subordinatesCombinedListView;

    protected override void GetFullObjects()
    {
        fullObjects = StrategicGameState.Instance.strategicGroups;
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        var view = new StrategicGroupView()
        {
            root = root.Q<VisualElement>("ContentContainer")  
        };
        view.Bind();

        root.Q<Button>("SortButton").clicked += () =>
        {
            StrategicGameState.Instance.strategicGroups.Sort((group0, group1) =>
            {
                // by country
                var res = group0.country.CompareTo(group1.country);
                if (res != 0)
                    return res;

                // by size
                res = group0.size.CompareTo(group1.size);
                if (res != 0)
                    return res;

                // by type
                res = group0.type.CompareTo(group1.type);
                if (res != 0)
                    return res;

                // by name
                return group0.name.GetMergedName().CompareTo(group1.name.GetMergedName());
            });
        };
    }

    protected override void ProcessCopiedLastOne(StrategicGroup group)
    {
        group.strategicGroupReference.referenceId = null;
        group.assignedMissionObjectId = null;
    }

}
