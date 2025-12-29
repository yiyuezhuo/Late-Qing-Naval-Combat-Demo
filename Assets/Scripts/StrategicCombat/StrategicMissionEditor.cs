using StrategicCombatCore;
using UnityEngine.UIElements;
using UnityEngine;
using CoreUtils;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

public class StrategicMissionEditor : LeftObjectPickerRightEditorStrategic<StrategicMissionEditor, StrategicMission>
{
    protected override void GetFullObjects()
    {
        fullObjects = StrategicGameState.Instance.missions;
    }

    protected override IEnumerable<StrategicMission> ExtraFilter(IEnumerable<StrategicMission> missions)
    {
        if(GamePreference.Instance.isInEditMode)
            return missions;
        
        var viewerSide = StrategicGameManager.Instance.GetViewerSide();
        var viewerSideObjectId = viewerSide?.objectId;
        return missions.Where(missions => missions.sideObjectId ==  null || missions.sideObjectId == "" || missions.sideObjectId == viewerSideObjectId);
    }

    protected override void ProcessRemovedOne(StrategicMission removedMission)
    {
        removedMission.RemoveCleanup();
    }

    protected override void OnAddObjectButtonClicked()
    {
        // var newObj = new ET();
        // EntityManager.Instance.Register(newObj, null);
        // fullObjects.Add(newObj);

        // ProcessAddedOne(newObj);

        // RefreshFilter();

        DialogRoot.Instance.PopupCreateMissionDialog(newObj =>
        {
            ProcessAddedOne(newObj);

            RefreshFilter();
        });
    }

    protected override void ProcessAddedOne(StrategicMission newMission)
    {
        var viewerSide = StrategicGameManager.Instance.GetViewerSide();
        var viewerSideObjectId = viewerSide?.objectId;
        newMission.sideObjectId = viewerSideObjectId;
    }

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

        BindDepotSetGotoButton(root.Q<Button>("SetSourceDepotButton"), root.Q<Button>("GotoSourceDepotButton"));
        BindDepotSetGotoButton(root.Q<Button>("SetTargetDepotButton"), root.Q<Button>("GotoTargetDepotButton"));

        var parentMissionButton = root.Q<Button>("ParentMissionButton");
        parentMissionButton.clicked += () =>
        {
            if(Utils.TryResolveCurrentValueForBinding<StrategicMissionReference>(parentMissionButton, out var strategicMissionRef))
            {
                var mission = strategicMissionRef.Get();
                if(mission != null)
                {
                    SwitchCenter.Instance.SwitchToMissionView(mission);
                }
            }
        };

        var childrenMissionsListView = root.Q<ListView>("ChildrenMissionsListView");
        Utils.BindItemsAddedRemoved<StrategicMissionReference>(childrenMissionsListView, () => null);
        childrenMissionsListView.makeItem = () =>
        {
            var el = childrenMissionsListView.itemTemplate.CloneTree();

            var gotoButton = el.Q<Button>("GotoButton");
            gotoButton.clicked += () =>
            {
                if(Utils.TryResolveCurrentValueForBinding(gotoButton, out StrategicMissionReference strategicMissionRef))
                {
                    var mission = strategicMissionRef.Get();
                    if(mission != null)
                    {
                        SwitchCenter.Instance.SwitchToMissionView(mission);
                    }
                }
            };

            var setButton = el.Q<Button>("SetButton");
            setButton.clicked += () =>
            {
                Debug.Log("SetButton clicked");

                if(Utils.TryResolveCurrentValueForBinding(editWaypointsButton, out StrategicMission parentMission) &&
                    Utils.TryResolveCurrentValueForBinding(setButton, out StrategicMissionReference strategicMissionRef))
                {
                    // strategicMissionRef.SetTo(parentMission, )
                    // DialogRoot.Instance.PopupMIssion
                    DialogRoot.Instance.PopupStrategicMissionSelectorDialogDocument(mission =>
                    {
                        strategicMissionRef.SetTo(parentMission, mission);
                    }, parentMission);
                }
            };

            return el;
        };

        var setSideButton = root.Q<Button>("SetSideButton");
        setSideButton.clicked += () =>
        {
            if(Utils.TryResolveCurrentValueForBinding(setSideButton, out StrategicMission mission))
            {
                DialogRoot.Instance.PopupSideStatePickerDialog(side =>
                {
                    mission.sideObjectId = side.objectId;
                });
            }
        };
    }

    public void BindDepotSetGotoButton(Button setButton, Button gotoButton)
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

                SwitchCenter.Instance.SwitchToLandUnitView(sourceDepot);
            }
        };
    }
}