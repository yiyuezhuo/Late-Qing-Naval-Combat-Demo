var tutorial7ShipCount = NavalGameState.Instance.shipLogs.Count;

if (tutorial7Phase === Tutorial7Phase.WaitingForFirstShipInsertion && tutorial7ShipCount >= 1) {
    let tutorial7FirstShipInsertedMsg = getLocalized(`
You have already added a Named Ship Yoshino to Blue Ship Group.

Click Insert again to open the insert dialog, you will notice Yoshino is no longer available in the Named Ship column. However it remains available in the Ship Class columns.

Insert two more "anonymous" Yoshino by Ship Class method.
`,
`
名前付き艦船「吉野」を青グループに追加しました。

もう一度「挿入（Ins）」をクリックして挿入ダイアログを開くと、「吉野」が名前付き艦船列では選択できなくなっていることに気づくはずです。ただし、艦級列では引き続き選択できます。

艦級方式で、さらに2隻の「匿名」吉野を挿入してください。
`,
`
你已经把具名舰船“吉野”添加到了蓝编组。

再次点击“插入(Ins)”打开插入对话框，你会发现“吉野”已经不再出现在具名舰船列中。不过它仍然会出现在舰船型号列中。

请通过舰船型号方式再插入两个“匿名”的吉野。
`,
`
你已經把具名艦船「吉野」新增到了藍編組。

再次點擊「插入（Ins）」開啟插入對話框，你會發現「吉野」已經不再出現在具名艦船欄中。不過它仍然會出現在艦船型號欄中。

請透過艦船型號方式再插入兩個「匿名」的吉野。
`);
    markdownBox(tutorial7FirstShipInsertedMsg);

    tutorial7Phase = Tutorial7Phase.WaitingForThirdShipInsertion;
}
else if (tutorial7Phase === Tutorial7Phase.WaitingForThirdShipInsertion && tutorial7ShipCount >= 3) {
    let tutorial7ThirdShipInsertedMsg = getLocalized(`
The two new "anonymous" Yoshino objects should be named something like "Yoshino1" and "Yoshino2".

Select Yoshino1, click F or Follow button in the Top Tab and click on the Yoshino to set it to follow Yoshino. Then set Yoshino2 to follow Yoshino1. Set the desired speed of Yoshino (group leader) to 10 knots and heading to 90 degree (east). And click "Set to Formation Position" button in the Editor tab (if the button is not enabled, enable "Edit mode" in the Command tab). Position, speed and heading of ships would would be updated according to their formation relationships.
`,
`
新しい2隻の「匿名」吉野は、おそらく「吉野1」「吉野2」のような名前になっているはずです。

「吉野1」を選択し、Fキーまたはトップタブの「追従(F)」ボタンを押してから「吉野」をクリックし、「吉野1」が「吉野」に追従するよう設定してください。次に、「吉野2」が「吉野1」に追従するよう設定します。「吉野」（グループリーダー）の希望速度を10ノット、針路を90度（東）に設定してください。その後、エディタタブの「陣形位置に設定」ボタンをクリックします（ボタンが有効でない場合は、コマンドタブで「エディターモード」を有効にしてください）。艦艇の位置、速度、針路が編隊関係に従って更新されます。
`,
`
两个新的“匿名”吉野应该会被命名为类似“吉野1”和“吉野2”的名字。

选择“吉野1”，按F键或点击顶部标签栏中的“跟随(F)”按钮，然后点击“吉野”，使“吉野1”跟随“吉野”。接着让“吉野2”跟随“吉野1”。把“吉野”（编队领舰）的期望速度设置为10节，航向设置为90度（向东）。然后点击编辑器标签页中的“根据队形设置位置”按钮（如果按钮不可用，请在命令标签页中启用“编辑模式”）。舰船的位置、速度和航向会根据它们的编队关系更新。
`,
`
兩個新的「匿名」吉野應該會被命名為類似「吉野1」和「吉野2」的名字。

選擇「吉野1」，按F鍵或點擊頂部標籤列中的「跟隨(F)」按鈕，然後點擊「吉野」，使「吉野1」跟隨「吉野」。接著讓「吉野2」跟隨「吉野1」。把「吉野」（編隊領艦）的期望速度設定為10節，航向設定為90度（向東）。然後點擊編輯器標籤頁中的「根據隊形設定位置」按鈕（如果按鈕不可用，請在指令標籤頁中啟用「編輯模式」）。艦船的位置、速度和航向會根據它們的編隊關係更新。
`);
    markdownBox(tutorial7ThirdShipInsertedMsg);

    tutorial7Phase = Tutorial7Phase.WaitingForFormationPosition;
}
