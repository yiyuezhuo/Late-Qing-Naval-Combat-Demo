using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public static class ResourceManager
{
    public static Dictionary<string, StyleBackground> styleBackgroundMap = new();

    public static StyleBackground GetStyleBackground(string name)
    {
        if(!styleBackgroundMap.TryGetValue(name, out var styleBackground))
        {
            var sprite = Resources.Load<Sprite>(name);
            styleBackground = styleBackgroundMap[name] = new StyleBackground(sprite);
        }
        return styleBackground;
    }

    public static StyleBackground GetFlag(string countryCode)
    {
        return GetStyleBackground("Flags/" + countryCode);
    }

    public static StyleBackground GetShipPortrait(string shipPortraitCode)
    {
        return GetStyleBackground("Ship_Portraits/" + shipPortraitCode);
    }

    public static StyleBackground GetLeaderPortrait(string leaderPortraitCode)
    {
        return GetStyleBackground("Leader_Portraits/" + leaderPortraitCode);
    }
}