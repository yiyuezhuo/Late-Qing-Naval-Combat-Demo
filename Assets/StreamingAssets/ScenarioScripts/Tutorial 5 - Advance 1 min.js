tutorial5ElapsedMinutes += 1;

if (tutorial5Phase === Tutorial5Phase.WaitingForAutomaticFiringDialog) {
    if (tutorial5ElapsedMinutes >= 3) {
        let msg = getLocalized(`
Automatic firing has begun. The Matsushima may engage two ships or one ship.

Select the Matsushima, press A (or click the corresponding button in the Information Panel on the right), and then select one of the ships (if Matsushima is firing at two ships, select one of them. If Matsushima is firing at one ship, select another ship).

An "Attacking: [Ship Name]" indicator will appear below the button in the information panel.

This indicates that the ship has applied a constraint to the Optimizer. The ship will now refrain from attacking any target other than the one specified. Note that this is not a "Force Attack" instruction. Instead, if the Optimizer determines that using a specific battery group against the designated target would result in too high over-concentration penalty, it may just not use this battery group at all.

This constraint will only take effect during the next weapon reassignment. Advance time until the targets are reassigned and the dialog popup again.
`,
`
自動射撃が始まりました。松島は2隻を同時に攻撃することも、1隻だけを攻撃することもあります。

松島を選択し、Aキーを押すか（または右側の情報パネルにある対応するボタンをクリックし）、次に敵艦のうち1隻を選択してください（松島が2隻を攻撃している場合はそのうちの1隻を、1隻だけを攻撃している場合は別の艦を選択してください）。

すると情報パネルのボタンの下に「攻撃中: [艦名]」という表示が現れます。

これは、その艦がオプティマイザに対して制約を与えたことを意味します。以後、その艦は指定された目標以外を攻撃しないようになります。ただし、これは「強制攻撃」命令ではありません。指定目標に特定の砲列を使うと過度集中ペナルティが高すぎるとオプティマイザが判断した場合、その砲列をまったく使わないこともあります。

この制約が有効になるのは次回の兵器再割り当て時です。目標が再割り当てされ、再びダイアログが表示されるまで時間を進めてください。
`,
`
自动射击已经开始。松岛可能会同时攻击两艘舰船，也可能只攻击一艘。

选择松岛，按下 A 键（或点击右侧信息面板中的对应按钮），然后选择其中一艘舰船作为目标（如果松岛正在向两艘舰船射击，就从这两艘里选一艘；如果松岛只在向一艘舰船射击，就选择另一艘舰船）。

信息面板中的按钮下方会出现“攻击中: [舰名]”的提示。

这表示该舰已经向优化器施加了一个约束。之后，该舰将避免攻击指定目标以外的其他目标。注意，这并不是“强制攻击”命令。相反，如果优化器判断某个炮组对指定目标开火会造成过高的过度集火惩罚，那么它也可能干脆不使用这个炮组。

这个约束只会在下一次武器重新分配时生效。请继续推进时间，直到目标重新分配并再次弹出对话框。
`,
`
自動射擊已經開始。松島可能會同時攻擊兩艘艦船，也可能只攻擊一艘。

選擇松島，按下 A 鍵（或點擊右側資訊面板中的對應按鈕），然後選擇其中一艘艦船作為目標（如果松島正在向兩艘艦船射擊，就從這兩艘裡選一艘；如果松島只在向一艘艦船射擊，就選擇另一艘艦船）。

資訊面板中的按鈕下方會出現「攻擊中: [艦名]」的提示。

這表示該艦已經向最佳化器施加了一個約束。之後，該艦將避免攻擊指定目標以外的其他目標。請注意，這並不是「強制攻擊」命令。相反，如果最佳化器判定某個炮組對指定目標開火會造成過高的過度集中懲罰，那麼它也可能乾脆不使用這個炮組。

這個約束只會在下一次武器重新分配時生效。請繼續推進時間，直到目標重新分配並再次彈出對話框。
`);

        msgBox(msg);
        tutorial5LastPromptMinute = tutorial5ElapsedMinutes;
        tutorial5Phase = Tutorial5Phase.WaitingForManualBatteryDialog;
    }
}
else if (tutorial5Phase === Tutorial5Phase.WaitingForManualBatteryDialog) {
    if (tutorial5ElapsedMinutes - tutorial5LastPromptMinute >= 2) {
        let msg = getLocalized(`
Now Matsushima should change its attacking behavior. Click A (or the button) and then click on an empty space on the map to cancel the attack constraint.

Then open the Ship State View of Matsushima and switch to the Battery tab. Scroll to the Secondary Battery section. There are 12 mounts, but only the 6 starboard mounts are firing.

Use three of the gun mount and one FCS position to attack another ship—do this by clicking the set button then clicking another ship for each gun or FCS position.

Go through the 3 gun mounts and the 1 FCS position one by one: for each, click the Set button, then click another ship to set it as the target.

Unlike attack constraint, this method will switch the target immediately.

Then switch to the Doctrine tab, disable Inherit of Automatic Fire, and set it to Manual so the algorithm will not reset the target anymore.

Advance time until the dialog pops up again.
`,
`
ここで松島の攻撃行動が変わるはずです。Aキー（または対応するボタン）をクリックしてから、海図上の何もない場所をクリックし、攻撃制約を解除してください。

次に松島の艦艇状態ビューを開き、砲組タブに切り替えます。副砲の項目までスクロールしてください。砲架は12基ありますが、発砲しているのは右舷側の6基だけです。

3つの砲座と1つのFCS位置を使って別の艦船を攻撃します。次の方法で行ってください：各砲座またはFCS位置について、設定ボタンをクリックし、その後別の艦船をクリックします。

攻撃制約とは異なり、この方法では目標が即座に切り替わります。

その後 条令 タブに切り替え、自動射撃 の 継承 を無効にして、設定を 手動 にしてください。これでアルゴリズムが目標を再設定しなくなります。

再びダイアログが表示されるまで時間を進めてください。
`,
`
现在松岛应该会改变其攻击行为。点击 A（或对应按钮），然后点击地图上的空白处，以取消攻击约束。

然后打开松岛的舰船状态视图并切换到炮组标签。滚动到副炮区域。这里共有 12 个炮位，但只有右舷的 6 个炮位正在开火。

用其中三个炮位与一个火控位攻击另一个舰船——通过以下方式做到：对于每个炮位与火控位，点击设置按钮，然后点击另一个舰船。

与攻击约束不同，这种方法会立即切换目标。

接着切换到条令标签，关闭自动射击的继承，并将其设为手动，这样算法就不会再重置目标了。

请继续推进时间，直到对话框再次弹出。
`,
`
現在松島應該會改變其攻擊行為。點擊 A（或對應按鈕），然後點擊地圖上的空白處，以取消攻擊約束。

然後打開松島的艦船狀態檢視並切換到炮組分頁。捲動到副炮區域。這裡共有 12 個炮位，但只有右舷的 6 個炮位正在開火。

使用其中三個炮位與一個FCS位攻擊另一艘艦船——方法如下：對於每個炮位或FCS位，點擊設定按鈕，然後點擊另一艘艦船。

與攻擊約束不同，這種方法會立即切換目標。

接著切換到條令分頁，關閉自動射擊的繼承，並將其設為手動，這樣演算法就不會再重設目標了。

請繼續推進時間，直到對話框再次彈出。
`);

        msgBox(msg);
        tutorial5LastPromptMinute = tutorial5ElapsedMinutes;
        tutorial5Phase = Tutorial5Phase.WaitingForConclusionDialog;
    }
}
else if (tutorial5Phase === Tutorial5Phase.WaitingForConclusionDialog) {
    if (tutorial5ElapsedMinutes - tutorial5LastPromptMinute >= 3) {
        let msg = getLocalized(`
As you can see, the algorithm will not change the target anymore since its corresponding "Automation" doctrine is disabled.

You can check the manual to see how other doctrines behave. The manual also explains the manual attack methods for torpedoes and rapid-firing batteries.

You may exit this tutorial and proceed to the next scenario.
`,
`
ご覧のとおり、対応する「自動化」条令が無効化されているため、アルゴリズムはもう目標を変更しません。

他のドクトリンの挙動についてはマニュアルを確認してください。マニュアルには魚雷や速射砲の手動攻撃方法についても説明があります。

このチュートリアルを終了し、次のシナリオへ進んでかまいません。
`,
`
如你所见，由于对应的“自动化”条令已被禁用，算法将不会再改变目标。

你可以查看手册，了解其他条令的行为。手册中也说明了鱼雷和速射炮的手动攻击方法。

你现在可以退出本教程并进入下一个剧本。
`,
`
如你所見，由於對應的「自動化」條令已被停用，演算法將不會再改變目標。

你可以查看手冊，了解其他條令的行為。手冊中也說明了魚雷和速射砲的手動攻擊方法。

你現在可以退出本教學並進入下一個劇本。
`);

        msgBox(msg);
        tutorial5Phase = Tutorial5Phase.End;
    }
}
