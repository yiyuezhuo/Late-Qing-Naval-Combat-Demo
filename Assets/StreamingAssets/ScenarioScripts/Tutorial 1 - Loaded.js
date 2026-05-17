// msgBox(`
// Welcome to the First Sino-Japanese War tutorial scenario 1. In this tutorial, you will learn how to perform map navigation and control individual units.

// On the bottom information bar, you can see the latitude and longitude of the current cursor position. The current UTC and local time are also shown, followed by an indication of whether it is day or night and the current time zone. Additionally, the sun's altitude and azimuth are displayed, which affect visibility and may sometimes provide gunnery bonuses or penalties depending on the angle.

// To move the camera, press and hold the right mouse button while moving the mouse (known as right-click dragging). This is a common method for camera movement in GIS applications and similar games.
// `);

let msg = getLocalized(`
Welcome to the First Sino-Japanese War tutorial scenario 1. In this tutorial, you will learn how to perform map navigation and control individual units.

On the bottom information bar, you can see the latitude and longitude of the current cursor position. The current local time is also shown, followed by an indication of whether it is day or night and the current time zone. Additionally, the sun's altitude and azimuth are displayed, which affect visibility and may sometimes provide gunnery bonuses or penalties depending on the angle.

To move the camera, press and hold the right mouse button while moving the mouse (known as right-click dragging). This is a common method for camera movement in GIS applications and similar games.

Now click Confirm to close this dialog and try to move the camera.
`,
`
日清戦争チュートリアルシナリオ1へようこそ。このチュートリアルでは、マップの移動や個々のユニットの操作方法を学びます。

下部の情報バーには、現在のカーソル位置の緯度と経度が表示されます。現在の現地時間、昼夜の区別、現在のタイムゾーンも表示されます。さらに、太陽高度と方位角が表示され、これらは視界に影響を与え、角度によっては砲撃にボーナスまたはペナルティが発生することがあります。

カメラを移動するには、右クリックを押したままマウスを動かしてください（右クリックドラッグ）。これは、GISアプリケーションや同種のゲームで一般的に用いられるカメラ操作方法です。

「確認」をクリックしてこのダイアログを閉じ、カメラを動かしてみてください。
`,
`
欢迎来到甲午战争教程剧本1。在本教程中，您将学习如何进行地图导航和控制单个单位。

底部信息栏显示当前光标位置的经纬度坐标，同时显示本地时间，并标注昼夜状态和当前时区。此外还会显示太阳高度角与方位角——这些数据会影响能见度，并根据角度关系为炮击提供增益或减益效果。

要移动视角，请按住鼠标右键并移动鼠标（即右键拖动）。这是一种在 GIS 程序及类似游戏中常见的视角移动方式。

现在点击“确认”关闭此对话框，并尝试移动视角。
`,
`
歡迎來到甲午戰爭教程劇本1。在本教程中，您將學習如何進行地圖導覽和控制單個單位。

底部資訊欄顯示當前光標位置的經緯度座標，同時顯示當前本地時間，並標註晝夜狀態和當前時區。此外還會顯示太陽高度角與方位角——這些數據會影響能見度，並根據角度關係為炮擊提供增益或減益效果。

要移動視角，請按住滑鼠右鍵並移動滑鼠（即右鍵拖曳）。這是在 GIS 應用程式及類似遊戲中常見的視角操作方式。

現在點擊「確認」關閉此對話框，並嘗試移動視角。
`)

msgBox(msg);

var Phase = {
    WaitForCameraMove : 1,
    WaitForCameraZoom : 2,
    WaitForTimeAdvanced : 3,
    WaitForUnitSelection : 4,
    WaitForSpeedChanged : 5,
    WaitForCourseChanged : 6,
    WaitForShipLogEditorOpened : 7,
    WaitForNamedShipEditorOpened : 8,
    WaitForShipClassEditorOpened : 9,
    WaitForShipClassEditorHidden : 10,
    End : 11
}

var phase = Phase.WaitForCameraMove;
