using System.Collections.Generic;
using System.Data;
using System.Linq;
using System;
using GeographicLib;
using System.Xml.Serialization;


namespace NavalCombatCore
{
    public class TorpedoSectorStatus
    {
        public int ammunition; // Denotes torpedos in magazine, torpedos that had been loaded in tubes is not counted here.
        public List<TorpedoMountStatusRecord> mountStatus = new();

        public void Step(float deltaSeconds)
        {
            foreach (var mountStatusRecord in mountStatus)
            {
                mountStatusRecord.Step(deltaSeconds);
            }
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
        NotDeployed, // Logically on "map" (earth), but not in ROI and battlefield (but may enter by event and time), it also works as a placeholder state for new created ShipLog. A unrelevent ship will just not be in the shiplog list.
        Deployed, // On Map
        Destroyed // Sunk
    }

    public enum ShipOperationalState // general performance evaluation,
    {
        Operational,
        FloodingObstruction, // Sinking, all armament are disabled, dynamic will not work. Crews may is fleeing from the ships.
    }

    public enum ControlMode
    {
        Independent, // Player/Top AI set desired speed, heading and etc to control the ship directly.
        FollowTarget,
        RelativeToTarget,
    }

    public class TorpedoBattery : IWTABattery
    {
        public ShipLog original;

        public float EvaluateFirepowerScore(float distanceYards, TargetAspect targetAspect, float targetSpeedKnots, float bearingRelativeToBowDeg)
        {
            var threat = original.EvaluateTorpedoThreatScore(distanceYards, bearingRelativeToBowDeg);

            return threat * 200;
        }
        public IWTAObject GetCurrentFiringTarget()
        {
            var grouping = original.torpedoSectorStatus.mountStatus
                .Where(m => m.GetFiringTarget() != null)
                .GroupBy(m => m.GetFiringTarget())
                .OrderByDescending(g => g.Count())
                .FirstOrDefault();

            return grouping?.Key;
        }

        public void SetFiringTargetAutomatic(ShipLog target)
        {
            if (target == null)
            {
                foreach (var mnt in original.torpedoSectorStatus.mountStatus)
                    mnt.SetFiringTarget(null);
                return;
            }

            // TODO: Enforce doctrine (torpedo should be handled in specialized doctrine I guess)
            
            var stats = MeasureStats.Measure(original, target);
            var relaxedAngle = original.shipClass.emergencyTurnDegPer2Min / 2;

            foreach (var mnt in original.torpedoSectorStatus.mountStatus)
            {
                if (mnt.status == MountStatus.Operational)
                {
                    var recordInfo = mnt.GetTorpedoMountLocationRecordInfo();

                    if (recordInfo.record.IsInArcRelaxed(stats.observerToTargetBearingRelativeToBowDeg, relaxedAngle))
                    {
                        mnt.SetFiringTarget(target);
                    }
                }
            }
        }

        void IWTABattery.SetFiringTarget(IWTAObject target) => SetFiringTargetAutomatic(target as ShipLog); // TODO: Handle other target

        public void ResetFiringTarget()
        {
            foreach (var mount in original.torpedoSectorStatus.mountStatus)
                mount.SetFiringTarget(null);
        }

        public int GetOverConcentrationCoef() => 0;
    }

    // public class AbstractModifier
    // {

    // }

    public abstract class UnitModule : IObjectIdLabeled, ISubject
    {
        public string objectId { get; set; }
        public List<SubState> subStates = new();

        public void AddSubState(SubState state)
        {
            subStates.Add(state);
        }
        public void RemoveSubState(SubState state)
        {
            subStates.Remove(state);
        }

        public abstract IEnumerable<IObjectIdLabeled> GetSubObjects();
    }


    public partial class ShipLog : UnitModule, IDF4Model, IShipGroupMember, IWTAObject, IExtrapolable, ICollider
    {
        // public string objectId { get; set; }
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

        public ShipOperationalState operationalState = ShipOperationalState.Operational;

        public float GetLatitudeDeg() => position.LatDeg;
        public float GetLongitudeDeg() => position.LonDeg;
        public float GetHeadingDeg() => headingDeg;
        public float GetSpeedKnots() => speedKnots;

        // public void InflictDamagePoint(float damagePointDelta)
        // {
        //     damagePoint += damagePointDelta;
        //     if (damagePoint > shipClass.damagePoint) // TODO: Temp prototyping purpose workaround, in true manner of SK5, the sinking is generally the result of Damage Effect instead of uniform accumulation of DP.
        //     {
        //         mapState = MapState.Destroyed; // TODO: How to handle destroyed but how to represent "remain an obstruction" state?
        //     }
        // }

