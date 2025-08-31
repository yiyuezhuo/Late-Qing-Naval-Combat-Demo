if(phase === Phase.WaitForCameraMove)
{
    msgBoxDelay("Camera is Moved, now scroll the mouse wheel to zoom map", 0.2);
    phase = Phase.WaitForCameraZoom;
}

