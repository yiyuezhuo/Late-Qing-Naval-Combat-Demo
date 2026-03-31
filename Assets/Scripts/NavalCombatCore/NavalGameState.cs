using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using System.Xml;
using System.IO;
using System.Linq;
using GeographicLib;
using CoreUtils;
using YYZ;

namespace NavalCombatCore
{

    public enum PostureType
    {
        Friendly,
        Hostile,
        Neutral,
        Unknown
    }

    public class SimulationClock
    {
        public float intervalSeconds;
        public float accumulateSecond;
        public float elapsedSeconds;
        public int Step(float deltaSeconds)
        {
            var unresolved = deltaSeconds + accumulateSecond;
            var tick = (int)Math.Floor(unresolved / intervalSeconds);
            accumulateSecond = unresolved % intervalSeconds;

            elapsedSeconds += deltaSeconds;

            return tick;
        }
    }

    public class CountDownClock
    {
        [XmlAttribute]
        public float remainSeconds;

        public bool Step(float deltaSeconds)
        {
            if(remainSeconds == 0)
                return false;
            remainSeconds = Math.Max(0, remainSeconds - deltaSeconds);
            if(remainSeconds == 0)
                return true;
            return false;
        }

        public void Restart(float countDownSeconds)
        {
            remainSeconds = countDownSeconds;
        }
    }

    public partial class SubjectLog
    {
        public string subjectId;
        public ShipLogLog log;
    }

    public class NavalGameState : AbstractGameState
    {
        public static ObstacleAvoidanceMode playerControlObstacleAvoidanceMode = ObstacleAvoidanceMode.Weak;
        public const float TacticalManeuverDistanceYards = 16000f;
        public const float TacticalManeuverDistanceSquaredYards = TacticalManeuverDistanceYards * TacticalManeuverDistanceYards;
        public const float AutoEndDisengagedDistanceYards = 48000f;
        public const float AutoEndDisengagedDistanceSquaredYards = AutoEndDisengagedDistanceYards * AutoEndDisengagedDistanceYards;
        const float OperationalRouteReplanTargetDriftYards = 1000f;

        public enum AutomaticManeuverMode
        {
            Tactical,
            Operational
        }

        public readonly struct RootGroupHostileSeparationKey : IEquatable<RootGroupHostileSeparationKey>
        {
            public readonly string rootGroupObjectIdA;
            public readonly string rootGroupObjectIdB;

            public RootGroupHostileSeparationKey(string rootGroupObjectIdA, string rootGroupObjectIdB)
            {
                if (string.CompareOrdinal(rootGroupObjectIdA, rootGroupObjectIdB) <= 0)
                {
                    this.rootGroupObjectIdA = rootGroupObjectIdA;
                    this.rootGroupObjectIdB = rootGroupObjectIdB;
                }
                else
                {
                    this.rootGroupObjectIdA = rootGroupObjectIdB;
                    this.rootGroupObjectIdB = rootGroupObjectIdA;
                }
            }

            public bool Equals(RootGroupHostileSeparationKey other)
            {
                return rootGroupObjectIdA == other.rootGroupObjectIdA
                    && rootGroupObjectIdB == other.rootGroupObjectIdB;
            }

            public override bool Equals(object obj)
            {
                return obj is RootGroupHostileSeparationKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(rootGroupObjectIdA, rootGroupObjectIdB);
            }
        }

        public sealed class ShipHostileProximityInfo
        {
            public ShipLog ship;
            public ShipLog nearestEnemy;
            public float nearestEnemyDistanceSquaredYards = float.PositiveInfinity;
            public AutomaticManeuverMode automaticManeuverMode = AutomaticManeuverMode.Operational;
        }

        public sealed class RootGroupHostileSeparationInfo
        {
            public IShipGroupMember rootGroupA;
            public IShipGroupMember rootGroupB;
            public float minDistanceSquaredYards = float.PositiveInfinity;
        }

        public sealed class HostileProximitySnapshot
        {
            public long minuteKey;
            internal readonly Dictionary<string, ShipHostileProximityInfo> shipInfoByShipObjectId = new();
            internal readonly Dictionary<RootGroupHostileSeparationKey, RootGroupHostileSeparationInfo> rootGroupSeparationByKey = new();
        }

        struct ControlChainEdge
        {
            public ShipLog controlledShip;
            public ShipLog targetShip;
            public ControlMode controlMode;
            public float distanceYards;
            public float azimuthDeg;
            public bool relativeToAbsolute;
        }

        static readonly Dictionary<ControlMode, int> formationControlModePriority = new()
        {
            { ControlMode.FollowTarget, -2 },
            { ControlMode.RelativeToTarget, -1 },
        };

        [XmlIgnore]
        HostileProximitySnapshot hostileProximitySnapshot;

        static ObstacleAvoidanceParameters ResolveObstacleAvoidanceParameters(ShipLog shipLog)
        {
            if (shipLog?.doctrine.GetManeuverAutomaticType() == AutomaticType.Automatic)
            {
                var controlRoot = shipLog.GetControlRoot();
                if (controlRoot != null
                    && controlRoot.GetEffectiveControlMode() == ControlMode.Independent
                    && Instance.TryGetShipHostileProximityInfo(controlRoot, out var proximityInfo))
                {
                    return proximityInfo.automaticManeuverMode == AutomaticManeuverMode.Tactical
                        ? ObstacleAvoidanceParameters.Strong
                        : ObstacleAvoidanceParameters.Weak;
                }

                return ObstacleAvoidanceParameters.Strong;
            }

            return playerControlObstacleAvoidanceMode switch
            {
                ObstacleAvoidanceMode.None => null,
                ObstacleAvoidanceMode.Weak => ObstacleAvoidanceParameters.Weak,
                ObstacleAvoidanceMode.Strong => ObstacleAvoidanceParameters.Strong,
                _ => ObstacleAvoidanceParameters.Weak
            };
        }

        static void DisableAllSearchlights(ShipLog ship)
        {
            if (ship?.searchLightHits == null)
                return;

            ship.searchLightHits.portEnabled = false;
            ship.searchLightHits.starboardEnabled = false;
        }

        static IEnumerable<ShipLog> EnumerateOrderedSearchlightTargets(ShipLog ship)
        {
            if (ship == null)
                yield break;

            foreach (var battery in ship.batteryStatus)
            {
                foreach (var mount in battery.mountStatus)
                {
                    var target = mount.GetFiringTarget();
                    if (target != null)
                        yield return target;
                }
            }

            foreach (var rapidBattery in ship.rapidFiringStatus)
            {
                foreach (var targetting in rapidBattery.targettingRecords)
                {
                    var target = targetting.GetTarget();
                    if (target != null)
                        yield return target;
                }
            }
        }

