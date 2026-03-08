using System.Collections.Generic;
using System.Linq;
using NavalCombatCore;
using UnityEngine.UIElements;
using UnityEngine;
using System;
using Unity.Properties;

public class BatteryRecordSelectorDialog
{
    List<BatteryRecord> fullBatteryRecords = new();
    public List<BatteryRecord> currentBatteryRecords = new();
    public Action<BatteryRecord> callback;
    string _filterString;

    [CreateProperty]
    public string filterString
    {
        get => _filterString;
        set
        {
            _filterString = value;
            RefreshFilter();
        }
    }

    public void OnCreated(object sender, VisualElement root)
    {
        fullBatteryRecords = SuperGameState.Instance.currentGameState.shipClasses.SelectMany(s => s.batteryRecords).ToList();
        RefreshFilter();
    }

    public void RefreshFilter()
    {
        if (string.IsNullOrEmpty(_filterString))
        {
            currentBatteryRecords = fullBatteryRecords;
            return;
        }

        currentBatteryRecords = fullBatteryRecords.Where(b => b.labelName != null && b.labelName.Contains(_filterString)).ToList();
    }

    public void OnConfirm(object sender, VisualElement root)
    {
        var listView = root.Q<ListView>("BatteryRecordListView");

        if(callback == null)
        {
            Debug.Log($"Fallback: Selected {listView.selectedItem as BatteryRecord}");
        }
        else
        {
            callback(listView.selectedItem as BatteryRecord);
        }
    }
}
