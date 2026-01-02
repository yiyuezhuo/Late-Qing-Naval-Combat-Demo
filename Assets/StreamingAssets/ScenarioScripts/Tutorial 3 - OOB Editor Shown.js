if(phase === Phase.WaitForOrderOfBattleShown)
{
    let msg = getLocalized(`
OOB denotes the historical or hypothesized Order of Battle. You can set the group leader, doctrine for ships and groups, or manipulate the tree itself.

Doctrine can be set at any OOB level. If a doctrine field is set to the default “inherit” value, it will inherit the value from its parent group (the top group will use the default value). This allows you to set general doctrine at the top OOB level and more specific doctrine at lower levels.

The most important doctrine settings are those related to automation. By default, firing is automated, but movement (course changes) is not. You can try turning off "inherit" and enabling movement automation for a root group. Alternatively, you can manually control both groups to play in a hotseat sandbox style.

Manual firing requires disabling Automatic Fire here; some more detailed options are also provided. Additionally, firing range can be set here. To effectively use long‑range torpedoes, the torpedo’s maximum firing range is important.

Click confirm to close and advance time until the two ships are within firing range. (They can be controlled by AI, manually, or by maintaining their initial courses.)
`,
`
OOBは、史実または仮説に基づいた戦闘序列を表します。ここでは、グループリーダーの設定、艦船やグループのドクトリンの設定、またはツリー構造自体の操作が可能です。

ドクトリンはOOBのどの階層でも設定できます。ドクトリンの項目がデフォルトの「継承」に設定されている場合、その値は親グループから引き継がれます（最上位グループの場合はデフォルト値が適用されます）。これにより、最上位のOOB階層で全般的なドクトリンを定め、下位階層でより詳細なドクトリンを個別に設定することが可能です。

最も重要なドクトリン設定は、自動化に関するものです。デフォルトでは、砲撃は自動化されていますが、移動（コース変更）は自動化されていません。「継承」をオフにして、ルートグループの移動自動化を有効にしてみてください。あるいは、両方のグループを手動で操作して hotseat 形式のサンドボックスとしてプレイすることもできます。

手動での砲撃を行うには、ここで「自動射撃」を無効にする必要があります。また、より詳細なオプションや、射程距離の設定もここで行えます。長距離魚雷を効果的に運用するには、魚雷の最大射程の設定が重要です。

「確定」をクリックして画面を閉じ、2隻の艦船が射程内に入るまで時間を進めてください。（AIによる操作、手動操作、または初期コースの維持、いずれの方法でも進行可能です。）
`,
`
OOB 代表历史或假想的战斗序列。您可以设置编组指挥、舰船和编组的条令，或直接操作树状结构。

条令可以在 OOB 的任何层级进行设置。如果某个条令字段设为默认的“继承”值，它将继承其父组的设置（最顶层组将使用系统默认值）。这允许您在 OOB 的顶层设置通用条令，而在较低层级设置更具体的条令。

最重要的条令设置是与自动化相关的选项。在默认情况下，开火是自动的，但移动（航线更改）则不是。您可以尝试关闭“继承”并为根组开启移动自动化。此外，您也可以手动控制双方编组，以热座沙盒风格进行游戏。

若要手动开火，需要在此处禁用“自动射击”；这里还提供了一些更详细的选项。此外，开火射程也可以在这里设置。为了有效地使用长程鱼雷，鱼雷的最大射程设置至关重要。

点击“确认”关闭窗口，并推进时间，直到两艘船进入开火射程。（它们可以由 AI 控制、手动控制，或保持初始航线行驶。）
`,
`
OOB 代表歷史或設想的戰鬥序列。您可以設定組長、艦船和編組的條令，或直接操作樹狀結構。

條令可以在 OOB 的任何層級進行設定。如果某個條令欄位設為預設的「繼承」值，它將繼承其父組的設定（最頂層組將使用系統預設值）。這允許您在 OOB 的頂層設定通用條令，而在較低層級設定更具體的條令。

最重要的條令設定是與自動化相關的選項。在預設情況下，開火是自動的，但移動（航線更改）則不是。您可以嘗試關閉「繼承」並為根組開啟移動自動化。此外，您也可以手動控制雙方編組，以熱座沙盒風格進行遊戲。

若要手動開火，需要在開處禁用「自動射擊」；這裡還提供了一些更詳細的選項。此外，開火射程也可以在這裡設定。為了有效地使用長程魚雷，魚雷的最大射程設定至關重要。

點擊「確認」視窗並推進時間，直到兩艘船進入開火射程。（它們可以由 AI 控制、手動控制，或保持初始航線行駛。）
`)

    msgBoxDelay(msg, 0.3);

    phase = Phase.WaitForFiringExchangeStarted;
}