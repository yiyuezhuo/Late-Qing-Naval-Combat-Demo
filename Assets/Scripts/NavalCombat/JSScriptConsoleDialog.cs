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
using System.IO;

public class ScriptEngine
{
    Engine engine;

    void Initialize()
    {
        engine = new Engine(cfg => cfg.AllowClr()); // Free Version
        var assembly = typeof(NavalGameState).Assembly;
        // Debug.Log($"assembly={assembly}");
        engine = new Engine(cfg => cfg.AllowClr(assembly));

        engine.SetValue("log", new Action<object>(msg => OnLog(msg)));
        engine.SetValue("NavalGameState", TypeReference.CreateTypeReference<NavalGameState>(engine));
        engine.SetValue("msgBox", new Action<object>(arg => DialogRoot.Instance.PopupMessageDialog(arg as string)));
        engine.SetValue("msgBoxDelay", new Action<object, object>(MsgDelay));
        // engine.SetValue("msgBoxDelay", new Action<object, object>((msg, seconds) => {
        //     DialogRoot.Instance.PopupMessageDialog(msg as string));
        // });
    }

    static void Msg(object msg)
    {
        DialogRoot.Instance.PopupMessageDialog(msg as string);
    }

    static IEnumerator DoMsgDelay(string msg, float seconds)
    {
        yield return new WaitForSeconds(seconds);
        DialogRoot.Instance.PopupMessageDialog(msg);
    }

    static void MsgDelay(object msg, object seconds)
    {
        var _msg = msg as string;
        var _seconds = (float)(double)seconds;
        BehaviourUtils.Instance.StartCoroutine(DoMsgDelay(_msg, _seconds));
    }

    public void Execute(string script)
    {
        try
        {
            // engine.Execute(inputTextField.text);
            // var res = engine.Evaluate(inputTextField.text);
            var res = engine.Evaluate(script);
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
    }

    public EventHandler<object> onLog;
    public EventHandler<JavaScriptException> onJSError;
    public EventHandler<object> onReturn;

    public void OnLog(object output)
    {
        onLog?.Invoke(this, output);
    }

    public void OnJSError(JavaScriptException ex)
    {
        onJSError?.Invoke(this, ex);
        // Debug.LogWarning(ex);
        // outputTextField.SetValueWithoutNotify(outputTextField.value + "[Error]: " + ex + "\n");
    }

    public void OnReturn(object obj)
    {
        onReturn?.Invoke(this, obj);
        // Debug.Log(obj);
        // outputTextField.SetValueWithoutNotify(outputTextField.value + "[Return]: " + obj + "\n");
    }

    static ScriptEngine instance;

    public static ScriptEngine Instance
    {
        get
        {
            if (instance == null)
            {
                instance = new ScriptEngine();
                instance.Initialize();
            }
            return instance;
        }
    }
}

public class JSScriptConsoleDialog : HideableDocument<JSScriptConsoleDialog>
{
    TextField outputTextField;
    TextField inputTextField;
    DropdownField builtInScriptDropdownField;

    // Engine engine;

    HashSet<RuntimePlatform> fileSystemNotWorkingPlatforms = new() 
    {
        RuntimePlatform.WebGLPlayer,
        RuntimePlatform.Android,
        RuntimePlatform.IPhonePlayer
    };

