namespace CoreUtils
{
    public partial class EntityManager
    {
        public NavalCombatCore.ShipLog GetOnMapShipLog(string id)
        {
            var shipLog = Get<NavalCombatCore.ShipLog>(id);
            if (shipLog == null || !shipLog.IsOnMap())
                return null;
            return shipLog;
        }
    }
}