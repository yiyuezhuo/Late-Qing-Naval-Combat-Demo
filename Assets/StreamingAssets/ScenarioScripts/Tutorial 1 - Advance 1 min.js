if(phase === Phase.WaitForTimeAdvanced)
{
    let msg = getLocalized(`
Time is advanced by 1min, you can find that time is changed in the bottom line and ship is moved. 

Now left click on the ship to select it
`,
`
時間が1分進みました。下部の時刻表示が変化し、艦艇が移動したことを確認できます。

艦艇を左クリックして選択してください。
`,
`
时间已前进1分钟，您可以看到底部时间显示已更新，且舰艇已移动。

请左键单击舰艇以选中它。
`,
`
時間已前進1分鐘，您可以看到底部時間顯示已更新，且艦艇已移動。

請左鍵單擊艦艇以選中它。
`);
    msgBoxDelay(msg, 0.2);

    phase = Phase.WaitForUnitSelection;
}
else if(phase === Phase.WaitForSpeedChanged)
{
    let shipLog = NavalGameState.Instance.shipLogs[0];
    if(shipLog.speedKnots >= 15)
    {
        let msg = getLocalized(`
Speed is increased to 15 knots.

Right-click on the map to select the desired heading (the desired heading will be set from the selected ship toward the clicked location).
This can also be adjusted by dragging the heading slider on the right panel, or by holding Shift and left-clicking on the map.
You can also click the Set Course button above, then click a point on the map to set the desired heading.
The right-click method is recommended.

Now, set the course to 90 degrees clockwise from north (east) and advance the time until reaching the desired heading.
`,
`
速度が 15 ノットに増加しました。

マップ上を右クリックして、目標方位を選択してください（選択した艦船からクリックした地点への方向が目標方位として設定されます）。
右パネルの方位スライダーをドラッグして調整することもでき、Shift キーを押しながらマップ上を左クリックして設定することもできます。
また、上部の 針路を設定 ボタンをクリックしてから、マップ上の地点をクリックして目標方位を設定することもできます。
右クリックの方法を推奨します。

それでは、針路を北から時計回りに 90 度（東）に設定し、目標の方位に到達するまで時間を進めてください。
`,
`
速度已提高至 15 节。

在地图上右键点击以选择目标航向（目标航向将从所选舰船指向点击的位置）。
您也可以通过拖动右侧面板上的航向滑块进行调整，或按住 Shift 键并在地图上左键点击来设定。
另外，也可以先点击上方的 设置航向 按钮，再点击地图上的一点来设定目标航向。
推荐使用右键点击的方法。

现在，请将航向设置为正北顺时针 90 度（正东），并推进时间直到达到目标航向。
`,
`
速度已提高至 15 節。

在地圖上右鍵點擊以選擇目標航向（目標航向將從所選艦船指向點擊的位置）。
您也可以透過拖動右側面板上的航向滑塊進行調整，或按住 Shift 鍵並在地圖上左鍵點擊來設定。
另外，也可以先點擊上方的 設定航向 按鈕，再點擊地圖上的一點來設定目標航向。
推薦使用右鍵點擊的方法。

現在，請將航向設置為正北順時針 90 度（正東），並推進時間直到達到目標航向。
`);

        msgBoxDelay(msg, 0.3);
        phase = Phase.WaitForCourseChanged;
    }
}
else if(phase === Phase.WaitForCourseChanged)
{
    let shipLog = NavalGameState.Instance.shipLogs[0];
    if(shipLog.headingDeg >= 75 && shipLog.headingDeg <= 105)
    {
        let msg = getLocalized(`
Course is changed.

Now right-click the unit on the map or left-click on unit's name hyper link in the information panel to open the Ship State View.

Note: The Ship State View will open as either a list editor or a single-ship view, depending on whether game is in edit mode.
`,
`
針路が変更されました。

マップ上のユニットを右クリックするか、情報パネル内のユニット名ハイパーリンクを左クリックして「艦船状態ビュー」を開いてください。

注意： ゲームがエディットモードであるかどうかに応じて、「艦船状態ビュー」はリストエディターまたは個別艦船ビューのいずれかで開きます。
`,
`
航向已改变。

现在，请右键点击地图上的单位，或左键点击信息面板中的单位名称超链接，以打开“舰船状态视图”。

注意： 根据游戏是否处于编辑模式，“舰船状态视图”将以列表编辑器或单舰视图的形式打开。
`,
`
航向已改變。

現在，請右鍵點擊地圖上的單位，或左鍵點擊資訊面板中的單位名稱超連結，以開啟「艦船狀態檢視」。

注意： 根據遊戲是否處於編輯模式，「艦船狀態檢視」將以清單編輯器或單艦檢視的形式開啟。
`);

        msgBoxDelay(msg, 0.3);
        phase = Phase.WaitForShipLogEditorOpened;
    }
}
