using StrategicCombatCore;
using UnityEngine.UIElements;
using NavalCombatCore;

public class SideStateEditor : LeftObjectPickerRightEditorStrategic<SideStateEditor, SideState>
{
    // public override string GetObjectListViewName() => "LandUnitListView";
    protected override void OnEnable()
    {
        base.OnEnable();

        var diplomacyRelationMultiColumnListView = root.Q<MultiColumnListView>("DiplomacyRelationMultiColumnListView");
        Utils.BindItemsAddedRemoved<DiplomacyRelation>(diplomacyRelationMultiColumnListView, () => null);

        var sideCol = diplomacyRelationMultiColumnListView.columns["side"];
        sideCol.makeCell = () =>
        {
            var el = sideCol.cellTemplate.CloneTree();

            var setButton = el.Q<Button>("SetButton");
            // PictureReferenceBinder.Bind(pictureField);
            setButton.clicked += () =>
            {
                if (Utils.TryResolveCurrentValueForBinding(setButton, out DiplomacyRelation dipRel))
                {
                    // dipRel
                    DialogRoot.Instance.PopupSideStatePickerDialog(sideState =>
                    {
                        dipRel.sideObjectId = sideState.objectId;
                    });
                }
            };

            return el;
        };

        var extraAmmunitionLoadoutWeightRecordsMultiColumnListView = root.Q<MultiColumnListView>("ExtraAmmunitionLoadoutWeightRecordsMultiColumnListView");
        Utils.BindItemsAddedRemoved<AmmunitionLoadoutWeightRecord>(extraAmmunitionLoadoutWeightRecordsMultiColumnListView, () => null);

        // PictureReferenceBinder.Bind(root.Q<VisualElement>("PictureField"));
    }

    protected override void OnConfirmButtonClickedBefore()
    {
        StrategicGameState.Instance.RebuildCacheForSideStates();
    }
}
