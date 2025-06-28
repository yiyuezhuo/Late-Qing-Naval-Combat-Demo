
log("Hello The battle of Yalu");

log(NavalGameState.Instance.shipLogs[0].namedShip.name.GetMergedName())
log(1+1+1)


var NavalCombatCore = importNamespace('NavalCombatCore');
log(NavalCombatCore.ShipClass)

yoshino = NavalCombatCore.ScriptHelpers.GetShipLogByName("吉野")
log(yoshino)

ns = importNamespace("")
log(ns.GameManager.Instance)

for(var shipLog of NavalGameState.Instance.shipLogsOnMap)
{
    log(shipLog.namedShip.name.GetMergedName())
}