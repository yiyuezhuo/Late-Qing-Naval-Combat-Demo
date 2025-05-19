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

        Register("Bool to DisplayStyle", (ref bool isShow) => (StyleEnum<DisplayStyle>)(isShow ? DisplayStyle.Flex : DisplayStyle.None));
        Register("Bool to DisplayStyle (Not)", (ref bool isShow) => (StyleEnum<DisplayStyle>)(isShow ? DisplayStyle.None : DisplayStyle.Flex));
        Register("object to DisplayStyle", (ref object obj) => (StyleEnum<DisplayStyle>)(obj != null ? DisplayStyle.Flex : DisplayStyle.Flex));
        Register("object to DisplayStyle (Not)", (ref object obj) => (StyleEnum<DisplayStyle>)(obj == null ? DisplayStyle.Flex : DisplayStyle.None));
        Register("ShipClass to DisplayStyle", (ref NavalCombatCore.ShipClass obj) => (StyleEnum<DisplayStyle>)(obj != null ? DisplayStyle.Flex : DisplayStyle.None));
        Register("ShipClass to DisplayStyle (Not)", (ref NavalCombatCore.ShipClass obj) => (StyleEnum<DisplayStyle>)(obj == null ? DisplayStyle.Flex : DisplayStyle.None));
    }


    static void Register<TSource, TDestination>(string name, TypeConverter<TSource, TDestination> converter)
    {
        var group = new ConverterGroup(name);
        group.AddConverter(converter);
        ConverterGroups.RegisterConverterGroup(group);
    }
}