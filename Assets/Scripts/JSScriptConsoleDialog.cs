using UnityEngine.UIElements;
using Jint;
using System;
using UnityEngine;
using Jint.Runtime;
using NavalCombatCore;
using Jint.Runtime.Interop;
using System.Linq;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;



public class JSScriptConsoleDialog : HideableDocument<JSScriptConsoleDialog>
{
    TextField outputTextField;
    TextField inputTextField;
    DropdownField builtInScriptDropdownField;

    Engine engine;

    protected override void Awake()
    {
        base.Awake();

        var titleLabel = root.Q<Label>("TitleLabel"); // TODO: Add dragger
        var clearButton = root.Q<Button>("ClearButton");
        outputTextField = root.Q<TextField>("OutputTextField");
        var executeButton = root.Q<Button>("ExecuteButton");
        inputTextField = root.Q<TextField>("InputTextField");
        var closeButton = root.Q<Button>("CloseButton");
        builtInScriptDropdownField = root.Q<DropdownField>("BuiltInScriptDropdownField");

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

        // var textAssets = Resources.LoadAll<TextAsset>("BuiltinEmbedScripts");
        // var names = textAssets.Select(ts => ts.name).ToList();
        // builtInScriptDropdownField.choices = names;
        builtInScriptDropdownField.RegisterValueChangedCallback(evt =>
        {
            StartCoroutine(FetchScriptAndUpdate(evt.newValue));
        });

        StartCoroutine(UpdateBuiltInScriptDropdownField()); // Fire and Forget
    }

    IEnumerator FetchScriptAndUpdate(string subPath)
    {
        var path = Application.streamingAssetsPath + subPath;

        var request = UnityWebRequest.Get(path);
        Debug.Log("Requesting: " + path);
        yield return request.SendWebRequest();
        if (request.result == UnityWebRequest.Result.Success)
        {
            inputTextField.SetValueWithoutNotify(request.downloadHandler.text);
        }
    }

    public IEnumerator UpdateBuiltInScriptDropdownField()
    {
        // Schedule async tasks
        string streamingAssetsPath = Application.streamingAssetsPath;
        var manifestPath = streamingAssetsPath + "/Manifest.xml";

        var request = UnityWebRequest.Get(manifestPath);
        Debug.Log($"manifestPath={manifestPath}");
        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            // jsonData = request.downloadHandler.text;
            Debug.Log($"request.downloadHandler.text={request.downloadHandler.text}");
            var manifestModel = XmlUtils.FromXML<ManifestModel>(request.downloadHandler.text);
            builtInScriptDropdownField.choices = manifestModel.builtinScripts;
        }
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