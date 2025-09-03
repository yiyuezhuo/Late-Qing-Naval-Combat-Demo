if(phase === Phase.WaitForShipClassEditorOpened)
{
    let msg = getLocalized(`
Ship Class Editor is displayed. You can switch different tabs to see different information. Ship Class save static information of a ship class like speed, DP, weapon and etc.

When you get a idea about the Ship Log Editor, click on 'Confirm' in the left bottom corner to go to main map.
`,
`
艦艇型号エディターが表示されました。各種タブを切り替えて様々な情報を確認できます。艦艇型号エディターは速度、DP、兵装など艦艇型号の静的情報を保存します。

艦艇ログエディターの操作を確認したら、左下隅の「確認」をクリックしてメインマップに戻ってください。
`,
`
舰艇型号编辑器已显示。您可以切换不同标签页查看各类信息。舰艇型号编辑器保存舰艇型号的静态信息，如速度、耐久值、武器等。

当您了解舰艇日志编辑器的功能后，请点击左下角的“确认”返回主地图。
`,
`
艦艇型號編輯器已顯示。您可以切換不同標籤頁查看各類資訊。艦艇型號編輯器保存艦艇型號的靜態資訊，如速度、耐久值、武器等。

當您了解艦艇日誌編輯器的功能後，請點擊左下角的「確認」返回主地圖。
`)

    msgBoxDelay(msg, 0.3);

    phase = Phase.WaitForShipClassEditorHidden;

}