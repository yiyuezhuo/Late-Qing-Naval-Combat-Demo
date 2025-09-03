if(phase === Phase.WaitForCameraZoom)
{
    let msg = getLocalized(`
Camera is zoomed. 

Now move and zoom camera to re-centering the ship in the screen, then press 1 in the keyboard or press 'Advance 1 min' button in the top bar`,
`
カメラがズームされました。

次に、カメラを移動およびズームして画面の中央に艦艇を再配置し、キーボードの「1」を押すか、上部バーの「1分進める」ボタンを押してください。
`,
`
相机已缩放。

现在移动和缩放相机，将舰艇重新置于屏幕中央，然后按键盘上的“1”或顶部工具栏中的“前进1分钟”按钮。
`,
`
相機已縮放。

現在移動和縮放相機，將艦艇重新置於螢幕中央，然後按鍵盤上的「1」或頂部工具列中的「前進1分鐘」按鈕。
`
)

    msgBoxDelay(msg, 0.2);
    phase = Phase.WaitForTimeAdvanced;
}