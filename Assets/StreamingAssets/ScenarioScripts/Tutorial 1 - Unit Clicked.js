if(phase === Phase.WaitForUnitSelection)
{
    let msg = getLocalized(`
Unit is selected. The corresponding information panel is displayed in the right.

Drag the desired speed slider, or enter the maximum speed directly in the adjacent value field, to set the desired speed to its maximum. The input focus will remain on the speed control, so click an empty area of the map to return focus to the map. Then advance time until the ship reaches 15 knots.
`,
`
ユニットが選択されました。対応する情報パネルが右側に表示されます。

目標速力スライダーをドラッグするか、隣の数値欄に最大速力を直接入力して、目標速力を最大値に設定してください。操作フォーカスは速力コントロールに残るため、マップ上の何もない場所をクリックしてフォーカスをマップに戻してください。その後、速力が15ノットに達するまで時間を進めてください。
`,
`
单位已选中。右侧显示了对应的信息面板。

拖动目标速度滑块，或在旁边的数值框中直接输入最大航速，将目标速度设为最大值。操作焦点此时会停留在速度控件上，请先点击地图空白处将焦点移回地图，然后推进时间，直到航速达到15节。
`,
`
單位已選中。右側顯示了對應的信息面板。

拖動目標速度滑桿，或在旁邊的數值欄位中直接輸入最大航速，將目標速度設為最大值。操作焦點此時會停留在速度控制項上，請先點擊地圖空白處將焦點移回地圖，然後推進時間，直到航速達到15節。
`);

    msgBoxDelay(msg, 0.3);

    phase = Phase.WaitForSpeedChanged;
}
