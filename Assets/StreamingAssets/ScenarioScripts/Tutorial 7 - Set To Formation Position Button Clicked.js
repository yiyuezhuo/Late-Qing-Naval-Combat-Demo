if (tutorial7Phase === Tutorial7Phase.WaitingForFormationPosition) {
    let tutorial7FormationPositionMsg = getLocalized(`
Now insert two other ships (Ex. China ships Chi Yuan and Kuang Yi), set Attach to Group to Red and click "Set to Formation Position" to arrange them. 

After that you have made a minimal scenario/sandbox and you can play it. 

Also you can save the scenario: Switch to File Tabs, disable "Save without Streaming Asset" (required because some anonymous Named Ship were created) and click the "Save (Edit)" button to save the scenario.

"Load Game" button in the Main Menu can be used to load the saved scenario to start to playing your new created custom scenario. 

You can also experiment with some other editing, like:

- Create/Delete group in the OOB editor
- Drag Ship/Group in the OOB Editor to change OOB relationship

Once you feel you have tested enough, exit this tutorial and move on to the next one.
`,
`
次に、別の艦艇を2隻挿入してください（例：中国艦の Chi Yuan と Kuang Yi）。Attach to Group を Red に設定し、「Set to Formation Position」をクリックして配置します。

これで、最小限のシナリオ／サンドボックスが完成し、プレイできるようになります。

また、このシナリオを保存することもできます。File タブに切り替え、「Save without Streaming Asset」を無効にして（匿名の Named Ship が作成されているため必要です）、「Save (Edit)」ボタンをクリックしてシナリオを保存してください。

メインメニューの「Load Game」ボタンを使うと、保存したシナリオを読み込み、新しく作成したカスタムシナリオでプレイを開始できます。

ほかにも、次のような編集を試すことができます。

- OOB editor でグループを作成／削除する
- OOB Editor で Ship/Group をドラッグして OOB 関係を変更する

十分に試したら、このチュートリアルを終了して次へ進んでください。
`,
`
现在再插入另外两艘船（例如中国舰 Chi Yuan 和 Kuang Yi），把 Attach to Group 设置为 Red，然后点击“Set to Formation Position”来排列它们。

完成后，你就做出了一个最小可用的剧本/沙盒，并且可以开始游玩。

你也可以保存这个剧本：切换到 File 标签页，关闭“Save without Streaming Asset”（因为创建了匿名 Named Ship，所以这里需要这样做），然后点击“Save (Edit)”按钮保存剧本。

主菜单中的“Load Game”按钮可以用来加载保存后的剧本，并开始游玩你新创建的自定义剧本。

你还可以尝试其他编辑操作，例如：

- 在 OOB editor 中创建/删除 group
- 在 OOB Editor 中拖动 Ship/Group 以改变 OOB 关系

当你觉得已经测试足够后，退出本教程并进入下一个教程。
`,
`
現在再插入另外兩艘船（例如中國艦 Chi Yuan 和 Kuang Yi），把 Attach to Group 設定為 Red，然後點擊「Set to Formation Position」來排列它們。

完成後，你就做出了一個最小可用的劇本/沙盒，並且可以開始遊玩。

你也可以儲存這個劇本：切換到 File 標籤頁，關閉「Save without Streaming Asset」（因為建立了匿名 Named Ship，所以這裡需要這樣做），然後點擊「Save (Edit)」按鈕儲存劇本。

主選單中的「Load Game」按鈕可以用來載入儲存後的劇本，並開始遊玩你新建立的自訂劇本。

你還可以嘗試其他編輯操作，例如：

- 在 OOB editor 中建立/刪除 group
- 在 OOB Editor 中拖動 Ship/Group 以改變 OOB 關係

當你覺得已經測試足夠後，退出本教學並進入下一個教學。
`);
    markdownBox(tutorial7FormationPositionMsg);

    tutorial7Phase = Tutorial7Phase.End;
}
