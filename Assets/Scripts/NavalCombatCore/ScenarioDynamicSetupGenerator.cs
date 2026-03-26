using System.Collections.Generic;
using System.Linq;
using CoreUtils;
using GeographicLib;


namespace NavalCombatCore
{
    public class ScenarioDynamicSetupGenerator
    {
        public float distanceYards = 12000;
        public float angleDeg = 45;
        public LatLon anchor;

        public void Setup()
        {
            var gameState = NavalGameState.Instance;
            var topShipGroups = gameState.shipGroups.Where(g => g.parentObjectId == null).ToList();

            if (topShipGroups.Count == 0)
            {
            }
            else if (topShipGroups.Count == 1)
            {
                SetupShipGroup(topShipGroups[0], ref anchor, angleDeg);
            }
            else
            {
                // var innerDist = MeasureUtils.yardToMeter * 500;
                var centerDistMeter = MeasureUtils.yardToMeter / 2 * distanceYards;

                var angleDeg2 = MeasureUtils.NormalizeAngle(angleDeg + 180);
                Geodesic.WGS84.Direct(anchor.LatDeg, anchor.LonDeg, angleDeg, centerDistMeter, out double lat, out double lon);
                Geodesic.WGS84.Direct(anchor.LatDeg, anchor.LonDeg, angleDeg2, centerDistMeter, out double lat2, out double lon2);

                var groupAnchor = new LatLon((float)lat, (float)lon);
                var groupAnchor2 = new LatLon((float)lat2, (float)lon2);

                SetupShipGroup(topShipGroups[0], ref groupAnchor, angleDeg);
                SetupShipGroup(topShipGroups[1], ref groupAnchor2, angleDeg2);
            }
        }

        public void SetupShipGroup(ShipGroup group, ref LatLon groupAnchor, float groupHeadingDegree)
        {
            var innerDist = MeasureUtils.yardToMeter * 500;

            var subShips = CollectDirectSubShips(group);
            if (subShips.Count > 0)
            {
                var speedKnots = subShips.Min(shipLog => shipLog.GetMaxSpeedKnots());
                ShipLog prevShip = null;
                foreach (var subShipLog in subShips)
                {
                    subShipLog.mapState = MapState.Deployed;
                    subShipLog.position = groupAnchor;
                    var headingDeg = MeasureUtils.NormalizeAngle(groupHeadingDegree + 180);
                    subShipLog.headingDeg = subShipLog.desiredHeadingDeg = headingDeg;
                    subShipLog.speedKnots = subShipLog.desiredSpeedKnots = speedKnots;
                    subShipLog.MarkNonPhysicalPoseChanged();

                    Geodesic.WGS84.Direct(groupAnchor.LatDeg, groupAnchor.LonDeg, groupHeadingDegree, innerDist, out double lat2, out double lon2);
                    groupAnchor = new LatLon((float)lat2, (float)lon2);

                    if (prevShip == null)
                    {
                        subShipLog.controlMode = ControlMode.Independent;
                    }
                    else
                    {
                        subShipLog.controlMode = ControlMode.FollowTarget;
                        subShipLog.followDistanceYards = 500;
                        subShipLog.followedTargetObjectId = prevShip.objectId;
                    }
                    prevShip = subShipLog;
                }
            }

            var subGroups = CollectDirectSubGroups(group);
            foreach (var subGroup in subGroups)
            {
                SetupShipGroup(subGroup, ref groupAnchor, groupHeadingDegree);
            }
        }

        public List<ShipLog> CollectDirectSubShips(ShipGroup group)
        {
            return group.childrenObjectIds.Select(id => EntityManager.Instance.Get<ShipLog>(id))
                .Where(shipLog => shipLog != null && shipLog.mapState != MapState.Destroyed)
                .ToList();
        }

        public List<ShipGroup> CollectDirectSubGroups(ShipGroup group)
        {
            return group.childrenObjectIds.Select(id => EntityManager.Instance.Get<ShipGroup>(id))
                .Where(shipLog => shipLog != null)
                .ToList();
        }

    }
}
