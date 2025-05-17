using System;
using System.Collections.Generic;
using System.Linq;

namespace NavalCombatCore
{
    [Serializable]
    public class GlobalString
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
    }
}