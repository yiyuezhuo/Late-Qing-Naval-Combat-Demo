if(phase == Phase.WaitForDistanceClosing)
{
    let ship0 = NavalGameState.Instance.shipLogs[0];
    let ship1 = NavalGameState.Instance.shipLogs[1];
    if(getDistanceYard(ship0, ship1) < 2500)
    {
        let msg = getLocalized(`
Distance is less than 2500 yards.

Now select a ship and press the "F" key, then click on another ship to have it follow. Alternatively, you can press the corresponding button in the top bar.

(This changes a unit from "independent" control mode to "follow" mode, and it will try to follow the target at a default distance of 500 yards. Its arrow will be hidden, as the arrow indicates a unit is in independent mode and is generally a group leader.)

Then advance time until they form a proper follow formation (with a closing heading and maintaining the target distance).
`,
`
距離が2500ヤード未満になりました。

艦艇を1隻選択し「F」キーを押した後、別の艦艇をクリックすると追従モードになります。画面上部の対応ボタンでも操作可能です。

（この操作でユニットは「独立」制御モードから「追従」モードに変更され、デフォルト距離500ヤードで目標を追従します。矢印は独立モード（通常は編隊リーダー）を示すため、追従モードでは非表示になります）

その後、時間を進めて適切な追従陣形を形成してください（針路を合わせながら目標距離を維持する状態になります）
`,
`
距离已小于2500码。

请选择一艘舰艇并按"F"键，然后点击另一艘舰艇使其进入跟随模式。也可点击顶部工具栏对应按钮实现。

（此操作将单位从"独立"控制模式切换为"跟随"模式，其将以默认500码距离跟随目标。箭头标记表示单位处于独立模式（通常为编队领舰），跟随模式下箭头将隐藏）

随后推进时间直至形成适当的跟随队形（保持接近航向并维持目标距离）
`,
`
距離已少於2500碼。

請選擇一艘艦艇並按「F」鍵，然後點擊另一艘艦艇使其進入跟隨模式。也可點擊頂部工具欄對應按鈕實現。

（此操作將單位從「獨立」控制模式切換為「跟隨」模式，其將以預設500碼距離跟隨目標。箭頭標記表示單位處於獨立模式（通常為編隊領艦），跟隨模式下箭頭將隱藏）

隨後推進時間直至形成適當的跟隨隊形（保持接近航向並維持目標距離）
`);

        msgBoxDelay(msg, 0.3);

        phase = Phase.WaitForFollowingEquilibrium;
    }
}
else if(phase === Phase.WaitForFollowingEquilibrium)
{
    let ship0 = NavalGameState.Instance.shipLogs[0];
    let ship1 = NavalGameState.Instance.shipLogs[1];
    let distYards = getDistanceYard(ship0, ship1);
    let headingAbsDiff = getPositiveAngleDifference(ship0, ship1);
    if(distYards >= 475 && distYards <= 525 && headingAbsDiff <= 10)
    {
        let msg = getLocalized(`
Follow formation is formed properly (a control equilibrium is reached).

Now open Ship State View for the non-indepedent ship (the ship following another ship) to set extra control parameter.
`,
`
追従陣形が適切に形成されました（制御均衡が達成されています）。

独立モードではない艦艇（他艦を追従中の艦艇）の艦船状態ビューを開き、追加制御パラメータを設定してください。
`,
`
跟随队形已正确形成（达到控制平衡状态）。

现在请打开非独立单位（跟随其他舰艇的舰艇）的舰艇动态状态视图，设置额外控制参数。
`,
`
跟隨隊形已正確形成（達到控制平衡狀態）。

現在請打開非獨立單位（跟隨其他艦艇的艦艇）的艦艇動態狀態檢視，設置額外控制參數。
`)

        msgBoxDelay(msg, 0.3);

        phase = Phase.WaitForShipLogEditorShown;
    }
}
else if(phase === Phase.WaitForFollowingEquilibrium2)
{
    let ship0 = NavalGameState.Instance.shipLogs[0];
    let ship1 = NavalGameState.Instance.shipLogs[1];
    let distYards = getDistanceYard(ship0, ship1);
    let headingAbsDiff = getPositiveAngleDifference(ship0, ship1);
    if(distYards >= 975 && distYards <= 1025 && headingAbsDiff <= 10)
    {
        let msg = getLocalized(`
A new control equilibrium is reached.

Now select the non-independent ship and press "R" (or corresponding button on the top bar) and click on another ship to set "Relative To" control mode, controlled unit will try to maintain a bearing and distance to target.

Advance time until new control equilibrium is reached.
`,
`
新しい制御均衡が達成されました。

非独立艦艇を選択し「R」キー（または画面上部の対応ボタン）を押した後、別の艦艇をクリックして「相対位置維持」制御モードを設定してください。これにより、制御対象艦艇は目標に対する方位と距離を維持しようとします。

新しい制御均衡が達成されるまで時間を進めてください。
`,
`
新的控制平衡已达成。

请选择非独立舰艇并按"R"键（或顶部工具栏对应按钮），然后点击另一艘舰艇设置"相对位置"控制模式。受控单位将尝试维持与目标的相对方位和距离。

推进时间直至达成新的控制平衡。
`,
`
新的控制平衡已達成。

請選擇非獨立艦艇並按「R」鍵（或頂部工具欄對應按鈕），然後點擊另一艘艦艇設置「相對位置」控制模式。受控單位將嘗試維持與目標的相對方位和距離。

推進時間直至達成新的控制平衡。
`)

        msgBoxDelay(msg, 0.3);

        phase = Phase.WaitForShipRelativeToEquilibrium;
    }
}
else if(phase === Phase.WaitForShipRelativeToEquilibrium)
{
    let ship0 = NavalGameState.Instance.shipLogs[0];
    let ship1 = NavalGameState.Instance.shipLogs[1];
    let stats = measure(ship0, ship1);
    let distYards = stats.distanceYards;
    let headingAbsDiff = getPositiveAngleDifference(ship0, ship1);
    let bearing01 = stats.observerToTargetBearingRelativeToBowDeg;
    let bearing10 = stats.targetToObserverBearingRelativeToBowDeg;
    let absBearingDiff = Math.min(Math.abs(bearing01 - 135), Math.abs(bearing10 - 135));
    if(distYards >= 225 && distYards <= 275 && headingAbsDiff <= 10 && absBearingDiff < 5)
    {
        let msg = getLocalized(`
Relative-To equilibrium is reached.

This tutorial scenario is concluded. You can play around extra parameter for relative-to control mode, control group lead and see how does controlled ship respond. Then return to main menu and check other tutorial scenarios.
`,
`
相対位置維持モードの制御均衡が達成されました。

本チュートリアルシナリオは終了です。以下の操作を自由にお試しください：

- 相対位置維持モードの追加パラメータ調整
- グループリーダーの制御設定変更
- 被制御艦艇の応答動作確認

その後、メインメニューに戻り他のチュートリアルシナリオをご確認ください。
`,
`
相对位置控制平衡已达成。

本教程场景已结束。您可以自由尝试以下操作：

- 调整相对位置控制模式的额外参数
- 更改编队领舰的控制设置
- 观察受控舰艇的响应行为

随后可返回主菜单查看其他教程场景。
`,
`
相對位置控制平衡已達成。

本教程場景已結束。您可以自由嘗試以下操作：

- 調整相對位置控制模式的額外參數
- 更改編隊領艦的控制設置
- 觀察受控艦艇的響應行為

隨後可返回主菜單查看其他教程場景。
`)

        msgBoxDelay(msg, 0.3);

        phase = Phase.End;
    }
}