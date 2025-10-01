using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
using Unity.Properties;

// using NavalCombatCore;
using CoreUtils;
using StrategicCombatCore;

public class LandUnitEditor : LeftObjectPickerRightEditorStrategic<LandUnitEditor, LandUnit>
{
    protected override void OnEnable()
    {
        base.OnEnable();

        var setLandUnitTemplateButton = root.Q<Button>("SetLandUnitTemplateButton");
        setLandUnitTemplateButton.clicked += () =>
        {
            Debug.Log("SetLandUnitTemplateButton clicked");

            DialogRoot.Instance.PopupLandUnitTemplatePickerDialog(template =>
            {
                if (Utils.TryResolveCurrentValueForBinding<LandUnit>(setLandUnitTemplateButton, out var landUnit))
                {
                    landUnit.landUnitTemplateId = template.objectId;
                }
            });
        };

        var gotoLandUnitTemplateButton = root.Q<Button>("GotoLandUnitTemplateButton");
        gotoLandUnitTemplateButton.clicked += () =>
        {
            if (Utils.TryResolveCurrentValueForBinding<LandUnit>(gotoLandUnitTemplateButton, out var landUnit))
            {
                var idx = currentGameState.landUnitTemplates.IndexOf(EntityManager.Instance.Get<LandUnitTemplate>(landUnit.landUnitTemplateId));
                if (idx != -1)
                {
                    LandUnitTemplateEditor.Instance.Show();
                    BehaviourUtils.Instance.ScheduleToSetSelectionForListView(LandUnitTemplateEditor.Instance.objectListView, idx);
                    // BehaviourUtils.Instance.ScheduleToSetSelectionForListView(objectListView, idx);
                }
            }
        };

        // StrategicGroupEditor.BindIStrategicGroupMemberReferenceable(root, this);
        Utils.BindIStrategicGroupMemberReferenceable(root, this);
    }

    protected override void ProcessCopliedLastOne(LandUnit landUnit)
    {
        landUnit.strategicGroupReference.referenceId = null;
    }

    // public override string GetObjectListViewName() => "LandUnitListView";
}
