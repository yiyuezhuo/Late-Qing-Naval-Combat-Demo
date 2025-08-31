if(phase === Phase.WaitForShipClassEditorOpened)
{
    msgBoxDelay(`
Ship Class Editor is displayed. You can switch different tabs to see different information. Ship Class save static information of a ship class like speed, DP, weapon and etc.

When you get a idea about the Ship Log Editor, click on 'Confirm' in the left bottom corner to go to main map.
`, 0.3);

    phase = Phase.WaitForShipClassEditorHidden;

}