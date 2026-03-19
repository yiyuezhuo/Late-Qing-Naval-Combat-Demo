
using NavalCombatCore;
using CoreUtils;
using StrategicCombatCore;
using System;

public interface ISwitchable
{
    public void SwitchClose(); // Close 
    public void SoftHide();
    public void Reshow();
    // public void SwitchOpen(); // Open. Add it if we want to implement "Go to Previous" feature
}

public class SwitchCenter
{
    ISwitchable currentActiveViewContainer; // A 2-columns editor or a TempDialog

    void UpdateCurrentActiveViewContainer(ISwitchable newContainer)
    {
        if(newContainer != currentActiveViewContainer) // the same => direct switch with no refresh.
        {
            currentActiveViewContainer?.SwitchClose();
            currentActiveViewContainer = newContainer;
        }
    }

    public void TryToSoftHideCurrent() // Temp Hack
    {
        // if(currentActiveViewContainer != null && currentActiveViewContainer is ShipLogEditor shipLogEditor)
        // {
        //     shipLogEditor.SoftHide();
        // }
        // else if(currentActiveViewContainer != null && currentActiveViewContainer is StrategicGroupEditor strategicGroupEditor)
        // {
        //     strategicGroupEditor.SoftHide();
        // }

        // if(currentActiveViewContainer != null && currentActiveViewContainer is HideableDocument<T> hideableDocument)
        // {
        //     currentActiveViewContainer.SoftHide();
        // }
        // else if(currentActiveViewContainer != null && currentActiveViewContainer is TempDialog tempDialog)
        // {
        //     tempDialog.SoftHide();
        // }

        currentActiveViewContainer?.SoftHide();
    }

    public void RetoreCurrentSoftHide()
    {
        currentActiveViewContainer?.Reshow();
    }
    
    public void SwitchToLeaderView(Leader leader) // close previous view (2 columns editor) or dialog and open new view (2 columns editor) or dialog.
    {
        if (leader != null)
        {
            // var idx = StrategicGameState.Instance.leaders.IndexOf(leader);
            var idx = SuperGameState.Instance.GetCurrentGameState().leaders.IndexOf(leader);
            if (idx != -1)
            {
                // currentActiveViewContainer?.SwitchClose();
                // currentActiveViewContainer = LeaderEditor.Instance;
                UpdateCurrentActiveViewContainer(LeaderEditor.Instance);
                // TODO: Branch according to global IsEditor flag

                LeaderEditor.Instance.Show();
                BehaviourUtils.Instance.ScheduleToSetSelectionForListView(LeaderEditor.Instance.objectListView, idx);
            }
        }
    }

    public void SwitchToMissionView(StrategicMission mission)
    {
        if (mission != null)
        {
            var idx = StrategicGameState.Instance.missions.IndexOf(mission);
            if (idx != -1)
            {
                // currentActiveViewContainer?.SwitchClose();
                // currentActiveViewContainer = StrategicMissionEditor.Instance;
                UpdateCurrentActiveViewContainer(StrategicMissionEditor.Instance);

                // TODO: Branching according to global IsEditor flag
                StrategicMissionEditor.Instance.Show();
                
                // BehaviourUtils.Instance.ScheduleToSetSelectionForListView(StrategicMissionEditor.Instance.objectListView, idx);
                // TODO: Move it to LeftObjectPickerRightEditor ?
                BehaviourUtils.Instance.ScheduleToSetSelectionForListView(
                    StrategicMissionEditor.Instance.objectListView, 
                    () =>
                    {
                        StrategicMissionEditor.Instance.RefreshFilter();
                        return StrategicMissionEditor.Instance.filteredObjects.IndexOf(mission);
                    }
                );
            }
        }
    }

    public void SwitchToStrategicGroupView(StrategicGroup group)
    {
        if(GamePreference.Instance.isInEditMode)
        {
            UpdateCurrentActiveViewContainer(StrategicGroupEditor.Instance);
            StrategicGroupEditor.Instance.Show();

            if(group != null)
            {
                var idx = StrategicGameState.Instance.strategicGroups.IndexOf(group);
                if(idx != -1)
                {
                    BehaviourUtils.Instance.ScheduleToSetSelectionForListView(StrategicGroupEditor.Instance.objectListView, idx);
                }
            }
        }
        else if(currentActiveViewContainer is TempDialog currentTempDialog && currentTempDialog.templateDataSource is StrategicGroup _group && group == _group && !currentTempDialog.closed) // ShipLog is the workaround to check if it's a ship log dialog
        {
            // Soft Hide workaround for dialog mode
            currentTempDialog.Reshow();
        }
        else
        {
            var tempDialog = DialogRoot.Instance.PopupStrategicGroupDialog(group);
            UpdateCurrentActiveViewContainer(tempDialog);
        }
    }

