if(phase === Phase.WaitForFiringExchangeStarted)
{
    if(hasFireExchanged())
    {
        let msg = getLocalized(`
A ship starts to fire!

Select a ship from the firing group (typically, Japanese ships fire first in this scenario). Red lines will appear, showing the ship firing at its target with its primary, secondary, tertiary, or RF batteries.

Open the ship state view and go to the Battery tab. There you can find information about the firing process; the most interesting entries are the ammunition and Processing Seconds.

Batteries carry different types of ammunition, and the AI will use the optimal ammo when firing.

Processing Seconds increase as time advances until the threshold determined by the Rate of Fire is reached. At that point, a shot is resolved and Processing Seconds are reset to zero. No flying shells are modeled (although launched torpedoes are modeled).

Different mounts have different firing arcs defined in their ship class correspondence record. Generally, a ship's broadside firepower is stronger than its forward or aft firepower. However, presenting the broadside angle also makes the ship easier to hit.

Turn off "Current Only" (which clears logs as time advances, so only the "current" log is shown) in the global log panel (bottom-left corner).

Advance time until a hit is scored.
`,
`
艦艇の射撃が開始されました！

射撃を行っているグループから艦艇を選択してください（本剧本では通常、日本艦隊が先に射撃します）。赤色の線が表示され、主砲・副砲・または速射砲による目標への射撃が可視化されます。

艦艇動態状態ビューを開き「兵装」タブに移動してください。ここで射撃プロセスの情報を確認でき、特に弾薬残数と処理秒数が注目すべき項目です。

各砲台は異なる種類の弾薬を搭載しており、AIは射撃時に最適な弲種を自動選択します。

処理秒数は時間経過とともに増加し、射撃速度で決定された閾値に達すると、射撃が解決され処理秒数がゼロにリセットされます。飛翔中の砲弾はモデル化されません（ただし、発射された魚雷はモデル化されます）。

各種砲架は、対応する艦艇型号レコードで定義された固有の射界を持ちます。一般的に艦艇の舷側火力は前後方向の火力よりも強力ですが、舷側を露出させることは被弾しやすい姿勢でもあります。

グローバルログパネル（画面左下）で「現在のみ表示」（時間経過とともにログを消去し「現在」のログのみ表示する機能）を無効にしてください。

命中が記録されるまで時間を進めてください。
`,
`
舰艇开始射击！

请从射击编组中选择一艘舰艇（本剧本中通常日方舰艇先开火）。红色线条将显示该舰正在使用主炮、副炮、或速射炮向目标开火。

打开舰艇状态视图并进入"武器"标签页。此处可查看射击过程信息，最值得关注的条目是弹药余量和处理秒数。

各炮组携带不同弹种，AI会在射击时自动选择最优弹药。

处理秒数随时间推进增加，直至达到射速决定的阈值时触发射击结算，随后归零。未模拟飞行中的炮弹（但已发射的鱼雷会持续模拟）。

不同炮座在对应舰艇型号记录中定义了特定射界。通常舰艇舷侧火力优于首尾方向火力，但暴露舷侧同时也更易被命中。

在全局日志面板（左下角）关闭"仅显示当前"（该功能会在时间推进时清除日志，只显示"当前"日志）。

推进时间直至出现命中记录。
`,
`
艦艇開始射擊！

請從射擊編組中選擇一艘艦艇（本劇本中通常日方艦艇先開火）。紅色線條將顯示該艦正在使用主炮、副炮、或速射炮向目標開火。

打開艦艇狀態檢視並進入「武器」標籤頁。此處可查看射擊過程信息，最值得關注的條目是彈藥餘量和處理秒數。

各炮組攜帶不同彈種，AI會在射擊時自動選擇最優彈藥。

處理秒數隨時間推進增加，直至達到射速決定的閾值時觸發射擊判定，隨後歸零。未模擬飛行中的炮彈（但已發射的魚雷會持續模擬）。

不同炮座在對應艦艇型號記錄中定義了特定射界。通常艦艇舷側火力優於首尾方向火力，但暴露舷側同時也更易被命中。

在全局日志面板（左下角）關閉「僅顯示當前」（該功能會在時間推進時清除日志，只顯示「當前」日志）。

推進時間直至出現命中記錄。
`);

        msgBoxDelay(msg, 0.3);

        phase = Phase.WaitForAHitScored;
    }
}
else if(phase === Phase.WaitForAHitScored)
{
    if(isHitScored())
    {
        let msg = getLocalized(`
A hit is scored!

A log entry will appear in the global log panel. You can also check the log at the individual ship level by opening the ship state view for the damaged ship and clicking the "Detail" button in the Basic tab.

The linear part of this tutorial is now complete. Notifications for concept like Damage Effect, Sunk, and Victory will be provided when they occur for the first time. Feel free to control the two groups and continue combat until only one remains on the battlefield.
`,
`
命中が記録されました！

グローバルログパネルにログエントリが表示されます。また、被弾艦の艦艇状態ビューを開き「基本」タブの「詳細」ボタンをクリックすると、個別艦艇レベルでの詳細ログを確認できます。

これでチュートリアルの線形部分は完了です。損傷効果・撃沈・勝利条件などの概念は、実際に初めて発生した際に通知されます。両グループを自由に操作し、戦場に一隻だけが残るまで戦闘を続けてください。
`,
`
命中已达成！

全局日志面板将出现日志条目。您也可通过打开受损舰艇的舰艇状态视图，点击"基本"标签页中的"详情"按钮，查看单舰层面的详细日志。

本教程的线性部分至此结束。损伤效果、击沉与胜利条件等概念将在首次发生时提供通知。请自由控制双方编组，继续战斗直至只剩一方存于战场。
`,
`
命中已達成！

全局日志面板將出現日志條目。您也可通過打開受損艦艇的艦艇狀態檢視，點擊「基本」標籤頁中的「詳情」按鈕，查看單艦層面的詳細日志。

本教程的線性部分至此結束。損傷效果、擊沉與勝利條件等概念將在首次發生時提供通知。請自由控制雙方編組，繼續戰鬥直至只剩一方存於戰場。
`)

        msgBox(msg);
        phase = Phase.End;
    }
}

