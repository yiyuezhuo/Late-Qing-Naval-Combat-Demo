using System;
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

    public void OnCreated(object sender, VisualElement root)
    {
        var toggle = root.Q<Toggle>("HasMetaInfoToggle");
        var metaInfoContainer = root.Q<VisualElement>("MetaInfoContainer");
        var shellSizeField = root.Q<FloatField>("ShellSizeInchField");

        void RefreshMetaInfoVisibility()
        {
            if (metaInfoContainer != null)
            {
                metaInfoContainer.dataSource = rapidFireBatteryRecord?.metaInfo;
                metaInfoContainer.style.display = hasMetaInfo ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (shellSizeField != null && rapidFireBatteryRecord?.metaInfo != null)
                shellSizeField.SetValueWithoutNotify(rapidFireBatteryRecord.metaInfo.shellSizeInch);
        }

        toggle?.RegisterValueChangedCallback(evt =>
        {
            hasMetaInfo = evt.newValue;
            RefreshMetaInfoVisibility();
        });

        shellSizeField?.RegisterValueChangedCallback(evt =>
        {
            if (rapidFireBatteryRecord?.metaInfo != null)
                rapidFireBatteryRecord.metaInfo.shellSizeInch = evt.newValue;
        });

        RefreshMetaInfoVisibility();
    }

    public void OnConfirm(object sender, VisualElement root)
    {
        callback?.Invoke();
    }
}
