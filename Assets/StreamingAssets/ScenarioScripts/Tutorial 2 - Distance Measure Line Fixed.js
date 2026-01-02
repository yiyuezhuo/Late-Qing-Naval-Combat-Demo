if(phase === Phase.WaitForDistanceMeasuring)
{
    let msg = getLocalized(`
The distance measurement line has been created and should display a value close to 5,000 yards.

You can press the Escape key to hide both the line and its label. Now adjust the course of the two ships so that they move closer to each other, reducing their distance to 2,500 yards.
`,
`
距離測定線が作成されました。約5000ヤードの値が表示されるはずです。

Escキーを押すと測定線とラベルを非表示にできます。
次に、2隻の艦艇の針路を変更して互いに接近させ、距離を2500ヤードまで縮めてください。
`,
`
距离测量线已创建，应显示数值约5000码。

按Esc键可隐藏测量线及标签。
现在请调整两舰航向使彼此靠近，将距离缩短至2500码。
`,
`
距離測量線已創建，應顯示數值約5000碼。

按Esc鍵可隱藏測量線及標籤。
現在請調整兩艦航向使彼此靠近，將距離縮短至2500碼。
`)

    msgBoxDelay(msg, 0.3);
    phase = Phase.WaitForDistanceClosing;
}

