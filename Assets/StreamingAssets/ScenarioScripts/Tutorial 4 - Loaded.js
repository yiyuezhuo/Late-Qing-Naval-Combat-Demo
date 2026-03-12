let tutorial4IntroMsg = getLocalized(`
Welcome to Tutorial 4 Formation Control. You have learn 'low-level' ship group control in the tutorial 2, here you will learn how to do it more efficiently.

First, watch Division 1 maneuver until new message dialog popup (it will last ~20 game minutes). This is also an illustration of historical maneuver in the Battle of Yellow Sea early stage.
`,
`
チュートリアル4「編隊統制」へようこそ。チュートリアル2では艦艇グループに対する「低レベル」の操作を学びましたが、ここではそれをより効率よく行う方法を学びます。

まずは第1戦隊の機動を、新しいメッセージダイアログが表示されるまで観察してください（およそ20分のゲーム内時間が経過します）。これは黄海海戦前半における歴史的機動の一例でもあります。
`,
`
欢迎来到教程 4：编队控制。你已经在教程 2 中学习过“低层级”的舰队编组控制；在这里，你将学习如何更高效地完成这些操作。

首先，请观察第 1 分队的机动，直到新的消息对话框弹出为止（大约会持续 20 分钟游戏时间）。这同时也是黄海海战前期历史机动的一个示意。
`,
`
歡迎來到教學 4：編隊控制。你已經在教學 2 中學過「低層級」的艦隊編組控制；在這裡，你將學習如何更有效率地完成這些操作。

首先，請觀察第 1 分隊的機動，直到新的訊息對話框彈出為止（大約會持續 20 分鐘遊戲時間）。這同時也是黃海海戰前期歷史機動的一個示意。
`);

msgBox(tutorial4IntroMsg);

var Tutorial4Phase = {
    WaitingForAiRelativeFormation: 1,
    WaitingForAiReverseChain: 2,
    WaitingForAiExplanation: 3,
    WaitingForPracticeExplanation: 4,
    End: 5
};

var tutorial4Phase = Tutorial4Phase.WaitingForAiRelativeFormation;
var tutorial4ElapsedMinutes = 0;
var tutorial4AiFlagship = NavalGameState.Instance.shipLogs[0];
var tutorial4AiFormationMinute = -1;
var tutorial4AiReverseMinute = -1;
var tutorial4AiExplanationMinute = -1;
var tutorial4FollowDistanceYards = 500;
