using System.Collections.Generic;
using System.Linq;

namespace NavalCombatCore
{
    public enum TrackingSystemState
    {
        // If a shooter fire at a target without tracking (FCS is destroyed, too many targeting or barrage fire), the firing is subject to Local Control modifier (-50% FC)
        Idle, // Tracking Position is not tracking anything.
        Destroyed,
        BeginTracking, // -50% ROF, -2 FC, BeginTracking will transition to Tracking once tracking is maintained at least 2 min 
        Tracking,
        Hitting // +2 FC, If target is hit by shooter, Hitting state will matain 2 min unless another hit is scored. 
    }

    public partial class FireControlSystemStatusRecord : UnitModule
    {
        // public string objectId { set; get; }
        public string targetObjectId { get; set; }
        public ShipLog GetTarget() => EntityManager.Instance.GetOnMapShipLog(targetObjectId);
        public TrackingSystemState trackingState;
        public float trackingSeconds;

        // public override IEnumerable<IObjectIdLabeled> GetSubObjects()
        // {
        //     yield break;
        // }

        public bool IsOperational()
        {
            if (trackingState == TrackingSystemState.Destroyed)
                return false;

            if (GetSubStates<IBatteryFireContrlStatusModifier>().Any(m => m.GetBatteryFireControlDisabled()))
                return false;

            return true;
        }

        public void Step(float deltaSeconds)
        {
            var target = GetTarget();
            if (target != null)
            {
                trackingSeconds += deltaSeconds;
                if (trackingSeconds >= 120)
                {
                    if (trackingState == TrackingSystemState.BeginTracking)
                    {
                        trackingState = TrackingSystemState.Tracking;
                    }
                }
            }
        }

        public void ResetToIntegrityState()
        {
            targetObjectId = null;
            trackingState = TrackingSystemState.Idle;
            trackingSeconds = 0;
        }

        public void SetTrackingTarget(ShipLog target)
        {
            if (target == null)
            {
                targetObjectId = null;
                trackingState = TrackingSystemState.Idle;
                trackingSeconds = 0;
            }
            var currentTarget = GetTarget();
            if (currentTarget == target)
                return;

            targetObjectId = target?.objectId;
            trackingState = TrackingSystemState.BeginTracking;
            trackingSeconds = 0;
        }

        public int GetSubIndex()
        {
            var batteryStatus = EntityManager.Instance.GetParent<BatteryStatus>(this);
            return batteryStatus.fireControlSystemStatusRecords.IndexOf(this);
        }
    }
}