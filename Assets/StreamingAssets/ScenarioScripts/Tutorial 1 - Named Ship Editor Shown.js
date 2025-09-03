if(phase === Phase.WaitForNamedShipEditorOpened)
{
    let msg = getLocalized(`
Named Ship Editor is displayed. Named ship is an 'instance' of a ship class, with some extra time related information attached.

When you get a idea about the Named Ship Editor, click on 'Go to Ship Class' in the right top corner to go to Ship Class Editor.
`,
`
艦名登録エディターが表示されました。艦名登録エディターは艦艇型号の「インスタンス」であり、時間関連の追加情報が付属しています。

艦名登録エディターの操作を確認したら、右上隅の「艦艇型号エディターへ移動」をクリックして艦艇型号エディターに進んでください。
`,
`
具名舰艇编辑器已显示。具名舰艇是舰艇型号的一个“实例”，附带有一些时间相关的额外信息。

当您了解具名舰艇编辑器的功能后，请点击右上角的“转到舰艇型号编辑器”进入舰艇型号编辑器。
`,
`
具名艦艇編輯器已顯示。具名艦艇是艦艇型號的一個「實例」，附帶有一些時間相關的額外資訊。

當您了解具名艦艇編輯器的功能後，請點擊右上角的「轉到艦艇型號編輯器」進入艦艇型號編輯器。
`)

    msgBoxDelay(msg, 0.3);

    phase = Phase.WaitForShipClassEditorOpened;

}