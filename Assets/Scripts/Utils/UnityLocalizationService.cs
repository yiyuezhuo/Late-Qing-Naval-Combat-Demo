using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Localization;

using CoreUtils;
using YYZ;

public class UnityLocalizationService: ILocalizeService
{
    static Dictionary<string, LocalizedString> localizedStringMap = new Dictionary<string, LocalizedString>();

    public static string GetEnumTypeKey(Type enumType)
    {
        var fullName = enumType?.FullName ?? enumType?.Name ?? string.Empty;
        var lastNamespaceSeparator = fullName.LastIndexOf('.');
        var typeKey = lastNamespaceSeparator >= 0 ? fullName[(lastNamespaceSeparator + 1)..] : fullName;
        return typeKey.Replace('+', '.');
    }

    public static string GetEnumKey(Type enumType, object enumValue) => $"{GetEnumTypeKey(enumType)}.{enumValue}";

    public static IEnumerable<string> GetEnumKeys(Type enumType, object enumValue)
    {
        var fullKey = GetEnumKey(enumType, enumValue);
        yield return fullKey;

        var legacyKey = $"{enumType?.Name}.{enumValue}";
        if (!string.Equals(legacyKey, fullKey, StringComparison.Ordinal))
            yield return legacyKey;
    }


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

    public string GetEnum(Type enumType, object enumValue)
    {
        var keys = GetEnumKeys(enumType, enumValue).ToList();
        foreach (var key in keys)
        {
            var result = Get(key);
            if (!string.Equals(result, key, StringComparison.Ordinal))
                return result;
        }

        return keys[0];
    }

    public string GetEnum<T>(T enumValue) => GetEnum(typeof(T), enumValue);
    
    static UnityLocalizationService instance = new();
    public static UnityLocalizationService Instance => instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void RegisterToServiceLocator()
    {
        ServiceLocator.Register<ILocalizeService>(Instance);
    }
}
