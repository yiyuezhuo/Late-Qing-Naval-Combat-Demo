using System.Collections.Generic;
using CoreUtils;

namespace StrategicCombatCore
{
    public partial class StrategicMission : IObjectIdLabeled
    {
        public string objectId { get; set; }

        public IEnumerable<IObjectIdLabeled> GetSubObjects()
        {
            yield break;
        }

        // General Parameter
        public GlobalString name = new();
        public List<StrategicGroupMemberReference> groups = new();
        public List<XY> waypoints = new();

        public enum MissionType
        {
            Patrol,
            Supply, // Transports load supplies from host and transfer to detination.
            OneWayUnload // Load is handled by player before task launched. Used for unland army unit or amphibious assault.
        }

        public MissionType type = MissionType.Patrol;


        public enum PatrolState
        {
            Assembling,
            StartToDestination,
            DestinationToStart
        }

        public PatrolState patrolState;

        public enum SupplyState
        {
            Assembling,
            Loading,
            StartToDestination,
            Unloading,
            DestinationToStart
        }

        public SupplyState supplyState;

        public StrategicGroupMemberReference startHq;
        public StrategicGroupMemberReference destinationHq;

        public enum OneWayUnloadState
        {
            Assembling,
            StartToDestination
        }
        
        public OneWayUnloadState oneWayUnloadState;
    }
}