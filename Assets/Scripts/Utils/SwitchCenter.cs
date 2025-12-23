
using NavalCombatCore;
using CoreUtils;
using StrategicCombatCore;

public interface ISwitchable
{
    public void SwitchClose(); // Close 
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
        if(currentActiveViewContainer != null && currentActiveViewContainer is ShipLogEditor shipLogEditor)
        {
            shipLogEditor.SoftHide();
        }
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
                BehaviourUtils.Instance.ScheduleToSetSelectionForListView(LeaderEditor.Instance.leadersListView, idx);
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
                BehaviourUtils.Instance.ScheduleToSetSelectionForListView(StrategicMissionEditor.Instance.objectListView, idx);
            }
        }
    }

    public void SwitchToStrategicGroupView(StrategicGroup group)
    {
        if(group != null)
        {
            var idx = StrategicGameState.Instance.strategicGroups.IndexOf(group);
            if (group != null && idx != -1)
            {
                // currentActiveViewContainer?.SwitchClose();
                // currentActiveViewContainer = StrategicGroupEditor.Instance;
                

                // TODO: Branching according to global IsEditor flag
                if(GamePreference.Instance.isInEditorMode)
                {
                    UpdateCurrentActiveViewContainer(StrategicGroupEditor.Instance);
                    StrategicGroupEditor.Instance.Show();
                    BehaviourUtils.Instance.ScheduleToSetSelectionForListView(StrategicGroupEditor.Instance.objectListView, idx);
                }
                else
                {
                    var tempDialog = DialogRoot.Instance.PopupStrategicGroupDialog(group);
                    UpdateCurrentActiveViewContainer(tempDialog);
                }
            }
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
                if(GamePreference.Instance.isInEditorMode)
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
                

                if(GamePreference.Instance.isInEditorMode)
                {
                    UpdateCurrentActiveViewContainer(ShipLogEditor.Instance);
                    ShipLogEditor.Instance.Show();
                    BehaviourUtils.Instance.ScheduleToSetSelectionForListView(ShipLogEditor.Instance.shipLogListView, idx);
                }
                else
                {
                    var tempDialog = DialogRoot.Instance.PopupShipLogDialog(shipLog);
                    UpdateCurrentActiveViewContainer(tempDialog);
                }
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
                BehaviourUtils.Instance.ScheduleToSetSelectionForListView(NamedShipEditor.Instance.namedShipListView, idx);
            }
        }
    }

    public void SwitchToShipClassView(ShipClass shipClass)
    {
        if (shipClass != null)
        {
            var idx = SuperGameState.Instance.GetCurrentGameState().shipClasses.IndexOf(shipClass);
            if (idx != -1)
            {
                UpdateCurrentActiveViewContainer(ShipClassEditor.Instance);

                ShipClassEditor.Instance.Show();
                BehaviourUtils.Instance.ScheduleToSetSelectionForListView(ShipClassEditor.Instance.shipClassListView, idx);
            }
        }
    }

    public void Reset()
    {
        currentActiveViewContainer = null;
    }

    public static SwitchCenter Instance { get; } = new SwitchCenter();
}
