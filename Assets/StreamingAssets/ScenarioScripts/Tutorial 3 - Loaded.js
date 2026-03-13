let NavalCombatCore = importNamespace('NavalCombatCore');
let CoreUtils = importNamespace("CoreUtils");

let msg = getLocalized(`
Welcome to the third tutorial scenario of the First Sino-Japanese War. In this tutorial, you will learn how to engage in combat.

In the First Sino-Japanese War scenario, different automation levels can be set in the OOB Tree. In a "standard" game, the player uses the default automation level for their own side, while the top group of the opposing side is fully automated.

Additionally, the player can control ships on every side to play a hotseat sandbox game, with dynamically adjusted parameters and the ability to create or delete units. The player can also take direct control of a single ship by setting it to zero-automation, which allows manual control of every device on that vessel.

All of this can be configured by setting the doctrine for Ship States and Ship Groups. Click the "Order of Battle" button in the "Status" tab on the top bar to begin. 
`,
`
日清戦争のチュートリアル・シナリオ第3弾へようこそ。このチュートリアルでは、戦闘への関与方法について学びます。

本シナリオでは、戦闘序列ツリーを通じて、さまざまなオートメーション（自動化）レベルを設定できます。「標準」設定のゲームでは、プレイヤーは自陣営にデフォルトのオートメーションレベルを使用し、敵対陣営のトップグループは完全に自動化されます。

さらに、プレイヤーは全陣営の艦船を操作して、パラメータを動的に調整したり、ユニットを作成・削除したりできるホットシート・サンドボックス・モードをプレイすることも可能です。また、特定の艦船を手動設定にすることで、その艦船のすべての装置を直接手動で操作できるようになります。

これらの設定はすべて、艦船状態や艦船グループの「ドクトリン」を設定することで構成可能です。まずはトップバーの「状態」タブにある「戦闘序列」ボタンをクリックして開始しましょう。
`,
`
欢迎来到《甲午战争》的第三个教程剧本。在本教程中，您将学习如何进行战斗。

在《甲午战争》剧本中，可以通过战斗序列树设置不同的自动化级别。在“标准”游戏模式下，玩家对己方阵营使用默认的自动化级别，而敌方阵营的最高层级组则完全由系统自动控制。

此外，玩家还可以控制所有阵营的舰船进行热座式沙盒游戏，实时调整参数并创建或删除单位。玩家还可以通过将单艘舰船自动化全关掉来进行完全控制，从而手动控制该舰船上的每一个设备。

以上所有内容均可通过设置“舰船状态”和“舰船编组”的条令来进行配置。请点击顶栏“状态”选项卡中的“战斗序列”按钮开始。
`,
`
歡迎來到《甲午戰爭》的第三個教學劇本。在本教學中，您將學習如何進行戰鬥。

在《甲午戰爭》劇本中，可以透過戰鬥序列樹設定不同的自動化層級。在「標準」遊戲模式下，玩家對己方陣營使用預設的自動化層級，而敵方陣營的最高層級組則完全由系統自動控制。

此外，玩家還可以控制所有陣營的艦船進行 Hotseat 沙盒遊戲，即時調整參數並建立或刪除單位。玩家還可以透過將單艘艦船設定為「零自動化」來直接接管該艦，從而手動控制該艦船上的每一個設備。

以上所有內容均可透過設定「艦船狀態」和「艦船組」的條令進行配置。請點擊頂欄「狀態」分頁中的「戰鬥序列」按鈕開始。
`)

msgBox(msg);

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