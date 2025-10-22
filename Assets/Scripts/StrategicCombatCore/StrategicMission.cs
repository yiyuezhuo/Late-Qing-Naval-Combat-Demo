using System.Collections.Generic;
using CoreUtils;

namespace StrategicCombatCore
{
    public partial class LandUnitReference
    {
        public string objectId;

        public LandUnit Get() => EntityManager.Instance.Get<LandUnit>(objectId);
    }

    public partial class StrategicMission : IObjectIdLabeled
    {
        public string objectId { get; set; }

        public IEnumerable<IObjectIdLabeled> GetSubObjects()
        {
            yield break;
        }

        // General Parameter
        public GlobalString name = new();
        public List<StrategicGroupMemberReference> groups = new(); // assigned groups
        public List<XY> waypoints = new();

        public enum MissionType
        {
            Patrol,
            Supply, // Transports load supplies from host and transfer to detination.
            NavalTransfer // Load is handled by player before task launched. Used for unland army unit or amphibious assault.
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
            AssemblingAndLoading,
            StartToDestinationAndUnloading,
            DestinationToStartAndLoading
        }

        public SupplyState supplyState;

        // public StrategicGroupMemberReference startHq;
        // public StrategicGroupMemberReference destinationHq;
        // public string sourceDepotObjectId;
        // public string targetDepotObjectId;
        public LandUnitReference sourceDepotReference = new();
        public LandUnitReference targetDepotReference = new();

        public enum NavalTransferState
        {
            Assembling,
            StartToDestination,
            DestinationToStart,
            Completed
        }

        public NavalTransferState navalTransferState;

        // public List<StrategicGroupMemberReference> loadTargetGroups = new();
        // Non-Fleet groups in assigned groups are transported groups.
        // Naval Transfer's destination is the end cell of waypoint.

        public Cell GetWaypointStartCell()
        {
            var xy = waypoints[0];
            return waypoints.Count == 0 ? null : StrategicGameState.Instance.cellMatrix[xy.x, xy.y];
        }

        public Cell GetWaypointDestinationCell()
        {
            var xy = waypoints[^1];
            return waypoints.Count == 0 ? null : StrategicGameState.Instance.cellMatrix[xy.x, xy.y];
        }


        public IEnumerable<T> WalkGroupMembers<T>() where T: IStrategicGroupMemberReferenceable
        {
            foreach(var groupRef in groups)
            {
                var group = groupRef.Get() as StrategicGroup;
                if(group != null)
                {
                    foreach(var obj in group.WalkGroupMembers<T>())
                    {
                        yield return obj;
                    }
                }
            }
        }
    }
}