    // protected override void Awake()
    void OnEnable()
    {
        // base.Awake();

        var titleLabel = root.Q<Label>("TitleLabel"); // TODO: Add dragger
        var clearButton = root.Q<Button>("ClearButton");
        outputTextField = root.Q<TextField>("OutputTextField");
        var executeButton = root.Q<Button>("ExecuteButton");
        inputTextField = root.Q<TextField>("InputTextField");
        var closeButton = root.Q<Button>("CloseButton");
        builtInScriptDropdownField = root.Q<DropdownField>("BuiltInScriptDropdownField");

        // engine = new Engine(cfg => cfg.AllowClr()); // Free Version
        // var assembly = typeof(NavalGameState).Assembly;
        // engine = new Engine(cfg => cfg.AllowClr(assembly));

        // engine.SetValue("log", new Action<object>(msg => OnLog(msg)));
        // engine.SetValue("NavalGameState", TypeReference.CreateTypeReference<NavalGameState>(engine));

        executeButton.clicked += () =>
        {
            ScriptEngine.Instance.Execute(inputTextField.text);

            // try
            // {
            //     // engine.Execute(inputTextField.text);
            //     // var res = engine.Evaluate(inputTextField.text);
            //     var res = engine.Evaluate(inputTextField.text);
            //     var obj = res.ToObject();
            //     if (obj != null)
            //     {
            //         OnReturn(obj);
            //     }
            // }
            // catch (JavaScriptException ex)
            // {
            //     OnJSError(ex);
            // }
        };

        clearButton.clicked += () =>
        {
            outputTextField.SetValueWithoutNotify("");
        };

        closeButton.clicked += Hide;

        var readButton = root.Q<Button>("ReadButton");
        var writeButton = root.Q<Button>("WriteButton");

        readButton.clicked += () =>
        {
            var subUrl = builtInScriptDropdownField.text;
            if (!CheckSubPathVaild(subUrl))
                return;
            StartCoroutine(FetchScriptAndUpdate(subUrl));
        };

        writeButton.clicked += () =>
        {
            var subUrl = builtInScriptDropdownField.text;
            if (!CheckSubPathVaild(subUrl))
                return;

            OverwriteScript(subUrl, inputTextField.text);
        };

        // dragging support
        root.AddManipulator(new MyDragger());

        // var textAssets = Resources.LoadAll<TextAsset>("BuiltinEmbedScripts");
        // var names = textAssets.Select(ts => ts.name).ToList();
        // builtInScriptDropdownField.choices = names;
        builtInScriptDropdownField.RegisterValueChangedCallback(evt =>
        {
            StartCoroutine(FetchScriptAndUpdate(evt.newValue));
        });

        if (fileSystemNotWorkingPlatforms.Contains(Application.platform)) // FIXME: Fragile 
        {
            // StartCoroutine(UpdateBuiltInScriptDropdownFieldUsingManifest()); // Fire and Forget
            ManifestModelCache.Instance.CommitTask(manifestModel =>
            {
                builtInScriptDropdownField.choices = manifestModel.builtinScripts;
            });
        }
        else
        {
            UpdateBuiltinScriptDropdownFieldUsingFileSystem();
        }

        BindToScriptEngine();
    }

    void OnDisable()
    {
        UnbindToScriptEngine();
    }

    void BindToScriptEngine()
    {
        Debug.Log("BindToScriptEngine");

        var scriptEngine = ScriptEngine.Instance;
        scriptEngine.onLog += OnLog;
        scriptEngine.onJSError += OnJSError;
        scriptEngine.onReturn += OnReturn;
    }

    void UnbindToScriptEngine()
    {
        Debug.Log("UnbindToScriptEngine");

        var scriptEngine = ScriptEngine.Instance;
        scriptEngine.onLog -= OnLog;
        scriptEngine.onJSError -= OnJSError;
        scriptEngine.onReturn -= OnReturn;
    }

    bool CheckSubPathVaild(string subPath)
    {
        var subUrl = builtInScriptDropdownField.text;
        if (subUrl == "")
        {
            DialogRoot.Instance.PopupMessageDialog("Path is not valid");
            return false;
        }
        return true;
            
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

    void OverwriteScript(string subPath, string content)
    {
#if UNITY_ANDROID || UNITY_IOS && !UNITY_EDITOR
        DialogRoot.Instance.PopupMessageDialog("Mobile platform does not support writing to disk");
        return;
#elif UNITY_WEBGL && !UNITY_EDITOR
        DialogRoot.Instance.PopupMessageDialog("WEBGL platform does not support writing to disk");
        return;
#else
        var path = Application.streamingAssetsPath + subPath;
        File.WriteAllText(path, content);
#endif
    }

    public void UpdateBuiltinScriptDropdownFieldUsingFileSystem()
    {
        var files = Directory.GetFiles(Application.streamingAssetsPath + "/BuiltinScripts", "*.js");
        var paths = files.Select(s => s.Replace('\\', '/').Replace(Application.streamingAssetsPath, "")).ToList();
        builtInScriptDropdownField.choices = paths;
    }

    public void OnLog(object sender, object output)
    {
        Debug.Log(output);
        outputTextField.SetValueWithoutNotify(outputTextField.value + "[Log]: " + output + "\n");
    }

    public void OnJSError(object sender, JavaScriptException ex)
    {
        Debug.LogWarning(ex);
        outputTextField.SetValueWithoutNotify(outputTextField.value + "[Error]: " + ex + "\n");
    }

    public void OnReturn(object sender, object obj)
    {
        Debug.Log(obj);
        outputTextField.SetValueWithoutNotify(outputTextField.value + "[Return]: " + obj + "\n");
    }
}