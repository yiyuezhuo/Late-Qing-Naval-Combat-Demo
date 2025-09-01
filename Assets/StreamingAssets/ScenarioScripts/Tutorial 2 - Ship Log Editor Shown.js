if(phase === Phase.WaitForShipLogEditorShown)
{
    msgBoxDelay(`
Ship Log Editor is displayed.

Verify the value of Control Mode field is "follow", then set Follow Distance to 1000 yards from default value 500 yards.

Confirm to close the editor and advance time until they reach to new equilibrium. 
`, 0.3);

    phase = Phase.WaitForFollowingEquilibrium2;
}