        public List<BatteryStatus> batteryStatus = new();
        public TorpedoSectorStatus torpedoSectorStatus = new();
        public List<RapidFiringStatus> rapidFiringStatus = new();
        public DynamicStatus dynamicStatus = new();
        public SearchLightStatus searchLightHits = new();
        public int damageControlRatingHits;
        public List<DamageEffectRecord> damageEffectRecords = new(); // TODO: Remove
        public List<SubState> damageEffects = new();
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
            get => EntityManager.Instance.GetOnMapShipLog(followedTargetObjectId);
        }
        public float followDistanceYards = 500;
        public string relativeTargetObjectId;
        public ShipLog relativeToTarget
        {
            get => EntityManager.Instance.GetOnMapShipLog(relativeTargetObjectId);
        }
        public ControlMode GetEffectiveControlMode()
        {
            if (controlMode == ControlMode.FollowTarget && followedTarget != null)
                return ControlMode.FollowTarget;
            if (controlMode == ControlMode.RelativeToTarget && relativeToTarget != null)
                return ControlMode.RelativeToTarget;
            return ControlMode.Independent;
        }

        public float relativeToTargetDistanceYards = 250;
        public float relativeToTargetAzimuth = 135; // right-after position

        public string GetMemberName() => namedShip.name.mergedName ?? "[Not Speicified]";// name.mergedName;

        public override IEnumerable<IObjectIdLabeled> GetSubObjects()
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

            yield return doctrine;
        }

        public Doctrine doctrine { get; set; } = new();

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

            Utils.SyncListToLength(
                _shipClass.torpedoSector.mountLocationRecords.Sum(r => r.mounts),
                torpedoSectorStatus.mountStatus,
                this
            );
            foreach (var m in torpedoSectorStatus.mountStatus)
                m.ResetDamageExpenditureState();
            torpedoSectorStatus.ammunition = _shipClass.torpedoSector.ammunitionCapacity - torpedoSectorStatus.mountStatus.Sum(m => m.reloadedLoad);

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
            var torpedoBarrelsAvailable = torpedoSectorStatus.mountStatus.Where(m => m.status == MountStatus.Operational).Sum(m => m.barrels);
            var torpedoAmmu = torpedoSectorStatus.ammunition;
            lines.Add($"x{torpedoBarrelsAvailable}/{torpedoBarrels} {_shipClass.torpedoSector.name.mergedName} ({torpedoAmmu})");

            lines.Add("Rapid Firing Battery:");
            lines.AddRange(rapidFiringStatus.Select(s => s.GetInfo()));

