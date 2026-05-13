var tutorial7ShipCount = NavalGameState.Instance.shipLogs.Count;

if (tutorial7Phase === Tutorial7Phase.WaitingForFirstShipInsertion && tutorial7ShipCount >= 1) {
    let tutorial7FirstShipInsertedMsg = getLocalized(`
You have already added a Named Ship Yoshino to Blue Ship Group.

Click Insert again to open the insert dialog, you will notice Yoshino is no longer available in the Named Ship column. However it remains available in the Ship Class columns.

Insert two more "anonymous" Yoshino by Ship Class method.
`,
`
Named Ship Yoshino を Blue Ship Group に追加しました。

もう一度 Insert をクリックして挿入ダイアログを開くと、Yoshino が Named Ship 列では選択できなくなっていることに気づくはずです。ただし、Ship Class 列では引き続き選択できます。

Ship Class 方式で、さらに2隻の「匿名」Yoshino を挿入してください。
`,
`
你已经把 Named Ship Yoshino 添加到了 Blue Ship Group。

再次点击 Insert 打开插入对话框，你会发现 Yoshino 已经不再出现在 Named Ship 列中。不过它仍然会出现在 Ship Class 列中。

请通过 Ship Class 方式再插入两个“匿名”的 Yoshino。
`,
`
你已經把 Named Ship Yoshino 新增到了 Blue Ship Group。

再次點擊 Insert 開啟插入對話框，你會發現 Yoshino 已經不再出現在 Named Ship 欄中。不過它仍然會出現在 Ship Class 欄中。

請透過 Ship Class 方式再插入兩個「匿名」的 Yoshino。
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
新しい2隻の「匿名」Yoshino は、おそらく「Yoshino1」「Yoshino2」のような名前になっているはずです。

Yoshino1 を選択し、F キーまたは Top Tab の Follow ボタンを押してから Yoshino をクリックし、Yoshino1 が Yoshino に追従するよう設定してください。次に、Yoshino2 が Yoshino1 に追従するよう設定します。Yoshino（グループリーダー）の desired speed を 10 knots、heading を 90 degree（東）に設定してください。その後、Editor タブの「Set to Formation Position」ボタンをクリックします（ボタンが有効でない場合は、Command タブで「Edit mode」を有効にしてください）。艦艇の位置、速度、針路が編隊関係に従って更新されます。
`,
`
两个新的“匿名”Yoshino 应该会被命名为类似“Yoshino1”和“Yoshino2”的名字。

选择 Yoshino1，按 F 键或点击 Top Tab 中的 Follow 按钮，然后点击 Yoshino，使 Yoshino1 跟随 Yoshino。接着让 Yoshino2 跟随 Yoshino1。把 Yoshino（编队领舰）的 desired speed 设置为 10 knots，heading 设置为 90 degree（向东）。然后点击 Editor 标签页中的“Set to Formation Position”按钮（如果按钮不可用，请在 Command 标签页中启用“Edit mode”）。舰船的位置、速度和航向会根据它们的编队关系更新。
`,
`
兩個新的「匿名」Yoshino 應該會被命名為類似「Yoshino1」和「Yoshino2」的名字。

選擇 Yoshino1，按 F 鍵或點擊 Top Tab 中的 Follow 按鈕，然後點擊 Yoshino，使 Yoshino1 跟隨 Yoshino。接著讓 Yoshino2 跟隨 Yoshino1。把 Yoshino（編隊領艦）的 desired speed 設定為 10 knots，heading 設定為 90 degree（向東）。然後點擊 Editor 標籤頁中的「Set to Formation Position」按鈕（如果按鈕不可用，請在 Command 標籤頁中啟用「Edit mode」）。艦船的位置、速度和航向會根據它們的編隊關係更新。
`);
    markdownBox(tutorial7ThirdShipInsertedMsg);

    tutorial7Phase = Tutorial7Phase.WaitingForFormationPosition;
}
