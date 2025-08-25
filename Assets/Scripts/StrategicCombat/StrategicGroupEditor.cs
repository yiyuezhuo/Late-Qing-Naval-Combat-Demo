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

    protected override void OnEnable()
    {
        base.OnEnable();

        subordinatesCombinedListView = root.Q<ListView>("SubordinatesCombinedListView");
        Utils.BindItemsAddedRemoved<StrategicGroupMemberReference>(subordinatesCombinedListView, () => null);
        subordinatesCombinedListView.makeItem = () =>
        {
            var item = subordinatesCombinedListView.itemTemplate.CloneTree();

            var setButton = item.Q<Button>("SetButton");
            setButton.clicked += () =>
            {
                if (Utils.TryResolveCurrentValueForBinding(setButton, out StrategicGroupMemberReference fieldReference))
                {
                    Debug.Log("reference SetButton clicked");

                    DialogRoot.Instance.PopupSubordinatePickerDialog(selectedReferenceables =>
                    {
                        var oldObj = fieldReference.Get();
                        if (oldObj != null)
                        {
                            // oldObj.SetStrategicGroupReference(null);
                            oldObj.strategicGroupReference.referenceId = null;
                        }

                        var selectedReferenceable = selectedReferenceables.FirstOrDefault();
                        if (selectedReferenceable != null && selectedObject != null)
                        {
                            selectedReferenceable.SetStrategicGroupReference(null);
                            fieldReference.referenceId = selectedReferenceable.objectId;
                            selectedReferenceable.strategicGroupReference.referenceId = selectedObject.objectId;
                            // reference.referenceId = element.objectId;
                        }
                    });
                }
            };

            var gotoButton = item.Q<Button>("GotoButton");
            gotoButton.clicked += () =>
            {
                if (Utils.TryResolveCurrentValueForBinding(gotoButton, out StrategicGroupMemberReference fieldReference))
                {
                    Debug.Log("reference GotoButton clicked");

                    var gotoObj = fieldReference.Get();
                    GotoReferenceable(gotoObj);
                }

            };

            return item;
        };

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

        var gotoParentButton = root.Q<Button>("GotoParentButton");
        gotoParentButton.clicked += () =>
        {
            if (Utils.TryResolveCurrentValueForBinding(gotoParentButton, out StrategicGroup group))
            {
                var parentGroup = group.strategicGroupReference.Get();
                var idx = StrategicGameState.Instance.strategicGroups.IndexOf(parentGroup);
                if (parentGroup != null && idx != -1)
                {
                    // BehaviourUtils.Instance.ScheduleToSetSelectionForListView(objectListView, idx);
                    Utils.SetSelectionForListView(objectListView, idx);
                }
            }
        };
    }

    void GotoReferenceable(IStrategicGroupMemberReferenceable gotoObj)
    {
        if (gotoObj is StrategicGroup group)
        {
            var idx = StrategicGameState.Instance.strategicGroups.IndexOf(group);
            if (group != null && idx != -1)
            {
                // BehaviourUtils.Instance.ScheduleToSetSelectionForListView(objectListView, idx);
                Utils.SetSelectionForListView(objectListView, idx);
            }
        }
        else if (gotoObj is ShipLog shipLog)
        {
            var idx = StrategicGameState.Instance.shipLogs.IndexOf(shipLog);
            if (shipLog != null && idx != -1)
            {
                ShipLogEditor.Instance.Show();
                BehaviourUtils.Instance.ScheduleToSetSelectionForListView(ShipLogEditor.Instance.shipLogListView, idx);
            }
        }
        else if (gotoObj is LandUnit landUnit)
        {
            var idx = StrategicGameState.Instance.landUnits.IndexOf(landUnit);
            if (landUnit != null && idx != -1)
            {
                LandUnitEditor.Instance.Show();
                BehaviourUtils.Instance.ScheduleToSetSelectionForListView(LandUnitEditor.Instance.objectListView, idx);
            }
        }

    }
}
