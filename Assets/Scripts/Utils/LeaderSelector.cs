using System.Collections.Generic;
using Unity.Properties;
using UnityEngine.UIElements;
using NavalCombatCore;
using System.Linq;
using CoreUtils;
using System;

public class NamedSelector<T> where T : class, INamed
{
    // Required Arguments
    public List<T> fullObjects;
    public Action<T> callback;

    string _filterString;
    
    public List<T> filteredObjects;

    // ListView namedShipListView;

    [CreateProperty]
    public string filterString
    {
        get => _filterString;
        set
        {
            _filterString = value;
            Refresh();
        }
    }

    public void Refresh()
    {
        if(_filterString == null || _filterString == "")
        {
            filteredObjects = fullObjects;
            return;
        }

        filteredObjects = fullObjects.Where(s => s.GetName().MatchAny(_filterString)).ToList();
    }

    public void OnConfirm(object sender, VisualElement el)
    {
        var objectListView = el.Q<ListView>("ObjectListView");
        var selectedObj = objectListView.selectedItem as T;
        callback(selectedObj);
    }

}


// Placeholder to deal with UITK Builder
public class PlaceholderNamedObject : INamed
{
    public GlobalString GetName() => null;
}

public class PlaceholderNamedSelector : NamedSelector<PlaceholderNamedObject>
{
}


public class LeaderSelector : NamedSelector<Leader>
{}