if(!damageEffectPrompted && hasAnyDamageEffect())
{
    damageEffectPrompted = true;

    let msg = getLocalized(`
A Damage Effect (Sub State) is applied to a ship!

You may determine affected ships by global log, or open the Ship State View and switch to the Damage Effect tab and check each ship.

Each hit inflicts a "homogeneous" amount of damage point loss, while more "heterogeneity" and location-specific damage—such as magazine explosions, flooding, rudder disablement, FCS misalignment, and so on—is handled by damage effects.

Some damage effects may be permanent or temporary, and they are displayed in the Damage Effect tab. Certain effects, especially shipboard fires, is damage controllable. They may tend to be worsen if no Damage Control points are allocated to them. The AI will use its Damage Control points to contain Damage Effects according to default priorities.
`,
`
艦艇に損傷効果（副状態）が適用されました！

全体ログで影響を受けた艦船を確認するか、艦船状態ビューを開いて「ダメージ状況」タブに切り替え、各艦を確認してください。

各命中は「均一的」な損傷点減少をもたらしますが、弾薬庫爆発・浸水・舵故障・射撃指揮装置誤差など、より「不均一」で部位特異的な損傷は損傷効果によって処理されます。

一部の損傷効果は永続的または一時的であり、「損傷効果」タブに表示されます。特に艦上火災などの効果は損傷制御可能です。損傷制御ポイントが割り当てられない場合、悪化する傾向があります。AIは既定の優先順位に従って損傷制御ポイントを使用し損傷効果を抑制します。
`,
`
舰艇已施加损伤效果（子状态）！

您可通过全局日志判定受影响舰艇，或打开舰艇状态视图切换到"损伤效果"标签页，使用键盘上下键查看各舰状态。

每次命中会造成"均匀"的损伤点损失，而更"不均匀"且部位特定的损伤（如弹药库爆炸、进水、舵机失效、火控系统错位等）则由损伤效果处理。

部分损伤效果可能是永久性或临时性的，并显示在"损伤效果"标签页中。某些效果（特别是舰上火灾）是可损害管制的。若未分配损害管制点，这些效果可能持续恶化。AI将根据默认优先级使用其损伤控制点来抑制损伤效果。
`,
`
艦艇已施加損傷效果（子狀態）！

您可通過全局日志判定受影響艦艇，或打開艦艇狀態ビュー切換到「損傷效果」標籤頁，使用鍵盤上下鍵查看各艦狀態。

每次命中會造成「均勻」的損傷點損失，而更「不均勻」且部位特定的損傷（如彈藥庫爆炸、進水、舵機失效、火控系統錯位等）則由損傷效果處理。

部分損傷效果可能是永久性或臨時性的，並顯示在「損傷效果」標籤頁中。某些效果（特別是艦上火災）是可損傷控制的。若未分配損傷控制點，這些效果可能持續惡化。AI將根據默認優先級使用其損傷控制點來抑制損傷效果。
`);

    msgBox(msg);

}

