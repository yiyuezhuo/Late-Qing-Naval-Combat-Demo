using System.Collections.Generic;
using System.Data;
using System.Linq;
using System;
using GeographicLib;


namespace NavalCombatCore
{
    public class BatteryAmmunitionRecord
    {
        public int ArmorPiercing;
        public int semiArmorPiercing;
        public int common;
        public int highExplosive;

        public string Summary()
        {
            var words = new List<string>();
            foreach ((var w, var num) in new (string, int)[]{
                ("AP", ArmorPiercing),
                ("SAP", semiArmorPiercing),
                ("COM", common),
                ("HE", highExplosive)
            })
            {
                if (num > 0)
                {
                    words.Add($"{w}: {num}");
                }
            }

            return string.Join(", ", words);
        }
    }

    public enum MountStatus
    {
        Operational,
        Disabled, // may restore after a period of time or by die roll
        Destroyed
    }

    public partial class MountStatusRecord : IObjectIdLabeled
    {
        public string objectId { get; set; }
        public MountStatus status; // Is "SectorStatus" a better name?
                                   // public int mountsDestroyed;
        public string firingTargetObjectId;
        public ShipLog GetFiringTarget() => EntityManager.Instance.Get<ShipLog>(firingTargetObjectId);

        public class MountLocationRecordInfo
        {
            public int recordIndex;
            public int subIndex;
            public MountLocationRecord record;

            public string Summary() // Used in Ship Log Editor
            {
                return $"#{recordIndex + 1} #{subIndex + 1} x{record.barrels} {record.mountLocation} ({record.SummaryArcs()})";
            }
        }

        MountLocationRecordInfo GetMountLocationRecordInfo(List<MountLocationRecord> mountLocationRecords, int mountIdx)
        {
            if (mountIdx < 0)
                return null;

            var _recordIndex = 0;
            var mntLocRecs = mountLocationRecords;
            while (_recordIndex < mntLocRecs.Count && mntLocRecs[_recordIndex].mounts <= mountIdx)
            {
                mountIdx -= mntLocRecs[_recordIndex].mounts;
                _recordIndex++;
            }
            if (_recordIndex < mntLocRecs.Count && mountIdx < mntLocRecs[_recordIndex].mounts)
            {
                return new()
                {
                    recordIndex = _recordIndex,
                    subIndex = mountIdx,
                    record = mntLocRecs[_recordIndex],
                };
            }
            return null;
        }

        public MountLocationRecordInfo GetMountLocationRecordInfo()
        {

            var battery = EntityManager.Instance.GetParent<BatteryStatus>(this);

            if (battery == null)
                return null;

            var mountIdx = battery.mountStatus.IndexOf(this);

            var batteryRecord = battery.GetBatteryRecord();
            if (batteryRecord == null)
                return null;

            var ret = GetMountLocationRecordInfo(batteryRecord.mountLocationRecords, mountIdx);
            // ret.isTorpedo = false;
            return ret;
        }

        public MountLocationRecordInfo GetTorpedoMountLocationRecordInfo()
        {
            var shipLog = EntityManager.Instance.GetParent<ShipLog>(this);
            if (shipLog == null)
                return null;

            var mountIdx = shipLog.torpedoSectorStatus.mountStatus.IndexOf(this);

            var shipClass = shipLog.shipClass;
            if (shipClass == null)
                return null;

            if (mountIdx < 0)
                return null;

            // var mountLocationRecord = shipClass.torpedoSector.mountLocationRecords[mountIdx];
            // return mountLocationRecord;
            var ret = GetMountLocationRecordInfo(shipClass.torpedoSector.mountLocationRecords, mountIdx);
            // ret.isTorpedo = true;
            return ret;
        }

        public void ResetDamageExpenditureState()
        {
            status = MountStatus.Operational;
            firingTargetObjectId = null;
            // mountsDestroyed = 0;
        }
    }
    
