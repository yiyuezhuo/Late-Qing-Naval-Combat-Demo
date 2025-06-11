using UnityEngine.UIElements;
using Jint;
using System;
using UnityEngine;

public class JSScriptConsoleDialog : HideableDocument<JSScriptConsoleDialog>
{
    Engine engine;

    protected override void Awake()
    {
        base.Awake();

        var titleLabel = root.Q<Label>("TitleLabel"); // TODO: Add dragger
        var clearButton = root.Q<Button>("ClearButton");
        var outputTextField = root.Q<TextField>("OutputTextField");
        var executeButton = root.Q<Button>("ExecuteButton");
        var inputTextField = root.Q<TextField>("InputTextField");
        var confirmButton = root.Q<Button>("ConfirmButton");

        // engine = new Engine(); // Sandboxed Version
        engine = new Engine(cfg => cfg.AllowClr()); // Free Version
        engine.SetValue("log", new Action<object>(msg => Debug.Log(msg)));
        engine.Execute("log('Hello World')");

        executeButton.clicked += () =>
        {
            engine.Execute(inputTextField.text);
        };

        confirmButton.clicked += Hide;
    }
}