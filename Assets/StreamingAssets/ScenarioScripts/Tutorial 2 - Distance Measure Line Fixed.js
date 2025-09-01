if(phase === Phase.WaitForDistanceMeasuring)
{
    msgBoxDelay(`
Distance Measure Line is Created, it should show a value close to 5000 yards.

You can press escape to hide the line and label. Now change two ship's course to make they are close to each other and reduce their distance to 2500 yards.

`, 0.3);
    phase = Phase.WaitForDistanceClosing;
}

