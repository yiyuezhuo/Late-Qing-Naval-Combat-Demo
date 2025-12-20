using System.Collections.Generic;
using Unity.Properties;
using UnityEngine.UIElements;
using NavalCombatCore;
using System.Linq;

public class NamedShipSelector
{
    public string _filterString;
    public List<NamedShip> fullNamedShips;
    public List<NamedShip> filteredNamedShips;

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
            filteredNamedShips = fullNamedShips;
            return;
        }

        filteredNamedShips = fullNamedShips.Where(s => s.name.MatchAny(_filterString)).ToList();
    }

    public void OnConfirm(object sender, VisualElement el)
    {
        var selectedShipLog = ShipLogEditor.Instance.selectedShipLog; // TODO: Generalize to support select NameShip in case other than selecting NamedShip for ShipLog

        var namedShipListView = el.Q<ListView>("NamedShipListView");
        var namedShip = namedShipListView.selectedItem as NamedShip;
        if (selectedShipLog != null && namedShip != null)
        {
            selectedShipLog.namedShipObjectId = namedShip.objectId;
        }
    }
}