// msgBox(`
// Welcome to the First Sino-Japanese War tutorial scenario 1. In this tutorial, you will learn how to perform map navigation and control individual units.

// On the bottom information bar, you can see the latitude and longitude of the current cursor position. The current UTC and local time are also shown, followed by an indication of whether it is day or night and the current time zone. Additionally, the sun's altitude and azimuth are displayed, which affect visibility and may sometimes provide gunnery bonuses or penalties depending on the angle.

// To move the camera, press and hold the right mouse button while moving the mouse (known as right-click dragging). This is a common method for camera movement in GIS applications and similar games.
// `);

let msg = getLocalized(`
Welcome to the First Sino-Japanese War tutorial scenario 1. In this tutorial, you will learn how to perform map navigation and control individual units.

On the bottom information bar, you can see the latitude and longitude of the current cursor position. The current UTC and local time are also shown, followed by an indication of whether it is day or night and the current time zone. Additionally, the sun's altitude and azimuth are displayed, which affect visibility and may sometimes provide gunnery bonuses or penalties depending on the angle.

To move the camera, press and hold the right mouse button while moving the mouse (known as right-click dragging). This is a common method for camera movement in GIS applications and similar games.
`,
`
日清戦争チュートリアルシナリオ1へようこそ。このチュートリアルでは、マップの移動や個々のユニットの操作方法を学びます。

下部の情報バーには、現在のカーソル位置の緯度と経度が表示されます。現在のUTCと現地時間、昼夜の区別、現在のタイムゾーンも表示されます。さらに、太陽高度と方位角が表示され、これらは視界に影響を与え、角度によっては砲撃にボーナスまたはペナルティが発生することがあります。

カメラを移動するには、マウスの右ボタンを押したままマウスを動かします（右クリックドラッグと呼ばれます）。これはGISアプリケーションや類似のゲームでカメラ移動に一般的に使われる方法です。
`,
`
欢迎来到甲午战争教程剧本1。在本教程中，您将学习如何进行地图导航和控制单个单位。

底部信息栏显示当前光标位置的经纬度坐标，同时显示当前UTC时间与本地时间，并标注昼夜状态和当前时区。此外还会显示太阳高度角与方位角——这些数据会影响能见度，并根据角度关系为炮击提供增益或减益效果。

移动镜头时，请按住鼠标右键并拖动鼠标（称为右键拖动）。这是GIS应用及同类游戏中常见的镜头操控方式。
`,
`
歡迎來到甲午戰爭教程劇本1。在本教程中，您將學習如何進行地圖導覽和控制單個單位。

底部資訊欄顯示當前光標位置的經緯度座標，同時顯示當前UTC時間與本地時間，並標註晝夜狀態和當前時區。此外還會顯示太陽高度角與方位角——這些數據會影響能見度，並根據角度關係為炮擊提供增益或減益效果。

移動鏡頭時，請按住滑鼠右鍵並拖動滑鼠（稱為右鍵拖動）。這是GIS應用及同類遊戲中常見的鏡頭操控方式。
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