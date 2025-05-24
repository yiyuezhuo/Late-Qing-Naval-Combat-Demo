using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;
using System;
using System.Collections.Generic;
using TMPro;
using System.Linq;
using NavalCombatCore;



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

        Register("ShipLog to DisplayStyle", (ref NavalCombatCore.ShipLog obj) => (StyleEnum<DisplayStyle>)(obj != null ? DisplayStyle.Flex : DisplayStyle.None));
        Register("ShipLog to DisplayStyle (Not)", (ref NavalCombatCore.ShipLog obj) => (StyleEnum<DisplayStyle>)(obj == null ? DisplayStyle.Flex : DisplayStyle.None));

        Register("ShipLog => ShipLog's merge name", (ref NavalCombatCore.ShipLog shipLog) =>
        {
            return shipLog?.name?.GetMergedName() ?? "[not defined]";
        });

        Register("ShipLog => ShipLog's ShipClass's DP", (ref NavalCombatCore.ShipLog shipLog) =>
        {
            return shipLog.shipClass?.damagePoint ?? 0;
        });

        Register("ShipLog => ShipLog's ShipClass's Name", (ref NavalCombatCore.ShipLog shipLog) =>
        {
            return shipLog.shipClass?.name;
        });

        Register("List<float> => Count", (ref List<float> floatList) =>
        {
            return floatList.Count;
        });

        Register("BatteryStatus => BatteryStatus's BatteryRecord's Name", (ref NavalCombatCore.BatteryStatus batteryStatus) =>
        {
            return batteryStatus?.batteryRecord?.name.mergedName ?? "Class Data Not Resolved";
        });

        Register("BatteryRecord => DisplayStyle", (ref NavalCombatCore.BatteryRecord batteryRecord) =>
        {
            return (StyleEnum<DisplayStyle>)(batteryRecord != null ? DisplayStyle.Flex : DisplayStyle.None);
        });

        Register("ShipGroup => DisplayStyle", (ref NavalCombatCore.ShipGroup shipGroup) =>
        {
            return (StyleEnum<DisplayStyle>)(shipGroup != null ? DisplayStyle.Flex : DisplayStyle.None);
        });

        Register("ShipGroup => DisplayStyle (Not)", (ref NavalCombatCore.ShipGroup shipGroup) =>
        {
            return (StyleEnum<DisplayStyle>)(shipGroup == null ? DisplayStyle.Flex : DisplayStyle.None);
        });

        Register("IShipGroupMember'object ID => string", (ref string objectId) =>
        {
            var obj = EntityManager.Instance.Get<IShipGroupMember>(objectId);
            if (obj is ShipGroup sg)
                return sg.name.mergedName ?? "[Not Specified SG]";
            if (obj is ShipLog sl)
                return sl.name.mergedName ?? "[Not Specified SL]";
            return "[Not Specified SGM]";
        });
    }

    // static ShipClass GetShipClassOfShipLog(NavalCombatCore.ShipLog shipLog)
    // {
    //     return shipLog.shipClass;
    //     // return GameManager.Instance.navalGameState.shipClasses.FirstOrDefault(x => x.name.english == shipLog.shipClassStr);
    // }


    static void Register<TSource, TDestination>(string name, TypeConverter<TSource, TDestination> converter)
    {
        var group = new ConverterGroup(name);
        group.AddConverter(converter);
        ConverterGroups.RegisterConverterGroup(group);
    }
}