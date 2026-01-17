if(phase === Phase.WaitForCameraZoom)
{
    let msg = getLocalized(`
Camera is zoomed.

Now move and zoom the camera to re-center the ship on the screen, then press the "1" key on your keyboard or click the "Advance 1 min" button in the top bar to advance 1 minute.

Note: Hotkeys such as “1” only work when no UI element has focus. Certain UI interactions—such as clicking UI elements, dragging sliders, or typing text or numbers—may assign focus to the UI and block hotkeys. In this case, pressing 1 will be consumed by the UI (e.g., entering 1 into an input field) instead of triggering the hotkey. UI focus can be cleared by clicking anywhere on the map.

Also, many buttons list their hotkeys in parentheses. Hotkeys are the developer-recommended primary interaction method on devices with a keyboard. Buttons should mainly serve as reminders of the hotkeys, or as a workaround for mobile devices.
`,
`
カメラがズームされました。

次に、カメラを移動およびズームして画面の中央に艦艇を再配置し、キーボードの「1」を押すか、上部バーの「1分進める」ボタンを押してください。

注意: 「1」などのホットキーは、UI 要素にフォーカスがない場合のみ有効です。UI 要素のクリック、スライダーのドラッグ、テキストや数値の入力などの操作により、UI にフォーカスが設定され、ホットキーが無効になることがあります。この場合、「1」を押してもホットキーは発動せず、UI によって入力として消費されます（例：入力欄に「1」が入力される）。マップ上の任意の場所をクリックすることで、UI のフォーカスを解除できます。

また、多くのボタンには括弧内に対応するホットキーが表示されています。ホットキーは、キーボードを備えたデバイスにおいて開発者が推奨する主要な操作方法です。ボタンは主にホットキーの確認用として、またはモバイル端末向けの代替操作手段として使用されることを想定しています。
`,
`
摄像机已缩放。

现在移动并缩放摄像机，使舰船重新位于屏幕中央，然后按下键盘上的“1”键，或点击顶部栏中的“推进1分钟(1)”按钮，使时间前进 1 分钟。

注意： 当没有任何 UI 元素获得焦点时，快捷键（例如“1”）才会生效。点击 UI 元素、拖动滑块或输入文字、数字等操作可能会使 UI 获得焦点，从而屏蔽快捷键。在这种情况下，按下 1 会被 UI 作为输入消耗（例如在输入框中输入数字 1），而不会触发快捷键。可通过点击地图上任意位置来清除 UI 焦点。

此外，许多按钮会在括号中标注其对应的快捷键。对于带有键盘的设备，快捷键是开发者推荐的主要交互方式。按钮主要用于提示快捷键，或作为移动设备上的临时替代方案。
`,
`
攝影機已縮放。

現在移動並縮放攝影機，使艦船重新置於畫面中央，然後按下鍵盤上的「1」鍵，或點擊頂部欄中的「推進1分鐘(1)」按鈕，讓時間前進 1 分鐘。

注意： 快捷鍵（例如「1」）僅在沒有任何 UI 元素取得焦點時才會生效。點擊 UI 元素、拖動滑桿，或輸入文字、數字等操作，可能會使 UI 取得焦點並阻擋快捷鍵。在此情況下，按下 1 會被 UI 作為輸入消耗（例如在輸入欄位中輸入數字 1），而不會觸發快捷鍵。可透過點擊地圖上任意位置來清除 UI 焦點。

此外，許多按鈕會在括號中標示其對應的快捷鍵。對於具備鍵盤的裝置，快捷鍵是開發者建議的主要操作方式。按鈕本身主要作為快捷鍵的提示，或是行動裝置上的替代方案。
`
)

    msgBoxDelay(msg, 0.2);
    phase = Phase.WaitForTimeAdvanced;
}