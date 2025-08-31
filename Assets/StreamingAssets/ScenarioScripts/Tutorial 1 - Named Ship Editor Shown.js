if(phase === Phase.WaitForNamedShipEditorOpened)
{
    msgBoxDelay(`
Named Ship Editor is displayed. Named ship is an 'instance' of a ship class, with some extra time related information attached.

When you get a idea about the Named Ship Editor, click on 'Go to Ship Class' in the right top corner to go to Ship Class Editor.
`, 0.3);

    phase = Phase.WaitForShipClassEditorOpened;

}