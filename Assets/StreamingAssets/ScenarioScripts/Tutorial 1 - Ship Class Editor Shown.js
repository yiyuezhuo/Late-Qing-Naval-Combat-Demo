if(phase === Phase.WaitForShipClassEditorOpened)
{
    let msg = getLocalized(`
The Ship Class View is displayed. You can switch between different tabs to see various details. This section saves the static information for a ship class, such as speed, DP, weaponry, etc.

Once you have an idea about the ship class, click "Confirm" in the bottom-left corner to return to the main map.
`,
`
艦級ビューが表示されます。タブを切り替えることで、さまざまな詳細情報を確認できます。このセクションには、速力、DP、兵装など、その艦級の固定情報（スタティック情報）が保存されています。

艦級についての確認が終わったら、左下にある「確認」をクリックしてメインマップに戻ってください。
`,
`
舰船型号视图已显示。您可以切换不同的标签页以查看各项细节。此板块保存了该舰船型号的静态信息，例如航速、DP、武器配置等。

对舰船型号有初步了解后，请点击左下角的“确认”以返回主地图。
`,
`
艦船型號檢視已顯示。您可以切換不同的分頁以查看各項細節。此區塊保存了該艦船型號的靜態資訊，例如航速、DP、武器配置等。

對艦船型號有初步了解後，請點擊左下角的「確認」以返回主地圖。
`)

    msgBoxDelay(msg, 0.3);

    phase = Phase.WaitForShipClassEditorHidden;

}
