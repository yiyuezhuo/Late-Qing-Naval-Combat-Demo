using Jint;
using System;
using UnityEngine;
using Jint.Runtime;
using NavalCombatCore;
using Jint.Runtime.Interop;
using System.Linq;
using System.Collections;
using UnityEngine.Localization.Settings;


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
        engine.SetValue("GameManager", TypeReference.CreateTypeReference<GameManager>(engine));
        engine.SetValue("msgBox", new Action<object>(Msg));
        engine.SetValue("msgBoxDelay", new Action<object, object>(MsgDelay));
        engine.SetValue("getShipLogByName", new Func<object, object>(GetShipLogByName));
        engine.SetValue("getDistanceYard", new Func<object, object, float>(GetDistanceYard));
        engine.SetValue("getPositiveAngleDifference", new Func<object, object, float>(GetPositiveAngleDifference));
        engine.SetValue("calculateInitialBearing", new Func<object, object, float>(CalculateInitialBearing));
        engine.SetValue("measure", new Func<object, object, MeasureStats>(Measure));
        engine.SetValue("getLocalized", new Func<object, object, object, object, string>(GetLocalizedObj));
        // GetPositiveAngleDifference
        // engine.SetValue("getUnitByName", )
        // engine.SetValue("msgBoxDelay", new Action<object, object>((msg, seconds) => {
        //     DialogRoot.Instance.PopupMessageDialog(msg as string));
        // });
    }

    static string GetLocalizedObj(object english, object japanese, object chineseSimplified, object chineseTraditional)
        => GetLocalized(english as string, japanese as string, chineseSimplified as string, chineseTraditional as string);

    static string GetLocalized(string english, string japanese, string chineseSimplified, string chineseTraditional)
    {
        var name = LocalizationSettings.SelectedLocale.Identifier.CultureInfo.Name;
        switch (name)
        {
            case "en":
                return english;
            case "ja":
                return japanese;
            case "zh-Hans":
                return chineseSimplified;
            case "zh-Hant":
                return chineseTraditional;
        }
        return english;
    }

    static MeasureStats Measure(object arg1, object arg2)
    {
        if (arg1 is ShipLog shipLog1 && arg2 is ShipLog shipLog2)
        {
            return MeasureStats.Measure(shipLog1, shipLog2);
        }
        return null;
    }

    static float GetPositiveAngleDifference(object arg1, object arg2)
    {
        if (arg1 is ShipLog shipLog1)
        {
            if (arg2 is ShipLog shipLog2)
            {
                return MeasureUtils.GetPositiveAngleDifference(shipLog1.GetHeadingDeg(), shipLog2.GetHeadingDeg());
            }
        }
        if (arg1 is double heading1)
        {
            if (arg2 is double heading2)
            {
                return MeasureUtils.GetPositiveAngleDifference((float)heading1, (float)heading2);
            }
        }
        return -1;
    }

    static float CalculateInitialBearing(object arg1, object arg2)
    {
        if (arg1 is ShipLog shipLog1 && arg2 is ShipLog shipLog2)
        {
            var angle = (float)MeasureStats.Approximation.CalculateInitialBearing(shipLog1, shipLog2);
            return angle;
        }
        return -1;
    }

    static float GetDistanceYard(object ship1, object ship2)
    {
        var shipLog1 = ship1 as ShipLog;
        var shipLog2 = ship2 as ShipLog;
        if (shipLog1 == null || shipLog2 == null)
            return -1;
        var distKm = MeasureStats.Approximation.HaversineDistanceKm(shipLog1.position.LatDeg, shipLog1.position.LonDeg, shipLog2.position.LatDeg, shipLog2.position.LonDeg);
        var distYards = MeasureUtils.kilometerToYard * (float)distKm;
        return distYards;
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
        MsgDelay(msg, 0.0);
    }

    static IEnumerator DoMsgDelay(string msg, float seconds)
    {
        // Tutorial prompts should still appear when naval simulation is paused (timeScale = 0).
        yield return new WaitForSecondsRealtime(seconds);
        DialogRoot.Instance.PopupMessageDialog(msg);
        // NavalGameState.Instance.tempSubjectLogs.A
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
        if (onJSError == null) // if no handler (for example, JS Console will fetch and output it) then rethrow to avoid silent failure
        {
            throw ex;
        }
        // Debug.LogError(ex);
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
