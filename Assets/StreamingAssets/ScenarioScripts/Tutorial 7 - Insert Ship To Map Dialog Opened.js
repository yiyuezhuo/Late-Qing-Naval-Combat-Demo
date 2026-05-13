if (tutorial7Phase === Tutorial7Phase.WaitingForFirstInsertDialog) {
    let tutorial7InsertDialogMsg = getLocalized(`
After selecting a position in insert mode, the Insert Dialog will be displayed.

You can insert three types of objects here:

- **Not Deployed Ship State**: Deploy a not deployed ship (the ship is usually created manually in the Ship State Editor). This is a somewhat advanced topic and is not covered in this tutorial.
- **Named Ship**: This create a Ship State from the selected Named Ship which is not associated with a Ship State (so that a Named Ship can only has a Ship State "embodiment") with proper initialization, and deploys it to map.
- **Ship Class**: This creates an "anonymous" Named Ship from the selected Ship Class and create a Ship State from that new Named Ship. It is used for hypothetical scenarios involving ship classes that exceed historical quantities. Some historical information from the built-in Named Ship is lost though.

Select Named Ship Yoshino (the first item in the Named Ship column), choose Blue in the "Attach to Group" Dropdown, and click the Confirm button.
`,
`
挿入モードで位置を選択すると、Insert Dialog が表示されます。

ここでは、3種類のオブジェクトを挿入できます。

- **Not Deployed Ship State**: 未配置の艦艇状態を配置します。この艦艇は通常 Ship State Editor で手動作成します。やや高度な内容なので、このチュートリアルでは扱いません。
- **Named Ship**: まだ Ship State と関連付けられていない Named Ship から Ship State を適切に初期化して作成し、マップに配置します。これにより、1つの Named Ship が持てる Ship State の「実体」は1つだけになります。
- **Ship Class**: 選択した Ship Class から「匿名」の Named Ship を作成し、その新しい Named Ship から Ship State を作成します。これは、史実の保有数を超える艦級を使った仮想シナリオに使用します。ただし、組み込みの Named Ship が持つ一部の史実情報は失われます。

Named Ship Yoshino（Named Ship 列の最初の項目）を選択し、「Attach to Group」ドロップダウンで Blue を選んで、Confirm ボタンをクリックしてください。
`,
`
在插入模式中选择位置后，Insert Dialog 会显示出来。

你可以在这里插入三类对象：

- **Not Deployed Ship State**：部署一个尚未部署的舰船状态。这个舰船通常是在 Ship State Editor 中手动创建的。这是稍微高级一些的主题，本教程不会讲解。
- **Named Ship**：从尚未关联 Ship State 的 Named Ship 创建一个经过适当初始化的 Ship State，并将其部署到地图上。这样一个 Named Ship 只能拥有一个 Ship State“实体”。
- **Ship Class**：从选中的 Ship Class 创建一个“匿名”的 Named Ship，并从这个新的 Named Ship 创建 Ship State。它用于涉及超过历史数量的舰级的假想剧本。不过，内置 Named Ship 的一部分历史信息会因此丢失。

选择 Named Ship Yoshino（Named Ship 列的第一项），在“Attach to Group”下拉框中选择 Blue，然后点击 Confirm 按钮。
`,
`
在插入模式中選擇位置後，Insert Dialog 會顯示出來。

你可以在這裡插入三類物件：

- **Not Deployed Ship State**：部署一個尚未部署的艦船狀態。這個艦船通常是在 Ship State Editor 中手動建立的。這是稍微進階一些的主題，本教學不會講解。
- **Named Ship**：從尚未關聯 Ship State 的 Named Ship 建立一個經過適當初始化的 Ship State，並將其部署到地圖上。這樣一個 Named Ship 只能擁有一個 Ship State「實體」。
- **Ship Class**：從選中的 Ship Class 建立一個「匿名」的 Named Ship，並從這個新的 Named Ship 建立 Ship State。它用於涉及超過歷史數量的艦級的假想劇本。不過，內建 Named Ship 的一部分歷史資訊會因此遺失。

選擇 Named Ship Yoshino（Named Ship 欄的第一項），在「Attach to Group」下拉框中選擇 Blue，然後點擊 Confirm 按鈕。
`);
    markdownBox(tutorial7InsertDialogMsg);

    tutorial7Phase = Tutorial7Phase.WaitingForFirstShipInsertion;
}
