msgBox(`
Welcome to the First Sino-Japanese War tutorial scenario. In this tutorial, you will learn how to perform map navigation and control individual units.

On the bottom information bar, you can see the latitude and longitude of the current cursor position. The current UTC and local time are also shown, followed by an indication of whether it is day or night and the current time zone. Additionally, the sun's altitude and azimuth are displayed, which affect visibility and may sometimes provide gunnery bonuses or penalties depending on the angle.

To move the camera, press and hold the right mouse button while moving the mouse (known as right-click dragging). This is a common method for camera movement in GIS applications and similar games.
`);

var Phase = {
    WaitForCameraMove : 1,
    WaitForCameraZoom : 2,
    WaitForTimeAdvanced : 3,
    WaitForUnitSelection : 4,
    WaitForSpeedChanged : 5
}

var phase = Phase.WaitForCameraMove;