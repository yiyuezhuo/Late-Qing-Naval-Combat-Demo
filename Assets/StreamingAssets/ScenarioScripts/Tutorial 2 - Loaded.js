let msg = getLocalized(`
Welcome to the Second Tutorial Scenario of the First Sino-Japanese War. In this tutorial, you will learn how to control group.

There are two ships on the map. Now, measure the distance between them. This can be achieved by:

- Pressing the hotkey "D" to enter distance measuring mode. Click on the position of one ship, and then click on the position of the other ship. The distance will be displayed.
- Switching to the "Tool" tab in the top bar, clicking the "Distance Measuring" button to enter distance measuring mode, and then clicking on the two locations.
`,
`
日清戦争第二チュートリアルシナリオへようこそ。このチュートリアルでは、グループの操作方法を学びます。

マップ上に2隻の艦艇が表示されています。これら2隻の距離を測定してください。以下の方法で測定可能です：

- ホットキー「D」を押して距離測定モードに入る→1隻目の位置をクリック→2隻目の位置をクリック（距離が表示されます）
- 画面上部の「ツール」タブを選択→「距離測定」ボタンをクリック→2か所の地点を順にクリック
`,
`
欢迎来到甲午战争第二教程场景。在本教程中，您将学习如何控制编队。

地图上现有两艘舰艇。请测量两者之间的距离，可通过以下方式实现：

- 按热键"D"进入距离测量模式→点击第一艘舰艇位置→点击第二艘舰艇位置（将显示距离值）
- 点击顶部工具栏"工具"标签页→选择"距离测量"功能→依次点击两个位置
`,
`
歡迎來到甲午戰爭第二教程場景。在本教程中，您將學習如何控制編隊。

地圖上現有兩艘艦艇。請測量兩者之間的距離，可透過以下方式實現：

- 按熱鍵「D」進入距離測量模式→點擊第一艘艦艇位置→點擊第二艘艦艇位置（將顯示距離值）
- 點擊頂部工具欄「工具」標籤頁→選擇「距離測量」功能→依次點擊兩個位置
`)

msgBox(msg);

var Phase = {
    WaitForDistanceMeasuring : 1,
    WaitForDistanceClosing : 2,
    WaitForFollowingEquilibrium : 3,
    WaitForShipLogEditorShown : 4,
    WaitForFollowingEquilibrium2 : 5,
    WaitForShipRelativeToEquilibrium : 6,
    End : 7
}

var phase = Phase.WaitForDistanceMeasuring;