    public enum TrackingSystemState
    {
        // If a shooter fire at a target without tracking (FCS is destroyed, too many targeting or barrage fire), the firing is subject to Local Control modifier (-50% FC)
        Idle, // Tracking Position is not tracking anything.
        Destroyed,
        BeginTracking, // -50% ROF, -2 FC, BeginTracking will transition to Tracking once tracking is maintained at least 2 min 
        Tracking,
        Hitting // +2 FC, If target is hit by shooter, Hitting state will matain 2 min unless another hit is scored. 
    }

    public partial class FireControlSystemStatusRecord : IObjectIdLabeled
    {
        public string objectId { set; get; }
        public string targetObjectId { get; set; }
        public ShipLog GetTarget() => EntityManager.Instance.Get<ShipLog>(targetObjectId);
        public TrackingSystemState trackingState;
        public float trackingSeconds;

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

    public partial class BatteryStatus : IObjectIdLabeled, IWTABattery
    {
        public string objectId { get; set; }
        public BatteryAmmunitionRecord ammunition = new(); // TODO: based on mount instead of battery?
        public List<MountStatusRecord> mountStatus = new();
        // public int fireControlHits;
        public List<FireControlSystemStatusRecord> fireControlSystemStatusRecords = new();

        public IEnumerable<IObjectIdLabeled> GetSubObjects()
        {
            foreach (var mount in mountStatus)
            {
                yield return mount;
            }
            foreach (var fireControlSystemStatusRecord in fireControlSystemStatusRecords)
            {
                yield return fireControlSystemStatusRecord;
            }
        }

        public BatteryRecord GetBatteryRecord()
        {
            var shipLog = EntityManager.Instance.GetParent<ShipLog>(this);
            if (shipLog == null)
                return null;
            var idx = shipLog.batteryStatus.IndexOf(this);
            var shipClass = shipLog.shipClass;
            if (shipClass == null || idx < 0 || idx >= shipClass.batteryRecords.Count)
                return null;
            return shipClass.batteryRecords[idx];
        }

        public void ResetDamageExpenditureState()
        {
            var batteryRecord = GetBatteryRecord();
            ammunition.ArmorPiercing = batteryRecord.ammunitionCapacity / 2;
            ammunition.common = batteryRecord.ammunitionCapacity / 2;

            var expectedLength = batteryRecord.mountLocationRecords.Sum(r => r.mounts);
            Utils.SyncListToLength(expectedLength, mountStatus, this);
            foreach (var s in mountStatus)
                s.ResetDamageExpenditureState();

            // fireControlHits = 0;
            Utils.SyncListToLength(batteryRecord.fireControlPositions, fireControlSystemStatusRecords, this);
            foreach (var s in fireControlSystemStatusRecords)
                s.ResetToIntegrityState();
        }

        public string Summary() // Used in information panel
        {
            var batteryRecord = GetBatteryRecord();
            if (batteryRecord == null)
                return "[Not Specified]";
            var barrels = batteryRecord.mountLocationRecords.Sum(r => r.barrels * r.mounts);
            // var availableBarrels = mountStatus.Where(m => m.status == MountStatus.Operational).Sum(m => (m.mountLocationRecord.mounts - m.mountsDestroyed) * m.mountLocationRecord.barrels);
            var availableBarrels = mountStatus.Where(m => m.status == MountStatus.Operational).Sum(m => m.GetMountLocationRecordInfo().record.barrels);
            return $"{availableBarrels}/{barrels} {batteryRecord.name.mergedName} ({ammunition.Summary()})";
        }

        public float EvaluateFirepowerScore()
        {
            var batteryRecord = GetBatteryRecord();
            var firepoweScorePerBarrel = batteryRecord.EvaluateFirepowerPerBarrel();
            // var availableBarrels = mountStatus.Where(m => m.status == MountStatus.Operational).Sum(m => (m.mountLocationRecord.mounts - m.mountsDestroyed) * m.mountLocationRecord.barrels);
            var availableBarrels = mountStatus.Where(m => m.status == MountStatus.Operational).Sum(m => m.GetMountLocationRecordInfo().record.barrels);
            return availableBarrels * firepoweScorePerBarrel;
        }

