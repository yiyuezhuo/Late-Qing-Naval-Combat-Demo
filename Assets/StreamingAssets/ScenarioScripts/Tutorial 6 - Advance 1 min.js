tutorial6ElapsedMinutes += 1;

if (tutorial6Phase === Tutorial6Phase.WaitingForNoFireConfirmationDialog) {
    if (tutorial6ElapsedMinutes >= 4) {
        let msg = getLocalized(`
You can see that none of the units on either side have fired. 

Now click on Chin Yuan, then click the Chin Yuan Squadron hyperlink in the information panel on the right to open the OOB editor.

Then disable the inherit of the squadron's auto-fire doctrine.

This will change the Chin Yuan Squadron's auto-fire doctrine from the inherited "Manual" to the default "Automatic".

Advance time until the next prompt appears.
`,
`
ご覧の通り、双方の全ユニットはまったく発砲していません。

次に致遠をクリックし、右側の情報パネルにある致遠隊のリンクをクリックして戦闘序列エディターを開きます。

その後、致遠隊の自動射撃ドクトリンの継承をオフにしてください。

これにより、致遠隊の自動射撃ドクトリンは継承された「手動」からデフォルトの「自動」に変更されます。

新しいヒントが表示されるまで時間を進めます。
`,
`
可以看到，双方所有单位的确没有开火。

现在点击致远然后在右信息栏中点击致远队的超链接打开战斗序列编辑器。

然后将致远队的自动射击条令的继承关闭。

这将把从致远队的自动射击条令从继承的"手动"改为默认的”自动“。

推进时间到新提示弹出。
`,
`
可以看到，雙方所有單位的確沒有開火。

現在點擊致遠，然後在右側資訊面板中點擊致遠隊的超連結以打開戰鬥序列編輯器。

接著將致遠隊的自動開火條令的繼承關閉。

這會把致遠隊的自動開火條令從繼承的「手動」改為預設的「自動」。

推進時間到新提示彈出。
`);

        msgBox(msg);
        tutorial6LastPromptMinute = tutorial6ElapsedMinutes;
        tutorial6Phase = Tutorial6Phase.WaitingForChinYuanFireDialog;
    }
}
else if (tutorial6Phase === Tutorial6Phase.WaitingForChinYuanFireDialog) {
    if (tutorial6ElapsedMinutes - tutorial6LastPromptMinute >= 5) {
        let msg = getLocalized(`
You can now see that Chin Yuan Squadron has started firing automatically because its Automatic Fire changed from Manual to Automatic, while Ting Yuen Squadron is still not firing because it remains Manual.

Click the Order of Battle button (under the Status tab in the Top Tab) to open the Order of Battle editor.

Then click the Beiyang Fleet entry.

Next, change "Auto-Fire" from "Manual" to "Automatic."

This will set the doctrine at the Beiyang Fleet level.

Advance time until the next prompt appears.
`,
`
ここで、致遠隊は自動射撃が手動から自動に変わったため自動で発砲を始め、一方で定遠隊はまだ手動のままなので発砲していないことが分かります。

トップタブの「状態」タブ内にある「戦闘序列」ボタンをクリックして、戦闘序列エディターを開きます。

次に、北洋艦隊の項目をクリックします。

その後、「自動射撃」を「手動」から「自動」に変更します。

これにより、このドクトリンが北洋艦隊レベルで設定されます。

新しいヒントが表示されるまで時間を進めます。
`,
`
可以看到由于致远队的自动射击从手动变为自动开始自动射击，同时定远队由于仍然是手动而未开火。

点击战斗序列按钮（在顶部标签栏的"状态"标签下）打开战斗序列编辑器。

然后点击北洋水师条目。

再把“自动射击”从“手动”改为自动。

这会在北洋水师的级别设置该条令。

推进时间到新提示弹出。
`,
`
可以看到由於致遠隊的自動開火從手動變為自動開始自動開火，同時定遠隊由於仍然是手動而未開火。

點擊戰鬥序列按鈕（位於頂部標籤欄的「狀態」標籤下）以打開戰鬥序列編輯器。

然後點擊北洋水師條目。

接著將「自動射擊」從「手動」改為「自動」。

這會在北洋水師的層級設定該條令。

推進時間到新提示彈出。
`);

        msgBox(msg);
        tutorial6LastPromptMinute = tutorial6ElapsedMinutes;
        tutorial6Phase = Tutorial6Phase.WaitingForBeiyangFleetFireDialog;
    }
}
else if (tutorial6Phase === Tutorial6Phase.WaitingForBeiyangFleetFireDialog) {
    if (tutorial6ElapsedMinutes - tutorial6LastPromptMinute >= 5) {
        let msg = getLocalized(`
You can see that all Beiyang Fleet units have started firing automatically. Now, at the Torpedo Boat Squadron level, turn off Inherit for Automatic Maneuver and switch it to Automatic. Advance time until the next prompt appears.
`,
`
ご覧の通り、北洋艦隊の全ユニットが自動で発砲し始めました。次に、魚雷艇隊レベルで自動機動の「継承」を解除し、自動に切り替えてください。新しいヒントが表示されるまで時間を進めます。
`,
`
可以看到所有北洋水师的单位开始自动射击了。现在在鱼雷艇队级别把自动机动的继承关闭并切为自动。推进到新提示弹出。
`,
`
可以看到所有北洋水師的單位開始自動開火了。現在在魚雷艇隊級別把自動機動的繼承關閉並切為自動。推進到新提示彈出。
`);

        msgBox(msg);
        tutorial6LastPromptMinute = tutorial6ElapsedMinutes;
        tutorial6Phase = Tutorial6Phase.WaitingForTorpedoBoatManeuverDialog;
    }
}
else if (tutorial6Phase === Tutorial6Phase.WaitingForTorpedoBoatManeuverDialog) {
    if (tutorial6ElapsedMinutes - tutorial6LastPromptMinute >= 6) {
        let msg = getLocalized(`
You can see that the torpedo boats have started moving toward the enemy, while the other units are not moving. The default behavior for torpedo boat or destroyer groups is to close in and attack the enemy, while other groups tend to keep range and fire instead.

Now try enabling Automatic Maneuver for Chin Yuan Squadron. You could do it the same way as before, but here is another method: 

Click the A/I button in the Commands tab on the top tab bar, then switch the Beiyang Fleet from Manual to Automated in the pop-up dialog.

Then open the OOB Editor and inspect the Beiyang Fleet entry. You will find that its Automatic Maneuver doctrine has changed to Automatic. Because this A/I dialog box is merely a shortcut for setting the automated maneuver doctrine at the top level of OOB.

Since you only want Chin Yuan Squadron to maneuver, go to the Ting Yuen Squadron formation level and turn off Inherit there. Because the default value of Automatic Maneuver is Manual, that will disable its automatic maneuver. Advance time until the next prompt appears.
`,
`
魚雷艇が敵に向かって動き始め、他のユニットは動いていないことが分かります。魚雷艇隊や駆逐艦隊の既定行動は敵へ接近して攻撃することで、他の艦種は距離を保って射撃する傾向があります。

次は致遠隊を自動機動させてみましょう。先ほどと同じ方法でも構いませんが、ここでは別のやり方を使います。

上部タブバーの『コマンド』タブにあるA/Iボタンをクリックし、表示されたダイアログで北洋水師を『手動』から『自動』に切り替えてください。

その後 OOB エディターで北洋艦隊の項目を開くと、自動機動ドクトリンが自動に変わっているはずです。この A/I ダイアログは、戦闘序列の最上位層における自動機動ドクトリンを設定するためのショートカットに過ぎません。

ただし機動させたいのは致遠隊だけなので、定遠隊の編制レベルで「継承」を解除してください。自動機動の既定値は手動なので、それで自動機動は無効になります。新しいヒントが表示されるまで時間を進めます。
`,
`
可以看到鱼雷艇开始向敌方移动，同时其他单位没有移动。鱼雷艇队或驱逐舰队的默认行为就是接近敌人攻击，而其他的则是保持距离射击等。现在测试让致远队自动机动。像刚才那样做当然也行，不过这里换另一种方式：

在顶部标签栏的命令标签中点击 A/I 按钮，然后在弹出的对话框中把北洋水师从手动切到自动。

然后打开 OOB 编辑器查看北洋水师的条目。你会发现北洋水师的自动机动条令已经变成了自动。因为这个 A/I 对话框当前其实只是设置 OOB 顶部编制的自动机动条令的快捷方式而已。

由于只需要让致远队机动，所以在定远队编制级别上关闭继承。由于自动机动的默认值是手动，所以已经关闭了自动机动。推进时间到新提示弹出。
`,
`
可以看到魚雷艇開始向敵方移動，同時其他單位沒有移動。魚雷艇隊或驅逐艦隊的預設行為就是接近敵人攻擊，而其他的則是保持距離射擊等。現在測試讓致遠隊自動機動。像剛才那樣做當然也行，不過這裡換另一種方式：

點擊頂部標籤欄中『命令』標籤頁下的A/I按鈕，然後在彈出的對話框中將北洋水師從『手動』切換為『自動』。

然後打開 OOB 編輯器查看北洋水師的條目。你會發現北洋水師的自動機動條令已經變成了自動。這個 A/I 對話框其實僅僅是用來設定戰鬥序列頂層編制中自動機動條令的快捷方式罷了。

由於只需要讓致遠隊機動，所以在定遠隊編制級別上關閉繼承。由於自動機動的預設值是手動，所以已經關閉了自動機動。推進時間到新提示彈出。
`);

        msgBox(msg);
        tutorial6LastPromptMinute = tutorial6ElapsedMinutes;
        tutorial6Phase = Tutorial6Phase.WaitingForChinYuanManeuverDialog;
    }
}
else if (tutorial6Phase === Tutorial6Phase.WaitingForChinYuanManeuverDialog) {
    if (tutorial6ElapsedMinutes - tutorial6LastPromptMinute >= 6) {
        let msg = getLocalized(`
You can see the Chin Yuan Squadron has also begun maneuvering, though their behavior will differ. Now, open the A/I dialog again and set the Combined Fleet to Automated. In standard scenarios, the A/I dialog pops up automatically upon loading; if you intend to play as one side, simply set the opposing side to Automated. Advance time until the next prompt appears.
`,
`
致遠隊も機動を開始したのが確認できますが、その挙動は異なります。ここで再度A/Iダイアログを開き、連合艦隊を『自動』に設定してください。通常のシナリオでは起動時にA/Iダイアログが自動表示されます。いずれかの陣営を操作したい場合は、もう一方を『自動』に設定するだけで済みます。次のプロンプトが表示されるまで時間を進めてください。
`,
`
可以看到致远队也开始机动，当然它们的行为方式会有所不同。现在再次打开 A/I 对话框，然后把联合舰队设为自动。普通剧本打开时会自动弹出 A/I 对话框，此时你如果是想扮演其中一方，就把另一方设置为自动即可。推进时间到新提示弹出。
`,
`
可以看到致遠隊也開始機動了，當然它們的行為方式會有所不同。現在再次打開A/I對話框，然後將聯合艦隊設為『自動』。普通劇本載入時會自動彈出A/I對話框，此時若您想扮演其中一方，只需將另一方設置為自動即可。推進時間直到下一個提示出現。
`);

        msgBox(msg);
        tutorial6LastPromptMinute = tutorial6ElapsedMinutes;
        tutorial6Phase = Tutorial6Phase.WaitingForCombinedFleetManeuverDialog;
    }
}
else if (tutorial6Phase === Tutorial6Phase.WaitingForCombinedFleetManeuverDialog) {
    if (tutorial6ElapsedMinutes - tutorial6LastPromptMinute >= 6) {
        let msg = getLocalized(`
You can see that the Three Views ships have started maneuvering too. They are not firing, however, because unlike in normal scenarios, their Automatic Fire was disabled at the start here to avoid uncontrolled results. You can turn it back on and let the AI fully control the Combined Fleet.

Then manually control Ting Yuen Squadron while the other two friendly squadrons remain under AI control, and fight a bit. You can also switch them back to Manual and control them yourself. You can test some other doctrines too; see the manual for their effects. Once you feel you have tested enough, exit this tutorial and move on to the next one.
`,
`
三景艦艇も機動を始めたことが分かります。ただし発砲はしていません。通常のシナリオと違って、ここでは結果が制御不能になるのを避けるため、開始時に自動射撃を無効にしてあるからです。必要ならそれを有効に戻して、連合艦隊を AI に全面的に任せることもできます。

そのうえで自分は定遠隊を手動で操作し、他の味方二隊は AI に任せたまま交戦してみてください。再び手動に戻して自分で操作しても構いません。ほかのドクトリンも試せますし、効果はマニュアルで確認できます。十分に試したと思ったら、このチュートリアルを終了して次へ進んでください。
`,
`
可以看到三景舰艇也开始机动，当然它们没有射击，因为为了避免不受控的结果，和普通剧本不同，它们的自动射击在开始时被关闭了。你可以把它们打开，让AI完全控制联合舰队。

然后自己手操定远队，让另外两个友方队继续被AI控制，如此交战一番。也可以把它们切回手动自己控制。你也可以测试一些别的条令，可以查看手册了解它们的效果。感觉测试足够后就可以退出本教程进入下一个教程。
`,
`
可以看到三景艦艇也開始機動，當然它們沒有射擊，因為為了避免不受控的結果，和普通劇本不同，它們的自動射擊在開始時被關閉了。你可以把它們打開，讓AI完全控制聯合艦隊。

然後自己手操定遠隊，讓另外兩個友方隊繼續被AI控制，如此交戰一番。也可以把它們切回手動自己控制。你也可以測試一些別的條令，可以查看手冊了解它們的效果。感覺測試足夠後就可以退出本教程進入下一個教程。
`);

        msgBox(msg);
        tutorial6Phase = Tutorial6Phase.End;
    }
}
