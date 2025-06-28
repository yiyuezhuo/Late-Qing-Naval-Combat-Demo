
public class GamePreference
{
    static GamePreference instance = new();
    public static GamePreference Instance => instance;

    public enum FiringLineDisplayMode
    {
        None,
        SelectedShip,
        SelectedGroup,
        SelectedRootGroup,
        All
    }

    public FiringLineDisplayMode firingLineDisplayMode = FiringLineDisplayMode.SelectedRootGroup;

    public float pulseLengthSeconds = 1; // 2; // 1;
    public float simulationRateRaio = 120; // 1s real time => 120s simulation time (similar to RTW's default advance speed)
}