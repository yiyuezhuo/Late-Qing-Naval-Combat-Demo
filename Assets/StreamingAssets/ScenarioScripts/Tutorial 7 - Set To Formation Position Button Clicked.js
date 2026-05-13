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
次に、別の艦艇を2隻挿入してください（例：中国艦の「済遠」と「広乙」）。「グループに配属」を「赤」に設定し、「陣形位置に設定」をクリックして配置します。

これで、最小限のシナリオ／サンドボックスが完成し、プレイできるようになります。

また、このシナリオを保存することもできます。ファイルタブに切り替え、「ストリーミングアセットなしで保存」を無効にして（匿名の名前付き艦船が作成されているため必要です）、「保存(編集)」ボタンをクリックしてシナリオを保存してください。

メインメニューの「ゲームをロード」ボタンを使うと、保存したシナリオを読み込み、新しく作成したカスタムシナリオでプレイを開始できます。

ほかにも、次のような編集を試すことができます。

- OOBエディタでグループ作成／グループ削除を行う
- OOBエディタで艦船/グループをドラッグしてOOB関係を変更する

十分に試したら、このチュートリアルを終了して次へ進んでください。
`,
`
现在再插入另外两艘船（例如中国舰“济远”和“广乙”），把“配属给编组”设置为“红”，然后点击“根据队形设置位置”来排列它们。

完成后，你就做出了一个最小可用的剧本/沙盒，并且可以开始游玩。

你也可以保存这个剧本：切换到文件标签页，关闭“保存时不包含流式资产”（因为创建了匿名具名舰船，所以这里需要这样做），然后点击“保存(编辑)”按钮保存剧本。

主菜单中的“加载游戏”按钮可以用来加载保存后的剧本，并开始游玩你新创建的自定义剧本。

你还可以尝试其他编辑操作，例如：

- 在战斗序列编辑器中创建编组/删除编组
- 在战斗序列编辑器中拖动舰船/编组以改变OOB关系

当你觉得已经测试足够后，退出本教程并进入下一个教程。
`,
`
現在再插入另外兩艘船（例如中國艦「濟遠」和「廣乙」），把「配屬給編組」設定為「紅」，然後點擊「根據隊形設定位置」來排列它們。

完成後，你就做出了一個最小可用的劇本/沙盒，並且可以開始遊玩。

你也可以儲存這個劇本：切換到檔案標籤頁，關閉「保存時不包含流式資產」（因為建立了匿名具名艦船，所以這裡需要這樣做），然後點擊「儲存(編輯)」按鈕儲存劇本。

主選單中的「載入遊戲」按鈕可以用來載入儲存後的劇本，並開始遊玩你新建立的自訂劇本。

你還可以嘗試其他編輯操作，例如：

- 在戰鬥序列編輯器中創建編組/刪除編組
- 在戰鬥序列編輯器中拖動艦船/編組以改變OOB關係

當你覺得已經測試足夠後，退出本教學並進入下一個教學。
`);
    markdownBox(tutorial7FormationPositionMsg);

    tutorial7Phase = Tutorial7Phase.End;
}