if(!sunkPrompted && hasAnySunk())
{
    sunkPrompted = true;

    let msg = getLocalized(`
A ship has been sunk!

As you may have noticed, in the First Sino-Japanese War, or Seekrieg 5, sinking is not guaranteed when the damage point percentage reaches 100%. A ship may sink before reaching 100%, or it might not sink even after exceeding 1000%. The damage point primarily establishes a probability distribution—ships tend to become combat-ineffective at 100%, but total destruction isn't certain.

Mechanically, damage points can trigger critical "General" Damage Effects when certain damage point tier (percentages thresholds) are crossed. A ship will sink if too many tiers are exceeded within a short period. However, it is possible—with a non-negligible probability—to reach 100% without any critical Damage Effects occurring. Beyond this point, additional damage point does not increase the chance of sinking, though certain Damage Effects can still cause the ship to sink. You can think of this situation as a ship having nothing left to explode—it’s merely a flooded hull adrift at sea. Adding more holes to the above-water section does not contribute to sinking.
`,
`
艦艇が撃沈されました！

お気付きかもしれませんが、『日清戦争』（Seekrieg 5システム）では、損傷点が100%に達しても撃沈は保証されません。艦艇は100%に達する前に沈没することもあれば、1000%を超えても沈没しない場合もあります。損傷点は主に確率分布を形成するもので、艦艇は100%で戦闘失能になる傾向がありますが、完全な破壊は確定しないのです。

メカニズム的には、損傷点が特定のティア（百分比しきい値）を超えると、致命的な「一般」損傷効果を引き起こす可能性があります。短期間に多数のティアを超えると艦艇は沈没します。しかし無視できない確率で、重大な損傷効果が発生せずに100%に達する場合もあります。この段階を超えると、追加の損傷点は沈没確率を上昇させません（ただし特定の損傷効果が沈没を引き起こす可能性は残ります）。これは「爆発するものがない状態」——海上を漂流する浸水した船体——と考えることができます。水上部分にさらに穴を開けても沈没には寄与しません。
`,
`
舰艇已被击沉！

您可能已注意到，在《甲午战争》（Seekrieg 5系统）中，损伤点达到100%并不保证击沉。舰艇可能在达到100%前沉没，也可能超过1000%仍不沉没。损伤点主要构成概率分布——舰艇倾向于在100%时战斗失能，但彻底毁灭并非必然。

机制上，损伤点超过特定层级（百分比阈值）时可能触发致命"通用"损伤效果。若短期内过多层级被超越，舰艇将会沉没。但存在不可忽视的概率，舰艇可能未触发任何重大损伤效果就达到100%损伤点。超过此点后，额外损伤点不会增加沉没概率（尽管某些损伤效果仍可能导致沉没）。您可以将其理解为"已无物可爆"的状态——仅是漂浮海面的进水船体。在水上部分打出更多的窟窿并不会促进沉没。
`,
`
艦艇已被擊沉！

您可能已注意到，在《甲午戰爭》（Seekrieg 5系統）中，損傷點達到100%並不保證擊沉。艦艇可能在達到100%前沉沒，也可能超過1000%仍不沉沒。損傷點主要構成概率分佈——艦艇傾向於在100%時戰鬥失能，但徹底毀滅並非必然。

機制上，損傷點超過特定層級（百分比閾值）時可能觸發致命「通用」損傷效果。若短期內過多層級被超越，艦艇將會沉沒。但存在不可忽視的概率，艦艇可能未觸發任何重大損傷效果就達到100%。超過此點後，額外損傷點不會增加沉沒概率（儘管某些損傷效果仍可能導致沉沒）。您可以將其理解為「已無物可爆」的狀態——僅是漂浮海面的進水船體。在水上部分打出更多的窟窿並不會促進沉沒。
`)

    msgBox(msg);
}

if(!groupDestroyedPrompted && hasGroupDestroyed())
{
    groupDestroyedPrompted = true;
    
    let msg = getLocalized(`
A group has been destroyed!

You can open the "Victory Status" dialog from the "Command" tab in the top bar. It will report the top group's losses and damage situation. Victory points are calculated based on a ship's damage state, firepower, DP, and armor. These values can be found in the Ship Class View (static values) and the Ship State View (dynamic values). These values are also used by the AI. Sinking a ship applies a ×2 modifier.
`,
`
部隊が壊滅しました！

トップバーの「コマンド」タブから「勝利状況」ダイアログを開くことができます。ここでは、最上位グループの損失や損害状況が報告されます。勝利ポイントは、艦船のダメージ状態、火力、DP、装甲に基づいて算出されます。これらの数値は、艦級ビュー（固定値）および艦船状態ビュー（動的数値）で確認でき、AIもこれらの数値を判断基準として使用します。なお、艦船を撃沈した場合は、ポイントに2倍の倍率が適用されます。
`,
`
编队已被摧毁！

您可以从顶栏的“命令”选项卡中打开“胜利状态”对话框。它将报告最高层级组的损失和受损情况。胜利点数是根据舰船的受损状态、火力、DP和装甲计算得出的。这些数值可以在舰船型号视图（静态值）和舰船状态视图（动态值）中查看。AI 同样会参考这些数值。击沉舰船将应用 2 倍的加成系数。
`,
`
編隊已被摧毀！

您可以從頂欄的「命令」分頁中開啟「勝利狀態」對話框。它將報告最高層級組的損失和受損情況。勝利點數是根據艦船的受損狀態、火力、DP和裝甲計算得出的。這些數值可以在艦船型號檢視（靜態值）和艦船狀態檢視（動態值）中查看。AI 同樣會參考這些數值。擊沉艦船將應用 2 倍的加成係數。
`)

    msgBox(msg);

}