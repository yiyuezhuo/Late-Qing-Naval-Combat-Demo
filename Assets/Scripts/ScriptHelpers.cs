using System;
using System.Linq;

namespace NavalCombatCore
{
    public static class ScriptHelpers
    {
        public static ShipLog GetShipLogByName(string name)
        {
            var shipLogs = NavalGameState.Instance.shipLogs;

            var r = shipLogs.FirstOrDefault(x => x.namedShip.name.english == name);
            if (r != null)
                return r;
            r = shipLogs.FirstOrDefault(x => x.namedShip.name.japanese == name);
            if (r != null)
                return r;
            r = shipLogs.FirstOrDefault(x => x.namedShip.name.chineseSimplified == name);
            if (r != null)
                return r;
            return shipLogs.FirstOrDefault(x => x.namedShip.name.chineseTraditional == name);
        }
    }
}