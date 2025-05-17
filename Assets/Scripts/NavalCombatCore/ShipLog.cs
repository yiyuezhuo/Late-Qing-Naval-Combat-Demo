using System.Collections.Generic;
using System.Data;

namespace NavalCombatCore
{
    public class BatteryAmmunitionRecord
    {
        public int ArmorPiercing;
        public int semiArmorPiercing;
        public int common;
        public int highExplosive;
    }

    public class ShipLog
    {
        // public ShipClass shipClass;
        public string shipClassStr;
        public GlobalString captain;
        public int crewRating;
        public List<BatteryAmmunitionRecord> batteryAmmunitionRecords = new() { new() };
        public int torpedos;
        public float damagePoints;
    }
}