        void ApplyAutomaticSearchlightAssignments(IEnumerable<ShipLog> ships)
        {
            var illuminatedTargetIds = new HashSet<string>();
            var orderedShips = ships
                .Where(ship => ship != null)
                .OrderBy(ship => ship.shipClass?.displacementTons ?? 0f)
                .ThenBy(ship => ship.objectId)
                .ToList();

            foreach (var ship in orderedShips)
            {
                if (ship?.searchLightHits == null)
                    continue;
                if (ship.doctrine?.GetSearchlightAutomaticType() != AutomaticType.Automatic)
                    continue;

                DisableAllSearchlights(ship);

                var ownSunState = scenarioState.GetSunPosition(ship.position);
                if (ownSunState == null || ownSunState.GetDayNightLevel() == DayNightLevel.Day)
                    continue;

                var portAssigned = false;
                var starboardAssigned = false;
                foreach (var target in EnumerateOrderedSearchlightTargets(ship))
                {
                    if (target == null || string.IsNullOrWhiteSpace(target.objectId) || illuminatedTargetIds.Contains(target.objectId))
                        continue;

                    if (!NavalUtils.TryResolveSearchlightTargetAssignment(ship, target, out var side, out var directionDeg))
                        continue;

                    if (side == RapidFiringBatteryLocation.Port)
                    {
                        if (portAssigned)
                            continue;

                        ship.searchLightHits.portDirectionDeg = directionDeg;
                        ship.searchLightHits.portEnabled = true;
                        portAssigned = ship.searchLightHits.portEnabled;
                        if (portAssigned)
                            illuminatedTargetIds.Add(target.objectId);
                    }
                    else
                    {
                        if (starboardAssigned)
                            continue;

                        ship.searchLightHits.starboardDirectionDeg = directionDeg;
                        ship.searchLightHits.starboardEnabled = true;
                        starboardAssigned = ship.searchLightHits.starboardEnabled;
                        if (starboardAssigned)
                            illuminatedTargetIds.Add(target.objectId);
                    }

                    if (portAssigned && starboardAssigned)
                        break;
                }
            }
        }

        static long GetMinuteKey(DateTime dateTime)
        {
            return dateTime.Ticks / TimeSpan.TicksPerMinute;
        }

        static bool IsSnapshotManeuverCandidateShip(ShipLog shipLog)
        {
            return shipLog != null
                && shipLog.mapState == MapState.Deployed
                && shipLog.operationalState == ShipOperationalState.Operational
                && !shipLog.IsLandBattery();
        }

        static bool IsSnapshotAutoEndOperationalShip(ShipLog shipLog)
        {
            return shipLog != null
                && shipLog.mapState == MapState.Deployed
                && shipLog.operationalState == ShipOperationalState.Operational
                && (
                    shipLog.GetMaxSpeedKnots() > 0
                    || (shipLog.IsLandBattery() && shipLog.isLandTarget)
                );
        }

        static MeasureUtils.LocalProjection BuildHostileProximityProjection(IReadOnlyList<ShipLog> shipLogs)
        {
            var minLat = float.PositiveInfinity;
            var maxLat = float.NegativeInfinity;
            var minLon = float.PositiveInfinity;
            var maxLon = float.NegativeInfinity;

            foreach (var shipLog in shipLogs)
            {
                minLat = Math.Min(minLat, shipLog.position.LatDeg);
                maxLat = Math.Max(maxLat, shipLog.position.LatDeg);
                minLon = Math.Min(minLon, shipLog.position.LonDeg);
                maxLon = Math.Max(maxLon, shipLog.position.LonDeg);
            }

            return new MeasureUtils.LocalProjection(
                (minLat + maxLat) * 0.5f,
                (minLon + maxLon) * 0.5f
            );
        }

        HostileProximitySnapshot BuildHostileProximitySnapshot()
        {
            var snapshot = new HostileProximitySnapshot()
            {
                minuteKey = GetMinuteKey(scenarioState.dateTime)
            };

            var maneuverCandidateShips = shipLogs.Where(IsSnapshotManeuverCandidateShip).ToList();
            var autoEndOperationalShips = shipLogs.Where(IsSnapshotAutoEndOperationalShip).ToList();
            var projectedShips = maneuverCandidateShips
                .Concat(autoEndOperationalShips)
                .Where(ship => ship != null)
                .GroupBy(ship => ship.objectId)
                .Select(group => group.First())
                .ToList();
            if (projectedShips.Count == 0)
                return snapshot;

            var projection = BuildHostileProximityProjection(projectedShips);
            var projectedByShipObjectId = projectedShips.ToDictionary(
                ship => ship.objectId,
                ship => (
                    rootGroup: ((IShipGroupMember)ship).GetRootParent(),
                    xYards: projection.LongitudeToX(ship.position.LonDeg),
                    yYards: projection.LatitudeToY(ship.position.LatDeg)
                ));

            foreach (var ship in maneuverCandidateShips)
            {
                if (!projectedByShipObjectId.TryGetValue(ship.objectId, out var projectedShip))
                    continue;

                var info = new ShipHostileProximityInfo()
                {
                    ship = ship
                };
                foreach (var enemy in maneuverCandidateShips)
                {
                    if (enemy == ship)
                        continue;
                    if (!projectedByShipObjectId.TryGetValue(enemy.objectId, out var projectedEnemy))
                        continue;
                    if (projectedShip.rootGroup == projectedEnemy.rootGroup)
                        continue;

                    var dx = projectedShip.xYards - projectedEnemy.xYards;
                    var dy = projectedShip.yYards - projectedEnemy.yYards;
                    var distanceSquaredYards = dx * dx + dy * dy;
                    if (distanceSquaredYards < info.nearestEnemyDistanceSquaredYards)
                    {
                        info.nearestEnemy = enemy;
                        info.nearestEnemyDistanceSquaredYards = distanceSquaredYards;
                    }
                }

                info.automaticManeuverMode = info.nearestEnemy != null
                    && info.nearestEnemyDistanceSquaredYards <= TacticalManeuverDistanceSquaredYards
                    ? AutomaticManeuverMode.Tactical
                    : AutomaticManeuverMode.Operational;
                snapshot.shipInfoByShipObjectId[ship.objectId] = info;
            }

            for (var i = 0; i < autoEndOperationalShips.Count; i++)
            {
                var shipA = autoEndOperationalShips[i];
                if (!projectedByShipObjectId.TryGetValue(shipA.objectId, out var projectedShipA))
                    continue;

                for (var j = i + 1; j < autoEndOperationalShips.Count; j++)
                {
                    var shipB = autoEndOperationalShips[j];
                    if (!projectedByShipObjectId.TryGetValue(shipB.objectId, out var projectedShipB))
                        continue;
                    if (projectedShipA.rootGroup == projectedShipB.rootGroup)
                        continue;

                    var separationKey = new RootGroupHostileSeparationKey(
                        projectedShipA.rootGroup.objectId,
                        projectedShipB.rootGroup.objectId);
                    if (!snapshot.rootGroupSeparationByKey.TryGetValue(separationKey, out var separationInfo))
                    {
                        separationInfo = new RootGroupHostileSeparationInfo()
                        {
                            rootGroupA = projectedShipA.rootGroup,
                            rootGroupB = projectedShipB.rootGroup
                        };
                        snapshot.rootGroupSeparationByKey[separationKey] = separationInfo;
                    }

                    var dx = projectedShipA.xYards - projectedShipB.xYards;
                    var dy = projectedShipA.yYards - projectedShipB.yYards;
                    var distanceSquaredYards = dx * dx + dy * dy;
                    if (distanceSquaredYards < separationInfo.minDistanceSquaredYards)
                    {
                        separationInfo.minDistanceSquaredYards = distanceSquaredYards;
                    }
                }
            }

            return snapshot;
        }

