using Jint;
using System;
using UnityEngine;
using Jint.Runtime;
using NavalCombatCore;
using Jint.Runtime.Interop;
using System.Linq;
using System.Collections;

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
        engine.SetValue("getShipLogByName", new Func<object, object>(GetShipLogByName));
        // engine.SetValue("getUnitByName", )
        // engine.SetValue("msgBoxDelay", new Action<object, object>((msg, seconds) => {
        //     DialogRoot.Instance.PopupMessageDialog(msg as string));
        // });
    }

    static ShipLog GetShipLogByName(object name)
    {
        var _name = name as string;
        if (name != null)
        {
            return NavalGameState.Instance.shipLogs.FirstOrDefault(x => x.namedShip.name.EqualsAny(_name));
        }
        return null;
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

    public void Reset()
    {
        Initialize();
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