        public float EvaluateFirepowerScore(float distanceYards, TargetAspect targetAspect, float targetSpeedKnots, float bearingRelativeToBowDeg)
        {
            var batteryRecord = GetBatteryRecord();
            if (distanceYards > batteryRecord.rangeYards)
                return 0;

            var firepowerPerBarrel = batteryRecord.EvaluateFirepowerPerBarrel(distanceYards, targetAspect, targetSpeedKnots);
            var barrels = mountStatus.Where(
                m => m.status == MountStatus.Operational &&
                m.GetMountLocationRecordInfo().record.IsInArc(bearingRelativeToBowDeg)
            ).Sum(m => m.GetMountLocationRecordInfo().record.barrels);
            return barrels * firepowerPerBarrel;
        }

        public void SetFiringTarget(ShipLog target)
        {
            foreach (var mnt in mountStatus)
            {
                mnt.firingTargetObjectId = null;
            }
            foreach (var fcs in fireControlSystemStatusRecords)
            {
                fcs.SetTrackingTarget(null);
            }

            if (target == null)
            {
                return;
            }

            var shipLog = EntityManager.Instance.GetParent<ShipLog>(this);
            if (shipLog == null)
                return;

            var stats = MeasureStats.Measure(shipLog, target);
            foreach (var fcs in fireControlSystemStatusRecords)
            {
                fcs.SetTrackingTarget(target);
            }

            foreach (var mnt in mountStatus)
            {
                if (mnt.status == MountStatus.Operational &&
                    mnt.GetMountLocationRecordInfo().record.IsInArc(stats.observerToTargetBearingRelativeToBowDeg))
                {
                    mnt.firingTargetObjectId = target.objectId;
                }
            }
        }

        void IWTABattery.SetFiringTarget(IWTAObject target) => SetFiringTarget(target as ShipLog); // TODO: Support other IWTAObject (land targets?)

        public void ResetFiringTarget()
        {
            foreach (var fcs in fireControlSystemStatusRecords)
            {
                fcs.SetTrackingTarget(null);
                // fcs.
            }

            foreach (var mnt in mountStatus)
                mnt.firingTargetObjectId = null;
        }

        IWTAObject IWTABattery.GetCurrentFiringTarget()
        {
            var targetMounts = mountStatus.Where(m => m.GetFiringTarget() != null).GroupBy(m => m.GetFiringTarget()).Select(g => (g.Key, g.Count())).ToList();
            if (targetMounts.Count == 0)
                return null;
            var maxCount = targetMounts.Max(r => r.Item2);
            return targetMounts.First(r => r.Item2 == maxCount).Item1;
        }

        public void Step(float deltaSeconds)
        {
            // TODO: Use general SubChildren and active to propagate Step command?

            // TODO: Do actual firing resolution
            foreach (var fcs in fireControlSystemStatusRecords)
                fcs.Step(deltaSeconds);
        }
    }

    public class TorpedoSectorStatus
    {
        public int ammunition;
        public List<MountStatusRecord> mountStatus = new();
    }

    public partial class RapidFiringStatus : IObjectIdLabeled
    {
        public string objectId { get; set; }
        public int portMountHits;
        public int starboardMountHits;
        public int fireControlHits;

        public void ResetDamageExpenditureState()
        {
            portMountHits = 0;
            starboardMountHits = 0;
            fireControlHits = 0;
        }

        public RapidFireBatteryRecord GetRapidFireBatteryRecord()
        {
            var shipLog = EntityManager.Instance.GetParent<ShipLog>(this);
            if (shipLog == null)
                return null;
            var idx = shipLog.rapidFiringStatus.IndexOf(this);
            var shipClass = shipLog.shipClass;
            if (shipClass == null || idx < 0 || idx >= shipClass.rapidFireBatteryRecords.Count)
                return null;
            return shipClass.rapidFireBatteryRecords[idx];
        }

        public string GetInfo()
        {
            var r = rapidFireBatteryRecord;
            if (r == null)
                return "Not Valid";

            var (portClass, portCurrent) = GetClassCurrentBarrels(r.barrelsLevelPort, portMountHits);
            var (starboardClass, starboardCurrent) = GetClassCurrentBarrels(r.barrelsLevelStarboard, starboardMountHits);

            return $"{portClass}({portCurrent})/{starboardClass}({starboardCurrent}) {r.name.mergedName}";
        }

