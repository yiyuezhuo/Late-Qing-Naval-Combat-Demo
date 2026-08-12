if(phase === Phase.WaitForUnitSelection)
{
    let msg = getLocalized(`
Unit is selected. The corresponding information panel is displayed in the right.

Now drag desired speed slider to set desired speed to max speed and advance time until speed reach to 15 knots.
`,
`
ユニットが選択されました。対応する情報パネルが右側に表示されます。

目標速力スライダーをドラッグして、目標速力を最大速力に設定し、速力が15ノットに達するまで時間を進めてください。
`,
`
单位已选中。右侧显示了对应的信息面板。

请拖动目标速度滑块设置为最大速度，然后推进时间直至速度达到15节。
`,
`
單位已選中。右側顯示了對應的信息面板。

請拖動目標速度滑塊設置為最大速度，然後推進時間直至速度達到15節。
`);

    msgBoxDelay(msg, 0.3);

    phase = Phase.WaitForSpeedChanged;
}
