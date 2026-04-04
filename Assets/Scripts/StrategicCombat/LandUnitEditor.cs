using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
using Unity.Properties;

// using NavalCombatCore;
using CoreUtils;
using StrategicCombatCore;

public class LandUnitView
{
    public VisualElement root;

    public void Bind()
    {
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
                var landUnitTemplate = EntityManager.Instance.Get<LandUnitTemplate>(landUnit.landUnitTemplateId);
                // var idx = currentGameState.landUnitTemplates.IndexOf(landUnitTemplate);
                // if (idx != -1)
                // {
                //     LandUnitTemplateEditor.Instance.Show();
                //     BehaviourUtils.Instance.ScheduleToSetSelectionForListView(LandUnitTemplateEditor.Instance.objectListView, idx);
                //     // BehaviourUtils.Instance.ScheduleToSetSelectionForListView(objectListView, idx);
                // }

                SwitchCenter.Instance.SwitchToLandUnitTemplateView(landUnitTemplate);
            }
        };

        // Utils.BindIStrategicGroupMemberReferenceable(root, this);
        Utils.BindIStrategicGroupMemberReferenceable(root);
    }
}

public class LandUnitEditor : LeftObjectPickerRightEditorStrategic<LandUnitEditor, LandUnit>
{
    protected override void OnEnable()
    {
        base.OnEnable();

        var binder = new LandUnitView()
        {
            root = root.Q<VisualElement>("LandUnitView")
        };
        binder.Bind();
    }

    protected override void ProcessCopiedLastOne(LandUnit landUnit)
    {
        landUnit.parentGroupReference.referenceId = null;
        landUnit.detachedFromGroupReference.referenceId = null;
        landUnit.enableAutoReattach = false;
    }

    // public override string GetObjectListViewName() => "LandUnitListView";
}
