if(phase === Phase.WaitForShipLogEditorOpened)
{
    let msg = getLocalized(`
Ship Log Editor is displayed. You can switch different tabs to see different information. Ship Log record unit's 'dynamic' information like damage and weapon states (ammunition, firing target, availability), doctrine and etc.

Some advance command can only be formed in the Ship Log Editor.

When you get a idea about the Ship Log Editor, click on 'Go to Named Ship' in the right top corner to go to Named Ship Editor.
`,
`
艦艇動態状態エディターが表示されました。各種タブを切り替えて、損傷状況や兵装状態（弾薬、射撃目標、使用可否）、作戦ドクトリンなど、ユニットの「動的」情報を確認できます。

一部の高度なコマンドは、艦艇動態状態エディターでのみ設定可能です。

艦艇動態状態エディターの操作を確認したら、右上隅の「艦名登録エディターへ移動」をクリックして艦名登録エディターに進んでください。
`,
`
舰艇动态状态编辑器已显示。您可以切换不同标签页查看舰艇的"动态"信息，如损伤情况、武器状态（弹药、射击目标、可用性）、作战条令等。

部分高级指令只能在舰艇动态状态编辑器中设置。

当您了解舰艇动态状态编辑器的功能后，请点击右上角的"转到具名舰艇编辑器"进入具名舰艇编辑器。
`,
`
艦艇動態狀態編輯器已顯示。您可以切換不同標籤頁查看艦艇的「動態」資訊，如損傷情況、武器狀態（彈藥、射擊目標、可用性）、作戰條令等。

部分高級指令只能在艦艇動態狀態編輯器中設置。

當您了解艦艇動態狀態編輯器的功能後，請點擊右上角的「轉到具名艦艇編輯器」進入具名艦艇編輯器。
`)

    msgBoxDelay(msg, 0.3);

    phase = Phase.WaitForNamedShipEditorOpened;
}