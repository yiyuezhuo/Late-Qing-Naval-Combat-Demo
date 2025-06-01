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
        public int mountsDestroyed;

        public MountLocationRecord GetMountLocationRecord()
        {
            var battery = EntityManager.Instance.GetParent<BatteryStatus>(this);

            if (battery == null)
                return null;

            var mountIdx = battery.mountStatus.IndexOf(this);

            var batteryRecord = battery.GetBatteryRecord();
            if (batteryRecord == null)
                return null;

            if (mountIdx < 0 || mountIdx >= batteryRecord.mountLocationRecords.Count)
                return null;

            var mountLocationRecord = batteryRecord.mountLocationRecords[mountIdx];

            return mountLocationRecord;
        }

        public MountLocationRecord GetTorpedoMountLocationRecord()
        {
            var shipLog = EntityManager.Instance.GetParent<ShipLog>(this);
            if (shipLog == null)
                return null;

            var mountIdx = shipLog.torpedoSectorStatus.mountStatus.IndexOf(this);

            var shipClass = shipLog.shipClass;
            if (shipClass == null)
                return null;

            if (mountIdx < 0 || mountIdx >= shipClass.torpedoSector.mountLocationRecords.Count)
                return null;

            var mountLocationRecord = shipClass.torpedoSector.mountLocationRecords[mountIdx];
            return mountLocationRecord;
        }

        public void ResetDamageExpenditureState()
        {
            status = MountStatus.Operational;
            mountsDestroyed = 0;
        }
    }

    public partial class BatteryStatus : IObjectIdLabeled
    {
        public string objectId { get; set; }
        public BatteryAmmunitionRecord ammunition = new(); // TODO: based on mount instead of battery?
        public List<MountStatusRecord> mountStatus = new();
        public int fireControlHits;

        public IEnumerable<IObjectIdLabeled> GetSubObjects()
        {
            foreach (var mount in mountStatus)
            {
                yield return mount;
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

            Utils.SyncListPairLength(batteryRecord.mountLocationRecords, mountStatus, this);
            foreach (var s in mountStatus)
                s.ResetDamageExpenditureState();

            fireControlHits = 0;
        }

        public string Summary()
        {
            var batteryRecord = GetBatteryRecord();
            var barrels = batteryRecord.mountLocationRecords.Sum(r => r.barrels * r.mounts);
            var availableBarrels = mountStatus.Where(m => m.status == MountStatus.Operational).Sum(m => (m.mountLocationRecord.mounts - m.mountsDestroyed) * m.mountLocationRecord.barrels);
            return $"x{availableBarrels}/{barrels} {batteryRecord.name.mergedName} ({ammunition.Summary()})";
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

            var ( portClass, portCurrent) = GetClassCurrentBarrels(r.barrelsLevelPort, portMountHits);
            var ( starboardClass, starboardCurrent) = GetClassCurrentBarrels(r.barrelsLevelStarboard, starboardMountHits);

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

    public partial class ShipLog : IObjectIdLabeled, IDF3Model, IShipGroupMember
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
        // public GlobalString name = new();
        // public GlobalString captain = new();
        // public int crewRating;
        public float damagePoint; // current damage point vs "max" damage point defined in the class
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

        public List<BatteryStatus> batteryStatus = new();
        public TorpedoSectorStatus torpedoSectorStatus = new();
        public List<RapidFiringStatus> rapidFiringStatus = new();
        public DynamicStatus dynamicStatus = new();
        public SearchLightStatus searchLightHits = new();
        public int damageControlRatingHits;
        public List<DamageEffectRecord> damageEffectRecords = new();
        public List<ShipboardFireStatus> shipboardFireStatus = new();

        public string parentObjectId { get; set; } // OOB perspective

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
            Utils.SyncListPairLength(_shipClass.torpedoSector.mountLocationRecords, torpedoSectorStatus.mountStatus, this);
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

            lines.AddRange(batteryStatus.Select(bs => bs.Summary()));

            var torpedoBarrels = _shipClass.torpedoSector.mountLocationRecords.Sum(r => r.barrels * r.mounts);
            var torpedoBarrelsAvailable = torpedoSectorStatus.mountStatus.Where(m => m.status == MountStatus.Operational).Sum(m => (m.torpedoMountLocationRecord.mounts - m.mountsDestroyed) * m.torpedoMountLocationRecord.barrels);
            var torpedoAmmu = torpedoSectorStatus.ammunition;
            lines.Add($"x{torpedoBarrelsAvailable}/{torpedoBarrels} {_shipClass.torpedoSector.name.mergedName} ({torpedoAmmu})");

            lines.AddRange(rapidFiringStatus.Select(s => s.GetInfo()));

            // lines.Add("DP")
            return string.Join("\n", lines);
        }

        public void Step(float deltaSeconds)
        {
            if (speedKnots >= 4)
            {
                var useEmergencyRudder = emergencyRudder && speedKnots >= 12;

                var turnCapPer2Min = useEmergencyRudder ? shipClass.emergencyTurnDegPer2Min : shipClass.standardTurnDegPer2Min;
                var turnCapThisPulse = turnCapPer2Min / 120 * deltaSeconds;
                var absDeltaDeg = Math.Min(MeasureUtils.GetPositiveAngleDifference(headingDeg, desiredHeadingDeg), turnCapThisPulse);
                var usePercent = absDeltaDeg / turnCapThisPulse;

                headingDeg = MeasureUtils.MoveAngleTowards(headingDeg, desiredHeadingDeg, turnCapThisPulse);

                var decayPercentPer2Min = (useEmergencyRudder ? 0.5f : 0.75f) * usePercent + 1 * ( 1- usePercent);
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
        }
    }
}