        (int, int) GetClassCurrentBarrels(List<int> barrelsLevel, int hit)
        {
            hit = Math.Max(0, hit);
            if (barrelsLevel.Count == 0)
                return (0, 0);
            var barrelsClass = barrelsLevel[0];
            var barrelsCurrent = hit >= barrelsLevel.Count ? 0 : barrelsLevel[hit];
            return (barrelsClass, barrelsCurrent);
        }

        public float EvaluateFirepowerScore()
        {
            var r = rapidFireBatteryRecord;
            if (r == null)
                return 0;

            var (portClass, portCurrent) = GetClassCurrentBarrels(r.barrelsLevelPort, portMountHits);
            var (starboardClass, starboardCurrent) = GetClassCurrentBarrels(r.barrelsLevelStarboard, starboardMountHits);
            var barrels = portCurrent + starboardCurrent;
            return GetRapidFireBatteryRecord().EvaluateFirepowerPerBarrel() * barrels;
        }
    }

    public class DynamicStatus
    {
        public float speedKnotsDirectModifier;
        public int engineRoomHits;
        public int propulsionShaftHits;
        public int boilerRoomHits;

        public void ResetDamageExpenditureState()
        {
            speedKnotsDirectModifier = 1;
            engineRoomHits = 0;
            propulsionShaftHits = 0;
            boilerRoomHits = 0;
        }
    }

    public class SearchLightStatus
    {
        public int portHit;
        public int starboardHit;

        public void ResetDamageExpenditureState()
        {
            portHit = 0;
            starboardHit = 0;
        }
    }

    public enum DamageEffectCode
    {
        Invalid,
        CannotToRight
    }

    public class DamageEffectRecord
    {
        public DamageEffectCode code;
        public float severity;
        public float countdownSeconds;
    }

    public class ShipboardFireStatus
    {
        public string description; // location & start time description
        public float severity;
        public float countdownSeconds;
    }

    public interface IDF3Model
    {
        float GetLatitudeDeg();
        float GetLongitudeDeg();
        float GetHeadingDeg();
    }

    public interface IDF4Model: IDF3Model
    {
        float GetSpeedKnots();
    }

    public enum MapState
    {
        NotDeployed,
        Deployed,
        Destroyed
    }

    public enum ControlMode
    {
        Independent, // Player/Top AI set desired speed, heading and etc to control the ship directly.
        FollowTarget,
        RelativeToTarget,
    }


    public partial class ShipLog : IObjectIdLabeled, IDF4Model, IShipGroupMember, IWTAObject, IExtrapolable
    {
        public string objectId { get; set; }
        // public ShipClass shipClass;
        // public string shipClassObjectId;
        public string namedShipObjectId;
        public NamedShip namedShip
        {
            get => EntityManager.Instance.Get<NamedShip>(namedShipObjectId);
        }
        // public string shipClassStr;
        public ShipClass shipClass
        {
            // get => NavalGameState.Instance.shipClasses.FirstOrDefault(x => x.name.english == shipClassStr);
            // get => EntityManager.Instance.Get<ShipClass>(shipClassObjectId);
            get
            {
                var _shipClassObjectId = namedShip?.shipClassObjectId;
                if (_shipClassObjectId == null)
                    return null;
                return EntityManager.Instance.Get<ShipClass>(_shipClassObjectId);
            }
        }
        public float damagePoint; // current taken damage point vs "max" damage point defined in the class
        public LatLon position = new();
        public float speedKnots; // current speed vs "max" speed defined in the class
        public float headingDeg;
        public float desiredSpeedKnots;
        public float desiredHeadingDeg;
        public MapState mapState;
        // DCR Modifier or DCR modifier type?

        public float GetLatitudeDeg() => position.LatDeg;
        public float GetLongitudeDeg() => position.LonDeg;
        public float GetHeadingDeg() => headingDeg;
        public float GetSpeedKnots() => speedKnots;

