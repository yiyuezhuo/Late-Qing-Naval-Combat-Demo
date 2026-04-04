using System.Collections.Generic;
using Unity.Properties;
using UnityEngine.UIElements;
using NavalCombatCore;
using StrategicCombatCore;
using System.Linq;
using System;
using CoreUtils;
using UnityEngine;

public class SubordinatePickerDialog
{
    public enum Mode
    {
        Free,
        ParentUnassignedMember,
        Depot,
        MissionUnassignedFleetGroup, // Patrol, Supply
        MissionUnassignedGroup // Naval Transfer (non-fleet group would be transferred and fleet will be used to tranport or escort)
    }

    // public bool showNonParentGroupOnly = true;
    public Mode mode = Mode.ParentUnassignedMember;

    // [CreateProperty]
    // public List<ShipLog> unassignedShipLogs => !showNonParentGroupOnly ? StrategicGameState.Instance.shipLogs : StrategicGameState.Instance.shipLogs.Where(shipLog => !shipLog.parentGroupReference.isReferenceAny()).ToList();

    // [CreateProperty]
    // public List<LandUnit> unassignedLandUnits => !showNonParentGroupOnly ? StrategicGameState.Instance.landUnits : StrategicGameState.Instance.landUnits.Where(landUnit => !landUnit.parentGroupReference.isReferenceAny()).ToList();

    // [CreateProperty]
    // public List<StrategicGroup> unassignedGroups => !showNonParentGroupOnly ? StrategicGameState.Instance.strategicGroups : StrategicGameState.Instance.strategicGroups.Where(group => !group.parentGroupReference.isReferenceAny()).ToList();

    public List<ShipLog> filteredShipLogs;
    public List<LandUnit> filteredLandUnits;
    public List<StrategicGroup> filteredGroups;

    // public List<ShipLog> MakeUnassignedShipLogs() => !showNonParentGroupOnly ? StrategicGameState.Instance.shipLogs : StrategicGameState.Instance.shipLogs.Where(shipLog => !shipLog.parentGroupReference.isReferenceAny()).ToList();
    // public List<LandUnit> MakeUnassignedLandUnits() => !showNonParentGroupOnly ? StrategicGameState.Instance.landUnits : StrategicGameState.Instance.landUnits.Where(landUnit => !landUnit.parentGroupReference.isReferenceAny()).ToList();
    // public List<StrategicGroup> MakeUnassignedGroups() => !showNonParentGroupOnly ? StrategicGameState.Instance.strategicGroups : StrategicGameState.Instance.strategicGroups.Where(group => !group.parentGroupReference.isReferenceAny()).ToList();

    ListView shipListView;
    ListView landUnitListView;
    ListView groupListView;

    public Action<List<IStrategicGroupMemberReferenceable>> confirmCallback;

    public void OnCreated(object sender, VisualElement el)
    {
        var mainTabView = el.Q<TabView>("MainTabView");

        shipListView = el.Q<ListView>("ShipListView");
        landUnitListView = el.Q<ListView>("LandUnitListView");
        groupListView = el.Q<ListView>("GroupListView");

        filteredShipLogs = StrategicGameState.Instance.shipLogs;
        filteredLandUnits = StrategicGameState.Instance.landUnits;
        filteredGroups = StrategicGameState.Instance.strategicGroups;

        if (mode == Mode.ParentUnassignedMember)
        {
            // Debug.Log(1);
            filteredShipLogs = filteredShipLogs.Where(shipLog => !shipLog.parentGroupReference.isReferenceAny()).ToList();
            filteredLandUnits = filteredLandUnits.Where(landUnit => !landUnit.parentGroupReference.isReferenceAny()).ToList();
            filteredGroups = filteredGroups.Where(group => !group.parentGroupReference.isReferenceAny()).ToList();
        }
        else if (mode == Mode.Depot)
        {
            mainTabView.selectedTabIndex = 1;
            filteredShipLogs = new();
            filteredLandUnits = filteredLandUnits.Where(landUnit => landUnit.GetLandUnitTemplate()?.unitType == LandUnitType.Supply).ToList();
            filteredGroups = new();
        }
        else if (mode == Mode.MissionUnassignedFleetGroup)
        {
            mainTabView.selectedTabIndex = 2;
            filteredShipLogs = new();
            filteredLandUnits = new();
            filteredGroups = filteredGroups.Where(
                group => group.assignedMissionObjectId == null &&
                // group.deployState == StrategicGroup.DeployState.Independent &&
                group.deployState != StrategicGroup.DeployState.Combined &&
                group.type == StrategicGroup.Type.Fleet
            ).ToList();
        }
        else if(mode == Mode.MissionUnassignedGroup)
        {
            mainTabView.selectedTabIndex = 2;
            filteredShipLogs = new();
            filteredLandUnits = new();
            filteredGroups = filteredGroups.Where(
                group => group.assignedMissionObjectId == null &&
                // group.deployState == StrategicGroup.DeployState.Independent
                group.deployState != StrategicGroup.DeployState.Combined
            ).ToList();
        }
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