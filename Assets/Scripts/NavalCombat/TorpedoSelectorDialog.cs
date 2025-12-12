using System.Collections.Generic;
using System.Linq;
using NavalCombatCore;
using UnityEngine.UIElements;
using UnityEngine;
using System;
using Unity.Properties;

public class TorpedoSectorSelectorDialog
{
    public List<Item> items = new();
    public Action<ShipClass> callback;

    public void OnCreated(object sender, VisualElement root)
    {
        items = SuperGameState.Instance.currentGameState.shipClasses.Select(
            s => new Item(){shipClass=s}
        ).ToList();
    }

    public void OnConfirm(object sender, VisualElement root)
    {
        var listView = root.Q<ListView>("ItemListView");

        if(callback == null)
        {
            Debug.Log($"Fallback: Selected {listView.selectedItem as Item}");
        }
        else
        {
            callback((listView.selectedItem as Item)?.shipClass);
        }
    }

    public class Item
    {
        public ShipClass shipClass;

        [CreateProperty]
        public string labelName => $"{shipClass.name.GetShortName()} | {shipClass.torpedoSector.name.GetShortName()} ({shipClass.torpedoSector.damageClass})";
    }
}