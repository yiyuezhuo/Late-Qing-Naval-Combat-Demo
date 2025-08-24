
using NavalCombatCore;
using Unity.Properties;
using CoreUtils;

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

    // public LanguageType shortLabelLanguageType = LanguageType.English;
    // public LanguageType longLabelLanguageType = LanguageType.All;

    [CreateProperty]
    public LanguageType shortLabelLanguageType
    {
        get => GlobalString.shortMode;
        set => GlobalString.shortMode = value;
    }

    [CreateProperty]
    public LanguageType longLabelLanguageType
    {
        get => GlobalString.mergeMode;
        set => GlobalString.mergeMode = value;
    }

    [CreateProperty]
    public CoreParameter navalCombatCoreParameter => CoreParameter.Instance;
}