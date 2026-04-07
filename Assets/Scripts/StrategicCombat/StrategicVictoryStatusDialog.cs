using System.Collections.Generic;
using System.Linq;
using CoreUtils;
using NavalCombatCore;
using StrategicCombatCore;
using Unity.Properties;
using YYZ;

public sealed class StrategicVictoryStatusRow
{
    public string sideObjectId;
    public string fallbackSideName;
    public int totalLandBattleLossMen;
    public int totalDestroyedShipCount;
    public int landBattleVictoryCount;
    public int landBattleDefeatCount;

    SideState GetSide() => EntityManager.Instance.Get<SideState>(sideObjectId);

    [CreateProperty]
    public string sideName => GetSide()?.name?.GetShortName() ?? fallbackSideName ?? sideObjectId ?? "";

    [CreateProperty]
    public string totalLandBattleLossMenText => totalLandBattleLossMen.ToString();

    [CreateProperty]
    public string totalDestroyedShipCountText => totalDestroyedShipCount.ToString();

    [CreateProperty]
    public string landBattleVictoryCountText => landBattleVictoryCount.ToString();

    [CreateProperty]
    public string landBattleDefeatCountText => landBattleDefeatCount.ToString();
}

public sealed class StrategicVictoryStatusDialogModel
{
    public List<StrategicVictoryStatusRow> rows = new();

    static string Localize(string key, params object[] args) => ServiceLocator.Get<ILocalizeService>().Get(key, args);

    [CreateProperty]
    public string title => Localize("Strategic Victory Status");

    public static StrategicVictoryStatusDialogModel Generate(StrategicGameState gameState)
    {
        var model = new StrategicVictoryStatusDialogModel();
        if (gameState == null)
            return model;

        model.rows = gameState.sideStates
            .Where(side => side != null)
            .Select(side =>
            {
                var sideId = side.objectId;
                var landBattles = gameState.landBattles.Where(battle => battle != null).ToList();

                var totalLandBattleLossMen = landBattles.Sum(battle =>
                {
                    if (battle.attacker?.sideId == sideId)
                        return (int)battle.attacker.GetTotalAccumulatedStrengthLoss();
                    if (battle.defender?.sideId == sideId)
                        return (int)battle.defender.GetTotalAccumulatedStrengthLoss();
                    return 0;
                });

                var totalDestroyedShipCount = gameState.shipLogs.Count(shipLog =>
                    shipLog != null &&
                    shipLog.side?.objectId == sideId &&
                    shipLog.mapState == MapState.Destroyed
                );

                var endedBattles = landBattles.Where(battle => battle.end);
                var landBattleVictoryCount = endedBattles.Count(battle =>
                    (battle.attacker?.sideId == sideId && battle.attackerVictory) ||
                    (battle.defender?.sideId == sideId && !battle.attackerVictory)
                );
                var landBattleDefeatCount = endedBattles.Count(battle =>
                    (battle.attacker?.sideId == sideId && !battle.attackerVictory) ||
                    (battle.defender?.sideId == sideId && battle.attackerVictory)
                );

                return new StrategicVictoryStatusRow()
                {
                    sideObjectId = sideId,
                    fallbackSideName = side.name?.GetShortName(),
                    totalLandBattleLossMen = totalLandBattleLossMen,
                    totalDestroyedShipCount = totalDestroyedShipCount,
                    landBattleVictoryCount = landBattleVictoryCount,
                    landBattleDefeatCount = landBattleDefeatCount
                };
            })
            .ToList();

        return model;
    }
}
