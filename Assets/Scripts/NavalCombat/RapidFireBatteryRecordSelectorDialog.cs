using System.Collections.Generic;
using System.Linq;
using NavalCombatCore;
using UnityEngine.UIElements;
using UnityEngine;
using System;
using Unity.Properties;

public class RapidFireBatteryRecordSelectorDialog
{
    List<Item> fullItems = new();
    public List<Item> items = new();
    public Action<RapidFireBatteryRecord> callback;
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
        fullItems = SuperGameState.Instance.currentGameState.shipClasses.SelectMany(
            s => s.rapidFireBatteryRecords.Select(b => new Item(){shipClass=s, rapidBatteryRecord=b})
        ).ToList();
        RefreshFilter();
    }

    public void RefreshFilter()
    {
        if (string.IsNullOrEmpty(_filterString))
        {
            items = fullItems;
            return;
        }

        items = fullItems.Where(item => item.labelName != null && item.labelName.Contains(_filterString)).ToList();
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
