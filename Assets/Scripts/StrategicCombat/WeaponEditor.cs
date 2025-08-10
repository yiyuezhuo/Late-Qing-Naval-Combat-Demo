using StrategicCombatCore;
using UnityEngine.UIElements;

public class WeaponEditor : LeftObjectPickerRightEditorStrategic<WeaponEditor, Weapon>
{
    // public override string GetObjectListViewName() => "LandUnitListView";
    protected override void OnEnable()
    {
        base.OnEnable();

        PictureReferenceBinder.Bind(root.Q<VisualElement>("PictureField"));
    }
}
