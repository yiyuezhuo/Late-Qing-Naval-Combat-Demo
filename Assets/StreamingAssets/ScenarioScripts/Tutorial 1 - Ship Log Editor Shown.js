if(phase === Phase.WaitForShipLogEditorOpened)
{
    msgBoxDelay(`
Ship Log Editor is displayed. You can switch different tabs to see different information. Ship Log record unit's 'dynamic' information like damage and weapon states (ammunition, firing target, availability), doctrine and etc.

Some advance command can only be formed in the Ship Log Editor.

When you get a idea about the Ship Log Editor, click on 'Go to Named Ship' in the right top corner to go to Named Ship Editor.
`, 0.3);

    phase = Phase.WaitForNamedShipEditorOpened;
}