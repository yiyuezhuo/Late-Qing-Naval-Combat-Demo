if(phase === Phase.WaitForOrderOfBattleShown)
{
    let msg = getLocalized(`
OOB denotes the historical Order of Battle (though not the case for the tutorial scenario). You can set doctrine, leader, and the OOB itself (by adding/removing ships or groups).

Doctrine can be set at the ship level or any OOB level. If a doctrine field is set to the default "inherited" value, it will inherit the value from its parent OOB. This allows you to set general doctrine at the top OOB level and more specific doctrine at lower levels.

The most important doctrine settings are those related to automation. By default, firing is automated, but movement (course changes) is not. You can try turning off "inherited" and enabling movement automation for a root group. Alternatively, you can manually control both groups to play in sandbox mode.

Manual firing is a somewhat advanced topic and will be covered later.

Confirm to close and advance time until the two ships are within firing range. (This can be controlled by AI, manually, or by maintaining their initial courses).
`,
`
OOB（戦力編成）は歴史的な戦闘序列を示します（チュートリアル剧本では史実と異なる場合があります）。ドクトリン（作戦教則）、リーダー設定、およびOOB自体（艦艇やグループの追加/削除）を設定可能です。

ドクトリンは艦艇単位または任意のOOBレベルで設定できます。「継承」値が設定されている場合、親OOBから値を継承します。これにより、最上位OOBレベルで基本ドクトリンを設定し、下位レベルで詳細な設定を行うことが可能です。

最も重要なドクトリン設定は自動化関連です。デフォルトでは射撃は自動化されていますが、移動（針路変更）は手動制御です。ルートグループで「継承」を無効にし移動の自動化を有効にしてみてください。または、サンドボックスモードで両グループを手動制御することも可能です。

手動射撃はやや高度なトピックであり、後ほど説明します。

確認をクリックして閉じ、両艦が交戦距離内に接近するまで時間を進めてください（これはAI制御・手動制御・初期針路維持のいずれでも可能です）。
`,
`
OOB（战斗序列）表示历史作战编制（教程剧本中可能与史实不同）。您可设置作战条令、指挥官及OOB本身（通过添加/删除舰艇或编组）。

条令可在舰艇层级或任意OOB层级设置。若条令字段设为默认"继承"值，将从父级OOB继承值。这允许您在顶层OOB设置通用条令，并在下级层级设置具体条令。

最重要的条令设置是自动化相关选项。默认情况下射击为自动，但移动（航向变更）为手动。您可以尝试关闭"继承"并为根编组启用移动自动化，或在沙盒模式下手动控制双方编组。

手动射击是稍高级的内容，后续将专门说明。

点击确认关闭窗口，推进时间直至两舰进入交战距离（可通过AI控制、手动控制或保持初始航向实现）。
`,
`
OOB（戰鬥序列）表示歷史作戰編制（教程劇本中可能與史實不同）。您可設置作戰條令、指揮官及OOB本身（通過添加/刪除艦艇或編組）。

條令可在艦艇層級或任意OOB層級設置。若條令字段設為默認「繼承」值，將從父級OOB繼承值。這允許您在頂層OOB設置通用條令，並在下級層級設置具體條令。

最重要的條令設置是自動化相關選項。默認情況下射擊為自動，但移動（航向變更）為手動。您可以嘗試關閉「繼承」並為根編組啟用移動自動化，或在沙盒模式下手動控制雙方編組。

手動射擊是稍高級的內容，後續將專門說明。

點擊確認關閉窗口，推進時間直至兩艦進入交戰距離（可通過AI控制、手動控制或保持初始航向實現）。
`)

    msgBoxDelay(msg, 0.3);

    phase = Phase.WaitForFiringExchangeStarted;
}