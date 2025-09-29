using CoreUtils;
using StrategicCombatCore;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
using NavalCombatCore;

public class StrategicGroupEditor : LeftObjectPickerRightEditorStrategic<StrategicGroupEditor, StrategicGroup>
{
    ListView subordinatesCombinedListView;

    // public void BindGotoButton(VisualElement item)
    // {
    //     var gotoButton = item.Q<Button>("GotoButton");
    //     gotoButton.clicked += () =>
    //     {
    //         if (Utils.TryResolveCurrentValueForBinding(gotoButton, out StrategicGroupMemberReference fieldReference))
    //         {
    //             Debug.Log("reference GotoButton clicked");

    //             var gotoObj = fieldReference.Get();
    //             GotoReferenceable(gotoObj);
    //         }
    //     };
    // }

    protected override void OnEnable()
    {
        base.OnEnable();

        var contentContainer = root.Q<VisualElement>("ContentContainer");
        subordinatesCombinedListView = root.Q<ListView>("SubordinatesCombinedListView");
        Utils.BindStrategicGroupMemberReferenceListView(subordinatesCombinedListView, contentContainer, this);

        // Utils.BindItemsAddedRemoved<StrategicGroupMemberReference>(subordinatesCombinedListView, () => null);
        // subordinatesCombinedListView.makeItem = () =>
        // {
        //     var item = subordinatesCombinedListView.itemTemplate.CloneTree();
        //     // BindStrategicGroupMemberReference(item);

        //     var setButton = item.Q<Button>("SetButton");
        //     setButton.clicked += () =>
        //     {
        //         if (Utils.TryResolveCurrentValueForBinding(contentContainer, out StrategicGroup selectedStrategicGroup) &&
        //             Utils.TryResolveCurrentValueForBinding(setButton, out StrategicGroupMemberReference fieldReference))
        //         {
        //             Debug.Log("reference SetButton clicked");

        //             DialogRoot.Instance.PopupSubordinatePickerDialog(selectedReferenceables =>
        //             {
        //                 var oldObj = fieldReference.Get();
        //                 if (oldObj != null)
        //                 {
        //                     // oldObj.SetStrategicGroupReference(null);
        //                     oldObj.strategicGroupReference.referenceId = null;
        //                 }

        //                 var selectedReferenceable = selectedReferenceables.FirstOrDefault();

        //                 if (selectedReferenceable != null && selectedStrategicGroup != null)
        //                 {
        //                     selectedReferenceable.SetStrategicGroupReference(null);
        //                     fieldReference.referenceId = selectedReferenceable.objectId;
        //                     selectedReferenceable.strategicGroupReference.referenceId = selectedStrategicGroup.objectId;
        //                 }
        //             }, true);
        //         }
        //     };

        //     BindGotoButton(item);

        //     return item;
        // };

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

        // BindIStrategicGroupMemberReferenceable(root, this);
        Utils.BindIStrategicGroupMemberReferenceable(root, this);

        var gotoAssignedMissionButton = root.Q<Button>("GotoAssignedMissionButton");
        gotoAssignedMissionButton.clicked += () =>
        {
            if (Utils.TryResolveCurrentValueForBinding<StrategicGroup>(gotoAssignedMissionButton, out var group))
            {
                var mission = EntityManager.Instance.Get<StrategicMission>(group.assignedMissionObjectId);
                if (mission != null)
                {
                    var idx = StrategicGameState.Instance.missions.IndexOf(mission);
                    if (idx != -1)
                    {
                        Hide();
                        StrategicMissionEditor.Instance.Show();
                        BehaviourUtils.Instance.ScheduleToSetSelectionForListView(StrategicMissionEditor.Instance.objectListView, idx);
                    }
                }
            }
        };
    }

    // void GotoReferenceable(IStrategicGroupMemberReferenceable gotoObj)
    // {
    //     if (gotoObj is StrategicGroup group)
    //     {
    //         var idx = StrategicGameState.Instance.strategicGroups.IndexOf(group);
    //         if (group != null && idx != -1)
    //         {
    //             // BehaviourUtils.Instance.ScheduleToSetSelectionForListView(objectListView, idx);
    //             Utils.SetSelectionForListView(objectListView, idx);
    //         }
    //     }
    //     else if (gotoObj is ShipLog shipLog)
    //     {
    //         var idx = StrategicGameState.Instance.shipLogs.IndexOf(shipLog);
    //         if (shipLog != null && idx != -1)
    //         {
    //             Hide();
    //             ShipLogEditor.Instance.Show();
    //             BehaviourUtils.Instance.ScheduleToSetSelectionForListView(ShipLogEditor.Instance.shipLogListView, idx);
    //         }
    //     }
    //     else if (gotoObj is LandUnit landUnit)
    //     {
    //         var idx = StrategicGameState.Instance.landUnits.IndexOf(landUnit);
    //         if (landUnit != null && idx != -1)
    //         {
    //             Hide();
    //             LandUnitEditor.Instance.Show();
    //             BehaviourUtils.Instance.ScheduleToSetSelectionForListView(LandUnitEditor.Instance.objectListView, idx);
    //         }
    //     }
    // }
}
