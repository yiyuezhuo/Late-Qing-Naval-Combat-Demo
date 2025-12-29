using StrategicCombatCore;
using CoreUtils;
using System;

public class CreateMissionDialog
{
    public StrategicMission.MissionType missionType;
    public Action<StrategicMission> callback;

    public void OnConfirm()
    {
        var newObj = StrategicMission.Create(missionType);
        EntityManager.Instance.Register(newObj, null);
        StrategicGameState.Instance.missions.Add(newObj);

        if(callback != null)
        {
            callback(newObj);
        }
    }
}