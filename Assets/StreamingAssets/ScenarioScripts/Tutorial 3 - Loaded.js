let NavalCombatCore = importNamespace('NavalCombatCore');
let CoreUtils = importNamespace("CoreUtils");

let msg = getLocalized(`
Welcome to Tutorial 3 - Combat. In this tutorial, you will learn some concepts of combat.

The Japanese side has been set to be automatically controlled as the opponent.

Control the two Beiyang Fleet ships you have seen before and approach the three Japanese ships to engage in combat until one side is eliminated or the time limit is reached (you will be prompted to check the "Victory Status").

Other tutorial tips will be displayed during the process.
`,
`
チュートリアル3へようこそ - 戦闘。このチュートリアルでは、戦闘のいくつかの概念を学びます。

日本側は対戦相手として自動操作に設定されています。

これまでに見た北洋艦隊の2隻を操作し、日本の3隻の艦船に接近して戦闘を行い、いずれかの側が全滅するか、制限時間に達するまで続けます（「勝利状況」を確認するように促されます）。

その他のチュートリアルのヒントは進行中に表示されます。
`,
`
欢迎来到教程3 - 战斗。在这个教程中你会学习战斗的一些概念。

日本方已经被设为被自动控制作为对方。

控制你之前见过的两艘北洋水师的船接近日本的三艘船进行战斗，直到一方被消灭或者时间限制抵达（你会被提示查看"胜利状况"）。

其他教学提示会在战斗过程中显示出来。
`,
`
歡迎來到教學3 - 戰鬥。在這個教學中你會學習戰鬥的一些概念。

日本方已被設定為自動控制作為對手。

控制你之前見過的兩艘北洋水師的船，接近日本的三艘船進行戰鬥，直到一方被消滅或時間限制到達（系統會提示你查看「勝利狀況」）。

其他教學提示將會在過程中顯示出來。
`)

msgBox(msg);

var fireExchangedPrompted = false;
var hitScoredPrompted = false;
var damageEffectPrompted = false;
var sunkPrompted = false;
// var groupDestroyedPrompted = false;

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