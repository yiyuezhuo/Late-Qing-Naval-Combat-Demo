using System.Collections.Generic;
using System.Linq;
using NavalCombatCore;
using UnityEngine.UIElements;
using UnityEngine;
using System;
using Unity.Properties;

public class RapidFireBatteryRecordSelectorDialog
{
    public List<Item> items = new();
    public Action<RapidFireBatteryRecord> callback;

    public void OnCreated(object sender, VisualElement root)
    {
        items = SuperGameState.Instance.currentGameState.shipClasses.SelectMany(
            s => s.rapidFireBatteryRecords.Select(b => new Item(){shipClass=s, rapidBatteryRecord=b})
        ).ToList();
    }

    public void OnConfirm(object sender, VisualElement root)
    {
        var listView = root.Q<ListView>("RapidFireBatteryRecordListView");

        if(callback == null)
        {
            Debug.Log($"Fallback: Selected {listView.selectedItem as Item}");
        }
        else
        {
            callback((listView.selectedItem as Item)?.rapidBatteryRecord);
        }
    }

    public class Item
    {
        public ShipClass shipClass;
        public RapidFireBatteryRecord rapidBatteryRecord;

        [CreateProperty]
        public string labelName => $"{shipClass.name.GetShortName()} | {rapidBatteryRecord.name.GetShortName()} ({rapidBatteryRecord.damageFactor})";
    }
}