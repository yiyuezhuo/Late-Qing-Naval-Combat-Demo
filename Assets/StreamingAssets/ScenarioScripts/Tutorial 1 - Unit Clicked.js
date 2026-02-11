if(phase === Phase.WaitForUnitSelection)
{
    let msg = getLocalized(`
Unit is selected. The corresponding information panel is displayed in the right.

Now drag desired speed slider to set desired speed to max speed and advance time until speed reach to 15 knots.

Note: Since dragging silder may assign focus to the slider UI. To use hotkey 1 to advance time, you may need to click on the map anywhere to reset focus.
`,
`
ユニットが選択されました。対応する情報パネルが右側に表示されます。

希望速度スライダーをドラッグして、希望速度を最大速度に設定し、速度が15ノットに達するまで時間を進めてください。

注: スライダーをドラッグすると、スライダーのUIにフォーカスが移動する場合があります。「1」キーで時間を進めるには、マップ上の任意の場所をクリックしてフォーカスをリセットする必要があるかもしれません。
`,
`
单位已选中。右侧显示了对应的信息面板。

请拖动目标速度滑块设置为最大速度，然后推进时间直至速度达到15节。

注意：拖动滑块可能会使焦点聚焦到滑块界面上。若要使用快捷键“1”来推进时间，您可能需要先点击地图任意位置以重置焦点。
`,
`
單位已選中。右側顯示了對應的信息面板。

請拖動目標速度滑塊設置為最大速度，然後推進時間直至速度達到15節。

注意：拖曳滑桿可能會將焦點設定到滑桿介面上。若要使用快速鍵「1」來推進時間，您可能需要先點擊地圖上的任意位置以重設焦點。
`);

    msgBoxDelay(msg, 0.3);

    phase = Phase.WaitForSpeedChanged;
}
