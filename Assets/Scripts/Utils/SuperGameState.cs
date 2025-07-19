
using Unity.Properties;

using CoreUtils;
using NavalCombatCore;
using StrategicCombatCore;

public enum GameMode
{
    Strategic,
    Naval
}

public class SuperGameState
{
    public GameMode currentGameMode;

    static SuperGameState instance = new();
    public static SuperGameState Instance => instance;

    public AbstractGameState GetCurrentGameState()
    {
        return currentGameMode switch
        {
            GameMode.Strategic => StrategicGameState.Instance,
            GameMode.Naval => NavalGameState.Instance,
            _ => throw new System.Exception("Invalid game mode")
        };
    }

    // [CreateProperty]
    // public AbstractGameState currentGameState => GetCurrentGameState();

}