using StrategicCombatCore;
using UnityEngine.UIElements;

public class SideStateEditor : LeftObjectPickerRightEditorStrategic<SideStateEditor, SideState>
{
    // public override string GetObjectListViewName() => "LandUnitListView";
    // protected override void OnEnable()
    // {
    //     base.OnEnable();

    //     PictureReferenceBinder.Bind(root.Q<VisualElement>("PictureField"));
    // }

    protected override void OnConfirmButtonClickedBefore()
    {
        StrategicGameState.Instance.RebuildCacheForSideStates();
    }
}
