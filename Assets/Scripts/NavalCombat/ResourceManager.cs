using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public static class ResourceManager
{
    public static Dictionary<string, StyleBackground> styleBackgroundMap = new();
    public static Dictionary<string, Sprite> spriteMap = new();

    public static StyleBackground GetStyleBackground(string name)
    {
        if (!styleBackgroundMap.TryGetValue(name, out var styleBackground))
        {
            var sprite = Resources.Load<Sprite>(name);
            styleBackground = styleBackgroundMap[name] = new StyleBackground(sprite);
        }
        return styleBackground;
    }

    public static StyleBackground GetFlagSB(string countryCode)
    {
        return GetStyleBackground("Flags/" + countryCode);
    }

    public static StyleBackground GetShipPortraitSB(string shipPortraitCode)
    {
        return GetStyleBackground("Ship_Portraits/" + shipPortraitCode);
    }

    public static StyleBackground GetLeaderPortraitSB(string leaderPortraitCode)
    {
        return GetStyleBackground("Leader_Portraits/" + leaderPortraitCode);
    }

    public static Sprite GetSprite(string name)
    {
        if (!spriteMap.TryGetValue(name, out var sprite))
        {
            sprite = spriteMap[name] = Resources.Load<Sprite>(name);
        }
        return sprite;
    }

    public static Sprite GetFlagSprite(string countryCode)
    {
        return GetSprite("Flags/" + countryCode);
    }

    public static Sprite GetShipPortraitSprite(string shipPortraitCode)
    {
        return GetSprite("Ship_Portraits/" + shipPortraitCode);
    }

    public static Sprite GetLeaderPortraitSprite(string leaderPortraitCode)
    {
        return GetSprite("Leader_Portraits/" + leaderPortraitCode);
    }
}