        public void EnsureHostileProximitySnapshotCurrent()
        {
            var currentMinuteKey = GetMinuteKey(scenarioState.dateTime);
            if (hostileProximitySnapshot != null && hostileProximitySnapshot.minuteKey == currentMinuteKey)
                return;

            hostileProximitySnapshot = BuildHostileProximitySnapshot();
        }

        public bool TryGetShipHostileProximityInfo(ShipLog shipLog, out ShipHostileProximityInfo info)
        {
            info = null;
            if (shipLog == null)
                return false;

            EnsureHostileProximitySnapshotCurrent();
            return hostileProximitySnapshot != null
                && hostileProximitySnapshot.shipInfoByShipObjectId.TryGetValue(shipLog.objectId, out info);
        }

        public bool AreOperationalRootGroupsDisengaged(IReadOnlyList<IShipGroupMember> operationalRootGroups)
        {
            if (operationalRootGroups == null || operationalRootGroups.Count <= 1)
                return false;

            EnsureHostileProximitySnapshotCurrent();
            if (hostileProximitySnapshot == null)
                return false;

            for (var i = 0; i < operationalRootGroups.Count; i++)
            {
                var rootGroupA = operationalRootGroups[i];
                if (rootGroupA == null)
                    return false;

                for (var j = i + 1; j < operationalRootGroups.Count; j++)
                {
                    var rootGroupB = operationalRootGroups[j];
                    if (rootGroupB == null)
                        return false;

                    var key = new RootGroupHostileSeparationKey(rootGroupA.objectId, rootGroupB.objectId);
                    if (!hostileProximitySnapshot.rootGroupSeparationByKey.TryGetValue(key, out var separationInfo))
                        return false;
                    if (separationInfo.minDistanceSquaredYards <= AutoEndDisengagedDistanceSquaredYards)
                        return false;
                }
            }

            return true;
        }

        static List<LatLon> ExtractPathRouteSegmentPoints(PathfindingResult result)
        {
            var extractedPoints = new List<LatLon>();
            if (result?.success != true || result.points == null || result.points.Count <= 1)
                return extractedPoints;

            var startIndex = 1;
            var endExclusive = result.points.Count;
            if (endExclusive - startIndex > 1)
                endExclusive--;

            for (var i = startIndex; i < endExclusive; i++)
            {
                var point = result.points[i];
                if (point != null)
                    extractedPoints.Add(point.Clone());
            }

            if (extractedPoints.Count == 0)
            {
                var fallbackPoint = result.points[^1];
                if (fallbackPoint != null)
                    extractedPoints.Add(fallbackPoint.Clone());
            }

            return extractedPoints;
        }

        static bool TryBuildAutomaticOperationalRoute(ShipLog shipLog, LatLon targetPosition, out List<LatLon> routePoints)
        {
            routePoints = null;
            if (shipLog == null || targetPosition == null)
                return false;
            if (ElevationService.Instance.elevationProvider is not ElevationProvider elevationProvider || !elevationProvider.HasValidROIShoreField())
                return false;

            var threshold = GamePreference.Instance.pathfindingShorePassableDistancePixels;
            var sourcePoint = shipLog.position;
            var exactPathfinder = new ExactROIShoreFieldPathfinder(elevationProvider, sourcePoint);
            var exactResult = exactPathfinder.FindPath(sourcePoint, targetPosition, threshold);
            PathfindingResult selectedResult = exactResult;
            if (exactResult != null
                && !exactResult.success
                && exactResult.failureReason == PathfindingFailureReason.SearchWindowExceeded)
            {
                var coarsePathfinder = new ROIShoreFieldPathfinder(elevationProvider);
                selectedResult = coarsePathfinder.FindPath(sourcePoint, targetPosition, threshold);
            }

            routePoints = ExtractPathRouteSegmentPoints(selectedResult);
            return routePoints.Count > 0;
        }

        void ApplyOperationalCombatRoute(ShipLog shipLog, ShipHostileProximityInfo proximityInfo)
        {
            var targetShip = proximityInfo?.nearestEnemy;
            var targetPosition = targetShip?.position;
            if (targetShip == null || targetPosition == null)
            {
                shipLog.ClearAutomaticOperationalRouteState();
                return;
            }

            if (!shipLog.ShouldReplanAutomaticOperationalRoute(targetShip, targetPosition, OperationalRouteReplanTargetDriftYards))
                return;

            if (TryBuildAutomaticOperationalRoute(shipLog, targetPosition, out var routePoints))
            {
                shipLog.ReplaceRouteFromPath(routePoints);
                shipLog.SetAutomaticOperationalRouteState(targetShip, targetPosition);
                return;
            }

            shipLog.ClearManualRoute();
            shipLog.ClearAutomaticOperationalRouteState();
        }

        static void ApplyOperationalRetreatHeading(ShipLog shipLog, ShipHostileProximityInfo proximityInfo)
        {
            var enemyPosition = proximityInfo?.nearestEnemy?.position;
            if (shipLog == null || enemyPosition == null)
                return;

            shipLog.desiredHeadingDeg = MeasureUtils.NormalizeAngle((float)MeasureStats.Approximation.CalculateInitialBearing(
                enemyPosition.LatDeg, enemyPosition.LonDeg,
                shipLog.position.LatDeg, shipLog.position.LonDeg));
        }

        HashSet<ShipLog> ApplyAutomaticManeuverModes(IReadOnlyList<ShipLog> autoOperationalShipLogs)
        {
            var tacticalControlRoots = new HashSet<ShipLog>();
            foreach (var shipLog in autoOperationalShipLogs)
            {
                if (shipLog.GetEffectiveControlMode() != ControlMode.Independent)
                    continue;
                if (!TryGetShipHostileProximityInfo(shipLog, out var proximityInfo))
                    continue;

                if (proximityInfo.automaticManeuverMode == AutomaticManeuverMode.Tactical)
                {
                    shipLog.ClearAutomaticOperationalRouteState();
                    if (shipLog.HasManualRoute())
                        shipLog.ClearManualRoute();
                    tacticalControlRoots.Add(shipLog);
                    continue;
                }

                if (shipLog.shipClass?.IsCombatShip() == true)
                {
                    ApplyOperationalCombatRoute(shipLog, proximityInfo);
                }
                else
                {
                    shipLog.ClearManualRoute();
                    shipLog.ClearAutomaticOperationalRouteState();
                    ApplyOperationalRetreatHeading(shipLog, proximityInfo);
                }
            }

            return tacticalControlRoots;
        }

        public List<ShipGroup> shipGroups = new();
        public ScenarioState scenarioState = new();
        public List<LaunchedTorpedo> launchedTorpedos = new();
        public List<SubjectLog> tempSubjectLogs = new();

        public event EventHandler<List<ShipGroup>> shipGroupsChanged;

