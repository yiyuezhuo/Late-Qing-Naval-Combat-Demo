let NavalCombatCore = importNamespace('NavalCombatCore');
let CoreUtils = importNamespace("CoreUtils");

let msg = getLocalized(`
Welcome to the third tutorial scenario of the First Sino-Japanese War. In this tutorial, you will learn how to engage in combat.

Unlike traditional games, First Sino-Japanese War primarily offers a TTS/Vassal-like sandbox experience. Players can view and edit everything in real-time — from moving and creating units, to editing weapon parameters and modifying damage situations. What elevates the experience beyond a pure sandbox — making it more like a traditional game — is called "automation." These automated features can create a game-like experience. For example, you can play against an AI (though it currently performs poorly, unfortunately), or choose any level of gameplay between pure sandbox and a fully automated (constrained) traditional game.

There are two groups of ships on the map. Click the "Order of Battle" button in the "Editor" tab on the top bar to view the order of battle.
`,
`
日清戦争第三チュートリアル剧本へようこそ。このチュートリアルでは、戦闘行動の基本を学びます。

従来のゲームとは異なり、本作は主にTTS/Vassal様のサンドボックス体験を提供します。プレイヤーはあらゆる要素をリアルタイムで閲覧・編集可能です——ユニットの移動/作成、兵装パラメータの編集、損傷状況の変更まで。純粋なサンドボックスを超えて伝統的なゲームに近づける要素が「自動化機能」です。例えばAIとの対戦（現状は残念ながら性能不足です）や、純サンドボックスから完全自動化（制約付き）の伝統的ゲームまで、任意のプレイスタイルを選択できます。

マップ上に2つの艦隊グループが存在します。画面上部の「編集」タブ内「戦力編成」ボタンをクリックして編成状況を確認してください。
`,
`
欢迎来到甲午战争第三教程剧本。在本教程中，您将学习如何实施战斗行动。

与传统游戏不同，本作主要提供类似TTS/Vassal的沙盒体验。玩家可以实时查看和编辑所有元素——从移动/创建单位、编辑武器参数到修改损伤状态。超越纯沙盒体验（使其更接近传统游戏）的核心要素称为"自动化功能"。这些功能可创造类游戏体验，例如与AI对战（尽管目前性能较差），或在纯沙盒与全自动（受约束）传统游戏之间任意选择玩法风格。

地图上存在两个舰船编组。请点击顶部"编辑"标签页中的"战斗序列"按钮查看编制状况。
`,
`
歡迎來到甲午戰爭第三教程劇本。在本教程中，您將學習如何實施戰鬥行動。

與傳統遊戲不同，本作主要提供類似TTS/Vassal的沙盒體驗。玩家可以實時查看和編輯所有元素——從移動/創建單位、編輯武器參數到修改損傷狀態。超越純沙盒體驗（使其更接近傳統遊戲）的核心要素稱為「自動化功能」。這些功能可創造類遊戲體驗，例如與AI對戰（儘管目前性能較差），或在純沙盒與全自動（受約束）傳統遊戲之間任意選擇玩法風格。

地圖上存在兩個艦船編組。請點擊頂部「編輯」標籤頁中的「戰鬥序列」按鈕查看編制狀況。
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