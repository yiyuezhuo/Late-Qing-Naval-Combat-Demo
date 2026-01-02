if(phase === Phase.WaitForShipLogEditorOpened)
{
    let msg = getLocalized(`
Ship State View displays a unit's dynamic information such as damage, weapon states (ammunition, firing target, availability), doctrine, and more. You can switch between tabs to view different categories.

Certain advanced commands can only be issued from the Ship State View.

Once you are familiar with the Ship State View, click “Go to Named Ship” in the top right corner to proceed to the Named Ship View.
`,
`
艦船状態ビューには、ダメージ状況、武器の状態（弾薬、攻撃対象、使用可否）、ドクトリンなど、ユニットの動的な情報が表示されます。タブを切り替えることで、さまざまなカテゴリの情報を確認できます。

一部の高度なコマンドは、この艦船状態ビューからのみ実行可能です。

操作に慣れたら、右上にある「名前付き艦船へ移動」をクリックして、名前付き艦船ビューへ進んでください。
`,
`
舰船状态视图显示单位的动态信息，例如损毁情况、武器状态（弹药、攻击目标、可用性）、条令等。您可以切换标签页以查看不同类别的信息。

某些高级指令只能从舰船状态视图中发布。

熟悉舰船状态视图后，请点击右上角的“前往具名舰船”以进入具名舰船视图。
`,
`
艦船狀態檢視顯示單位的動態資訊，例如損毀情況、武器狀態（彈藥、攻擊目標、可用性）、條令等。您可以切換分頁以查看不同類別的資訊。

某些高級指令只能從艦船狀態檢視中發佈。

熟悉艦船狀態檢視後，請點擊右上角的「前往具名艦船」以進入具名艦船檢視。
`)

    msgBoxDelay(msg, 0.3);

    phase = Phase.WaitForNamedShipEditorOpened;
}