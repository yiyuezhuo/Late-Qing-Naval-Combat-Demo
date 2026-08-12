if(phase === Phase.WaitForCameraZoom)
{
    let msg = getLocalized(`
Camera is zoomed.

Now move and zoom the camera to re-center the ship on the screen, then press Space to start advancing time. Press Space again to pause. The Play toggle in the top bar performs the same action, and the adjacent selector controls the advance speed.

The default ×120 speed is suitable for normal play. When many things are happening at once, reduce it to ×30, ×10, or lower as needed.

When you need precise control, press “1” to advance exactly one minute, or use the “Advance 1 Min (1)” button in the top bar. This is called “WEGO-style simultaneous turn resolution.”

Tip: Hotkeys do not work while the input focus is on a UI control. Click an empty area of the map to return focus to the map before using hotkeys.

On mouse-and-keyboard devices, use hotkeys as the primary controls. The top-bar controls mainly serve as hotkey reminders and as alternatives for touch input.
`,
`
カメラがズームされました。

次に、カメラを移動およびズームして艦艇を画面中央に戻し、スペースキーを押して時間の進行を開始してください。もう一度スペースキーを押すと一時停止します。上部バーの「再生」トグルでも同じ操作ができ、その隣の選択欄で進行速度を変更できます。

通常のプレイには既定の ×120 が適しています。同時に多くのことが起きている場合は、必要に応じて ×30、×10、またはそれ以下に下げてください。

時間を細かく制御したい場合は、「1」を押すと正確に1分進められます。上部バーの「1分進める（1）」ボタンでも同じ操作ができます。これは「WEGO方式の同時ターン解決」と呼ばれます。

ヒント：操作フォーカスが UI コントロールにある間、ホットキーは機能しません。ホットキーを使用する前に、マップ上の何もない場所をクリックしてフォーカスをマップに戻してください。

マウスとキーボードを使用する端末では、ホットキーを主要な操作方法として使用してください。上部バーの操作項目は、主にホットキーの確認用およびタッチ操作向けの代替手段です。
`,
`
摄像机已缩放。

现在移动并缩放视角，使舰船重新位于屏幕中央，然后按空格键开始推进时间。再次按空格键可暂停。顶部栏中的“播放”开关具有相同作用，旁边的选项可调整推进速度。

默认的 ×120 适合一般操作。当同时发生的事情较多时，可以根据需要降低到 ×30、×10 或更低。

需要精确控制时间时，可以按“1”单次推进一分钟，也可以使用顶部栏中的“推进 1 分钟（1）”按钮。这被称为“同步回合制式（WEGO）推进”。

提示：当操作焦点位于界面控件上时，快捷键不会生效。点击地图空白处可将焦点移回地图，然后再使用快捷键。

在键鼠设备上，请优先使用快捷键。顶部栏中的操作项主要用于提示对应的快捷键，并为触屏操作提供替代方式。
`,
`
攝影機已縮放。

現在移動並縮放視角，使艦船重新置於畫面中央，然後按空白鍵開始推進時間。再次按空白鍵可暫停。頂部欄中的「播放」開關具有相同作用，旁邊的選項可調整推進速度。

預設的 ×120 適合一般操作。當同時發生的事情較多時，可以視需要降低到 ×30、×10 或更低。

需要精確控制時間時，可以按「1」單次推進一分鐘，也可以使用頂部欄中的「推進 1 分鐘（1）」按鈕。這稱為「同步回合制式（WEGO）推進」。

提示：當操作焦點位於介面控制項上時，快捷鍵不會生效。點擊地圖空白處可將焦點移回地圖，然後再使用快捷鍵。

在鍵鼠裝置上，請優先使用快捷鍵。頂部欄中的操作項目主要用於提示對應的快捷鍵，並為觸控操作提供替代方式。
`
)

    msgBoxDelay(msg, 0.2);
    phase = Phase.WaitForTimeAdvanced;
}
