using StrategicCombatCore;

public class StrategicViewState
{
    // camera's position
    public float xPosition;
    public float yPosition;

    public float orthographicSize;
}

public class StrategicFullState
{
    // TODO: Add Streaming Asset Reference
    public StrategicGameState gameState;
    public StrategicViewState viewState;
    // TODO: Add eventState?
}