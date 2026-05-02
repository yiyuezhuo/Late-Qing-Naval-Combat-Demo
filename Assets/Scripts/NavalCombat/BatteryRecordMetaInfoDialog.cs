using System;
using NavalCombatCore;
using Unity.Properties;
using UnityEngine.UIElements;

public class BatteryRecordMetaInfoDialog
{
    public BatteryRecord batteryRecord;
    public Action callback;

    [CreateProperty]
    public bool hasMetaInfo
    {
        get => batteryRecord?.metaInfo != null;
        set
        {
            if (batteryRecord == null)
                return;

            if (value)
            {
                EnsureMetaInfo();
            }
            else
            {
                batteryRecord.metaInfo = null;
            }

        }
    }

    [CreateProperty]
    public DisplayStyle metaInfoDisplay => hasMetaInfo ? DisplayStyle.Flex : DisplayStyle.None;

    [CreateProperty]
    public int capTypeIndex
    {
        get => batteryRecord?.metaInfo?.naabLikeProjectile == null ? 0 : Math.Clamp(batteryRecord.metaInfo.naabLikeProjectile.hcwclcrCapType, 0, 4);
        set
        {
            EnsureMetaInfo();
            if (batteryRecord?.metaInfo?.naabLikeProjectile == null)
                return;
            batteryRecord.metaInfo.naabLikeProjectile.hcwclcrCapType = Math.Clamp(value, 0, 4);
        }
    }

    [CreateProperty]
    public int dragFunctionIndex
    {
        get => batteryRecord?.metaInfo?.naabLikeProjectile == null ? 2 : NaabLikeCalculatorDialog.GetDragFunctionIndex(batteryRecord.metaInfo.naabLikeProjectile.dragFunction);
        set
        {
            EnsureMetaInfo();
            if (batteryRecord?.metaInfo?.naabLikeProjectile == null)
                return;
            batteryRecord.metaInfo.naabLikeProjectile.dragFunction = value switch
            {
                0 => NaabLikeDragFunction.G1,
                1 => NaabLikeDragFunction.G2,
                3 => NaabLikeDragFunction.G6,
                4 => NaabLikeDragFunction.G7,
                5 => NaabLikeDragFunction.G8,
                6 => NaabLikeDragFunction.G9,
                7 => NaabLikeDragFunction.GS,
                8 => NaabLikeDragFunction.GL,
                _ => NaabLikeDragFunction.G5
            };
        }
    }

    public void OnCreated(object sender, VisualElement root)
    {
        root.dataSource = this;
        ConfigureDropdown(root.Q<DropdownField>("CapTypeField"), new() { "None", "Hard Cap", "Medium Cap", "Soft Cap", "Hood" }, capTypeIndex);
        ConfigureDropdown(root.Q<DropdownField>("DragFunctionField"), new() { "G1", "G2", "G5", "G6", "G7", "G8", "G9", "GS", "GL" }, dragFunctionIndex);

        var displayButton = root.Q<Button>("DisplayButton");
        if (displayButton != null)
            displayButton.clicked += OpenCalculator;
    }

    public void OnConfirm(object sender, VisualElement root)
    {
        callback?.Invoke();
    }

    void OpenCalculator()
    {
        if (batteryRecord == null)
            return;
        EnsureMetaInfo();
        DialogRoot.Instance.PopupNaabLikeCalculatorDialog(
            NaabLikeCalculatorLaunchContext.FromBatteryRecord(batteryRecord, true));
    }

    void EnsureMetaInfo()
    {
        if (batteryRecord == null)
            return;
        var createdMeta = batteryRecord.metaInfo == null;
        batteryRecord.metaInfo ??= new BatteryRecordMetaInfo();

        if (batteryRecord.metaInfo.naabLikeProjectile == null || createdMeta)
            batteryRecord.metaInfo.naabLikeProjectile = CreateProjectileFromBatteryRecord();
    }

    NaabLikeProjectile CreateProjectileFromBatteryRecord()
    {
        var projectile = NaabLikeProjectile.CreateDefaultMetaProjectile();
        if (batteryRecord.shellSizeInch > 0f)
            projectile.diameterInches = batteryRecord.shellSizeInch;
        if (batteryRecord.shellWeightPounds > 0f)
        {
            projectile.totalWeightPounds = batteryRecord.shellWeightPounds;
            projectile.bodyWeightPounds = batteryRecord.shellWeightPounds;
        }
        if (batteryRecord.rangeYards > 0f)
            projectile.maxRangeYards = batteryRecord.rangeYards;
        return projectile;
    }

    static void ConfigureDropdown(DropdownField field, System.Collections.Generic.List<string> choices, int index)
    {
        if (field == null)
            return;
        field.choices = choices;
        field.index = Math.Clamp(index, choices.Count > 0 ? 0 : -1, choices.Count - 1);
    }
}
