using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using CoreUtils;
using NavalCombatCore;


namespace StrategicCombatCore
{
    public class SubStrategicCombatItem
    {
        public StrategicGroupMemberReference groupReference = new();
        public float commitPercent; // 0f ~ 1f
    }

    public enum SubStrategicCombatType
    {
        Fire,
        Assault
    }

    public class SubStrategicCombat // A sub-combat denotes a general infantry assault, roughtly a scenario in the Squad Battle
    {
        public List<SubStrategicCombatItem> attackers = new();
        public List<SubStrategicCombatItem> defenders = new();
        public SubStrategicCombatType type;
        public int currentTurn; // generally turn = 5min, roughly a turn in the Squad Battle, 
        public int maxTurn = 10;
    }
}