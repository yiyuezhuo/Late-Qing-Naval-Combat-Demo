using System.Collections.Generic;
using System.Linq;
using NavalCombatCore;
using UnityEngine.UIElements;
using UnityEngine;
using System;
using UnityEngine.AI;

public class BatteryRecordSelectorDialog
{
    public List<BatteryRecord> currentBatteryRecords = new();
    public Action<BatteryRecord> callback;

    public void OnCreated(object sender, VisualElement root)
    {
        currentBatteryRecords = SuperGameState.Instance.currentGameState.shipClasses.SelectMany(s => s.batteryRecords).ToList();
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