        public List<BatteryStatus> batteryStatus = new();
        public TorpedoSectorStatus torpedoSectorStatus = new();
        public List<RapidFiringStatus> rapidFiringStatus = new();
        public DynamicStatus dynamicStatus = new();
        public SearchLightStatus searchLightHits = new();
        public int damageControlRatingHits;
        public List<DamageEffectRecord> damageEffectRecords = new();
        public List<ShipboardFireStatus> shipboardFireStatus = new();

        public string parentObjectId { get; set; } // OOB perspective
        // Get Parent / Root Parent method is defined in IShipGroupMember

        public string leaderObjectId;
        public Leader leader
        {
            get => EntityManager.Instance.Get<Leader>(leaderObjectId);
        }

        public bool emergencyRudder;
        public bool assistedDeceleration = true;
        public ControlMode controlMode;
        public string followedTargetObjectId;
        public ShipLog followedTarget
        {
            get => EntityManager.Instance.Get<ShipLog>(followedTargetObjectId);
        }
        public float followDistanceYards = 500;
        public string relativeTargetObjectId;
        public ShipLog relativeToTarget
        {
            get => EntityManager.Instance.Get<ShipLog>(relativeTargetObjectId);
        }
        public float relativeToTargetDistanceYards = 250;
        public float relativeToTargetAzimuth = 135; // right-after position

        public string GetMemberName() => namedShip.name.mergedName ?? "[Not Speicified]";// name.mergedName;

        public IEnumerable<IObjectIdLabeled> GetSubObjects()
        {
            foreach (var obj in batteryStatus)
            {
                yield return obj;
            }

            foreach (var obj in torpedoSectorStatus.mountStatus)
            {
                yield return obj;
            }

            foreach (var obj in rapidFiringStatus)
            {
                yield return obj;
            }
        }

        public bool IsOnMap() => mapState == MapState.Deployed;

        public void ResetDamageExpenditureState()
        {
            desiredHeadingDeg = headingDeg;
            desiredSpeedKnots = speedKnots;

            damagePoint = 0;
            // foreach (var batteryStatusRec in batteryStatus)
            // {
            //     batteryStatusRec.
            // }
            var _shipClass = shipClass;
            Utils.SyncListPairLength(_shipClass.batteryRecords, batteryStatus, this);
            foreach (var batteryStatusRec in batteryStatus)
                batteryStatusRec.ResetDamageExpenditureState();

            torpedoSectorStatus.ammunition = _shipClass.torpedoSector.ammunitionCapacity;
            Utils.SyncListToLength(
                _shipClass.torpedoSector.mountLocationRecords.Sum(r => r.mounts),
                torpedoSectorStatus.mountStatus,
                this
            );
            foreach (var m in torpedoSectorStatus.mountStatus)
                m.ResetDamageExpenditureState();

            Utils.SyncListPairLength(_shipClass.rapidFireBatteryRecords, rapidFiringStatus, this);
            foreach (var r in rapidFiringStatus)
                r.ResetDamageExpenditureState();

            dynamicStatus.ResetDamageExpenditureState();
            searchLightHits.ResetDamageExpenditureState();

            damageControlRatingHits = 0;
            damageEffectRecords.Clear();
            shipboardFireStatus.Clear();
        }

        public string Summary()
        {
            var _shipClass = shipClass;
            var lines = new List<string>();

            lines.Add("Battery:");
            lines.AddRange(batteryStatus.Select(bs => bs.Summary()));

            lines.Add("Torpedo:");
            var torpedoBarrels = _shipClass.torpedoSector.mountLocationRecords.Sum(r => r.barrels * r.mounts);
            // var torpedoBarrelsAvailable = torpedoSectorStatus.mountStatus.Where(m => m.status == MountStatus.Operational).Sum(m => (m.torpedoMountLocationRecord.mounts - m.mountsDestroyed) * m.torpedoMountLocationRecord.barrels);
            var torpedoBarrelsAvailable = torpedoSectorStatus.mountStatus.Where(m => m.status == MountStatus.Operational).Sum(m => m.GetTorpedoMountLocationRecordInfo().record.barrels);
            var torpedoAmmu = torpedoSectorStatus.ammunition;
            lines.Add($"x{torpedoBarrelsAvailable}/{torpedoBarrels} {_shipClass.torpedoSector.name.mergedName} ({torpedoAmmu})");

            lines.Add("Rapid Firing Battery:");
            lines.AddRange(rapidFiringStatus.Select(s => s.GetInfo()));

            // lines.Add("DP")
            return string.Join("\n", lines);
        }

