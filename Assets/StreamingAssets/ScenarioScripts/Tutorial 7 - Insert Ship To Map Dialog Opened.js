if (tutorial7Phase === Tutorial7Phase.WaitingForFirstInsertDialog) {
    let tutorial7InsertDialogMsg = getLocalized(`
After selecting a position in insert mode, the Insert Dialog will be displayed.

You can insert three types of objects here:

- **Not Deployed Ship**: Deploy a not deployed ship (the ship is usually created manually in the Ship State Editor). This is a somewhat advanced topic and is not covered in this tutorial.
- **Named Ship**: This create a Ship State from the selected Named Ship which is not associated with a Ship State (so that a Named Ship can only has a Ship State "embodiment") with proper initialization, and deploys it to map.
- **Ship Class**: This creates an "anonymous" Named Ship from the selected Ship Class and create a Ship State from that new Named Ship. It is used for hypothetical scenarios involving ship classes that exceed historical quantities. Some historical information from the built-in Named Ship is lost though.

Select Named Ship Yoshino (the first item in the Named Ship column), choose Blue in the "Attach to Group" Dropdown, and click the Confirm button.
`,
`
挿入モードで位置を選択すると、挿入ダイアログが表示されます。

ここでは、3種類のオブジェクトを挿入できます。

- **未配置の艦船**：未配置の艦船を配置します。この艦艇は通常、艦船状態編集で手動作成します。やや高度な内容なので、このチュートリアルでは扱いません。
- **名前付き艦船**：まだ艦船状態と関連付けられていない名前付き艦船から艦船状態を適切に初期化して作成し、マップに配置します。これにより、1つの名前付き艦船が持てる艦船状態の「実体」は1つだけになります。
- **艦級**：選択した艦級から「匿名」の名前付き艦船を作成し、その新しい名前付き艦船から艦船状態を作成します。これは、史実の保有数を超える艦級を使った仮想シナリオに使用します。ただし、組み込みの名前付き艦船が持つ一部の史実情報は失われます。

名前付き艦船「吉野」（名前付き艦船列の最初の項目）を選択し、「グループに配属」ドロップダウンで「青」を選んで、「確認」ボタンをクリックしてください。
`,
`
在插入模式中选择位置后，插入对话框会显示出来。

你可以在这里插入三类对象：

- **未部署舰船**：部署一个尚未部署的舰船。这个舰船通常是在舰船状态编辑中手动创建的。这是稍微高级一些的主题，本教程不会讲解。
- **具名舰船**：从尚未关联舰船状态的具名舰船创建一个经过适当初始化的舰船状态，并将其部署到地图上。这样一个具名舰船只能拥有一个舰船状态“实体”。
- **舰船型号**：从选中的舰船型号创建一个“匿名”的具名舰船，并从这个新的具名舰船创建舰船状态。它用于涉及超过历史数量的舰级的假想剧本。不过，内置具名舰船的一部分历史信息会因此丢失。

选择具名舰船“吉野”（具名舰船列的第一项），在“配属给编组”下拉框中选择“蓝”，然后点击“确认”按钮。
`,
`
在插入模式中選擇位置後，插入對話框會顯示出來。

你可以在這裡插入三類物件：

- **未部署艦船**：部署一個尚未部署的艦船。這個艦船通常是在艦船狀態編輯中手動建立的。這是稍微進階一些的主題，本教學不會講解。
- **具名艦船**：從尚未關聯艦船狀態的具名艦船建立一個經過適當初始化的艦船狀態，並將其部署到地圖上。這樣一個具名艦船只能擁有一個艦船狀態「實體」。
- **艦船型號**：從選中的艦船型號建立一個「匿名」的具名艦船，並從這個新的具名艦船建立艦船狀態。它用於涉及超過歷史數量的艦級的假想劇本。不過，內建具名艦船的一部分歷史資訊會因此遺失。

選擇具名艦船「吉野」（具名艦船欄的第一項），在「配屬給編組」下拉框中選擇「藍」，然後點擊「確認」按鈕。
`);
    markdownBox(tutorial7InsertDialogMsg);

    tutorial7Phase = Tutorial7Phase.WaitingForFirstShipInsertion;
}
