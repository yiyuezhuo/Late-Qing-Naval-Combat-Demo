using System;
using System.Collections.Generic;
using NavalCombatCore;
using Unity.Properties;
using UnityEngine.UIElements;

public class RapidFireBatteryRecordMetaInfoDialog
{
    public RapidFireBatteryRecord rapidFireBatteryRecord;
    public Action callback;

    [CreateProperty]
    public bool hasMetaInfo
    {
        get => rapidFireBatteryRecord?.metaInfo != null;
        set
        {
            if (rapidFireBatteryRecord == null)
                return;

            if (value)
                rapidFireBatteryRecord.metaInfo ??= new RapidFireBatteryRecordMetaInfo();
            else
                rapidFireBatteryRecord.metaInfo = null;
        }
    }

    [CreateProperty]
    public int fireControlTierIndex
    {
        get => Math.Clamp(rapidFireBatteryRecord?.metaInfo?.fireControlTier ?? 0, 0, 5);
        set
        {
            EnsureMetaInfo();
            if (rapidFireBatteryRecord?.metaInfo == null)
                return;

            rapidFireBatteryRecord.metaInfo.fireControlTier = Math.Clamp(value, 0, 5);
        }
    }

    public void OnCreated(object sender, VisualElement root)
    {
        root.dataSource = this;
        ConfigureDropdown(root.Q<DropdownField>("FireControlTierField"), FireControlTierChoices(), fireControlTierIndex);

        // var toggle = root.Q<Toggle>("HasMetaInfoToggle");
        // var metaInfoContainer = root.Q<VisualElement>("MetaInfoContainer");
        // var shellSizeField = root.Q<FloatField>("ShellSizeInchField");
        // var shellWeightField = root.Q<FloatField>("ShellWeightPoundsField");
        var inferOtherButton = root.Q<Button>("InferOtherButton");

        // void RefreshMetaInfoVisibility()
        // {
        //     if (metaInfoContainer != null)
        //     {
        //         metaInfoContainer.dataSource = rapidFireBatteryRecord?.metaInfo;
        //         metaInfoContainer.style.display = hasMetaInfo ? DisplayStyle.Flex : DisplayStyle.None;
        //     }

        //     if (shellSizeField != null && rapidFireBatteryRecord?.metaInfo != null)
        //         shellSizeField.SetValueWithoutNotify(rapidFireBatteryRecord.metaInfo.shellSizeInch);
        //     if (shellWeightField != null && rapidFireBatteryRecord?.metaInfo != null)
        //         shellWeightField.SetValueWithoutNotify(rapidFireBatteryRecord.metaInfo.shellWeightPounds);
        // }

        // toggle?.RegisterValueChangedCallback(evt =>
        // {
        //     hasMetaInfo = evt.newValue;
        //     RefreshMetaInfoVisibility();
        // });

        // shellSizeField?.RegisterValueChangedCallback(evt =>
        // {
        //     if (rapidFireBatteryRecord?.metaInfo != null)
        //         rapidFireBatteryRecord.metaInfo.shellSizeInch = evt.newValue;
        // });

        // shellWeightField?.RegisterValueChangedCallback(evt =>
        // {
        //     if (rapidFireBatteryRecord?.metaInfo != null)
        //         rapidFireBatteryRecord.metaInfo.shellWeightPounds = evt.newValue;
        // });

        // inferOtherButton?.RegisterCallback<ClickEvent>(_ =>
        // {
        //     rapidFireBatteryRecord?.InferDamageFactorFromMetaInfo();
        // });

        inferOtherButton.clicked += () =>
        {
            rapidFireBatteryRecord?.InferDamageFactorFromMetaInfo();
            rapidFireBatteryRecord?.InferFireControlRecordsFromMetaInfo();
        };

        // RefreshMetaInfoVisibility();
    }

    public void OnConfirm(object sender, VisualElement root)
    {
        callback?.Invoke();
    }

    void EnsureMetaInfo()
    {
        if (rapidFireBatteryRecord == null)
            return;

        rapidFireBatteryRecord.metaInfo ??= new RapidFireBatteryRecordMetaInfo();
    }

    static List<string> FireControlTierChoices()
    {
        return new List<string>
        {
            "Unspecified",
            "1 - (3pdr 5cwt Mk I,II, Royal Sovereign class pre-dreadnoughts, 1892-1908)",
            "2 - (3\"/50 12pdr QF Mk I, HMS Dreadnought, 1906-1920)",
            "3 - (4\"/45 QF MkV HA, HMS Hood, 1920-1940)",
            "4 - (25mm/60 Type, Yamato class battleships, 1941-1945)",
            "5 - (40mm/56 Mk1 Bofors, Iowa class battleships, 1943-1945)",
        };
    }

    static void ConfigureDropdown(DropdownField field, List<string> choices, int index)
    {
        if (field == null)
            return;

        field.choices = choices;
        field.index = Math.Clamp(index, choices.Count > 0 ? 0 : -1, choices.Count - 1);
    }
}
