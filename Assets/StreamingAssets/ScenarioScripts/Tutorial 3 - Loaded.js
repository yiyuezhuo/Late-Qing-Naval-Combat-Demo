let NavalCombatCore = importNamespace('NavalCombatCore');
let CoreUtils = importNamespace("CoreUtils");


msgBox(`
Welcome to the third tutorial scenario of the First Sino-Japanese War. In this tutorial, you will learn how to engage in combat.

Unlike traditional games, First Sino-Japanese War primarily offers a TTS/Vassal-like sandbox experience. Players can view and edit everything in real-time — from moving and creating units, to editing weapon parameters and modifying damage situations. What elevates the experience beyond a pure sandbox — making it more like a traditional game — is called "automation." These automated features can create a game-like experience. For example, you can play against an AI (though it currently performs poorly, unfortunately), or choose any level of gameplay between pure sandbox and a fully automated (constrained) traditional game.

There are two groups of ships on the map. Click the "Order of Battle" button in the "Editor" tab on the top bar to view the order of battle.
`);

var Phase = {
    WaitForOrderOfBattleShown : 1,
    WaitForFiringExchangeStarted : 2,
    WaitForAHitScored : 3,
    End : 4
}

var phase = Phase.WaitForOrderOfBattleShown;

var damageEffectPrompted = false;
var sunkPrompted = false;
var groupDestroyedPrompted = false;

function hasFireExchanged(){
    let fireAny = false;
    for(var shipLog of NavalGameState.Instance.shipLogs)
    {
        // log(shipLog.namedShip.name.GetMergedName())
        for(var btyStatus of shipLog.batteryStatus)
        {
            // log(btyStatus)
            for(var mnt of btyStatus.mountStatus)
            {
                // log(mnt.firingTargetObjectId);
                fireAny = fireAny ||  mnt.firingTargetObjectId !== null
            }
        }
    }
    return fireAny;
}

function isHitScored()
{
    let hitAny = false;
    for(var shipLog of NavalGameState.Instance.shipLogs)
    {
        if(shipLog.logs.length > 0)
        {
            hitAny = true;
            break;
        }
    }
    return hitAny;
}

function hasAnyDamageEffect()
{
    for(let shipLog of NavalGameState.Instance.shipLogs)
        if(shipLog.subStatesDownward.Count > 0)
            return true;
    return false;
}

function hasAnySunk()
{
    for(let shipLog of NavalGameState.Instance.shipLogs)
        // if(shipLog.mapState === NavalGameState.MapState.Destroyed)
        if(shipLog.mapState === NavalCombatCore.MapState.Destroyed)
            return true;
    return false;
}

function hasGroupDestroyed()
{
    for(let group of NavalGameState.Instance.shipGroups)
    {
        // log(group)
        let destroyedAll = true;
        for(let id of group.childrenObjectIds)
        {
            // log(id)
            // log(CoreUtils.EntityManager.Instance.GetOnMapShipLog(id))
            if(CoreUtils.EntityManager.Instance.GetOnMapShipLog(id) !== null)
            {
                destroyedAll = false;
                break;
            }
        }
        if(destroyedAll)
            return true;
    }
    return false;
}