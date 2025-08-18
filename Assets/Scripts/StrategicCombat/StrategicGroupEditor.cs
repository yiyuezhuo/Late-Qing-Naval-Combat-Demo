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

                    DialogRoot.Instance.PopupSubordinatePickerDialog(objects => {
                        if (Utils.TryResolveCurrentValueForBinding(setButton, out StrategicGroupMemberReference reference))
                        {
                            var element = objects.FirstOrDefault();
                            if (element != null)
                            {
                                reference.referenceId = element.objectId;
                            }
                        }
                    });
                }
            };
            return item;
        };
    }
}
