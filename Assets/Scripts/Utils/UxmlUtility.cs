using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine.Bindings;

// Copied from UnityCsReference (partially)

public static class UxmlUtility
{
    public static Type ParseType(string value, Type defaultType = null)
    {
        try
        {
            if (!string.IsNullOrEmpty(value))
            {
                var type = Type.GetType(value, true);
                return type;
            }
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogException(e);
        }
        return defaultType;
    }

    public static string TypeToString(Type value)
    {
        if (value == null)
            return null;
        return $"{value.FullName}, {value.Assembly.GetName().Name}";
    }
}