        public void Step(float deltaSeconds)
        {
            if (speedKnots >= 4) // Turn and induced speed change
            {
                var useEmergencyRudder = emergencyRudder && speedKnots >= 12;

                var turnCapPer2Min = useEmergencyRudder ? shipClass.emergencyTurnDegPer2Min : shipClass.standardTurnDegPer2Min;
                var turnCapThisPulse = turnCapPer2Min / 120 * deltaSeconds;
                var absDeltaDeg = Math.Min(MeasureUtils.GetPositiveAngleDifference(headingDeg, desiredHeadingDeg), turnCapThisPulse);
                var usePercent = absDeltaDeg / turnCapThisPulse;

                headingDeg = MeasureUtils.MoveAngleTowards(headingDeg, desiredHeadingDeg, turnCapThisPulse);

                var decayPercentPer2Min = (useEmergencyRudder ? 0.5f : 0.75f) * usePercent + 1 * (1 - usePercent);
                var decayPercentThisPulse = (float)Math.Pow(decayPercentPer2Min, deltaSeconds / 120f);
                speedKnots *= decayPercentThisPulse;
            }

            var accelerationKnotsCapPer2Min = shipClass.speedIncreaseRecord.Where(r => speedKnots >= r.thresholdSpeedKnots).Select(r => r.increaseSpeedKnots).Min();
            var accelerationKnotsCapPerSec = accelerationKnotsCapPer2Min / 120;
            var decelerationKnotsCapPer2Min = shipClass.speedKnots * (assistedDeceleration ? 0.6f : 0.2f);
            var decelerationKnotsCapPerSec = decelerationKnotsCapPer2Min / 120f;

            if (controlMode == ControlMode.FollowTarget)
            {
                if (followedTarget != null)
                {
                    var followedPosition = followedTarget?.position;
                    var inverseLine = Geodesic.WGS84.InverseLine(
                        position.LatDeg, position.LonDeg,
                        followedPosition.LatDeg, followedPosition.LonDeg
                    );
                    var currentFollowedDistM = inverseLine.Distance;
                    var currentFollowedDistYards = currentFollowedDistM * 1.09361f;

                    var expectedDistanceDiffYards = currentFollowedDistYards - followDistanceYards;

                    var extraDistanceYards = 0f;
                    if (speedKnots > followedTarget.speedKnots)
                    {
                        var diffKnots = speedKnots - followedTarget.speedKnots;
                        var decelerationSeconds = diffKnots / decelerationKnotsCapPerSec;
                        var extraDistanceNm = (diffKnots / 3600) * decelerationSeconds / 2;
                        extraDistanceYards = extraDistanceNm * 2025.37f;
                    }

                    if (Math.Abs(expectedDistanceDiffYards) < 20 && Math.Abs(speedKnots - followedTarget.speedKnots) < 1)
                    {
                        desiredSpeedKnots = followedTarget.speedKnots;
                    }
                    else if (expectedDistanceDiffYards > 0)
                    {
                        if (speedKnots < followedTarget.speedKnots)
                        {
                            desiredSpeedKnots = shipClass.speedKnots; // max speed
                        }
                        else
                        {
                            if (currentFollowedDistYards > followDistanceYards + extraDistanceYards)
                            {
                                desiredSpeedKnots = shipClass.speedKnots; // max speed
                            }
                            else
                            {
                                desiredSpeedKnots = followedTarget.speedKnots;
                            }
                        }
                    }
                    else
                    {
                        if (speedKnots < followedTarget.speedKnots)
                        {
                            desiredSpeedKnots = followedTarget.speedKnots * 0.9f;
                        }
                        else if (currentFollowedDistM > extraDistanceYards)
                        {
                            desiredSpeedKnots = followedTarget.speedKnots * 0.8f;
                        }
                        else
                        {
                            desiredSpeedKnots = 0; // Too harsh? But in fact it
                        }
                    }
                    desiredHeadingDeg = MeasureUtils.NormalizeAngle((float)inverseLine.Azimuth);
                }
            }
            else if (controlMode == ControlMode.RelativeToTarget)
            {
                if (relativeToTarget != null)
                {
                    var rtp = relativeToTarget.position;
                    var azi = MeasureUtils.NormalizeAngle(relativeToTarget.headingDeg + relativeToTargetAzimuth);
                    var s12 = relativeToTargetDistanceYards * 0.9144;
                    Geodesic.WGS84.Direct(rtp.LatDeg, rtp.LonDeg, azi, s12, out var targetLat, out var targetLon);

                    var inverseLine = Geodesic.WGS84.InverseLine(
                        position.LatDeg, position.LonDeg,
                        targetLat, targetLon
                    );
                    var distanceToTargetM = inverseLine.Distance;
                    var distanceToTargetYards = distanceToTargetM * 1.09361f;

                    var targetPointIsFrontOfShip = MeasureUtils.GetPositiveAngleDifference((float)inverseLine.Azimuth, headingDeg) < 90;
                    var shipIsFrontOfTarget = MeasureUtils.GetPositiveAngleDifference((float)inverseLine.Azimuth + 180, relativeToTarget.headingDeg) < 90;

                    if (distanceToTargetYards < 20)
                    {
                        desiredSpeedKnots = relativeToTarget.speedKnots;
                        desiredHeadingDeg = relativeToTarget.headingDeg;
                    }
                    else if (shipIsFrontOfTarget && !targetPointIsFrontOfShip) // Wait target to "close"
                    {
                        desiredSpeedKnots = relativeToTarget.speedKnots * 0.75f;
                        desiredHeadingDeg = relativeToTarget.headingDeg;
                    }
                    else // Move to target
                    {
                        desiredHeadingDeg = MeasureUtils.NormalizeAngle((float)inverseLine.Azimuth);

                        if (speedKnots < relativeToTarget.speedKnots)
                        {
                            desiredSpeedKnots = shipClass.speedKnots; // max speed
                        }
                        else
                        {
                            var diffKnots = speedKnots - relativeToTarget.speedKnots;
                            var decelerationSeconds = diffKnots / decelerationKnotsCapPerSec;
                            var extraDistanceNm = (diffKnots / 3600) * decelerationSeconds / 2;
                            var extraDistanceYards = extraDistanceNm * 2025.37f;
                            if (distanceToTargetYards > extraDistanceYards)
                            {
                                desiredSpeedKnots = shipClass.speedKnots; // max speed
                            }
                            else
                            {
                                desiredSpeedKnots = relativeToTarget.speedKnots * 0.8f;
                            }
                        }
                    }
                }
            }

            if (desiredSpeedKnots > speedKnots)
            {
                var accelerationKnotsCapThisPulse = accelerationKnotsCapPerSec * deltaSeconds;
                speedKnots += Math.Min(desiredSpeedKnots - speedKnots, accelerationKnotsCapThisPulse);
            }
            else if (desiredSpeedKnots < speedKnots)
            {
                var decelerationKnotsCapThisPulse = decelerationKnotsCapPerSec * deltaSeconds;
                speedKnots -= Math.Min(speedKnots - desiredSpeedKnots, decelerationKnotsCapThisPulse);
            }

            var distNm = speedKnots / 3600 * deltaSeconds;
            var distM = distNm * 1852;
            double arcLength = Geodesic.WGS84.Direct(position.LatDeg, position.LonDeg, headingDeg, distM, out double lat2, out double lon2);
            position = new LatLon((float)lat2, (float)lon2);

            // 

            foreach (var bs in batteryStatus)
                bs.Step(deltaSeconds);
        }

