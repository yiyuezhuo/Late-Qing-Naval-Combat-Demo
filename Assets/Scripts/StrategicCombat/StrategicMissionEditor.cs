using StrategicCombatCore;
using UnityEngine.UIElements;
using UnityEngine;

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
    }
}