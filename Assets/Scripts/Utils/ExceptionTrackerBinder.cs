using UnityEngine;
using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using Unity.Properties;

public class ExceptionTracker
{
    public class ExceptionRecord
    {
        public string condition;
        public string stackTrace;
        public LogType type;

        public string Summary()
        {
            return $"{type}: {condition}: {stackTrace}";
        }
    }

    List<ExceptionRecord> exceptions = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void InstallExceptionHandlers()
    {
        // AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        // Application.logMessageReceived += OnLogMessage;

        Application.logMessageReceived += HandleException;
    }

    static void HandleException(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Exception)
        {
            var record = new ExceptionRecord()
            {
                condition = condition,
                stackTrace = stackTrace,
                type = type
            };

            Instance.exceptions.Add(record);
        }
    }

    // static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    // {
    //     var ex = e.ExceptionObject as Exception;
    //     Instance.exceptions.Add(ex);
    // }

    static ExceptionTracker instance;
    public static ExceptionTracker Instance
    {
        get
        {
            if (instance == null)
            {
                instance = new ExceptionTracker();
            }
            return instance;
        }
    }

    public void Clear()
    {
        exceptions.Clear();
    }

    [CreateProperty]
    public bool shouldDisplay => exceptions.Count > 0;

    [CreateProperty]
    public string exceptionMessage
    {
        get
        {
            var lastDesc = exceptions.Count > 0 ? $"Latest: {exceptions[^1].Summary()}" : "";
            return $"{exceptions.Count} exceptions.\n {lastDesc}";
        }
    }
}

// TODO: Use to SetActive instead of DisplayStyle binding to improve performance.
public class ExceptionTrackerBinder : MonoBehaviour
{
    public void Start()
    {
        var doc = GetComponent<UIDocument>();
        var root = doc.rootVisualElement;
        root.dataSource = ExceptionTracker.Instance;

        var clearButton = root.Q<Button>("ClearButton");
        clearButton.clicked += () =>
        {
            ExceptionTracker.Instance.Clear();
        };
    }
}