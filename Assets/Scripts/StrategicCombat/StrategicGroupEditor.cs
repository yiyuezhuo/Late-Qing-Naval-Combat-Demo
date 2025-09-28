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

    public void BindGotoButton(VisualElement item)
    {
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
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        subordinatesCombinedListView = root.Q<ListView>("SubordinatesCombinedListView");
        Utils.BindItemsAddedRemoved<StrategicGroupMemberReference>(subordinatesCombinedListView, () => null);
        subordinatesCombinedListView.makeItem = () =>
        {
            var item = subordinatesCombinedListView.itemTemplate.CloneTree();
            // BindStrategicGroupMemberReference(item);

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
                    }, true);
                }
            };

            BindGotoButton(item);

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

        // BindIStrategicGroupMemberReferenceable(root, this);
        Utils.BindIStrategicGroupMemberReferenceable(root, this);

        // var gotoParentButton = root.Q<Button>("GotoParentButton");
        // gotoParentButton.clicked += () =>
        // {
        //     if (Utils.TryResolveCurrentValueForBinding(gotoParentButton, out IStrategicGroupMemberReferenceable group))
        //     {
        //         var parentGroup = group.strategicGroupReference.Get();
        //         var idx = StrategicGameState.Instance.strategicGroups.IndexOf(parentGroup);
        //         if (parentGroup != null && idx != -1)
        //         {
        //             // BehaviourUtils.Instance.ScheduleToSetSelectionForListView(objectListView, idx);
        //             Utils.SetSelectionForListView(objectListView, idx);
        //         }
        //     }
        // };

        // var currentSourceDepotButton = root.Q<Button>("CurrentSourceDepotButton");
        // currentSourceDepotButton.clicked += () =>
        // {
        //     if (Utils.TryResolveCurrentValueForBinding(currentSourceDepotButton, out IStrategicGroupMemberReferenceable group))
        //     {
        //         var currentSourceDepot = group.GetCurrentSourceDepot();
        //         if (currentSourceDepot != null)
        //         {
        //             var idx = StrategicGameState.Instance.landUnits.IndexOf(currentSourceDepot);
        //             if (idx != -1)
        //             {
        //                 // Hide();
        //                 LandUnitEditor.Instance.Show();
        //                 BehaviourUtils.Instance.ScheduleToSetSelectionForListView(LandUnitEditor.Instance.objectListView, idx);
        //             }
        //         }
        //     }
        // };
    }

    // TODO: Move to a neutral location
    // public static void BindIStrategicGroupMemberReferenceable<T>(VisualElement root, SingletonDocument<T> meDoc) where T : MonoBehaviour
    // {
    //     var gotoParentButton = root.Q<Button>("GotoParentButton");
    //     gotoParentButton.clicked += () =>
    //     {
    //         if (Utils.TryResolveCurrentValueForBinding(gotoParentButton, out IStrategicGroupMemberReferenceable group))
    //         {
    //             var parentGroup = group.strategicGroupReference.Get();
    //             var idx = StrategicGameState.Instance.strategicGroups.IndexOf(parentGroup);
    //             if (parentGroup != null && idx != -1)
    //             {
    //                 // gameObject
    //                 if (!StrategicGroupEditor.Instance.gameObject.activeSelf)
    //                 {
    //                     meDoc.Hide();
    //                     StrategicGroupEditor.Instance.Show();
    //                 }
    //                 // Utils.SetSelectionForListView(StrategicGroupEditor.Instance.objectListView, idx);
    //                 BehaviourUtils.Instance.ScheduleToSetSelectionForListView(StrategicGroupEditor.Instance.objectListView, idx);
    //             }
    //         }
    //     };

    //     var currentSourceDepotButton = root.Q<Button>("CurrentSourceDepotButton");
    //     currentSourceDepotButton.clicked += () =>
    //     {
    //         if (Utils.TryResolveCurrentValueForBinding(currentSourceDepotButton, out IStrategicGroupMemberReferenceable group))
    //         {
    //             var currentSourceDepot = group.GetCurrentSourceDepot();
    //             if (currentSourceDepot != null)
    //             {
    //                 var idx = StrategicGameState.Instance.landUnits.IndexOf(currentSourceDepot);
    //                 if (idx != -1)
    //                 {
    //                     // Hide();
    //                     if (!LandUnitEditor.Instance.gameObject.activeSelf)
    //                     {
    //                         meDoc.Hide();
    //                         LandUnitEditor.Instance.Show();
    //                     }
    //                     BehaviourUtils.Instance.ScheduleToSetSelectionForListView(LandUnitEditor.Instance.objectListView, idx);
    //                 }
    //             }
    //         }
    //     };
    // }

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
                Hide();
                ShipLogEditor.Instance.Show();
                BehaviourUtils.Instance.ScheduleToSetSelectionForListView(ShipLogEditor.Instance.shipLogListView, idx);
            }
        }
        else if (gotoObj is LandUnit landUnit)
        {
            var idx = StrategicGameState.Instance.landUnits.IndexOf(landUnit);
            if (landUnit != null && idx != -1)
            {
                Hide();
                LandUnitEditor.Instance.Show();
                BehaviourUtils.Instance.ScheduleToSetSelectionForListView(LandUnitEditor.Instance.objectListView, idx);
            }
        }

    }
}