        public float EvaluateArmorScore()
        {
            return EvaluateArmorScore(TargetAspect.Broad, RangeBand.Short);
        }

        public float EvaluateArmorScore(TargetAspect targetAspect, RangeBand rangeBand)
        {
            return shipClass.EvaluateArmorScore(targetAspect, rangeBand);
        }

        public float EvaluateSurvivability()
        {
            var armorScoreSmoothed = 1 + EvaluateArmorScore();
            var dp = shipClass.damagePoint - damagePoint;
            return dp * armorScoreSmoothed;
        }

        public float EvaluateBatteryFirepowerScore()
        {
            return batteryStatus.Sum(bs => bs.EvaluateFirepowerScore());
        }

        public float EvaluateBatteryFirepowerScore(float distanceYards, TargetAspect targetAspect, float targetSpeedKnots, float bearingRelativeToBowDeg)
        {
            return batteryStatus.Sum(bs => bs.EvaluateFirepowerScore(distanceYards, targetAspect, targetSpeedKnots, bearingRelativeToBowDeg));
        }

        public float EvaluateTorpedoThreatScore()
        {
            // var torpedoBarrelsAvailable = torpedoSectorStatus.mountStatus.Where(m => m.status == MountStatus.Operational).Sum(m => (m.torpedoMountLocationRecord.mounts - m.mountsDestroyed) * m.torpedoMountLocationRecord.barrels);
            var torpedoBarrelsAvailable = torpedoSectorStatus.mountStatus.Where(m => m.status == MountStatus.Operational).Sum(m => m.GetTorpedoMountLocationRecordInfo().record.barrels);
            return torpedoBarrelsAvailable * shipClass.torpedoSector.EvaluateTorpedoThreatPerBarrel();
        }

