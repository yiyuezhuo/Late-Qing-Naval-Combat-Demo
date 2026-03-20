let tutorial6IntroMsg = getLocalized(`
Welcome to Tutorial 6 - Hierarchical Automation & AI. In the previous tutorial, you already tried disabling Automatic Fire for a single ship in Doctrine. Besides setting it at the single-ship level, you can also set it at non-ship formation levels in the OOB. If child nodes use "Inherit", they will inherit the value defined on that formation.

At the moment, both sides have Automatic Fire set to Manual at their top-level formations, so every unit inheriting that doctrine will not fire automatically. Use "Advance 1 Min (1)" a few times until the next prompt appears.
`,
`
チュートリアル6 - 階層的自動化とAIへようこそ。前のチュートリアルでは、ドクトリンで単艦の自動射撃を無効にする方法を試しました。これは単艦レベルだけでなく、OOB 内の艦船ではない編制レベルでも設定できます。その場合、下位ノードが「継承」になっていれば、その編制で定義した値を受け継ぎます。

現在、双方とも最上位編制で自動射撃が手動に設定されているため、そのドクトリンを継承している全ユニットは自動で発砲しません。新しいヒントが表示されるまで時間を進めます。
`,
`
欢迎来到教程 6 - 分层自动化与AI。前面你已经尝试了如何在条令中关闭单个舰船的射击自动化。除了在单舰级别设置以外，也可以在 OOB 的非舰船的编制级别上设置，此时它们下属节点如果采用了“继承”模式，则会继承编制上定义的值。

当前双方已经在最顶层编制上自动射击设置为手动，所以所有继承了该条令设置的所有单位均不会自动射击。推进时间到新提示弹出。
`,
`
歡迎來到教程 6 - 分層自動化與AI。前面你已經嘗試了如何在條令中關閉單個艦船的射擊自動化。除了在單艦級別設定以外，也可以在 OOB 的非艦船的編制級別上設定，此時它們下屬節點如果採用「繼承」模式，則會繼承編制上定義的值。

當前雙方已經在最頂層編制上把自動開火設為手動，所以所有繼承了該條令設定的所有單位均不會自動開火。推進時間到新提示彈出。
`);

msgBox(tutorial6IntroMsg);

var Tutorial6Phase = {
    WaitingForNoFireConfirmationDialog: 1,
    WaitingForChinYuanFireDialog: 2,
    WaitingForBeiyangFleetFireDialog: 3,
    WaitingForTorpedoBoatManeuverDialog: 4,
    WaitingForChinYuanManeuverDialog: 5,
    WaitingForCombinedFleetManeuverDialog: 6,
    End: 7
};

var tutorial6Phase = Tutorial6Phase.WaitingForNoFireConfirmationDialog;
var tutorial6ElapsedMinutes = 0;
var tutorial6LastPromptMinute = 0;
