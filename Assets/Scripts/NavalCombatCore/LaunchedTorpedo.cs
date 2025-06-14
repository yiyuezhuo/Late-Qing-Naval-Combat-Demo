using System.Collections.Generic;
using System.Data;
using System.Linq;
using System;
using GeographicLib;
using System.Xml.Serialization;
using System.Diagnostics;


namespace NavalCombatCore
{

    public partial class LaunchedTorpedo : IObjectIdLabeled, IDF4Model, ICollider
    {
        public string objectId { get; set; }
        public string shooterId;
        public ShipLog GetShooter() => EntityManager.Instance.Get<ShipLog>(shooterId); // shooter can sink
        public string desiredTargetObjectId;
        public ShipLog GetDesiredTarget() => EntityManager.Instance.Get<ShipLog>(desiredTargetObjectId); // torpedo may not attack its initial desired target but this can be labeled

        public LatLon position = new();
        public float speedKnots; // current speed vs "max" speed defined in the class
        public float headingDeg;
        public MapState mapState;
        public float lengthFoot = 11.58f;
        public float beamFoot = 1.3f;

        public float GetBeamFoot() => beamFoot;
        public float GetLengthFoot() => lengthFoot;
        public LatLon GetPosition() => position;

        public float GetLatitudeDeg() => position.LatDeg;
        public float GetLongitudeDeg() => position.LonDeg;
        public float GetHeadingDeg() => headingDeg;
        public float GetSpeedKnots() => speedKnots;

        public float maxRangeYards;
        public float movedDistanceYards;
        public TorpedoDamageClass damageClass;
        public GlobalString sourceName;

        public void StepMoveToNewPosition(float deltaSeconds)
        {
            var distNm = speedKnots / 3600 * deltaSeconds;
            var distM = distNm * 1852;
            double arcLength = Geodesic.WGS84.Direct(position.LatDeg, position.LonDeg, headingDeg, distM, out double lat2, out double lon2);

            var newPosition = new LatLon((float)lat2, (float)lon2);

            var newPositionBlocked = false;
            if (CoreParameter.Instance.checkLandCollision)
            {
                newPositionBlocked = ElevationService.Instance.GetElevation(newPosition) > 0;
            }

            if (!newPositionBlocked)
            {
                var collidedShipLog = NavalGameState.Instance.shipLogsOnMap.FirstOrDefault(other => other.objectId != shooterId && CollideUtils.IsCollided(newPosition, this, other));
                if (collidedShipLog != null)
                {
                    // TODO: Process torpedo attack
                    var logger = ServiceLocator.Get<ILoggerService>();
                    logger.LogWarning($"Torpedo {objectId} collides ship {collidedShipLog.namedShip.name.GetMergedName()}");
                    newPositionBlocked = true;
                }
            }

            if (newPositionBlocked)
            {
                mapState = MapState.Destroyed;
            }
            else
            {
                position = newPosition;
            }
            movedDistanceYards += distM * MeasureUtils.meterToYard;
            if (movedDistanceYards > maxRangeYards)
            {
                mapState = MapState.Destroyed;
            }
        }
    }
}