using NavalCombatCore;

namespace StrategicCombatCore
{
    public enum DamageRepairType
    {
        ShipOperationalState, // Major, Abandon Ship, Flooding Obstruction => Operational
        MaxSpeedOffet,
        AccelerationOffset,
        EngineRoomHits,
        PropulsionShaftHis, // Major
        EngineRoomFlooding, // Major
        BoilerRoomFlooding, // Major
        DamageControlRatingHit,
        PortSearchlightHits,
        StartboardSearchlightHits,
        SmokeGeneratorDisabled,
        SubState,
        BatteryMountStatus,
        FiringControlSystemState,
        TorpedoMountStatus,
        RapidFireBatteryPortMountHits,
        RapidFireBatteryStartboardMountHits,
        RapidFireBatteryFireControlHits,
    }

    public class DamageRepairRecord
    {
        // Callback or a dedicated structure to reference a repair? (EX: engineRoomHits -=1 for a ship)
        public float priorityLevel1; // Human given, for example, high priority may be given to a ship
        public float priorityLevel2; // Generated
        public float repairPointCost;

        public void Extract(ShipLog shipLog)
        {
            
        }
    }

    public class DamageRepairResolver
    {
        
    }
}