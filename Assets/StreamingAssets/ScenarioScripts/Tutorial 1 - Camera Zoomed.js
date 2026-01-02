if(phase === Phase.WaitForCameraZoom)
{
    let msg = getLocalized(`
Camera is zoomed.

Now move and zoom the camera to re-center the ship on the screen, then press the "1" key on your keyboard or click the "Advance 1 min" button in the top bar to advance 1 minute.

Note: Hotkeys such as "1" only work when the mouse is hovering over the map. They are disabled when the mouse is over the UI.

Also, many buttons list their hotkeys in parentheses. Hotkeys are the developer-recommended primary interaction method on devices with a keyboard. Buttons should mainly serve as reminders of the hotkeys, or as a workaround for mobile devices.
`,
`
カメラがズームされました。

次に、カメラを移動およびズームして画面の中央に艦艇を再配置し、キーボードの「1」を押すか、上部バーの「1分進める」ボタンを押してください。
`,
`
摄像机已缩放。

现在移动并缩放摄像机，使舰船重新位于屏幕中央，然后按下键盘上的“1”键，或点击顶部栏中的“推进1分钟(1)”按钮，使时间前进 1 分钟。

注意：“1”等快捷键仅在鼠标指针悬停在地图上时才会生效。当鼠标位于 UI 上时，快捷键将被禁用。

此外，许多按钮会在括号中标注其对应的快捷键。对于带有键盘的设备，快捷键是开发者推荐的主要交互方式。按钮主要用于提示快捷键，或作为移动设备上的临时替代方案。
`,
`
攝影機已縮放。

現在移動並縮放攝影機，使艦船重新置於畫面中央，然後按下鍵盤上的「1」鍵，或點擊頂部欄中的「推進1分鐘(1)」按鈕，讓時間前進 1 分鐘。

注意：「1」等快捷鍵僅在滑鼠游標停留在地圖上時才會生效。當滑鼠位於 UI 上時，快捷鍵將被停用。

此外，許多按鈕會在括號中標示其對應的快捷鍵。對於具備鍵盤的裝置，快捷鍵是開發者建議的主要操作方式。按鈕本身主要作為快捷鍵的提示，或是行動裝置上的替代方案。
`
)

    msgBoxDelay(msg, 0.2);
    phase = Phase.WaitForTimeAdvanced;
}