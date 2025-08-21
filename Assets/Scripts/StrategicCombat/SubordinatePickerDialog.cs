using System.Collections.Generic;
using Unity.Properties;
using UnityEngine.UIElements;
using NavalCombatCore;
using StrategicCombatCore;
using System.Linq;
using System;
using CoreUtils;

public class SubordinatePickerDialog
{
    [CreateProperty]
    public List<ShipLog> unassignedShipLogs => StrategicGameState.Instance.shipLogs.Where(shipLog => !shipLog.strategicGroupReference.isReferenceAny()).ToList();

    [CreateProperty]
    public List<LandUnit> unassignedLandUnits => StrategicGameState.Instance.landUnits.Where(landUnit => !landUnit.strategicGroupReference.isReferenceAny()).ToList();

    [CreateProperty]
    public List<StrategicGroup> unassignedGroups => StrategicGameState.Instance.strategicGroups.Where(group => !group.strategicGroupReference.isReferenceAny()).ToList();

    ListView shipListView;
    ListView landUnitListView;
    ListView groupListView;

    public Action<List<IStrategicGroupMemberReferenceable>> confirmCallback;

    public void OnCreated(object sender, VisualElement el)
    {
        shipListView = el.Q<ListView>("ShipListView");
        landUnitListView = el.Q<ListView>("LandUnitListView");
        groupListView = el.Q<ListView>("GroupListView");
    }

    public void OnConfirmed(object sender, VisualElement el)
    {
        var selectedItems = new List<IStrategicGroupMemberReferenceable>();
        selectedItems.AddRange(shipListView.selectedItems.Select(item => item as IStrategicGroupMemberReferenceable).Where(item => item != null));
        selectedItems.AddRange(landUnitListView.selectedItems.Select(item => item as IStrategicGroupMemberReferenceable).Where(item => item != null));
        selectedItems.AddRange(groupListView.selectedItems.Select(item => item as IStrategicGroupMemberReferenceable).Where(item => item != null));

        confirmCallback(selectedItems);
    }
}