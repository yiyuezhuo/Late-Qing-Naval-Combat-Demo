using StrategicCombatCore;
using UnityEngine.UIElements;

public class WeaponEditor : LeftObjectPickerRightEditorStrategic<WeaponEditor, Weapon>
{
    // public override string GetObjectListViewName() => "LandUnitListView";
    protected override void OnEnable()
    {
        base.OnEnable();

        PathReferenceBinder.BindPictureReference(root.Q<VisualElement>("PictureField"));
    }
}
