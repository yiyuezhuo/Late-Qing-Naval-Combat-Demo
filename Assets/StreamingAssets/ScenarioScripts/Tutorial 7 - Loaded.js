var Tutorial7Phase = {
    WaitingForFirstInsertDialog: 1,
    WaitingForFirstShipInsertion: 2,
    WaitingForThirdShipInsertion: 3,
    WaitingForFormationPosition: 4,
    End: 5
};

var tutorial7Phase = Tutorial7Phase.WaitingForFirstInsertDialog;

let tutorial7IntroMsg = getLocalized(`
Welcome to Tutorial 7: Custom Scenarios. Here, you will learn how to build a custom scenario. What you are currently seeing is an almost blank scenario state - not completely blank, because the two default Red and Blue Ship Groups have already been created for convenience. This is also the state you enter from the main menu by using "Start As Empty." When you want to create a custom scenario, or start fighting in a temporary sandbox, you can also use "Start As Empty" to enter this state.

Press the Insert key, or the corresponding button in Top Tab - Editor, then click anywhere on the map to insert a unit.
`,
`
チュートリアル7「カスタムシナリオ」へようこそ。ここでは、カスタムシナリオの作り方を学びます。現在表示されているのは、ほぼ空白のシナリオ状態です。ただし完全な空白ではありません。便利なように、既定の Red と Blue の Ship Group がすでに作成されています。これはメインメニューで「Start As Empty」を使ったときに入る状態でもあります。カスタムシナリオを作りたいときや、一時的なサンドボックスで戦闘を始めたいときも、「Start As Empty」からこの状態に入れます。

Insert キー、または Top Tab - Editor の対応するボタンを押してから、マップ上の任意の場所をクリックしてユニットを挿入してください。
`,
`
欢迎来到教程 7：自定义剧本。在这里，你将学习如何创建自定义剧本。你当前看到的是一个几乎空白的剧本状态，不过并不是完全空白，因为为了方便，默认的 Red 和 Blue 两个 Ship Group 已经创建好了。这也是你从主菜单使用“Start As Empty”进入时看到的状态。当你想创建自定义剧本，或只是进入一个临时沙盒进行战斗时，也可以使用“Start As Empty”进入这个状态。

按 Insert 键，或点击 Top Tab - Editor 中对应的按钮，然后在地图上的任意位置点击以插入单位。
`,
`
歡迎來到教學 7：自訂劇本。在這裡，你將學習如何建立自訂劇本。你目前看到的是一個幾乎空白的劇本狀態，不過並不是完全空白，因為為了方便，預設的 Red 和 Blue 兩個 Ship Group 已經建立好了。這也是你從主選單使用「Start As Empty」進入時看到的狀態。當你想建立自訂劇本，或只是進入一個臨時沙盒進行戰鬥時，也可以使用「Start As Empty」進入這個狀態。

按 Insert 鍵，或點擊 Top Tab - Editor 中對應的按鈕，然後在地圖上的任意位置點擊以插入單位。
`);

markdownBox(tutorial7IntroMsg);
