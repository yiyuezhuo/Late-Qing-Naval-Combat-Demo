using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
using Unity.Properties;

// using NavalCombatCore;
using CoreUtils;
using StrategicCombatCore;


public class LandUnitTemplateEditor : LeftObjectPickerRightEditorStrategic<LandUnitTemplateEditor, LandUnitTemplate>
{
    // public override string GetObjectListViewName() => "LandUnitListView";
    protected override void OnEnable()
    {
        base.OnEnable();

        var weaponRecordsMultiColumnListView = root.Q<MultiColumnListView>("WeaponRecordsMultiColumnListView");
        Utils.BindItemsAddedRemoved<WeaponRecord>(weaponRecordsMultiColumnListView, () => null);

        var setColumn = weaponRecordsMultiColumnListView.columns["set"];
        setColumn.makeCell = () =>
        {
            var el = setColumn.cellTemplate.CloneTree();
            var setButton = el.Q<Button>("SetButton");
            setButton.clicked += () =>
            {
                if (Utils.TryResolveCurrentValueForBinding(setButton, out WeaponRecord weaponRecord))
                {
                    // Debug.Log("Pick Weapon");

                    DialogRoot.Instance.PopupWeaponPickerDialog(weapon =>
                    {
                        weaponRecord.weaponObjectId = weapon.objectId;
                    });
                }
            };

            return el;
        };
    }
}
