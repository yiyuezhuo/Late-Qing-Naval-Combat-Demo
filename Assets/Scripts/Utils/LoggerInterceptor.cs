using UnityEngine;
using System;

public class MyLoggerHandler : ILogHandler
{
    private ILogHandler m_DefaultLogger;

    public MyLoggerHandler()
    {
        m_DefaultLogger = Debug.unityLogger.logHandler;
        Debug.unityLogger.logHandler = this;
    }

    public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args)
    {
        string message = string.Format(format, args);

        if (logType == LogType.Error && message.Contains("Layout update is struggling to process current layout (consider simplifying to avoid recursive layout)"))
        {
            m_DefaultLogger.LogFormat(LogType.Warning, context, format, args);
            return;
        }

        m_DefaultLogger.LogFormat(logType, context, format, args);
    }

    public void LogException(Exception exception, UnityEngine.Object context)
    {
        m_DefaultLogger.LogException(exception, context);
    }
}


public static class LoggerInterceptor
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void ReplaceLogger()
    {
        new MyLoggerHandler();
    }
}