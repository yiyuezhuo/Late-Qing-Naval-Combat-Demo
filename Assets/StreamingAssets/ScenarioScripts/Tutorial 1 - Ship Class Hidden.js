if(phase === Phase.WaitForShipClassEditorHidden)
{
    msgBoxDelay(`
Single ship tutorial is concluded, you may want to Go back to main menu with the button in the 'File' tab and browse other tutorials.
`, 0.3);

    phase = Phase.End;
}