            // lines.Add("DP")
            return string.Join("\n", lines);
        }

        public void StepProcessTurn(float deltaSeconds)
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
        }

        public void StepProcessControl()
        {
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
        }

        public void StepProcessSpeed(float deltaSeconds)
        {
            if (desiredSpeedKnots > speedKnots)
            {
                var accelerationKnotsCapPer2Min = shipClass.speedIncreaseRecord.Where(r => speedKnots >= r.thresholdSpeedKnots).Select(r => r.increaseSpeedKnots).Min();
                var accelerationKnotsCapPerSec = accelerationKnotsCapPer2Min / 120;

                var accelerationKnotsCapThisPulse = accelerationKnotsCapPerSec * deltaSeconds;
                speedKnots += Math.Min(desiredSpeedKnots - speedKnots, accelerationKnotsCapThisPulse);
            }
            else if (desiredSpeedKnots < speedKnots)
            {
                var decelerationKnotsCapPer2Min = shipClass.speedKnots * (assistedDeceleration ? 0.6f : 0.2f);
                var decelerationKnotsCapPerSec = decelerationKnotsCapPer2Min / 120f;

                var decelerationKnotsCapThisPulse = decelerationKnotsCapPerSec * deltaSeconds;
                speedKnots -= Math.Min(speedKnots - desiredSpeedKnots, decelerationKnotsCapThisPulse);
            }
        }

        public void StepTryMoveToNewPosition(float deltaSeconds)
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
            if (!newPositionBlocked && CoreParameter.Instance.checkShipCollision)
            {
                ShipLog collided = null; // "Collider" check
                foreach (var other in NavalGameState.Instance.shipLogsOnMap)
                {
                    if (other == this)
                        continue;
                    var isCollided = CollideUtils.IsCollided(newPosition, this, other);
                    if (isCollided)
                    {
                        collided = other;
                    }
                    // var otherPos = other.position;

                    // // Exact Method
                    // // Geodesic.WGS84.Inverse(newPosition.LatDeg, newPosition.LonDeg, otherPos.LatDeg, otherPos.LonDeg, out var distanceM, out var azi1, out var azi2);
                    // // Approximation Method
                    // var (distanceKm, azi1) = MeasureStats.Approximation.CalculateDistanceKmAndBearingDeg(newPosition.LatDeg, newPosition.LonDeg, otherPos.LatDeg, otherPos.LonDeg);
                    // var distanceM = distanceKm * 1000;
                    // var azi2 = azi1;

                    // if (MeasureUtils.GetPositiveAngleDifference(headingDeg, (float)azi1) > 90)
                    //     continue;

                    // var distanceFoot = distanceM * MeasureUtils.meterToFoot;
                    // var lengthFoot = shipClass.lengthFoot;
                    // var otherLengthFoot = other.shipClass.lengthFoot;
                    // if (distanceFoot < lengthFoot / 2 + otherLengthFoot / 2)
                    // {
                    //     var diff = MeasureUtils.GetPositiveAngleDifference(other.headingDeg, (float)azi2);
                    //     var coef = Math.Abs(diff - 90) / 90;
                    //     var otherMix = otherLengthFoot * coef + other.shipClass.beamFoot * (1 - coef);
                    //     if (distanceFoot < lengthFoot / 2 + otherMix / 2)
                    //     {
                    //         collided = other;
                    //         break;
                    //     }
                    // }
                }
                newPositionBlocked = collided != null; // TODO: Handle deliberately hostile ramming and speed change
            }

            if (!newPositionBlocked)
            {
                position = newPosition;
            }
            else
            {
                speedKnots = 0; // TODO: Use a smoother method
            }
        }

        public void StepTorpedoSector(float deltaSeconds)
        {
            torpedoSectorStatus.Step(deltaSeconds);
        }

        public void StepBatteryStatus(float deltaSeconds)
        {
            foreach (var bs in batteryStatus)
            {
                bs.Step(deltaSeconds);
            }

            foreach (var rf in rapidFiringStatus)
            {
                rf.Step(deltaSeconds);
            }
        }

        public void StepDamageResolution(float deltaSeconds)
        {
            if (damagePoint > shipClass.damagePoint) // TODO: Temp workaround, this will be replaced with DE based implementation. 
            {
                mapState = MapState.Destroyed;
                var logger = ServiceLocator.Get<ILoggerService>();
                logger.LogWarning($"{namedShip.name.GetMergedName()} ({objectId}) is destroyed");
            }
        }

        // public void Step(float deltaSeconds)
        // {
        //     StepProcessTurn(deltaSeconds);

        //     // var accelerationKnotsCapPer2Min = shipClass.speedIncreaseRecord.Where(r => speedKnots >= r.thresholdSpeedKnots).Select(r => r.increaseSpeedKnots).Min();
        //     // var accelerationKnotsCapPerSec = accelerationKnotsCapPer2Min / 120;
        //     // var decelerationKnotsCapPer2Min = shipClass.speedKnots * (assistedDeceleration ? 0.6f : 0.2f);
        //     // var decelerationKnotsCapPerSec = decelerationKnotsCapPer2Min / 120f;

        //     StepProcessControl();

        //     StepProcessSpeed(deltaSeconds);

        //     StepTryMoveToNewPosition(deltaSeconds);

        //     StepSubObjects(deltaSeconds);
        // }

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
            var torpedoBarrelsAvailable = torpedoSectorStatus.mountStatus.Where(m => m.status == MountStatus.Operational).Sum(m => m.barrels);
            return torpedoBarrelsAvailable * shipClass.torpedoSector.EvaluateTorpedoThreatPerBarrel();
        }

        public float EvaluateTorpedoThreatScore(float distanceYards, float bearingRelativeToBowDeg)
        {
            var sc = shipClass;
            var classSector = sc.torpedoSector;
            // AngleDifferenceFromArc
            var setting = classSector.torpedoSettings.LastOrDefault();
            if (setting == null)
                return 0;

            var rangeCoef = 1 - distanceYards / (100 + setting.rangeYards * CoreParameter.Instance.automaticTorpedoFiringRangeRelaxedCoef);
            if (rangeCoef <= 0)
                return 0;

            var effBarrels = torpedoSectorStatus.mountStatus
                .Where(m => m.status == MountStatus.Operational && m.currentLoad > 0)
                .Select(m => (m, m.GetTorpedoMountLocationRecordInfo().record))
                .Where(p => p.Item2.IsInArcRelaxed(bearingRelativeToBowDeg, sc.emergencyTurnDegPer2Min / 2))
                .Sum(
                    p =>
                        Math.Min(p.m.currentLoad, p.m.barrels) *
                        (1 - p.Item2.AngleDifferenceFromArc(bearingRelativeToBowDeg) / 360)
                );
            return rangeCoef * effBarrels * classSector.EvaluateTorpedoThreatPerBarrel();
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
            foreach (var rfs in rapidFiringStatus)
            {
                foreach (var r in rfs.targettingRecords)
                {
                    var tgt = r.GetTarget();
                    if (tgt != null)
                    {
                        targets.Add(tgt);
                    }
                }
            }
            return targets;
        }

        public IEnumerable<IWTABattery> GetBatteries()
        {
            foreach (var bs in batteryStatus)
                yield return bs;

            foreach (var rf in rapidFiringStatus)
            {
                foreach (var b in rf.GetSideBatteries())
                    yield return b;
            }

            if (shipClass.torpedoSector.torpedoSettings.Count > 0)
            {
                yield return new TorpedoBattery()
                {
                    original = this
                };
            }
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

        public LatLon GetPosition() => position;
        // public float GetHeadingDeg() => headingDeg;
        public float GetLengthFoot() => shipClass.lengthFoot;
        public float GetBeamFoot() => shipClass.beamFoot;

    }
}