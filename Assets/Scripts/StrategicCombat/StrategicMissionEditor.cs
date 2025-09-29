using StrategicCombatCore;
using UnityEngine.UIElements;
using UnityEngine;
using CoreUtils;
using System.Linq;
using UnityEditor.Localization.Plugins.XLIFF.V12;

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
        };

        var contentContainer = root.Q<VisualElement>("ContentContainer");
        var groupsListView = root.Q<ListView>("GroupsListView");
        // Utils.BindStrategicGroupMemberReferenceListView(groupsListView, contentContainer, this);
        Utils.BindMissionMembership(groupsListView, contentContainer, this);

        BindSetGotoButton(root.Q<Button>("SetSourceDepotButton"), root.Q<Button>("GotoSourceDepotButton"));
        BindSetGotoButton(root.Q<Button>("SetTargetDepotButton"), root.Q<Button>("GotoTargetDepotButton"));

        // var setSourceDepotButton = root.Q<Button>("SetSourceDepotButton");
        // setSourceDepotButton.clicked += () =>
        // {
        //     if (Utils.TryResolveCurrentValueForBinding(setSourceDepotButton, out StrategicMission mission))
        //     {
        //         DialogRoot.Instance.PopupSubordinatePickerDialog(selectedReferenceables =>
        //         {
        //             var depot = selectedReferenceables.FirstOrDefault() as LandUnit;
        //             if (depot != null)
        //             {
        //                 mission.sourceDepotObjectId = depot.objectId;
        //             }

        //         }, SubordinatePickerDialog.Mode.Depot);
        //     }
        // };

        // var gotoSourceDepotButton = root.Q<Button>("GotoSourceDepotButton");
        // gotoSourceDepotButton.clicked += () =>
        // {
        //     if (Utils.TryResolveCurrentValueForBinding(setSourceDepotButton, out StrategicMission mission))
        //     {
        //         var sourceDepot = EntityManager.Instance.Get<LandUnit>(mission.sourceDepotObjectId);
        //         if (sourceDepot == null)
        //             return;

        //         var idx = StrategicGameState.Instance.landUnits.IndexOf(sourceDepot);
        //         if (idx != -1)
        //         {
        //             Hide();
        //             LandUnitEditor.Instance.Show();
        //             BehaviourUtils.Instance.ScheduleToSetSelectionForListView(LandUnitEditor.Instance.objectListView, idx);
        //         }
        //     }
        // };
    }

    public void BindSetGotoButton(Button setButton, Button gotoButton)
    {
        setButton.clicked += () =>
        {
            if (Utils.TryResolveCurrentValueForBinding(setButton, out LandUnitReference landUnitRef))
            {
                DialogRoot.Instance.PopupSubordinatePickerDialog(selectedReferenceables =>
                {
                    var depot = selectedReferenceables.FirstOrDefault() as LandUnit;
                    if (depot != null)
                    {
                        landUnitRef.objectId = depot.objectId;
                    }

                }, SubordinatePickerDialog.Mode.Depot);
            }
        };

        gotoButton.clicked += () =>
        {
            if (Utils.TryResolveCurrentValueForBinding(gotoButton, out LandUnitReference landUnitRef))
            {
                var sourceDepot = landUnitRef.Get();
                if (sourceDepot == null)
                    return;

                var idx = StrategicGameState.Instance.landUnits.IndexOf(sourceDepot);
                if (idx != -1)
                {
                    Hide();
                    LandUnitEditor.Instance.Show();
                    BehaviourUtils.Instance.ScheduleToSetSelectionForListView(LandUnitEditor.Instance.objectListView, idx);
                }
            }
        };

    }
}