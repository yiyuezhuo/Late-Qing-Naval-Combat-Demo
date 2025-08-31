if(phase === Phase.WaitForCameraZoom)
{
    msgBoxDelay(`
Camera is zoomed. 

Now move and zoom camera to re-centering the ship in the screen, then press 1 in the keyboard or press 'Advance 1 min' button in the top bar
`, 0.2);
    phase = Phase.WaitForTimeAdvanced;
}