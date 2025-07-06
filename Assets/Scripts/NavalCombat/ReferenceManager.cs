using NavalCombatCore;
using Unity.Properties;


public class ReferenceManager
{
    static ReferenceManager _instance = new();
    public static ReferenceManager Instance => _instance;

    // Reference ReferenceManager instead of NavalGameState.Instance directly to ensure latest binding is used
    [CreateProperty]
    public NavalGameState navalGameState => NavalGameState.Instance;

    [CreateProperty]
    public StreamingAssetReference streamingAssetReference => StreamingAssetReference.Instance;

}