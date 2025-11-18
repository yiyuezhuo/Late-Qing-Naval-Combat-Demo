using NavalCombatCore;
using StrategicCombatCore;
using Unity.Properties;

public class LandBattleDialog
{
    // Core State
    public LandBattle landBattle;
    public LandBattleSideStateDynamic attacker;
    public LandBattleSideStateDynamic defender;

    // View State

    // Aux
    [CreateProperty]
    public string title
    {
        get
        {
            return $"The battle of {landBattle.cellXY}";
        }
    }

    [CreateProperty]
    public string dateTimeRange
    {
        get
        {
            var beginDateTimeStr = CoreParameter.Instance.GetReferenceTimeZoneDateTimeOffsetString(landBattle.beginDateTime);
            var endDateTimeStr = landBattle.end ? CoreParameter.Instance.GetReferenceTimeZoneDateTimeOffsetString(landBattle.endDateTime) : "(Continued)";
            return $"{beginDateTimeStr} - {endDateTimeStr}";
        }
    }
}