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
        // public string english = "unnamed";
        public string english = "none";
        public string japanese;
        public string chineseSimplified;
        public string chineseTraditional;
        public static LanguageType mergeMode = LanguageType.All;
        public static LanguageType shortMode = LanguageType.English;
        public static bool includeEnglishInMergedName = true;
        public static bool includeJapaneseInMergedName = true;
        public static bool includeSimplifiedChineseInMergedName = false;
        public static bool includeTraditionalChineseInMergedName = true;
        public string GetMergedNamePure()
        {
            var names = new List<string>();
            if (includeEnglishInMergedName)
                names.Add(english);
            if (includeJapaneseInMergedName)
                names.Add(japanese);
            if (includeSimplifiedChineseInMergedName)
                names.Add(chineseSimplified);
            if (includeTraditionalChineseInMergedName)
                names.Add(chineseTraditional);
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
                // LanguageType.English => NullIfWhiteSpace(english),
                LanguageType.English =>english,
                LanguageType.Japanese => FirstNonWhiteSpace(japanese, english),
                LanguageType.ChineseSimplified => FirstNonWhiteSpace(chineseSimplified, english),
                LanguageType.ChineseTraditional => FirstNonWhiteSpace(chineseTraditional, chineseSimplified, english),
                LanguageType.All => GetMergedNamePure(),
                _ => NullIfWhiteSpace(english)
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

        public GlobalString Add(string s) // In general, s should be pure numerical or simple enough acronym 
        {
            return Add(new GlobalString()
            {
                english = s,
                japanese = s,
                chineseSimplified = s,
                chineseTraditional = s
            });
        }

        string ValidAdd(string a, string b)
        {
            if (a == null || a == "" || b == null || b == "")
                return null;
            return a + b;
        }

        static string NullIfWhiteSpace(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        static string FirstNonWhiteSpace(params string[] values)
        {
            foreach (var value in values)
            {
                var normalized = NullIfWhiteSpace(value);
                if (normalized != null)
                    return normalized;
            }

            return null;
        }

        public bool EqualsAny(string str)
        {
            return english == str || japanese == str || chineseSimplified == str || chineseTraditional == str;
        }

        public bool MatchAny(string str)
        {
            return (english != null && english.Contains(str)) 
                || (japanese != null && japanese.Contains(str))
                || (chineseSimplified != null && chineseSimplified.Contains(str))
                || (chineseTraditional != null && chineseTraditional.Contains(str));
        }

        public override string ToString()
        {
            return $"GlobalString({english}, {japanese}, {chineseSimplified}, {chineseTraditional})";
        }

        public static GlobalString redStr = new()
        {
            english = "Red",
            japanese = "赤",
            chineseSimplified = "红",
            chineseTraditional = "紅",
        };

        public static GlobalString blueStr = new()
        {
            english = "Blue",
            japanese = "青",
            chineseSimplified = "蓝",
            chineseTraditional = "藍",
        };

    }
}
