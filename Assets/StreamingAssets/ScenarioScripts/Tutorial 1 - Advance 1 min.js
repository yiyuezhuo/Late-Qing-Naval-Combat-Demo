if(phase === Phase.WaitForTimeAdvanced)
{
    msgBoxDelay(`
Time is advanced by 1min, you can check bottom line and ship is moved. 

Now left click on the ship to select it`
    , 0.2);

    phase = Phase.WaitForUnitSelection;
}
else if(phase === Phase.WaitForSpeedChanged)
{
    let shipLog = NavalGameState.Instance.shipLogs[0];
    if(shipLog.speedKnots >= 15)
    {
        msgBoxDelay(`
Speed is increased to 15.

Now drag heading slider to change desired heading, and holding shift and left click to the map to set desired heading as well.
Point to 75-120 True North Clockwise and then advance time until the ship reaches the desired heading.

`, 0.3);
        phase = Phase.WaitForCourseChanged;
    }
}
else if(phase === Phase.WaitForCourseChanged)
{
    let shipLog = NavalGameState.Instance.shipLogs[0];
    if(shipLog.headingDeg >= 75 && shipLog.headingDeg <= 105)
    {
        msgBoxDelay(`
Course is changed.

Now right-click the unit or left-click on unit's name hyper link in the information panel to open the ship log editor (the game use the same UI for game and 'editor', just like CMO)

`, 0.3);
        phase = Phase.WaitForShipLogEditorOpened;
    }
}