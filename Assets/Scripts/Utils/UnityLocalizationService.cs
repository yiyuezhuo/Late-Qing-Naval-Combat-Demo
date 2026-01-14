using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Localization;

using CoreUtils;
using YYZ;

public class UnityLocalizationService: ILocalizeService
{
    static Dictionary<string, LocalizedString> localizedStringMap = new Dictionary<string, LocalizedString>();


    public string Get(string key, params object[] args)
    {
        if (!localizedStringMap.TryGetValue(key, out var localizedString))
        {
            localizedString = new LocalizedString("Dynamic Table", key);
            localizedStringMap[key] = localizedString;
        }

        var ret = localizedString.GetLocalizedString(args);  // null is possible
        if (ret == null || ret.StartsWith("No translation found"))
        {
            ret = key; // TODO: Temp workaround
        }
        
        return ret;

    }

    public string GetFor(object obj)
    {
        return Get(obj.ToString());
    }

    public string GetEnum<T>(T enumValue) => Get($"{typeof(T).Name}.{enumValue}");
    
    static UnityLocalizationService instance = new();
    public static UnityLocalizationService Instance => instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void RegisterToServiceLocator()
    {
        ServiceLocator.Register<ILocalizeService>(Instance);
    }
}