if(phase === Phase.WaitForUnitSelection)
{
    msgBoxDelay(`
Unit is selected. The corresponding information panel is displayed in the right.

Now drag desired speed slider to set desired speed to max speed and advance time until speed reach to 15 knots.
`, 0.3);

    phase = Phase.WaitForSpeedChanged;
}
