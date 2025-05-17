using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;
using System;
using System.Collections.Generic;
using TMPro;


#if UNITY_EDITOR
using UnityEditor;
#endif

public static class RegisteredConverters
{
#if UNITY_EDITOR
    [InitializeOnLoadMethod]
#else
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
#endif
    public static void RegisterConverters()
    {
        Debug.Log("RegisterConverters");

        Register("ShipClass => ShipClass's merge name", (ref NavalCombatCore.ShipClass shipClass) =>
        {
            return shipClass?.name?.GetMergedName() ?? "[not defined]";
        });
    }


    static void Register<TSource, TDestination>(string name, TypeConverter<TSource, TDestination> converter)
    {
        var group = new ConverterGroup(name);
        group.AddConverter(converter);
        ConverterGroups.RegisterConverterGroup(group);
    }
}