        static NavalGameState _instance;
        public static NavalGameState Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new();
                }
                return _instance;
            }
        }
        public static void UpdateInstance(NavalGameState newInstance)
        {
            _instance = newInstance;
            Instance.scenarioState?.FillBeginDateTimeIfMissing();

            Instance.ResetAndRegisterAll();
            Instance.SyncShipLogParentWithGroupHierarchy();

            // rootShipGroupsChanged?.Invoke(this, rootShipGroups);
            Instance.shipGroupsChanged?.Invoke(Instance, Instance.shipGroups);
        }

        public static void ClearInstance()
        {
            _instance = null;
        }

        public override void ResetAndRegisterAll()
        {
            base.ResetAndRegisterAll();

            foreach (var shipLog in shipLogs)
            {
                shipLog.EnforceLandBatteryFixedKinematics();
            }

            foreach (var shipGroup in shipGroups)
            {
                EntityManager.Instance.Register(shipGroup, null);
                // ResetAndRegisterAllShipGroup(shipGroup);
            }

            // Since currently no one is referencing torpedo so it's not necessary.
            foreach (var torpedo in launchedTorpedos)
            {
                EntityManager.Instance.Register(torpedo, null);
            }
        }

        public string ShipGroupsToXML()
        {
            return XmlUtils.ToXML(shipGroups);
        }

        public void ShipGroupsFromXML(string xml)
        {
            shipGroups = XmlUtils.FromXML<List<ShipGroup>>(xml);

            ResetAndRegisterAll();
            SyncShipLogParentWithGroupHierarchy();

            shipGroupsChanged?.Invoke(this, shipGroups);
        }

        public void ScenarioStateFromXML(string xml)
        {
            scenarioState = XmlUtils.FromXML<ScenarioState>(xml);
            scenarioState?.FillBeginDateTimeIfMissing();
        }

        public string ScenarioStateToXML()
        {
            return XmlUtils.ToXML(scenarioState);
        }

        public void SyncShipLogParentWithGroupHierarchy()
        {
            var shipTracked = new HashSet<string>();

            foreach (var shipGroup in shipGroups)
            {
                foreach (var child in shipGroup.GetChildren()) // if it's not resolved, child may be null and raise exception
                {
                    if (child != null)
                    {
                        child.parentObjectId = shipGroup.objectId;
                        if (child is ShipLog subShipLog)
                        {
                            shipTracked.Add(subShipLog.objectId);
                        }
                    }
                }
            }

            foreach (var shipLog in shipLogs)
            {
                if (!shipTracked.Contains(shipLog.objectId))
                {
                    shipLog.parentObjectId = null;
                }
            }
        }

        // public void UpdateTo(NavalGameState newState)
        // {
        //     shipClasses = newState.shipClasses;
        //     shipLogs = newState.shipLogs;
        //     // rootShipGroups = newState.rootShipGroups;
        //     shipGroups = newState.shipGroups;

        //     ResetAndRegisterAll();
        //     SyncShipLogParentWithGroupHierarchy();

        //     // rootShipGroupsChanged?.Invoke(this, rootShipGroups);
        //     shipGroupsChanged?.Invoke(this, shipGroups);
        // }

        public IEnumerable<IShipGroupMember> GetShipGroupMembersRecursive()
        {
            foreach (var shipGroup in shipGroups)
            {
                if (shipGroup.parentObjectId == null) // "root" groups
                {
                    foreach (var ret in GetShipGroupMembersRecursive(shipGroup))
                    {
                        yield return ret;
                    }
                }
            }
        }

        public IEnumerable<IShipGroupMember> GetShipGroupMembersRecursive(ShipGroup shipGroup)
        {
            yield return shipGroup;

            foreach (var child in shipGroup.GetChildren())
            {
                if (child is ShipLog subShipLog)
                {
                    yield return subShipLog;
                }
                else if (child is ShipGroup subShipGroup)
                {
                    foreach (var ret in GetShipGroupMembersRecursive(subShipGroup))
                    {
                        yield return ret;
                    }
                }
            }
        }

        public Dictionary<IShipGroupMember, PostureType> CalcualtePostureMap(IShipGroupMember refGroup)
        {
            var refRoot = FindRoot(refGroup);
            return GetShipGroupMembersRecursive().ToDictionary(
                g => g,
                g => FindRoot(g) == refRoot ? PostureType.Friendly : PostureType.Hostile
            );
        }

        public static IShipGroupMember FindRoot(IShipGroupMember member)
        {
            if (member == null)
                return null;

            var p = member;
            while (p.GetParentGroup() != null)
            {
                p = p.GetParentGroup();
            }
            return p;
        }

        static Dictionary<ControlMode, int> controModeToScore = new()
        {
            {ControlMode.RelativeToTarget, -1},
            {ControlMode.FollowTarget, -2} // priority
        };


        public void ProcessZeroSpeedFormationAdjustment()
        {
            var immobilizedShipLogSet = shipLogs.Where(shipLog => shipLog.mapState != MapState.Deployed || shipLog.GetMaxSpeedKnots() <= 4).ToHashSet();

            foreach(var grouping in shipLogsOnMap.GroupBy(shipLog => shipLog.GetControlPredecessor()))
            {
                var predShipLog = grouping.Key;
                
                if(predShipLog != null && // Handle subs which is not effective independent 
                    immobilizedShipLogSet.Contains(predShipLog)) // but predecessor is sunk, missing or lost of speed
                {
                    var subShipLogs = grouping.ToList();
                    subShipLogs.Sort((s1, s2) => controModeToScore.GetValueOrDefault(s1.controlMode).CompareTo(controModeToScore.GetValueOrDefault(s2.controlMode)));
                    
                    // Handle inherit
                    var inheritShipLog = subShipLogs[0];
                    var predShipLogControlMode = predShipLog.GetEffectiveControlMode();
                    if(predShipLogControlMode == ControlMode.Independent)
                    {
                        inheritShipLog.controlMode = ControlMode.Independent;
                    }
                    else if(predShipLogControlMode == ControlMode.FollowTarget)
                    {
                        inheritShipLog.controlMode = ControlMode.FollowTarget;
                        inheritShipLog.followedTargetObjectId = predShipLog.followedTargetObjectId;
                    }
                    else if(predShipLogControlMode == ControlMode.RelativeToTarget) // Those shit should be refactored
                    {
                        inheritShipLog.controlMode = ControlMode.RelativeToTarget;
                        inheritShipLog.relativeTargetObjectId = predShipLog.relativeTargetObjectId;
                        inheritShipLog.relativeToTargetAzimuth = predShipLog.relativeToTargetAzimuth;
                        inheritShipLog.relativeToTargetDistanceYards = predShipLog.relativeToTargetDistanceYards;
                        inheritShipLog.relativeToAbsolute = predShipLog.relativeToAbsolute;
                    }

                    // Handle retarget
                    foreach(var subShipLog in subShipLogs.Skip(1))
                    {
                        if(subShipLog.controlMode == ControlMode.FollowTarget)
                        {
                            subShipLog.followedTargetObjectId = inheritShipLog.objectId;
                        }
                        else if(subShipLog.controlMode == ControlMode.RelativeToTarget)
                        {
                            subShipLog.relativeTargetObjectId = inheritShipLog.objectId;
                        }
                    }
                }
            }

            // Auto-detach
            foreach(var immobilizedShipLog in immobilizedShipLogSet)
            {
                immobilizedShipLog.controlMode = ControlMode.Independent;
            }
        }

        public void _ProcessZeroSpeedFormationAdjustment()
        {
            var immobilizedShipLogIds = shipLogs.Where(shipLog => shipLog.mapState != MapState.Deployed || shipLog.GetMaxSpeedKnots() <= 4).Select(shipLog => shipLog.objectId).ToHashSet(); // `<= 4` => cannot to turn => impossible to main formation

            var fixedAny = false;
            do
            {
                fixedAny = false;

                foreach (var shipLog in shipLogsOnMap)
                {
                    if (shipLog.controlMode == ControlMode.FollowTarget && immobilizedShipLogIds.Contains(shipLog.followedTargetObjectId))
                    {
                        var immobilizedShipLog = EntityManager.Instance.Get<ShipLog>(shipLog.followedTargetObjectId);
                        shipLog.followedTargetObjectId = immobilizedShipLog.followedTargetObjectId;
                        fixedAny = true;
                    }
                    else if (shipLog.controlMode == ControlMode.RelativeToTarget && immobilizedShipLogIds.Contains(shipLog.relativeTargetObjectId))
                    {
                        var immobilizedShipLog = EntityManager.Instance.Get<ShipLog>(shipLog.relativeTargetObjectId);
                        // FixRelativeTree(immobilizedShipLog, immobilizedShipLog.relativeToTargetAzimuth, immobilizedShipLog.relativeToTargetDistanceYards, true);
                        FixRelativeTree2(immobilizedShipLog, true);
                        fixedAny = true;
                    }
                }
            } while (fixedAny);
        }

        // public void FixInvalidControlledShipLog(ShipLog shipLog, ShipLog prevControlShipLog)
        // {
        //     if(prevControlShipLog.controlMode == ControlMode.Independent)
        //     {
        //         shipLog.controlMode = ControlMode.Independent;
        //     }
        //     else if(shipLog.controlMode == ControlMode.FollowTarget && prevControlShipLog.GetEffectiveControlMode() == ControlMode.FollowTarget)
        //     {
        //         shipLog.followedTargetObjectId = prevControlShipLog.followedTargetObjectId;
        //     }
        // }

        void FixRelativeTree(ShipLog displacedShipLog, float azimuth, float distance, bool absolute, bool first)
        {
            // var displacedShipLog = EntityManager.Instance.Get<ShipLog>(displacedId);

            var subs = shipLogsOnMap.Where(shipLog => shipLog.controlMode == ControlMode.RelativeToTarget && shipLog.relativeTargetObjectId == displacedShipLog.objectId).ToList();
            if (subs.Count == 0)
                return;
            var newAnchor = subs[0];

            var azimuth2 = newAnchor.relativeToTargetAzimuth;
            var distance2 = newAnchor.relativeToTargetDistanceYards;
            var absolute2 = newAnchor.relativeToAbsolute;

            if (first)
            {
                newAnchor.relativeTargetObjectId = displacedShipLog.relativeTargetObjectId;
            }

            newAnchor.relativeToTargetAzimuth = azimuth;
            newAnchor.relativeToTargetDistanceYards = distance;
            newAnchor.relativeToAbsolute = absolute;

            foreach (var sub in subs.Skip(1))
            {
                sub.relativeTargetObjectId = newAnchor.objectId;
            }

            FixRelativeTree(newAnchor, azimuth2, distance2, absolute2, false);
        }

        void FixRelativeTree2(ShipLog displacedShipLog, bool first)
        {
            var subs = shipLogsOnMap.Where(shipLog => shipLog.controlMode == ControlMode.RelativeToTarget && shipLog.relativeTargetObjectId == displacedShipLog.objectId).ToList();
            if (subs.Count > 0)
            {
                FixRelativeTree2(subs[0], false);
                foreach (var sub in subs.Skip(1))
                {
                    sub.relativeTargetObjectId = subs[0].objectId;
                }
            }
            if (first)
            {
                if (subs.Count > 0)
                {
                    subs[0].relativeTargetObjectId = displacedShipLog.relativeTargetObjectId;
                }
            }
            else
            {
                var relativeToTarget = EntityManager.Instance.Get<ShipLog>(displacedShipLog.relativeTargetObjectId);
                displacedShipLog.relativeToTargetAzimuth = relativeToTarget.relativeToTargetAzimuth;
                displacedShipLog.relativeToTargetDistanceYards = relativeToTarget.relativeToTargetDistanceYards;
                displacedShipLog.relativeToAbsolute = relativeToTarget.relativeToAbsolute;
            }
        }

        public void Step(float deltaSeconds)
        {
            scenarioState.doingStep = true;
            var shipLogsOnMapList = shipLogs.Where(x => x.mapState == MapState.Deployed).ToList();
            var autoOperationalShipLogs = shipLogsOnMapList.Where(s =>
                s.operationalState == ShipOperationalState.Operational
                && !s.IsLandBattery()
                && s.doctrine.GetManeuverAutomaticType() == AutomaticType.Automatic
            ).ToList();
            var obstacleAvoidOperationalShipLogs = shipLogsOnMapList.Where(s =>
                s.operationalState == ShipOperationalState.Operational
                && !s.IsLandBattery()
            ).ToList();

            // tempSubjectLogs.Clear();

            // pre-advance resolution
            var weaponSimulationAssignmentClockTicked = scenarioState.weaponSimulationAssignmentClock.Step(deltaSeconds) > 0;
            if (weaponSimulationAssignmentClockTicked) // The clock is not limited to Weapon allocation though
            {
                EnsureHostileProximitySnapshotCurrent();
                var tacticalControlRoots = ApplyAutomaticManeuverModes(autoOperationalShipLogs);
                foreach ((var meShipLogs, var otherShipLogs) in GetOpposeSidePairs())
                {
                    var solver = new WeaponTargetAssignmentSolver();
                    solver.Solve(
                        meShipLogs.Where(s => s.doctrine.GetFireAutomaticType() == AutomaticType.Automatic),
                        otherShipLogs
                    );

                    var planner = new LowLevelCoursePlanner();
                    planner.Plan(
                        meShipLogs.Where(s =>
                            s.doctrine.GetManeuverAutomaticType() == AutomaticType.Automatic
                            && s.GetControlRoot().doctrine.GetManeuverAutomaticType() == AutomaticType.Automatic
                            && tacticalControlRoots.Contains(s.GetControlRoot())),
                        otherShipLogs,
                        CoreParameter.Instance.extrapolateSeconds
                    ); // Extrapolate 360s
                }

                foreach(var g in shipLogsOnMapList.GroupBy(s => s.GetControlRoot()))
                {
                    var leadShipLog = g.Key;
                    if(leadShipLog.doctrine.GetManeuverAutomaticType() == AutomaticType.Automatic)
                    {
                        var leadMaxSpeed = leadShipLog.GetMaxSpeedKnots();
                        var desiredSpeedKnots = g.Select(s => s.GetMaxSpeedKnots()).Where(s => s >= 4).DefaultIfEmpty(leadMaxSpeed).Min();
                        leadShipLog.desiredSpeedKnots = Math.Max(0, desiredSpeedKnots - 2); // RTW stlye max speed - 2
                    }
                    // leadShipLog.desiredSpeedKnots = desiredSpeedKnots;// Group's max speed
                }

                ApplyAutomaticSearchlightAssignments(shipLogsOnMapList);

                // foreach(var shipLog in leadShipLogs)
                // {
                //     shipLog.desiredSpeedKnots = shipLog.GetMaxSpeedKnots();
                //     // TODO: Limit the speed with controlled speed
                // }

                // // Obstacle Avoid
                // // foreach(var shipLog in leadShipLogs)
                // foreach(var shipLog in activceShipLogs)
                // {
                //     var checker = ObstacleAvoidChecker.Extract(shipLog);
                //     shipLog.desiredHeadingDeg = checker.Check();
                // }

            }

            // Reset Formation - zero speed is detached automatically, "children" reset their targets according to detached unit's previous command.
            ProcessZeroSpeedFormationAdjustment(); // TODO: Is it too frequenct to do it every step?

            // Advance
            scenarioState.Step(deltaSeconds);

            foreach (var shipLog in shipLogsOnMapList)
                shipLog.dirtySeconds = deltaSeconds; // update heading

            foreach (var shipLog in shipLogsOnMapList)
                shipLog.StepProcessTurn(deltaSeconds); // update heading

            foreach (var shipLog in shipLogsOnMapList)
                shipLog.StepProcessControl(deltaSeconds); // set desired heading / desired speed

            // Obstacle Avoid
            // if(weaponSimulationAssignmentClockTicked)
            // {
            // }

            // if(scenarioState.obstacleAvoidCheckClock.Step(deltaSeconds) > 0)
            // {
            //     foreach(var shipLog in shipLogs.Where(s =>
            //         s.mapState == MapState.Deployed
            //         && s.operationalState == ShipOperationalState.Operational
            //         // && s.speedKnots > 0
            //         && s.doctrine.GetManeuverAutomaticType() == AutomaticType.Automatic
            //     ))
            //     {
            //         var checker = ObstacleAvoidChecker.Extract(shipLog);
            //         var newDesiredHeadingDeg = checker.Check();
            //         shipLog.preCollsionAvoiding = newDesiredHeadingDeg != shipLog.desiredHeadingDeg;
            //         shipLog.desiredHeadingDeg = newDesiredHeadingDeg;
            //     }
            // }

            // always mode
            foreach (var shipLog in obstacleAvoidOperationalShipLogs)
            {
                var obstacleAvoidanceParameters = ResolveObstacleAvoidanceParameters(shipLog);
                if (obstacleAvoidanceParameters == null)
                {
                    shipLog.preCollsionAvoiding = false;
                    continue;
                }

                var checker = ObstacleAvoidChecker.Extract(shipLog, obstacleAvoidanceParameters, false);
                var newDesiredHeadingDeg = checker.Check();
                shipLog.preCollsionAvoiding = newDesiredHeadingDeg != shipLog.desiredHeadingDeg;
                shipLog.desiredHeadingDeg = newDesiredHeadingDeg;
            }

            foreach (var shipLog in shipLogsOnMapList)
                shipLog.StepProcessSpeed(deltaSeconds); // update speed

            foreach (var shipLog in shipLogsOnMapList)
                shipLog.StepTryMoveToNewPosition(deltaSeconds); // update position

            // PrecalculationContext.Instance.gunneryFireContext.Calculate();
            using (GunneryFireContext.Begin())
            {
                foreach (var shipLog in shipLogsOnMapList)
                    shipLog.StepBatteryStatus(deltaSeconds); // gunnery resolution

                // Use GunneryFireContext's LOS result
                using (TorpedoAttackContext.Begin())
                {
                    foreach (var shipLog in shipLogsOnMapList)
                    {
                        shipLog.StepTorpedoSector(deltaSeconds);
                    }
                }
            }

            foreach (var shipLog in shipLogsOnMapList)
                shipLog.StepDamageResolution(deltaSeconds);

            foreach (var launchedTorpedo in launchedTorpedosOnMap)
                launchedTorpedo.StepMoveToNewPosition(deltaSeconds); // TODO: Move before damage resolution?

            foreach (var shipLog in shipLogsOnMapList)
                shipLog.StepLogging();

            EnsureHostileProximitySnapshotCurrent();
            scenarioState.doingStep = false;
        }

        public IEnumerable<ShipLog> shipLogsOnMap => shipLogs.Where(x => x.mapState == MapState.Deployed);
        public IEnumerable<LaunchedTorpedo> launchedTorpedosOnMap => launchedTorpedos.Where(x => x.mapState == MapState.Deployed);
        public IEnumerable<ShipLog> shipLogsOnMapOrDestroyed => shipLogs.Where(x => x.mapState == MapState.Deployed || x.mapState == MapState.Destroyed);

        public Dictionary<IShipGroupMember, List<ShipLog>> GroupByShipLogByRootGroup()
        {
            var ret = new Dictionary<IShipGroupMember, List<ShipLog>>();
            foreach (var shipLog in shipLogsOnMap)
            {
                var rootParent = (shipLog as IShipGroupMember).GetRootParent();
                if (!ret.TryGetValue(rootParent, out var list))
                {
                    list = ret[rootParent] = new List<ShipLog>();
                }
                list.Add(shipLog);
            }
            return ret;
        }

        public IEnumerable<(List<ShipLog>, List<ShipLog>)> GetOpposeSidePairs()
        {
            var rootToShipLogs = GroupByShipLogByRootGroup();

            foreach ((var me, var meShipLogs) in rootToShipLogs)
            {
                var otherShipLogs = new List<ShipLog>();
                foreach ((var other, var otherSubShipLogs) in rootToShipLogs)
                {
                    if (me == other)
                        continue;
                    otherShipLogs.AddRange(otherSubShipLogs);
                }
                yield return (meShipLogs, otherShipLogs);
            }
        }

        public Dictionary<IShipGroupMember, List<ShipLog>> GroupByShipLogByLevel1Group()
        {
            var ret = new Dictionary<IShipGroupMember, List<ShipLog>>();
            foreach (var shipLog in shipLogsOnMap)
            {
                var rootParent = (shipLog as IShipGroupMember).GetParentGroup();
                if (!ret.TryGetValue(rootParent, out var list))
                {
                    list = ret[rootParent] = new List<ShipLog>();
                }
                list.Add(shipLog);
            }
            return ret;
        }

        public static Dictionary<ShipLog, IShipGroupMember> InverseContainerToMembersMap(Dictionary<IShipGroupMember, List<ShipLog>> containerToShipLogs)
        {
            var ret = new Dictionary<ShipLog, IShipGroupMember>();
            foreach ((var container, var subShipLogs) in containerToShipLogs)
            {
                foreach (var subShipLog in subShipLogs)
                {
                    ret[subShipLog] = container;
                }
            }
            return ret;
        }

        public List<ShipLog> GetSameLevel1GroupShipLogs(ShipLog shipLog)
        {
            var containerToShipLogs = GroupByShipLogByLevel1Group();
            var shipLogToContainer = InverseContainerToMembersMap(containerToShipLogs);
            return containerToShipLogs[shipLogToContainer[shipLog]];
        }

        public List<ShipLog> GetSameRootGroupShipLogs(ShipLog shipLog)
        {
            var containerToShipLogs = GroupByShipLogByRootGroup();
            var shipLogToContainer = InverseContainerToMembersMap(containerToShipLogs);
            return containerToShipLogs[shipLogToContainer[shipLog]];
        }

        public void ApplyKeepCurrentRelativeFormation(ShipLog anchorShip, bool absolute)
        {
            if (anchorShip == null)
                throw new ArgumentNullException(nameof(anchorShip));

            var controlTree = BuildFormationControlTree(anchorShip);
            if (controlTree.edges.Count == 0)
                return;

            ApplyKeepCurrentRelativeFormation(controlTree.edges, absolute);
        }

        public void ApplyRelativeFormation(ShipLog anchorShip, RelativeFormationDialogModel model)
        {
            if (anchorShip == null)
                throw new ArgumentNullException(nameof(anchorShip));
            if (model == null)
                throw new ArgumentNullException(nameof(model));

            var controlTree = BuildFormationControlTree(anchorShip);
            if (controlTree.edges.Count == 0)
                return;

            switch (model.mode)
            {
                case RelativeFormationMode.KeepCurrentPosition:
                    ApplyKeepCurrentRelativeFormation(controlTree.edges, model.absolute);
                    break;
                case RelativeFormationMode.LineAbreast:
                case RelativeFormationMode.LineOfBearing:
                    ApplyPatternRelativeFormation(anchorShip, controlTree.childrenMap, controlTree.oobOrderIndex, model);
                    break;
            }
        }

        public void ApplyFollowFormation(ShipLog anchorShip, float followDistanceYards)
        {
            if (anchorShip == null)
                throw new ArgumentNullException(nameof(anchorShip));

            var controlTree = BuildFormationControlTree(anchorShip);
            if (controlTree.edges.Count == 0)
                return;

            var chain = FlattenFormationTreeForFollow(anchorShip, controlTree.childrenMap, controlTree.oobOrderIndex);
            var previousShip = anchorShip;
            foreach (var ship in chain)
            {
                ship.controlMode = ControlMode.FollowTarget;
                ship.followedTargetObjectId = previousShip.objectId;
                ship.followDistanceYards = followDistanceYards;
                ship.relativeTargetObjectId = null;
                previousShip = ship;
            }
        }

        public void ReverseControlChain(ShipLog rootShip)
        {
            if (!TryReverseControlChain(rootShip, out var message))
                throw new InvalidOperationException(message);
        }

        public bool TryReverseControlChain(ShipLog rootShip, out string message)
        {
            if (!TryBuildReversibleControlChain(rootShip, out var chain, out message))
                return false;

            var reversedEdges = new List<ControlChainEdge>();
            for (var i = 0; i < chain.Count - 1; i++)
            {
                var parent = chain[i];
                var child = chain[i + 1];
                reversedEdges.Add(CreateReversedEdge(parent, child));
            }

            foreach (var edge in reversedEdges)
            {
                ApplyControlChainEdge(edge);
            }

            SetIndependentControl(chain[^1]);
            message = null;
            return true;
        }

        bool TryBuildReversibleControlChain(ShipLog rootShip, out List<ShipLog> chain, out string message)
        {
            chain = null;

            if (rootShip == null)
            {
                message = "No ship is selected.";
                return false;
            }

            if (rootShip.GetEffectiveControlMode() != ControlMode.Independent)
            {
                message = "Reverse Control Chain requires the selected ship to be an independent unit.";
                return false;
            }

            var childrenMap = shipLogsOnMap
                .Where(ship => ship.GetEffectiveControlMode() != ControlMode.Independent)
                .GroupBy(ship => ship.GetControlPredecessorOnMap())
                .Where(group => group.Key != null)
                .ToDictionary(group => group.Key, group => group.ToList());

            if (!childrenMap.TryGetValue(rootShip, out var rootChildren) || rootChildren.Count == 0)
            {
                message = "The selected ship does not control any units, so there is no control chain to reverse.";
                return false;
            }

            chain = new List<ShipLog>() { rootShip };
            var visited = new HashSet<string>() { rootShip.objectId };
            var current = rootShip;

            while (true)
            {
                if (!childrenMap.TryGetValue(current, out var children) || children.Count == 0)
                {
                    message = null;
                    return true;
                }

                if (children.Count > 1)
                {
                    message = $"Control chain cannot be reversed because {current.GetMemberName()} controls multiple units.";
                    chain = null;
                    return false;
                }

                var next = children[0];
                if (!visited.Add(next.objectId))
                {
                    message = "Control chain cannot be reversed because a control loop was detected.";
                    chain = null;
                    return false;
                }

                chain.Add(next);
                current = next;
            }
        }

        ControlChainEdge CreateReversedEdge(ShipLog parent, ShipLog child)
        {
            switch (child.GetEffectiveControlMode())
            {
                case ControlMode.FollowTarget:
                    return new ControlChainEdge()
                    {
                        controlledShip = parent,
                        targetShip = child,
                        controlMode = ControlMode.FollowTarget,
                        distanceYards = child.followDistanceYards,
                    };
                case ControlMode.RelativeToTarget:
                    return new ControlChainEdge()
                    {
                        controlledShip = parent,
                        targetShip = child,
                        controlMode = ControlMode.RelativeToTarget,
                        distanceYards = child.relativeToTargetDistanceYards,
                        azimuthDeg = child.relativeToAbsolute
                            ? MeasureUtils.NormalizeAngle(child.relativeToTargetAzimuth + 180f)
                            : MeasureUtils.NormalizeAngle(parent.headingDeg + child.relativeToTargetAzimuth + 180f - child.headingDeg),
                        relativeToAbsolute = child.relativeToAbsolute,
                    };
                default:
                    throw new InvalidOperationException($"Unsupported control mode in chain reversal: {child.GetEffectiveControlMode()}");
            }
        }

        void ApplyControlChainEdge(ControlChainEdge edge)
        {
            switch (edge.controlMode)
            {
                case ControlMode.FollowTarget:
                    edge.controlledShip.controlMode = ControlMode.FollowTarget;
                    edge.controlledShip.followedTargetObjectId = edge.targetShip.objectId;
                    edge.controlledShip.followDistanceYards = edge.distanceYards;
                    edge.controlledShip.relativeTargetObjectId = null;
                    break;
                case ControlMode.RelativeToTarget:
                    edge.controlledShip.controlMode = ControlMode.RelativeToTarget;
                    edge.controlledShip.relativeTargetObjectId = edge.targetShip.objectId;
                    edge.controlledShip.relativeToTargetDistanceYards = edge.distanceYards;
                    edge.controlledShip.relativeToTargetAzimuth = MeasureUtils.NormalizeAngle(edge.azimuthDeg);
                    edge.controlledShip.relativeToAbsolute = edge.relativeToAbsolute;
                    edge.controlledShip.followedTargetObjectId = null;
                    break;
            }
        }

        void SetIndependentControl(ShipLog ship)
        {
            ship.controlMode = ControlMode.Independent;
            ship.followedTargetObjectId = null;
            ship.relativeTargetObjectId = null;
        }

        (List<(ShipLog parent, ShipLog child)> edges, Dictionary<ShipLog, List<ShipLog>> childrenMap, Dictionary<string, int> oobOrderIndex) BuildFormationControlTree(ShipLog anchorShip)
        {
            var allShips = shipLogsOnMap.ToList();
            var predecessorToChildren = allShips
                .Where(ship => ship != anchorShip)
                .GroupBy(ship => ship.GetControlPredecessor())
                .Where(group => group.Key != null)
                .ToDictionary(group => group.Key, group => group.ToList());

            var edges = new List<(ShipLog parent, ShipLog child)>();
            var childrenMap = new Dictionary<ShipLog, List<ShipLog>>();
            var visited = new HashSet<string>() { anchorShip.objectId };
            var queue = new Queue<ShipLog>();
            queue.Enqueue(anchorShip);

            while (queue.Count > 0)
            {
                var parent = queue.Dequeue();
                if (!predecessorToChildren.TryGetValue(parent, out var directChildren))
                    continue;

                childrenMap[parent] = directChildren;
                foreach (var child in directChildren)
                {
                    if (!visited.Add(child.objectId))
                        continue;

                    edges.Add((parent, child));
                    queue.Enqueue(child);
                }
            }

            return (edges, childrenMap, BuildOobOrderIndex(anchorShip));
        }

        Dictionary<string, int> BuildOobOrderIndex(ShipLog anchorShip)
        {
            var oobOrderIndex = new Dictionary<string, int>();
            var rootParent = ((IShipGroupMember)anchorShip).GetRootParent();
            var nextIndex = 0;

            void Visit(IShipGroupMember member)
            {
                if (member == null)
                    return;

                oobOrderIndex[member.objectId] = nextIndex++;
                if (member is ShipGroup shipGroup)
                {
                    foreach (var child in shipGroup.GetChildren())
                    {
                        Visit(child);
                    }
                }
            }

            Visit(rootParent);
            return oobOrderIndex;
        }

        List<ShipLog> FlattenFormationTreeForFollow(
            ShipLog parent,
            Dictionary<ShipLog, List<ShipLog>> childrenMap,
            Dictionary<string, int> oobOrderIndex)
        {
            var orderedChildren = GetOrderedFormationChildren(parent, childrenMap, oobOrderIndex);
            var result = new List<ShipLog>();
            foreach (var child in orderedChildren)
            {
                result.Add(child);
                result.AddRange(FlattenFormationTreeForFollow(child, childrenMap, oobOrderIndex));
            }
            return result;
        }

        List<ShipLog> GetOrderedFormationChildren(
            ShipLog parent,
            Dictionary<ShipLog, List<ShipLog>> childrenMap,
            Dictionary<string, int> oobOrderIndex)
        {
            if (!childrenMap.TryGetValue(parent, out var children))
                return new List<ShipLog>();

            return children
                .OrderBy(ship => formationControlModePriority.GetValueOrDefault(ship.controlMode, 0))
                .ThenBy(ship => oobOrderIndex.GetValueOrDefault(ship.objectId, int.MaxValue))
                .ToList();
        }

        void ApplyKeepCurrentRelativeFormation(List<(ShipLog parent, ShipLog child)> edges, bool absolute)
        {
            foreach (var edge in edges)
            {
                Geodesic.WGS84.Inverse(
                    edge.parent.position.LatDeg,
                    edge.parent.position.LonDeg,
                    edge.child.position.LatDeg,
                    edge.child.position.LonDeg,
                    out var distanceM,
                    out var azimuthDeg,
                    out _
                );

                edge.child.relativeTargetObjectId = edge.parent.objectId;
                edge.child.relativeToTargetDistanceYards = (float)distanceM * MeasureUtils.meterToYard;
                edge.child.relativeToTargetAzimuth = absolute
                    ? MeasureUtils.NormalizeAngle((float)azimuthDeg)
                    : MeasureUtils.NormalizeAngle((float)azimuthDeg - edge.parent.headingDeg);
                edge.child.relativeToAbsolute = absolute;
                edge.child.followedTargetObjectId = null;
                edge.child.controlMode = ControlMode.RelativeToTarget;
            }
        }

        void ApplyPatternRelativeFormation(
            ShipLog anchorShip,
            Dictionary<ShipLog, List<ShipLog>> childrenMap,
            Dictionary<string, int> oobOrderIndex,
            RelativeFormationDialogModel model)
        {
            var chain = FlattenFormationTreeForFollow(anchorShip, childrenMap, oobOrderIndex);
            if (chain.Count == 0)
                return;

            if (!model.isSymmetric)
            {
                ShipLog previousShip = anchorShip;
                foreach (var ship in chain)
                {
                    SetRelativeFormationLink(ship, previousShip, model.distanceYards, model.angleDeg, model.absolute);
                    previousShip = ship;
                }
                return;
            }

            ShipLog rightPreviousShip = anchorShip;
            ShipLog leftPreviousShip = anchorShip;
            var mirroredAngle = MeasureUtils.NormalizeAngle(360f - model.angleDeg);

            for (var i = 0; i < chain.Count; i++)
            {
                var ship = chain[i];
                if (i % 2 == 0)
                {
                    SetRelativeFormationLink(ship, rightPreviousShip, model.distanceYards, model.angleDeg, model.absolute);
                    rightPreviousShip = ship;
                }
                else
                {
                    SetRelativeFormationLink(ship, leftPreviousShip, model.distanceYards, mirroredAngle, model.absolute);
                    leftPreviousShip = ship;
                }
            }
        }

        void SetRelativeFormationLink(ShipLog ship, ShipLog targetShip, float distanceYards, float azimuthDeg, bool absolute)
        {
            ship.controlMode = ControlMode.RelativeToTarget;
            ship.relativeTargetObjectId = targetShip.objectId;
            ship.relativeToTargetDistanceYards = distanceYards;
            ship.relativeToTargetAzimuth = MeasureUtils.NormalizeAngle(azimuthDeg);
            ship.relativeToAbsolute = absolute;
            ship.followedTargetObjectId = null;
        }

        public IEnumerable<BatteryStatus> batteryStatusesFireable
        {
            get
            {
                foreach (var shipLog in shipLogsOnMap)
                {
                    foreach (var batteryStatus in shipLog.batteryStatus)
                    {
                        yield return batteryStatus;
                    }
                }
            }
        }

        public IEnumerable<MountStatusRecord> mountStatusesFireable
        {
            get
            {
                foreach (var bty in batteryStatusesFireable)
                {
                    foreach (var mnt in bty.mountStatus)
                    {
                        if (mnt.IsOperational())
                        {
                            yield return mnt;
                        }
                    }
                }
            }
        }


        public GlobalString GetNameForNewShipClass(ShipClass shipClass)
        {
            var englishNameSet = namedShips.Select(s => s.name.english).ToHashSet();

            var i = 1;
            while(true)
            {
                var testName = shipClass.name.Add(i.ToString());
                if(!englishNameSet.Contains(testName.english))
                    return testName;
                i++;
            }
        }

    }
}
