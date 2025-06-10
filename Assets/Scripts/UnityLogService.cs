using NavalCombatCore;
using UnityEngine;

public class UnityLogService : ILoggerService
{
    public void Log(string message)
    {
        Debug.Log(message);
    }

    public void LogWarning(string message) => Debug.LogWarning(message);

    static UnityLogService instance = new UnityLogService();
    public static UnityLogService Instance => instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void RegisterToServiceLocator()
    {
        ServiceLocator.Register<ILoggerService>(Instance);
    }
}