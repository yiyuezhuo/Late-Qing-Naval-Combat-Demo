using System;
using System.Collections.Generic;
using System.Linq;

namespace CoreUtils
{
    public enum LanguageType
    {
        English,
        Japanese,
        ChineseSimplified,
        ChineseTraditional,
        All,
    }

    // [Serializable]
    public partial class GlobalString
    {
        public string english = "unnamed";
        public string japanese;
        public string chineseSimplified;
        public string chineseTraditional;
        public static LanguageType mergeMode = LanguageType.All;
        public static LanguageType shortMode = LanguageType.English;
        public string GetMergedNamePure()
        {
            var names = new List<string>() { english, japanese, chineseSimplified, chineseTraditional };
            return string.Join("/", names.Where(n => n != null && n.Length > 0));
        }
        public string GetMergedName()
        {
            return GetNameFromType(mergeMode);
        }
        public string GetShortName()
        {
            return GetNameFromType(shortMode);
        }
        public string GetNameFromType(LanguageType type)
        {
            return type switch
            {
                LanguageType.English => english,
                LanguageType.Japanese => japanese ?? english,
                LanguageType.ChineseSimplified => chineseSimplified ?? english,
                LanguageType.ChineseTraditional => chineseTraditional ?? english,
                LanguageType.All => GetMergedNamePure(),
                _ => english
            };
        }

        public GlobalString Clone()
        {
            return new()
            {
                english = english,
                japanese = japanese,
                chineseSimplified = chineseSimplified,
                chineseTraditional = chineseTraditional
            };
        }

        public GlobalString Add(GlobalString other)
        {
            return new()
            {
                english = ValidAdd(english, other.english),
                japanese = ValidAdd(japanese, other.japanese),
                chineseSimplified = ValidAdd(chineseSimplified, other.chineseSimplified),
                chineseTraditional = ValidAdd(chineseTraditional, other.chineseTraditional)
            };
        }

        string ValidAdd(string a, string b)
        {
            if (a == null || a == "")
                return null;
            return a + b;
        }
    }
}