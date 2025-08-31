if(phase === Phase.WaitForUnitSelection)
{
    msgBoxDelay(`
Unit is selected.

Now drag speed to max speed and advance time to see unit speed's change.
`, 0.3);

    phase = Phase.WaitForSpeedChanged;
}