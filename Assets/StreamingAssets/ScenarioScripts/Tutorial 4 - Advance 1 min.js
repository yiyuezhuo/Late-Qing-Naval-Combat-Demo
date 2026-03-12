tutorial4FollowDistanceYards = 500;

tutorial4ElapsedMinutes += 1;

if (tutorial4Phase === Tutorial4Phase.WaitingForAiRelativeFormation) {
    if (tutorial4ElapsedMinutes >= 5) {
        NavalGameState.Instance.ApplyKeepCurrentRelativeFormation(tutorial4AiFlagship, true);
        tutorial4AiFlagship.desiredHeadingDeg = 150;
        tutorial4AiFormationMinute = tutorial4ElapsedMinutes;
        tutorial4Phase = Tutorial4Phase.WaitingForAiReverseChain;
    }
}
else if (tutorial4Phase === Tutorial4Phase.WaitingForAiReverseChain) {
    if (tutorial4ElapsedMinutes - tutorial4AiFormationMinute >= 10) {
        NavalGameState.Instance.ReverseControlChain(tutorial4AiFlagship);
        let tutorial4NewRoot = tutorial4AiFlagship.GetControlRoot();
        NavalGameState.Instance.ApplyFollowFormation(tutorial4NewRoot, tutorial4FollowDistanceYards);
        let tutorial4NewLeader = NavalGameState.Instance.shipLogs[NavalGameState.Instance.shipLogs.Count - 1];
        tutorial4NewLeader.desiredHeadingDeg = 60;
        tutorial4AiReverseMinute = tutorial4ElapsedMinutes;
        tutorial4Phase = Tutorial4Phase.WaitingForAiExplanation;
    }
}
else if (tutorial4Phase === Tutorial4Phase.WaitingForAiExplanation) {
    if (tutorial4ElapsedMinutes - tutorial4AiReverseMinute >= 5) {
        GameManager.Instance.PlotAllShipTrajectories(false);
        let msg = getLocalized(`
The recent maneuver was performed by the AI. If you wish to execute it manually as a player, follow these steps:

- After moving the fleet for 5 minutes, use the "Arrange the formation in relative bearing" button. Select "Keep Current Location" and check the "Absolute Angle" option. This will cause the formation to attempt to maintain their current positions (e.g., one ship remaining at 30 degrees relative bearing and 500 yards from another).
- When the flagship turns, the other vessels will turn in unison to maintain their absolute positional relationships.
- Upon reaching the desired position, use the "Reverse Control Chain" button to invert all control relationships (e.g., changing "A follows B" to "B follows A"). Then, use the "Arrange formation in follow chain" button's dialog to switch from absolute positioning to a follow formation. Continue moving for another 5 minutes.

Practice this maneuver; after 20 minutes, a new dialog will appear with further instructions.
`,
`
直前の機動はAIによって実行されました。これをプレイヤーとして手動で行いたい場合は、次の手順に従ってください。

- 艦隊を5分間航行させた後、「相対方位で隊形を組む」ボタンを使います。ダイアログで「現在位置を維持」を選び、「絶対角」オプションを有効にしてください。これにより、編隊は現在の位置関係を保とうとします（たとえば、ある艦が別の艦に対して相対方位30度・距離500ヤードの位置を維持します）。
- 旗艦が変針すると、他の艦艇も絶対的な位置関係を保つために一斉に変針します。
- 所望の位置に到達したら、「指揮連鎖を反転」ボタンを使ってすべての統制関係を反転させます（たとえば「AがBに追従」を「BがAに追従」に変えます）。その後、「追従列で隊形を組む」ボタンのダイアログを使って、絶対位置保持から追従隊形へ切り替えてください。さらに5分間航行を続けます。

この機動を練習してください。20分後に、次の指示を示す新しいダイアログが表示されます。
`,
`
刚才的机动由 AI 执行完成。如果你想以玩家身份手动完成它，请按以下步骤操作：

- 先让舰队航行 5 分钟，然后使用“按相对方位排列队形”按钮。在对话框中选择“保持当前位置”，并勾选“绝对角”选项。这样编队会尝试保持当前的位置关系不变，例如某艘舰仍然保持在另一艘舰相对方位 30 度、距离 500 码的位置。
- 当旗舰转向时，其他舰只会同步转向，以维持这种绝对位置关系。
- 到达期望位置后，使用“反转控制链”按钮，将所有控制关系反转，例如把“A 跟随 B”变成“B 跟随 A”。然后在“按跟随链排列队形”按钮的对话框中，将编队从绝对定位切换为跟随队形。再继续航行 5 分钟。

请练习这个机动；20 分钟后会出现新的对话框，给出进一步说明。
`,
`
剛才的機動由 AI 執行完成。如果你想以玩家身分手動完成它，請按以下步驟操作：

- 先讓艦隊航行 5 分鐘，然後使用「按相對方位排列隊形」按鈕。在對話框中選擇「保持目前位置」，並勾選「絕對角」選項。這樣編隊會嘗試保持目前的位置關係不變，例如某艘艦仍然保持在另一艘艦相對方位 30 度、距離 500 碼的位置。
- 當旗艦轉向時，其他艦隻會同步轉向，以維持這種絕對位置關係。
- 到達期望位置後，使用「反轉控制鏈」按鈕，將所有控制關係反轉，例如把「A 跟隨 B」變成「B 跟隨 A」。然後在「按跟隨鏈排列隊形」按鈕的對話框中，將編隊從絕對定位切換為跟隨隊形。再繼續航行 5 分鐘。

請練習這個機動；20 分鐘後會出現新的對話框，給出進一步說明。
`);

        msgBox(msg);
        tutorial4AiExplanationMinute = tutorial4ElapsedMinutes;
        tutorial4Phase = Tutorial4Phase.WaitingForPracticeExplanation;
    }
}
else if (tutorial4Phase === Tutorial4Phase.WaitingForPracticeExplanation) {
    if (tutorial4ElapsedMinutes - tutorial4AiExplanationMinute >= 20) {
        let msg = getLocalized(`
The "Arrange formation in follow chain" command is used to set up a Line Ahead formation, whereas "Arrange the formation in relative bearing" can be used to establish Line Abreast or Line of Bearing formations.

Please test the other options within the "Arrange the formation in relative bearing" dialog and observe the effects of toggling the "Symmetric" toggle. Once you have completed these tests, you may exit this tutorial and proceed to the next scenario.
`,
`
「追従列で隊形を組む」コマンドは縦隊を設定するために使われます。一方で、「相対方位で隊形を組む」は横隊や方位線の隊形を作るために使えます。

「相対方位で隊形を組む」ダイアログ内の他のオプションも試し、「左右対称」トグルのオン・オフでどのような違いが出るか観察してください。これらの確認が終わったら、このチュートリアルを終了して次の劇本に進んで構いません。
`,
`
“按跟随链排列队形”命令用于建立纵队；而“按相对方位排列队形”则可用于建立横队或方位线队形。

请测试“按相对方位排列队形”对话框中的其他选项，并观察切换“对称”开关后的效果。完成这些测试后，你可以退出本教程并继续下一个剧本。
`,
`
「按跟隨鏈排列隊形」命令用於建立縱隊；而「按相對方位排列隊形」則可用於建立橫隊或方位線隊形。

請測試「按相對方位排列隊形」對話框中的其他選項，並觀察切換「對稱」開關後的效果。完成這些測試後，你可以退出本教學並繼續下一個劇本。
`);

        msgBox(msg);
        tutorial4Phase = Tutorial4Phase.End;
    }
}
