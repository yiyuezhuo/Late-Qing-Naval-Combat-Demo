if(phase == Phase.WaitForDistanceClosing)
{
    let ship0 = NavalGameState.Instance.shipLogs[0];
    let ship1 = NavalGameState.Instance.shipLogs[1];
    if(getDistanceYard(ship0, ship1) < 2500)
    {
        msgBoxDelay(`
Distance is less than 2500 yards.

Now select a ship and press the "F" key, then click on another ship to have it follow. Alternatively, you can press the corresponding button in the top bar.

(This changes a unit from "independent" control mode to "follow" mode, and it will try to follow the target at a default distance of 500 yards. Its arrow will be hidden, as the arrow indicates a unit is in independent mode and is generally a group leader.)

Then advance time until they form a proper follow formation (with a closing heading and maintaining the target distance).
`, 0.3);

        phase = Phase.WaitForFollowingEquilibrium;
    }
}
else if(phase === Phase.WaitForFollowingEquilibrium)
{
    let ship0 = NavalGameState.Instance.shipLogs[0];
    let ship1 = NavalGameState.Instance.shipLogs[1];
    let distYards = getDistanceYard(ship0, ship1);
    let headingAbsDiff = getPositiveAngleDifference(ship0, ship1);
    if(distYards >= 475 && distYards <= 525 && headingAbsDiff <= 10)
    {
        msgBoxDelay(`
Follow formation is formed properly (a control equilibrium is reached).

Now open ship log editor for the non-indepedent ship (the ship following another ship) to set extra control parameter.
`, 0.3);

        phase = Phase.WaitForShipLogEditorShown;
    }
}
else if(phase === Phase.WaitForFollowingEquilibrium2)
{
    let ship0 = NavalGameState.Instance.shipLogs[0];
    let ship1 = NavalGameState.Instance.shipLogs[1];
    let distYards = getDistanceYard(ship0, ship1);
    let headingAbsDiff = getPositiveAngleDifference(ship0, ship1);
    if(distYards >= 975 && distYards <= 1025 && headingAbsDiff <= 10)
    {
        msgBoxDelay(`
A new control equilibrium is reached.

Now select the non-independent ship and press "R" (or corresponding button on the top bar) and click on another ship to set "Relative To" control mode, controlled unit will try to maintain a bearing and distance to target.

Advance time until new control equilibrium is reached.
`, 0.3);

        phase = Phase.WaitForShipRelativeToEquilibrium;
    }
}
else if(phase === Phase.WaitForShipRelativeToEquilibrium)
{
    let ship0 = NavalGameState.Instance.shipLogs[0];
    let ship1 = NavalGameState.Instance.shipLogs[1];
    let stats = measure(ship0, ship1);
    let distYards = stats.distanceYards;
    let headingAbsDiff = getPositiveAngleDifference(ship0, ship1);
    let bearing01 = stats.observerToTargetBearingRelativeToBowDeg;
    let bearing10 = stats.targetToObserverBearingRelativeToBowDeg;
    let absBearingDiff = Math.min(Math.abs(bearing01 - 135), Math.abs(bearing10 - 135));
    if(distYards >= 225 && distYards <= 275 && headingAbsDiff <= 10 && absBearingDiff < 5)
    {
        msgBoxDelay(`
Relative-To equilibrium is reached.

This tutorial scenario is concluded. You can play around extra parameter for relative-to control mode, control group lead and see how does controlled ship respond. Then return to main menu and check other tutorial scenarios.
`, 0.3);

        phase = Phase.End;
    }
}