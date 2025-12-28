using UnityEngine;
using Unity.Properties;

// using NavalCombatCore;
using CoreUtils;
using StrategicCombatCore;


public abstract class LeftObjectPickerRightEditorStrategic<ST, ET> : LeftObjectPickerRightEditor<ST, ET> where ET : class, IObjectIdLabeled, new() where ST : MonoBehaviour
{

    [CreateProperty]
    public StrategicGameState currentGameState => StrategicGameState.Instance;

}
