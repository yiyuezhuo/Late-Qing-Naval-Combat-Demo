using System.Collections.Generic;
using System.Data;
using System.Linq;
using System;
using GeographicLib;
using System.Xml.Serialization;
using UnityEngine;
using TMPro;


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
        public float maxSpeedKnotsOffset; // May be inflicted by DE (EX. DE 116)
        public float accelerationOffset; // May be infliceted by DE as well
        public int engineRoomHits;
        public int propulsionShaftHits;
        public int boilerRoomHits;

        // Machinery Space Flooding Hit Percent >= 80% will lead to roll of 75% causing ship to capsize / sink. 
        public int engineRoomFloodingHits;
        public int boilerRoomFloodingHits;

        public void ResetDamageExpenditureState()
        {
            maxSpeedKnotsOffset = 0;
            accelerationOffset = 0;
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
        AbandonShip, // morale check - (though in the battle of yalu, ships may flee but no ships are abandoned)
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
                if (mnt.IsOperational())
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
        public bool IsChangeTargetBlocked() => false;
    }

    [XmlInclude(typeof(ShipLogStringLog))]
    [XmlInclude(typeof(ShipLogBatteryHitLog))]
    [XmlInclude(typeof(ShipLogRapidFiringGunHitLog))]
    [XmlInclude(typeof(ShipLogTorpedoHitLog))]
    public class ShipLogLog
    {
        [XmlAttribute]
        public DateTime time;

        public virtual string Summary() => $"{GetType()}: {time}";
    }

    public class ShipLogStringLog : ShipLogLog
    {
        public string description;

        public override string Summary() => $"{time}: {description}";
    }

    public class ShipLogBatteryHitLog : ShipLogLog
    {
        [XmlAttribute]
        public ArmorLocation armorLocation;

        [XmlAttribute]
        public HitPenDetType hitPenDetType;

        [XmlAttribute]
        public float damagePoint;

        [XmlAttribute]
        public string damageEffectId;

        public override string Summary() => $"{time}: Bty Hit: {armorLocation} {hitPenDetType} DP:{damagePoint} DE:{damageEffectId}";
    }

    public class ShipLogRapidFiringGunHitLog : ShipLogLog
    {
        [XmlAttribute]
        public float damagePoint;

        public override string Summary() => $"{time}: RF Hit: DP: {damagePoint}";
    }

    public class ShipLogTorpedoHitLog : ShipLogLog
    {
        [XmlAttribute]
        public string torpedoObjectId;

        public LaunchedTorpedo GetTorpedo() => EntityManager.Instance.Get<LaunchedTorpedo>(torpedoObjectId);

        [XmlAttribute]
        public float damagePoint;

        [XmlAttribute]
        public string damageEffectId;

        public override string Summary() => $"{time}: Torpedo Hit: {GetTorpedo().sourceName} DP:{damagePoint} DE:{damageEffectId}";
    }

    // public class ShipLogDamageEffectBegin : ShipLogLog
    // {
    // }

    // public class ShipLogDamageEffectEnd : ShipLogLog
    // { 
    // }

    public partial class ShipLog : UnitModule, IDF4Model, IShipGroupMember, IWTAObject, IExtrapolable, ICollider
    {
        // public string objectId { get; set; }
        // public ShipClass shipClass;
        // public string shipClassObjectId;
        public string namedShipObjectId;

        [XmlIgnore]
        NamedShip namedShipCache;

        public NamedShip namedShip
        {
            get
            {
                if (NavalGameState.Instance.doingStep)
                {
                    if (namedShipCache == null)
                    {
                        namedShipCache = EntityManager.Instance.Get<NamedShip>(namedShipObjectId);
                    }
                    return namedShipCache;
                }
                return EntityManager.Instance.Get<NamedShip>(namedShipObjectId);
            }
        }
        // public string shipClassStr;
        public ShipClass shipClass
        {
            // get => NavalGameState.Instance.shipClasses.FirstOrDefault(x => x.name.english == shipClassStr);
            // get => EntityManager.Instance.Get<ShipClass>(shipClassObjectId);
            get
            {
                // var _shipClassObjectId = namedShip?.shipClassObjectId;
                // if (_shipClassObjectId == null)
                //     return null;
                // return EntityManager.Instance.Get<ShipClass>(_shipClassObjectId);
                return namedShip?.shipClass;
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
        public float desiredSpeedKnotsForBoilerRoom; // DE 124, process command delay. Desired Speed Knot known by boiler room operator is the "effective" desired speed. 
        public bool smokeGeneratorDisabled;

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
        public (ControlMode, ShipLog) GetControlModeAndTargetInlucdeNonMap()
        {
            if (controlMode == ControlMode.FollowTarget)
                return (ControlMode.FollowTarget, EntityManager.Instance.Get<ShipLog>(followedTargetObjectId));
            if (controlMode == ControlMode.RelativeToTarget)
                return (ControlMode.RelativeToTarget, EntityManager.Instance.Get<ShipLog>(relativeTargetObjectId));
            return (ControlMode.Independent, null);
        }

        public float relativeToTargetDistanceYards = 250;
        public float relativeToTargetAzimuth = 135; // right-after position

        public string GetMemberName() => namedShip.name.mergedName ?? "[Not Speicified]";// name.mergedName;

        public override IEnumerable<IObjectIdLabeled> GetSubObjects()
        {
            foreach (var so in base.GetSubObjects())
                yield return so;


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

        public bool isEvasiveManeuvering;

        public float pendingDamagePoint;

        public List<ShipLogLog> logs = new(); // TODO: Switch to structure logging?

        public string DescribeDetail()
        {
            var lines = new List<string>()
            {
                $"ShipLog Detail: {objectId}"
            };

            lines.AddRange(logs.Select(r => r.Summary()));
            return string.Join("\n", lines);
        }

        public void AddStringLog(string description)
        {
            logs.Add(new ShipLogStringLog()
            {
                time = NavalGameState.Instance.scenarioState.dateTime,
                description = description
            });
        }

        public bool IsOnMap() => mapState == MapState.Deployed;

        public void ResetDamageExpenditureState()
        {
            desiredHeadingDeg = headingDeg;
            desiredSpeedKnots = speedKnots;
            desiredSpeedKnotsForBoilerRoom = speedKnots;

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
            if (_shipClass == null)
                return "[Class Invalid or not binded]";

            var lines = new List<string>();

            lines.Add("Battery:");
            lines.AddRange(batteryStatus.Select(bs => bs.Summary()));

            lines.Add("Torpedo:");
            var torpedoBarrels = _shipClass.torpedoSector.mountLocationRecords.Sum(r => r.barrels * r.mounts);
            // var torpedoBarrelsAvailable = torpedoSectorStatus.mountStatus.Where(m => m.status == MountStatus.Operational).Sum(m => (m.torpedoMountLocationRecord.mounts - m.mountsDestroyed) * m.torpedoMountLocationRecord.barrels);
            var torpedoBarrelsAvailable = torpedoSectorStatus.mountStatus.Where(m => m.IsOperational()).Sum(m => m.barrels);
            var torpedoAmmu = torpedoSectorStatus.ammunition;
            lines.Add($"x{torpedoBarrelsAvailable}/{torpedoBarrels} {_shipClass.torpedoSector.name.mergedName} ({torpedoAmmu})");

            lines.Add("Rapid Firing Battery:");
            lines.AddRange(rapidFiringStatus.Select(s => s.GetInfo()));

            // lines.Add("DP")
            return string.Join("\n", lines);
        }

        public void AddDamagePoint(float addedDamagePoint)
        {
            // var damageTier = GetDamageTier();
            // damagePoint += addedDamagePoint;
            // var newDamageTier = GetDamageTier();

            // for (int dt = damageTier + 1; dt <= newDamageTier; dt++)
            // {
            //     var ctx = new DamageEffectContext()
            //     {
            //         subject = this,
            //         cause = DamageEffectCause.General,
            //     };
            //     DamageEffectChart.AddNewDamageEffect(ctx);
            // }
            pendingDamagePoint += addedDamagePoint;
        }

        public float GetTurnCap(bool useEmergencyRudder)
        {
            if (useEmergencyRudder && !GetSubStates<IDynamicModifier>().Any(m => m.IsEmergencyTurnBlocked()))
            {
                var cap = shipClass.emergencyTurnDegPer2Min;
                var upperLimit = GetSubStates<IDynamicModifier>().Select(m => m.GetEmergencyTurnUpperLimit()).DefaultIfEmpty(100000).Min();
                var coef = GetSubStates<IDynamicModifier>().Select(m => m.GetEmergencyTurnCoef()).DefaultIfEmpty(1).Min();
                return Mathf.Min(cap * coef, upperLimit);
            }
            else
            {
                var cap = shipClass.standardTurnDegPer2Min;
                var upperLimit = GetSubStates<IDynamicModifier>().Select(m => m.GetStandardTurnUpperLimit()).DefaultIfEmpty(100000).Min();
                var coef = GetSubStates<IDynamicModifier>().Select(m => m.GetStandardTurnCoef()).DefaultIfEmpty(1).Min();
                return Mathf.Min(cap * coef, upperLimit);
            }
        }

        public void StepProcessTurn(float deltaSeconds) // Turn and induced speed change
        {
            var mods = GetSubStates<IDynamicModifier>().ToList();
            if (mods.Any(m => m.IsCourseChangeBlocked()))
                return;
            
            var diffDeg = MeasureUtils.NormalizeAngle(desiredHeadingDeg - headingDeg) - 180;
            if (diffDeg > 0 && mods.Any(m => m.IsTurnStarboardBlocked()))
                return;
            if (diffDeg < 0 && mods.Any(m => m.IsTurnPortBlocked()))
                return;

            if (speedKnots >= 4) // Turn requires at least 4 knots speed to do
            {
                var useEmergencyRudder = emergencyRudder && speedKnots >= 12;
                var turnCapPer2Min = GetTurnCap(useEmergencyRudder);

                var turnCapThisPulse = turnCapPer2Min / 120 * deltaSeconds;
                var absDeltaDeg = Math.Min(MeasureUtils.GetPositiveAngleDifference(headingDeg, desiredHeadingDeg), turnCapThisPulse);
                var usePercent = absDeltaDeg / turnCapThisPulse;

                var _desiredHeadingDeg = desiredHeadingDeg + mods.Select(m => m.GetDesiredHeadingOffset()).Sum();
                // GetDesiredHeadingOffset

                headingDeg = MeasureUtils.MoveAngleTowards(headingDeg, _desiredHeadingDeg, turnCapThisPulse);

                var decayPercentPer2Min = (useEmergencyRudder ? 0.5f : 0.75f) * usePercent + 1 * (1 - usePercent);
                var decayPercentThisPulse = (float)Math.Pow(decayPercentPer2Min, deltaSeconds / 120f);
                speedKnots *= decayPercentThisPulse;
            }
        }

        public void StepProcessControl()
        {
            var maxSpeedKnots = GetMaxSpeedKnots();

            var decelerationKnotsCapPer2Min = maxSpeedKnots * (assistedDeceleration ? 0.6f : 0.2f);
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
                            desiredSpeedKnots = maxSpeedKnots; // max speed
                        }
                        else
                        {
                            if (currentFollowedDistYards > followDistanceYards + extraDistanceYards)
                            {
                                desiredSpeedKnots = maxSpeedKnots; // max speed
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
                            desiredSpeedKnots = maxSpeedKnots; // max speed
                        }
                        else
                        {
                            var diffKnots = speedKnots - relativeToTarget.speedKnots;
                            var decelerationSeconds = diffKnots / decelerationKnotsCapPerSec;
                            var extraDistanceNm = (diffKnots / 3600) * decelerationSeconds / 2;
                            var extraDistanceYards = extraDistanceNm * 2025.37f;
                            if (distanceToTargetYards > extraDistanceYards)
                            {
                                desiredSpeedKnots = maxSpeedKnots; // max speed
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

        public float GetMaxSpeedKnots() // cache in context?
        {
            if (shipClass == null || operationalState != ShipOperationalState.Operational)
                return 0;

            var maxSpeedKnots = shipClass.speedKnots + dynamicStatus.maxSpeedKnotsOffset;

            var modifiers = GetSubStates<IDynamicModifier>().ToList();
            if (modifiers.Count > 0)
            {
                var coef = modifiers.Select(m => m.GetMaxSpeedKnotCoef()).Min();
                var offset = modifiers.Select(m => m.GetMaxSpeedKnotOffset()).Min();
                var upperLimit = modifiers.Select(m => m.GetMaxSpeedUpperLimit()).Min();
                maxSpeedKnots = Math.Clamp((maxSpeedKnots + offset) * coef, 0, upperLimit);
            }

            var propulsionUpperLimit = shipClass.speedKnotsPropulsionShaftLevels.ElementAtOrDefault(dynamicStatus.propulsionShaftHits);
            maxSpeedKnots = Math.Min(maxSpeedKnots, propulsionUpperLimit);

            var engineRoomHits = dynamicStatus.engineRoomHits + dynamicStatus.engineRoomFloodingHits + GetSubStates<IEngineRoomHitModifier>().Select(m => m.GetEngineRoomHitOffset()).DefaultIfEmpty(0).Sum();
            var engineUpperLimit = shipClass.speedKnotsEngineRoomsLevels.ElementAtOrDefault(engineRoomHits);
            maxSpeedKnots = Math.Min(maxSpeedKnots, engineUpperLimit);

            var boilerRoomHits = dynamicStatus.boilerRoomHits + dynamicStatus.boilerRoomFloodingHits + GetSubStates<IBoilerRoomHitModifier>().Select(m => m.GetBoilerRoomHitOffset()).DefaultIfEmpty(0).Sum();
            var boilerUpperLimit = shipClass.speedKnotsBoilerRooms.ElementAtOrDefault(boilerRoomHits); // default => 0 => Upper Limit => 0 (not movable)
            maxSpeedKnots = Math.Min(maxSpeedKnots, boilerUpperLimit);

            return maxSpeedKnots;
        }

        public float GetMinSpeedKnots() => -GetMaxSpeedKnots() / 3;

        public void StepProcessSpeed(float deltaSeconds)
        {
            var maxSpeedKnots = GetMaxSpeedKnots();
            var minSpeedKnots = -maxSpeedKnots / 3;

            desiredSpeedKnots = Math.Clamp(desiredSpeedKnots, minSpeedKnots, maxSpeedKnots);
            desiredSpeedKnotsForBoilerRoom = Math.Clamp(desiredSpeedKnotsForBoilerRoom, minSpeedKnots, maxSpeedKnots); // not ideal

            var commandBlocked = GetSubStates<IDesiredSpeedUpdateToBoilerRoomBlocker>().Any(blocker => blocker.IsDesiredSpeedCommandBlocked());
            var dynamicBlocked = GetSubStates<IDynamicModifier>().Any(m => m.IsSpeedChangeBlocked()); // TODO: Merge?
            if (!commandBlocked && !dynamicBlocked)
            {
                desiredSpeedKnotsForBoilerRoom = desiredSpeedKnots;
            }

            // var modifiers = GetSubStates<IDynamicModifier>().ToList();

            // var maxSpeed = shipClass.speedKnots + dynamicStatus.maxSpeedKnotsOffset + GetSubStates<IDynamicModifier>().Max(dm => dm.GetMaxSpeedKnotOffset());
            var absDesiredSpeedKnotsForBoilerRoom = Math.Abs(desiredSpeedKnotsForBoilerRoom);
            var absSpeedKnots = Math.Abs(speedKnots);
            var absSpeedDiff = absDesiredSpeedKnotsForBoilerRoom - absSpeedKnots;
            var signSpeedKnots = Math.Sign(speedKnots);
            var signDesiredSpeedKnotsForBoilerRoom = Math.Sign(desiredSpeedKnotsForBoilerRoom);
            var sameDirection = (signSpeedKnots * signDesiredSpeedKnotsForBoilerRoom) >= 0;

            if (speedKnots == absDesiredSpeedKnotsForBoilerRoom)
            {
                // Fixed Point
            }
            else if (absSpeedDiff > 0 && sameDirection)
            {
                var accelerationKnotsCapPer2Min = shipClass.speedIncreaseRecord.Where(r => absSpeedKnots >= r.thresholdSpeedKnots).Select(r => r.increaseSpeedKnots).Min();

                var upperLimit = GetSubStates<IDynamicModifier>().Select(m => m.GetAccelerationUpperLimit()).DefaultIfEmpty(10000).Min();
                accelerationKnotsCapPer2Min = Math.Min(accelerationKnotsCapPer2Min, upperLimit);

                var accelerationKnotsCapPerSec = accelerationKnotsCapPer2Min / 120;

                var accelerationKnotsCapThisPulse = accelerationKnotsCapPerSec * deltaSeconds;
                speedKnots += signDesiredSpeedKnotsForBoilerRoom * Math.Min(absSpeedDiff, accelerationKnotsCapThisPulse);
            }
            else
            {
                var decelerationKnotsCapPer2Min = shipClass.speedKnots * (assistedDeceleration ? 0.6f : 0.2f);
                var decelerationKnotsCapPerSec = decelerationKnotsCapPer2Min / 120f;

                var decelerationKnotsCapThisPulse = decelerationKnotsCapPerSec * deltaSeconds;
                speedKnots -= signSpeedKnots * Math.Min(sameDirection ? -absSpeedDiff : absSpeedKnots, decelerationKnotsCapThisPulse);
            }

            // speedKnots = Math.Clamp(speedKnots, 0, GetMaxSpeedKnots());
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
            if (!newPositionBlocked && CoreParameter.Instance.checkShipCollision && speedKnots > 0) // TODO: Check Reverse Movement
            {
                // ShipLog collided = null; // "Collider" check
                // foreach (var other in NavalGameState.Instance.shipLogsOnMap)
                // {
                //     if (other == this)
                //         continue;
                //     var isCollided = CollideUtils.IsCollided(newPosition, this, other);
                //     if (isCollided)
                //     {
                //         collided = other;
                //     }
                // }
                // newPositionBlocked = collided != null; // TODO: Handle deliberately hostile ramming and speed change

                var maskCheckService = ServiceLocator.Get<IMaskCheckService>();
                var collideCheckResult = maskCheckService.CollideCheck(this, distNm * MeasureUtils.navalMileToYard + GetLengthFoot() / 2 * MeasureUtils.footToYard);
                if (collideCheckResult != null)
                {
                    newPositionBlocked = true;
                    var collided = collideCheckResult.collided;
                    var isHostile = (this as IShipGroupMember).GetRootParent() != (collided as IShipGroupMember).GetRootParent();

                    // Handle Ramming Damage
                    if (isHostile && speedKnots >= 6)
                    {
                        var targetArmorActualInch = collided.shipClass.armorRating.GetRecord(collideCheckResult.collideLocation).actualInch;
                        var isRammerUnarmored = shipClass.armorRating.GetArmorEffectiveInch(ArmorLocation.MainBelt) > 0 || shipClass.armorRating.GetArmorEffectiveInch(ArmorLocation.Deck) > 0;

                        // Hostile collision invoke ramming damage only at this point.
                        var rammingResolutionParameter = new RuleChart.RammingResolutionParameter()
                        {
                            rammerDamagePoint = shipClass.damagePoint,
                            impactAngleDeg = collideCheckResult.impactAngleDeg,
                            ramType = shipClass.ram,
                            targetArmorActualInch = targetArmorActualInch,
                            targetSpeedKnots = collided.speedKnots,
                            rammerSpeedKnots = speedKnots,
                            targetBuiltYear = collided.namedShip.applicableYearBegin,
                            isTargetSubmarine = false, // TODO: Handle Submarine
                            isTargetNonWarship = false, // TODO: Add more data
                            isRammerNonWarship = false,
                            isRammerUnarmored = isRammerUnarmored
                        };

                        var ramResolutionResult = rammingResolutionParameter.Resolve();

                        AddStringLog("Ramming to other ship");

                        AddDamagePoint(ramResolutionResult.inflictToRammerDamagePoint);
                        if (ramResolutionResult.inflictToRammerDamagePoint / shipClass.damagePoint > 0.1f)
                        {
                            DamageEffectChart.AddNewDamageEffect(new()
                            {
                                subject = this,
                                baseDamagePoint = ramResolutionResult.inflictToRammerDamagePoint,
                                cause = DamageEffectCause.BeltEnd,
                                hitPenDetType = HitPenDetType.PassThrough,
                                ammunitionType = AmmunitionType.ArmorPiercing,
                                shellDiameterInch = 10, // Rulebook isn't very clear for DE for ramming
                                addtionalDamageEffectProbility = 0.5f,
                            });
                        }

                        collided.AddStringLog("Rammed by other ship");

                        collided.AddDamagePoint(ramResolutionResult.inflictToTargetDamagePoint);
                        if (ramResolutionResult.inflictToTargetDamagePoint / collided.shipClass.damagePoint > 0.1f)
                        {
                            DamageEffectChart.AddNewDamageEffect(new()
                            {
                                subject = collided,
                                baseDamagePoint = ramResolutionResult.inflictToRammerDamagePoint,
                                cause = collideCheckResult.collideLocation == ArmorLocation.MainBelt ? DamageEffectCause.MainBelt : DamageEffectCause.BeltEnd,
                                hitPenDetType = HitPenDetType.PassThrough,
                                ammunitionType = AmmunitionType.ArmorPiercing,
                                shellDiameterInch = 10, // Rulebook isn't very clear for DE resolution of ramming
                                addtionalDamageEffectProbility = 0.5f,
                            });
                        }
                    }
                }
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

        public int GetDamageControlRating()
        {
            var modOffset = GetSubStates<IDamageControlModifier>().Select(m => m.GetDamageControlRatingOffset()).DefaultIfEmpty(0).Sum();
            return Math.Max(0, shipClass?.damageControlRatingUnmodified ?? 0 - damageControlRatingHits + modOffset);
        }

        public void DamageControlAssetAllocation()
        {
            // Damage Control Rating Allocation

            // Priority
            // 1. Shipboard fire (high to low)
            // 2. Low Severity Modifiers (low to high (multiplied by priority))
            // 3. Other Damage Controllable

            var damageControllableSubStates = GetSubStatesDownward().Where(s => s.damageControllable).ToList();

            // TODO: Doctrine should provide `reset all`, `not reset and allocate remain DCR`, and `not automate anything` mode.
            // Reset DCA states
            foreach (var subState in damageControllableSubStates)
            {
                subState.damageControlApplied = false;
            }

            damageControllableSubStates.Sort((left, right) => -left.GetDamageControlPriority().CompareTo(right.GetDamageControlPriority()));
            var n = Math.Min(GetDamageControlRating(), damageControllableSubStates.Count);
            for (int i = 0; i < n; i++)
            {
                damageControllableSubStates[i].damageControlApplied = true;
            }
        }

        public override void StepDamageResolution(float deltaSeconds)
        {
            // if (damagePoint > shipClass.damagePoint) // TODO: Temp workaround, this will be replaced with DE based implementation. 
            // {
            //     mapState = MapState.Destroyed;
            //     var logger = ServiceLocator.Get<ILoggerService>();
            //     logger.LogWarning($"{namedShip.name.GetMergedName()} ({objectId}) is destroyed");
            // }

            DamageControlAssetAllocation();

            // Damage Resolution

            // Step DE attached to this object and broadcast to sub-objects.
            base.StepDamageResolution(deltaSeconds);

            // Damage Effects from Crossing Damage Tier (possibly sinking caused)
            var p1 = damagePoint / Math.Max(1, shipClass.damagePoint);

            damagePoint += pendingDamagePoint;
            pendingDamagePoint = 0;

            var p2 = (damagePoint + pendingDamagePoint) / Math.Max(1, shipClass.damagePoint);

            RuleChart.ResolveCrossingDamageTierDamageEffects(p1, p2, namedShip.crewRating, out int damageEffectTier, out int damageEffectCount, out bool crossingDamageTierSinking, out bool abandonShip);

            for (int i = 0; i < damageEffectCount; i++)
            {
                var ctx = new DamageEffectContext()
                {
                    subject = this,
                    cause = DamageEffectCause.General,
                };
                DamageEffectChart.AddNewDamageEffect(ctx);
            }

            if (abandonShip)
            {
                operationalState = DamageEffectChart.MaxEnum(operationalState, ShipOperationalState.AbandonShip);
                AddStringLog("Ship Abandoned due to failed morale check");
            }

            if (crossingDamageTierSinking)
            {
                mapState = MapState.Destroyed;
                AddStringLog("Sunk due to Catastrophic damage (crossing 8 damage tier in 1 turn)");
            }

            // Sunk due to flooded machinery spaces
            var machinerySpaces = shipClass.speedKnotsEngineRoomsLevels.Count + shipClass.speedKnotsBoilerRooms.Count;
            var floodedMachinerySpaces = dynamicStatus.engineRoomFloodingHits + dynamicStatus.boilerRoomFloodingHits;
            var floodedPercent = floodedMachinerySpaces / Math.Max(1, machinerySpaces);

            if (floodedPercent >= NavalCombatCoreUtils.CalibrateSurviceProbFromTurnProb(0.8f, deltaSeconds))
            {
                mapState = MapState.Destroyed;
                AddStringLog("Sunk due to flooded machinery spaces");
            }
        }

        public float EvaluateArmorScore()
        {
            return EvaluateArmorScore(TargetAspect.Broad, RangeBand.Short);
        }

        public float EvaluateArmorScore(TargetAspect targetAspect, RangeBand rangeBand)
        {
            return shipClass?.EvaluateArmorScore(targetAspect, rangeBand) ?? 0;
        }

        public float EvaluateSurvivability()
        {
            var armorScoreSmoothed = 1 + EvaluateArmorScore();
            var dp = shipClass?.damagePoint ?? 0 - damagePoint;
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
            var torpedoBarrelsAvailable = torpedoSectorStatus.mountStatus.Where(m => m.IsOperational()).Sum(m => m.barrels);
            var torpedoThreatPerBarrel = shipClass?.torpedoSector?.EvaluateTorpedoThreatPerBarrel() ?? 0;
            return torpedoBarrelsAvailable * torpedoThreatPerBarrel;
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
                .Where(m => m.IsOperational() && m.currentLoad > 0)
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

        public int GetDamageTier()
        {
            // var maxDamagePoint = shipClass.damagePoint;
            // return (int)Math.Floor((damagePoint / maxDamagePoint) * 10);
            return RuleChart.GetDamageTier(damagePoint / Math.Max(1, shipClass?.damagePoint ?? 0));
        }

        public bool IsEvasiveManeuvering()
        {
            // TODO: Add Emergency turn? The evasive maneuver is not impelmented as well
            return isEvasiveManeuvering && !GetSubStates<IDynamicModifier>().Any(m => m.IsEvasiveManeuverBlocked());
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