using System;
using System.Collections.Generic;
using System.Linq;

namespace NavalCombatCore
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
        public string GetMergedName()
        {
            var names = new List<string>() { english, japanese, chineseSimplified, chineseTraditional };
            return string.Join("/", names.Where(n => n != null && n.Length > 0));
        }
        public string GetNameFromType(LanguageType type)
        {
            return type switch
            {
                LanguageType.English => english,
                LanguageType.Japanese => japanese,
                LanguageType.ChineseSimplified => chineseSimplified,
                LanguageType.ChineseTraditional => chineseTraditional,
                LanguageType.All => mergedName,
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
    }
}