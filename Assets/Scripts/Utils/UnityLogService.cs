using UnityEngine;

using CoreUtils;

public class UnityLogService : ILoggerService
{
    public void Log(string message)
    {
        Debug.Log(message);
    }

    public void LogWarning(string message) => Debug.LogWarning(message);
    public void LogError(string message) => Debug.LogError(message);

    static UnityLogService instance = new UnityLogService();
    public static UnityLogService Instance => instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void RegisterToServiceLocator()
    {
        ServiceLocator.Register<ILoggerService>(Instance);
    }
}