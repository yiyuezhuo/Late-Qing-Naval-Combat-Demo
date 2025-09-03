using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using System;
// using UnityEngine.SocialPlatforms;

public static class MyLocale
{
    static Dictionary<string, LocalizedString> localizedStringMap = new Dictionary<string, LocalizedString>();
    public static string Get(string key, params object[] args)
    {
        if (!localizedStringMap.TryGetValue(key, out var localizedString))
        {
            localizedString = new LocalizedString("Standard Table", key);
            localizedStringMap[key] = localizedString;
        }
        // var s1 = Get(key);
        // var s2 = localizedString.GetLocalizedStringAsync();
        return localizedString.GetLocalizedString(args); // null is possible
    }
    public static string GetFor(object obj)
    {
        return Get(obj.ToString());
    }
}