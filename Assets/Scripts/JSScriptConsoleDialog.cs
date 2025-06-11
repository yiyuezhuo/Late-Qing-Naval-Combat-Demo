using UnityEngine.UIElements;
using Jint;
using System;
using UnityEngine;
using Jint.Runtime;
using NavalCombatCore;
using Jint.Runtime.Interop;

public class JSScriptConsoleDialog : HideableDocument<JSScriptConsoleDialog>
{
    TextField outputTextField;

    Engine engine;

    protected override void Awake()
    {
        base.Awake();

        var titleLabel = root.Q<Label>("TitleLabel"); // TODO: Add dragger
        var clearButton = root.Q<Button>("ClearButton");
        outputTextField = root.Q<TextField>("OutputTextField");
        var executeButton = root.Q<Button>("ExecuteButton");
        var inputTextField = root.Q<TextField>("InputTextField");
        var closeButton = root.Q<Button>("CloseButton");

        // engine = new Engine(); // Sandboxed Version
        engine = new Engine(cfg => cfg.AllowClr()); // Free Version
        // engine = new Engine(cfg => cfg.AllowClr(typeof(NavalGameState).Assembly));
        engine.SetValue("log", new Action<object>(msg => OnLog(msg)));
        engine.SetValue("NavalGameState", TypeReference.CreateTypeReference<NavalGameState>(engine));
        // engine.Execute("log('Hello World')");

        executeButton.clicked += () =>
        {
            try
            {
                // engine.Execute(inputTextField.text);
                var res = engine.Evaluate(inputTextField.text);
                var obj = res.ToObject();
                if (obj != null)
                {
                    OnReturn(obj);
                }
            }
            catch (JavaScriptException ex)
            {
                OnJSError(ex);
            }
        };

        clearButton.clicked += () =>
        {
            outputTextField.SetValueWithoutNotify("");
        };

        closeButton.clicked += Hide;

        // dragging support
        root.AddManipulator(new MyDragger());

    }

    public void OnLog(object output)
    {
        Debug.Log(output);
        outputTextField.SetValueWithoutNotify(outputTextField.value + "[Log]: " + output + "\n");
    }

    public void OnJSError(JavaScriptException ex)
    {
        Debug.LogWarning(ex);
        outputTextField.SetValueWithoutNotify(outputTextField.value + "[Error]: " + ex + "\n");
    }

    public void OnReturn(object obj)
    {
        Debug.Log(obj);
        outputTextField.SetValueWithoutNotify(outputTextField.value + "[Return]: " + obj + "\n");
    }
}