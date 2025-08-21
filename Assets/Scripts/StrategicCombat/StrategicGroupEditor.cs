using CoreUtils;
using StrategicCombatCore;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

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
                if (Utils.TryResolveCurrentValueForBinding(setButton, out StrategicGroupMemberReference reference))
                {
                    Debug.Log("reference SetButton clicked");

                    DialogRoot.Instance.PopupSubordinatePickerDialog(selectedReferenceables =>
                    {
                        if (Utils.TryResolveCurrentValueForBinding(setButton, out StrategicGroupMemberReference fieldReference))
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
                        }
                    });
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
    }
}
