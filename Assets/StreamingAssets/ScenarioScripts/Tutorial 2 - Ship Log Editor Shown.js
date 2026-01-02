if(phase === Phase.WaitForShipLogEditorShown)
{
    let msg = getLocalized(`
Ship State View is displayed.

Verify the value of Control Mode field is "follow", then set Follow Distance to 1000 yards from default value 500 yards.

Confirm to close the view and advance time until they reach to new equilibrium. 
`,
`
艦船状態ビューが表示されました。

「制御モード」フィールドの値が「追従」であることを確認し、「追従距離」をデフォルト値の500ヤードから1000ヤードに変更してください。

画面を閉じて確認し、新しい均衡状態に達するまで時間を進めてください。
`,
`
舰艇状态视图已显示。

请确认"控制模式"字段值为"跟随"，然后将"跟随距离"从默认值500码调整为1000码。

确认关闭视图，推进时间直至达到新的平衡状态。
`,
`
艦艇狀態已顯示。

請確認「控制模式」字段值為「跟隨」，然後將「跟隨距離」從默認值500碼調整為1000碼。

確認關閉編輯器，推進時間直至達到新的平衡狀態。
`)

    msgBoxDelay(msg, 0.3);

    phase = Phase.WaitForFollowingEquilibrium2;
}