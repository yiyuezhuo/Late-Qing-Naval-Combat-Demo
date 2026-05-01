using System;
using NavalCombatCore;
using Unity.Properties;
using UnityEngine.UIElements;

public class BatteryRecordMetaInfoDialog : INotifyBindablePropertyChanged
{
    public BatteryRecord batteryRecord;
    public Action callback;

    public event EventHandler<BindablePropertyChangedEventArgs> propertyChanged;

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
                batteryRecord.metaInfo ??= new BatteryRecordMetaInfo();
                batteryRecord.metaInfo.naabLikeProjectile ??= new NaabLikeProjectile();
            }
            else
            {
                batteryRecord.metaInfo = null;
            }

            Notify(nameof(hasMetaInfo));
            Notify(nameof(metaInfoDisplay));
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
            Notify(nameof(capTypeIndex));
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
            Notify(nameof(dragFunctionIndex));
        }
    }

    public void OnCreated(object sender, VisualElement root)
    {
        root.dataSource = this;
        ConfigureDropdown(root.Q<DropdownField>("CapTypeField"), new() { "None", "Hard Cap", "Medium Cap", "Soft Cap", "Hood" }, capTypeIndex);
        ConfigureDropdown(root.Q<DropdownField>("DragFunctionField"), new() { "G1", "G2", "G5", "G6", "G7", "G8", "G9", "GS", "GL" }, dragFunctionIndex);

        var fitButton = root.Q<Button>("FitButton");
        var displayButton = root.Q<Button>("DisplayButton");
        if (fitButton != null)
            fitButton.clicked += () => OpenCalculator(false);
        if (displayButton != null)
            displayButton.clicked += () => OpenCalculator(true);
    }

    public void OnConfirm(object sender, VisualElement root)
    {
        callback?.Invoke();
    }

    void OpenCalculator(bool displayProjectile)
    {
        if (batteryRecord == null)
            return;
        if (displayProjectile)
        {
            batteryRecord.metaInfo ??= new BatteryRecordMetaInfo();
            batteryRecord.metaInfo.naabLikeProjectile ??= new NaabLikeProjectile();
        }
        DialogRoot.Instance.PopupNaabLikeCalculatorDialog(
            NaabLikeCalculatorLaunchContext.FromBatteryRecord(batteryRecord, displayProjectile));
    }

    void EnsureMetaInfo()
    {
        if (batteryRecord == null)
            return;
        batteryRecord.metaInfo ??= new BatteryRecordMetaInfo();
        batteryRecord.metaInfo.naabLikeProjectile ??= new NaabLikeProjectile();
    }

    static void ConfigureDropdown(DropdownField field, System.Collections.Generic.List<string> choices, int index)
    {
        if (field == null)
            return;
        field.choices = choices;
        field.index = Math.Clamp(index, choices.Count > 0 ? 0 : -1, choices.Count - 1);
    }

    void Notify(string propertyName)
    {
        var bindingId = new BindingId(propertyName);
        propertyChanged?.Invoke(this, new BindablePropertyChangedEventArgs(in bindingId));
    }
}