    public void SwitchToLandUnitView(LandUnit landUnit)
    {
        if (landUnit != null)
        {
            var idx = StrategicGameState.Instance.landUnits.IndexOf(landUnit);
            if (idx != -1)
            {
                // currentActiveViewContainer?.SwitchClose();
                // currentActiveViewContainer = StrategicGroupEditor.Instance;
                if(GamePreference.Instance.isInEditMode)
                {
                    UpdateCurrentActiveViewContainer(LandUnitEditor.Instance);
                    LandUnitEditor.Instance.Show();
                    BehaviourUtils.Instance.ScheduleToSetSelectionForListView(LandUnitEditor.Instance.objectListView, idx);
                }
                else
                {
                    var tempDialog = DialogRoot.Instance.PopupLandUnitDialog(landUnit);
                    UpdateCurrentActiveViewContainer(tempDialog);
                }
            }
        }
    }

    public EventHandler shipLogViewShown;

    public void SwitchToShipLogView(ShipLog shipLog)
    {
        if(shipLog != null)
        {
            // var idx = NavalGameState.Instance.shipLogs.IndexOf(shipLog);
            var idx = SuperGameState.Instance.GetCurrentGameState().shipLogs.IndexOf(shipLog);
            if (idx != -1)
            {
                // currentActiveViewContainer?.SwitchClose();
                // currentActiveViewContainer = ShipLogEditor.Instance;

                if(GamePreference.Instance.isInEditMode)
                {
                    UpdateCurrentActiveViewContainer(ShipLogEditor.Instance);
                    ShipLogEditor.Instance.Show();
                    BehaviourUtils.Instance.ScheduleToSetSelectionForListView(ShipLogEditor.Instance.shipLogListView, idx);
                }
                else if(currentActiveViewContainer is TempDialog currentTempDialog && currentTempDialog.templateDataSource is ShipLog _shipLog && shipLog == _shipLog && !currentTempDialog.closed) // ShipLog is the workaround to check if it's a ship log dialog
                {
                    // Soft Hide workaround for dialog mode
                    currentTempDialog.Reshow();
                }
                else
                {
                    var tempDialog = DialogRoot.Instance.PopupShipLogDialog(shipLog);
                    UpdateCurrentActiveViewContainer(tempDialog);
                }

                shipLogViewShown?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public void SwitchToWeaponView(Weapon weapon)
    {
        if (weapon != null)
        {
            var idx = StrategicGameState.Instance.weapons.IndexOf(weapon);
            if (idx != -1)
            {
                UpdateCurrentActiveViewContainer(WeaponEditor.Instance);

                WeaponEditor.Instance.Show();
                BehaviourUtils.Instance.ScheduleToSetSelectionForListView(WeaponEditor.Instance.objectListView, idx);
            }
        }
    }

    public void SwitchToLandUnitTemplateView(LandUnitTemplate landUnitTemplate)
    {
        if (landUnitTemplate != null)
        {
            var idx = StrategicGameState.Instance.landUnitTemplates.IndexOf(landUnitTemplate);
            if (idx != -1)
            {
                UpdateCurrentActiveViewContainer(LandUnitTemplateEditor.Instance);

                LandUnitTemplateEditor.Instance.Show();
                BehaviourUtils.Instance.ScheduleToSetSelectionForListView(LandUnitTemplateEditor.Instance.objectListView, idx);
            }
        }
    }

    public void SwitchToNamedShipView(NamedShip namedShip)
    {
        if(namedShip != null)
        {
            // var idx = NavalGameState.Instance.namedShips.IndexOf(namedShip);
            var idx = SuperGameState.Instance.GetCurrentGameState().namedShips.IndexOf(namedShip);
            if (idx != -1)
            {
                UpdateCurrentActiveViewContainer(NamedShipEditor.Instance);

                NamedShipEditor.Instance.Show();
                BehaviourUtils.Instance.ScheduleToSetSelectionForListView(NamedShipEditor.Instance.objectListView, idx);
            }
        }
    }

    public void SwitchToShipClassView(ShipClass shipClass)
    {
        if (shipClass != null)
        {
            if (SuperGameState.Instance.GetCurrentGameState().shipClasses.IndexOf(shipClass) != -1)
            {
                UpdateCurrentActiveViewContainer(ShipClassEditor.Instance);

                ShipClassEditor.Instance.Show();
                ShipClassEditor.Instance.SelectObject(shipClass, clearFilterIfHidden: true);
            }
        }
    }

    public void SwitchByIStrategicGroupMemberReferenceable(IStrategicGroupMemberReferenceable gotoObj)
    {
        if (gotoObj is StrategicGroup group)
        {
            SwitchToStrategicGroupView(group);
        }
        else if (gotoObj is ShipLog shipLog)
        {
            SwitchToShipLogView(shipLog);
        }
        else if (gotoObj is LandUnit landUnit)
        {
            SwitchToLandUnitView(landUnit);
        }
    }

    public void Reset()
    {
        currentActiveViewContainer = null;
    }

    public static SwitchCenter Instance { get; } = new SwitchCenter();
}