        public float EvaluateRapidFiringFirepowerScore()
        {
            return rapidFiringStatus.Sum(rf => rf.EvaluateFirepowerScore());
        }

        public float EvaluateFirepowerScore()
        {
            var batteryFirepower = EvaluateBatteryFirepowerScore();
            // Torpedo is not handled here
            var torpedoThreat = EvaluateTorpedoThreatScore();
            var rapidFiringFirepower = EvaluateRapidFiringFirepowerScore();

            return 1f * batteryFirepower + 1f * torpedoThreat + 1f * rapidFiringFirepower;
        }

        public float EvaluateGeneralScore()
        {
            var armorScore = EvaluateArmorScore();
            var firepowerScore = EvaluateFirepowerScore();
            return 1f * armorScore + 1f * firepowerScore;
        }

        public HashSet<ShipLog> GetFiringToTargets()
        {
            var targets = new HashSet<ShipLog>();
            foreach (var bs in batteryStatus)
            {
                foreach (var mnt in bs.mountStatus)
                {
                    var target = mnt.GetFiringTarget();
                    if (target != null)
                    {
                        targets.Add(mnt.GetFiringTarget());
                    }
                }
            }
            // TODO: Add torpedos?
            // TODO: Add rapid firing batteries
            return targets;
        }

        public IEnumerable<IWTABattery> GetBatteries()
        {
            foreach (var bs in batteryStatus)
                yield return bs;
        }

        public float EvaluateBowFirepowerScore() => EvaluateBatteryFirepowerScore(0, TargetAspect.Broad, 0, 0);
        public float EvaluateStarboardFirepowerScore() => EvaluateBatteryFirepowerScore(0, TargetAspect.Broad, 0, 90);
        public float EvaluateSternFirepowerScore() => EvaluateBatteryFirepowerScore(0, TargetAspect.Broad, 0, 180);
        public float EvaluatePortFirepowerScore() => EvaluateBatteryFirepowerScore(0, TargetAspect.Broad, 0, 270);
        public ControlMode GetControlMode() => controlMode;
        IExtrapolable IExtrapolable.GetFollowedTarget() => followedTarget;
        float IExtrapolable.GetFollowDistanceYards() => followDistanceYards;
        IExtrapolable IExtrapolable.GetRelativeToTarget() => relativeToTarget;
        float IExtrapolable.GetRelativeToTargetDistanceYards() => relativeToTargetDistanceYards;
        float IExtrapolable.GetRelativeToTargetAzimuth() => relativeToTargetAzimuth;
        void IExtrapolable.SetDesiredHeadingDeg(float desiredHeadingDeg) => this.desiredHeadingDeg = desiredHeadingDeg;
    }
}