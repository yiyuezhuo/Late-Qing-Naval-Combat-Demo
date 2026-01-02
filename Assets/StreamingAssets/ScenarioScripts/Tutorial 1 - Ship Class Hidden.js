if(phase === Phase.WaitForShipClassEditorHidden)
{
    let msg = getLocalized(`
Single Ship Tutorial is concluded, you may want to Go back to main menu with the button in the 'File' tab and browse other tutorials.
`,
`
単艦操作チュートリアルは終了しました。「ファイル」タブのボタンからメインメニューに戻り、他のチュートリアルを参照してください。
`,
`
单舰教程已结束。您可以通过“文件”选项卡中的按钮返回主菜单，并浏览其他教程。
`,
`
單艦教程已結束。您可以通過「檔案」選項卡中的按鈕返回主菜單，並瀏覽其他教程。
`)

    msgBoxDelay(msg, 0.3);

    phase = Phase.End;
}