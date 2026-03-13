let tutorial5IntroMsg = getLocalized(`
Welcome to Tutorial 5 - Manual Firing. In this tutorial, you will learn how to engage targets manually.

By default, the Optimizer automatically performs a global optimization to select the best targets (though the current algorithm isn't very smart) for every unit. However, there are times when manual control is necessary.

The game recalculates weapon target assignments every two minutes (one SK5 turn). Advance time until the automatic firing begins and new instructions appear.
`,
`
チュートリアル5 - 手動射撃へようこそ。このチュートリアルでは、目標を手動で交戦する方法を学びます。

デフォルトでは、オプティマイザが各艦艇に対して最適な目標を選ぶための全体最適化を自動で行います（ただし、現在のアルゴリズムはあまり賢くありません）。しかし、手動操作が必要になる場面もあります。

ゲームでは2分ごと（SK5の1ターンごと）に兵器の目標割り当てが再計算されます。自動射撃が始まり、新しい指示が表示されるまで時間を進めてください。
`,
`
欢迎来到教程 5 - 手动射击。在本教程中，你将学习如何手动与目标交战。

默认情况下，优化器会自动为每艘舰船执行一次全局优化，以选择最佳目标（虽然当前算法并不算聪明）。但在某些情况下，仍然需要手动控制。

游戏每两分钟（即 SK5 的一个回合）重新计算一次武器目标分配。请推进时间，直到自动射击开始并出现新的说明。
`,
`
歡迎來到教學 5 - 手動射擊。在本教學中，你將學習如何手動與目標交戰。

預設情況下，最佳化器會自動為每艘艦船執行一次全域最佳化，以選擇最佳目標（雖然目前的演算法並不算聰明）。但在某些情況下，仍然需要手動控制。

遊戲每兩分鐘（即 SK5 的一個回合）重新計算一次武器目標分配。請推進時間，直到自動射擊開始並出現新的說明。
`);

msgBox(tutorial5IntroMsg);

var Tutorial5Phase = {
    WaitingForAutomaticFiringDialog: 1,
    WaitingForManualBatteryDialog: 2,
    WaitingForConclusionDialog: 3,
    End: 4
};

var tutorial5Phase = Tutorial5Phase.WaitingForAutomaticFiringDialog;
var tutorial5ElapsedMinutes = 0;
var tutorial5LastPromptMinute = 0;
