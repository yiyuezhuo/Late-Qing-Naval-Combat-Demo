if(phase === Phase.WaitForNamedShipEditorOpened)
{
    let msg = getLocalized(`
Named Ship View is displayed. A named ship is an 'instance' of a ship class, with additional time-related information attached (e.g., the captain at a specific time point).

Once you understand the Named Ship View, click on 'Go to Ship Class' in the top right corner to proceed to the Ship Class View.
`,
`
名前付き艦船ビューが表示されます。名前付き艦船とは、艦級の「インスタンス」であり、特定の時点における艦長などの時間に関連する追加情報が付随しています。

名前付き艦船ビューの内容を理解したら、右上にある「艦級へ移動」をクリックして、艦級ビューへ進んでください。
`,
`
具名舰船视图已显示。具名舰船是舰船型号的一个“实例”，并附带有与时间相关的额外信息（例如特定时间点的舰长）。

了解具名舰船视图后，请点击右上角的“前往舰船型号”以进入舰船型号视图。
`,
`
具名艦船檢視已顯示。具名艦船是艦船型號的一個「實例」，並附帶有與時間相關的額外資訊（例如特定時間點的艦長）。

了解具名艦船檢視後，請點擊右上角的「前往艦船型號」以進入艦船型號檢視。
`)

    msgBoxDelay(msg, 0.3);

    phase = Phase.WaitForShipClassEditorOpened;

}