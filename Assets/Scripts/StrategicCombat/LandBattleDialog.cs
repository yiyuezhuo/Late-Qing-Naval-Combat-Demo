using NavalCombatCore;
using StrategicCombatCore;
using Unity.Properties;
using UnityEngine.UIElements;

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
            var endDateTimeStr = landBattle.end ? CoreParameter.Instance.GetReferenceTimeZoneDateTimeOffsetString(landBattle.endDateTime) : "now";
            return $"{beginDateTimeStr} - {endDateTimeStr}";
        }
    }

    [CreateProperty]
    public string summary => $"Attacker Situation: {landBattle.attackerSituation:+0.00%;-0.00%;0.00%}";

    public void OnCreated(object sender, VisualElement root)
    {
        foreach(var listView in root.Query<ListView>("LandUnitBundleListView").ToList())
        {
            listView.makeItem = () =>
            {
                var el = listView.itemTemplate.CloneTree();
                var nameLabel = el.Q<Label>("NameLabel");
                Utils.RegisterLinkTag(nameLabel, new()
                {
                    {"nameLink", () =>{
                        if(Utils.TryResolveCurrentValueForBinding<LandBattleSideStateDynamic.LandUnitBundle>(nameLabel, out var landUnitBundle))
                        {
                            var idx = StrategicGameState.Instance.landUnits.IndexOf(landUnitBundle.landUnit);
                            if(idx != -1)
                            {
                                LandUnitEditor.Instance.Show();
                                BehaviourUtils.Instance.ScheduleToSetSelectionForListView(LandUnitEditor.Instance.objectListView, idx);
                            }
                        }
                    }}
                });
                return el;
            };
        }
    }
}