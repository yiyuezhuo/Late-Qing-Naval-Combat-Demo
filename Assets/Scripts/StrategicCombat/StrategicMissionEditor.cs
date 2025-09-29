using StrategicCombatCore;
using UnityEngine.UIElements;
using UnityEngine;
using CoreUtils;

public class StrategicMissionEditor : LeftObjectPickerRightEditorStrategic<StrategicMissionEditor, StrategicMission>
{
    protected override void OnEnable()
    {
        base.OnEnable();

        var editWaypointsButton = root.Q<Button>("EditWaypointsButton");
        editWaypointsButton.clicked += () =>
        {
            Hide();
            StrategicGameManager.Instance.mapEditMode = StrategicMapEditMode.WaypointPlotting;

            // StrategicGameManager.Instance.ScheduleOneshotCellClickCallback(cell =>
            // {
            //     Debug.Log("StrategicMissionEditor waypoint click edit");
            // });
        };

        var contentContainer = root.Q<VisualElement>("ContentContainer");
        var groupsListView = root.Q<ListView>("GroupsListView");
        // Utils.BindStrategicGroupMemberReferenceListView(groupsListView, contentContainer, this);
        Utils.BindMissionMembership(groupsListView, contentContainer, this);

        // BindMissionMembership
        // Utils.BindItemsAddedRemoved<StrategicGroupMemberReference>(groupsListView, () => null);
        // groupsListView.makeItem = () =>
        // {
        //     var item = groupsListView.itemTemplate.CloneTree();

        //     return item;
        // };

    }
}