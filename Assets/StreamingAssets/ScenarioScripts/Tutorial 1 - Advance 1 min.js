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
Speed is increased to 15.

Now drag heading slider to change desired heading, and holding shift and left click to the map to set desired heading as well.
Point to 75-120 True North Clockwise and then advance time until the ship reaches the desired heading.
`,
`
速度が15に増加しました。

次に、ヘディングスライダーをドラッグして目標針路を変更するか、Shiftキーを押しながら地図を左クリックして目標針路を設定します。

真北時計回り75-120度の方向を指定し、艦艇が目標針路に到達するまで時間を進めてください。
`,
`
速度已增加至15。

现在拖动航向滑块更改目标航向，或按住Shift键并左键单击地图来设置目标航向。

指向真北顺时针75-120度方向，然后推进时间直至舰艇到达目标航向。
`,
`
速度已增加至15。

現在拖動航向滑塊更改目標航向，或按住Shift鍵並左鍵單擊地圖來設置目標航向。

指向真北順時針75-120度方向，然後推進時間直至艦艇到達目標航向。
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

Now right-click the unit or left-click on unit's name hyper link in the information panel to open the ship log editor (the game use the same UI for game and 'editor', just like CMO)
`,
`
針路が変更されました。

ユニットを右クリックするか、情報パネル内のユニット名ハイパーリンクを左クリックすると、艦艇動態状態エディターが開きます（本ゲームはCMOと同様、ゲームと「エディター」で同一のUIを使用しています）。
`,
`
航向已更改。

现在右键单击单位，或左键单击信息面板中的单位名称超链接，即可打开舰艇动态状态编辑器（本游戏与CMO一样，游戏和“编辑器”使用相同的UI）。
`,
`
航向已更改。

現在右鍵單擊單位，或左鍵單擊信息面板中的單位名稱超鏈接，即可打開艦艇動態狀態編輯器（本遊戲與CMO一樣，遊戲和「編輯器」使用相同的UI）。
`);

        msgBoxDelay(msg, 0.3);
        phase = Phase.WaitForShipLogEditorOpened;
    }
}