if(phase === Phase.WaitForTimeAdvanced)
{
    msgBoxDelay(`
Time is advanced by 1min, you can check bottom line and ship is moved. 

Now left click on the ship to select it`
    , 0.2);

    phase = Phase.WaitForUnitSelection;
}