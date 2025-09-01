msgBox(`
Welcome to the Second Tutorial Scenario of the First Sino-Japanese War. In this tutorial, you will learn how to control group.

There are two ships on the map. Now, measure the distance between them. This can be achieved by:

- Pressing the hotkey "D" to enter distance measuring mode. Click on the position of one ship, and then click on the position of the other ship. The distance will be displayed.
- Switching to the "Tool" tab in the top bar, clicking the "Distance Measuring" button to enter distance measuring mode, and then clicking on the two locations.
`);

var Phase = {
    WaitForDistanceMeasuring : 1,
    WaitForDistanceClosing : 2,
    WaitForFollowingEquilibrium : 3,
    WaitForShipLogEditorShown : 4,
    WaitForFollowingEquilibrium2 : 5,
    WaitForShipRelativeToEquilibrium : 6,
    End : 7
}

var phase = Phase.WaitForDistanceMeasuring;