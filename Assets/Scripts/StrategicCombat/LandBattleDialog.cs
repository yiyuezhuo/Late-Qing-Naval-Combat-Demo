using CoreUtils;
using NavalCombatCore;
using StrategicCombatCore;
using Unity.Properties;
using UnityEngine.UIElements;
using UnityEngine;
using System;

public class LandBattleDialogLazy : IDataSourceViewHashProvider
{
    public string landBattleId;

    [CreateProperty]
    public LandBattleDialog landBattleDialog
    {
        get
        {
            // Debug.LogWarning("landBattleDialog refreshed");

            var landBattle = EntityManager.Instance.Get<LandBattle>(landBattleId);
            
            if(landBattle.end)
                return null;
            
            return new LandBattleDialog()
            {
                landBattle = landBattle,
                attacker = landBattle.GetAttackerDynamic(),
                defender = landBattle.GetDefenderDynamic(),
            };
        }
    }

    [CreateProperty]
    public LandBattle landBattle => EntityManager.Instance.Get<LandBattle>(landBattleId);

    [CreateProperty]
    public bool isLandBattleEnd => landBattle?.end ?? false;

    // lazy refresh when time advanced.
    public long  GetViewHashCode()
    {
        return HashCode.Combine(StrategicGameState.Instance.scenarioState.dateTime);
    }

}

public class LandBattleDialog
{
    // Core State
    public LandBattle landBattle;
    public LandBattleSideStateDynamic attacker;
    public LandBattleSideStateDynamic defender;

    // View State

    LazyLocalizedString MakeTitle() => LazyLocalizedString.MakeTemplate(
        "The battle of {0}",
        StrategicGameState.Instance.GetCellNameLazyStr(landBattle.cellXY)
    );

    // Aux
    // [CreateProperty]
    // public string title
    // {
    //     get
    //     {
    //         // return $"The battle of {landBattle.cellXY}";
    //         return $"The battle of {StrategicGameState.Instance.GetCellName(landBattle.cellXY)}";
    //     }
    // }

    [CreateProperty]
    public string title => MakeTitle().Resolve();

    static string Localize(string key, params object[] args) => ServiceLocator.Get<ILocalizeService>().Get(key, args);


    [CreateProperty]
    public string dateTimeRange
    {
        get
        {
            var beginDateTimeStr = CoreParameter.Instance.GetReferenceTimeZoneDateTimeOffsetString(landBattle.beginDateTime);
            // var endDateTimeStr = landBattle.end ? CoreParameter.Instance.GetReferenceTimeZoneDateTimeOffsetString(landBattle.endDateTime) : "now";
            var endDateTimeStr = landBattle.end ? CoreParameter.Instance.GetReferenceTimeZoneDateTimeOffsetString(landBattle.endDateTime) : Localize("now");
            return $"{beginDateTimeStr} - {endDateTimeStr}";
        }
    }

    LazyLocalizedString MakeSummary() => LazyLocalizedString.MakeTemplate(
        "Attacker Situation: {0}",
        LazyLocalizedString.MakeRaw($"{landBattle.attackerSituation:+0.00%;-0.00%;0.00%}")
    );

    // [CreateProperty]
    // public string summary => $"Attacker Situation: {landBattle.attackerSituation:+0.00%;-0.00%;0.00%}";

    [CreateProperty]
    public string summary => MakeSummary().Resolve();


    public static void OnCreated(object sender, VisualElement root)
    {
        foreach(var listView in root.Query<ListView>("LandUnitBundleListView").ToList())
        {
            listView.makeItem = () =>
            {
                var el = listView.itemTemplate.CloneTree();
                // TODO: Disable hyperlink since it seem to related to strange UITK bugs
                // var nameLabel = el.Q<Label>("NameLabel");
                // Utils.RegisterLinkTag(nameLabel, new()
                // {
                //     {"nameLink", () =>{
                //         if(Utils.TryResolveCurrentValueForBinding<LandBattleSideStateDynamic.LandUnitBundle>(nameLabel, out var landUnitBundle))
                //         {
                //             var idx = StrategicGameState.Instance.landUnits.IndexOf(landUnitBundle.landUnit);
                //             if(idx != -1)
                //             {
                //                 LandUnitEditor.Instance.Show();
                //                 BehaviourUtils.Instance.ScheduleToSetSelectionForListView(LandUnitEditor.Instance.objectListView, idx);
                //             }
                //         }
                //     }}
                // });
                var nameButton = el.Q<Button>("NameButton");
                nameButton.clicked += () =>
                {
                    if(Utils.TryResolveCurrentValueForBinding<LandBattleSideStateDynamic.LandUnitBundle>(nameButton, out var landUnitBundle))
                    {
                        // var idx = StrategicGameState.Instance.landUnits.IndexOf(landUnitBundle.landUnit);
                        // if(idx != -1)
                        // {
                        //     LandUnitEditor.Instance.Show();
                        //     BehaviourUtils.Instance.ScheduleToSetSelectionForListView(LandUnitEditor.Instance.objectListView, idx);
                        // }

                        SwitchCenter.Instance.SwitchToLandUnitView(landUnitBundle.landUnit);
                    }
                };
                return el;
            };
        }
    }

}