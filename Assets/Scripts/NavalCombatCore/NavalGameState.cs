using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using System.Xml;
using System.IO;
using System.Linq;
using CoreUtils;
using System.Windows.Forms;

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

            Instance.ResetAndRegisterAll();
            Instance.SyncShipLogParentWithGroupHierarchy();

            // rootShipGroupsChanged?.Invoke(this, rootShipGroups);
            Instance.shipGroupsChanged?.Invoke(Instance, Instance.shipGroups);
        }

        public override void ResetAndRegisterAll()
        {
            base.ResetAndRegisterAll();

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

        void FixRelativeTree(ShipLog displacedShipLog, float azimuth, float distance, bool first)
        {
            // var displacedShipLog = EntityManager.Instance.Get<ShipLog>(displacedId);

            var subs = shipLogsOnMap.Where(shipLog => shipLog.controlMode == ControlMode.RelativeToTarget && shipLog.relativeTargetObjectId == displacedShipLog.objectId).ToList();
            if (subs.Count == 0)
                return;
            var newAnchor = subs[0];

            var azimuth2 = newAnchor.relativeToTargetAzimuth;
            var distance2 = newAnchor.relativeToTargetDistanceYards;

            if (first)
            {
                newAnchor.relativeTargetObjectId = displacedShipLog.relativeTargetObjectId;
            }

            newAnchor.relativeToTargetAzimuth = azimuth;
            newAnchor.relativeToTargetDistanceYards = distance;

            foreach (var sub in subs.Skip(1))
            {
                sub.relativeTargetObjectId = newAnchor.objectId;
            }

            FixRelativeTree(newAnchor, azimuth2, distance2, false);
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
            }
        }

        public void Step(float deltaSeconds)
        {
            scenarioState.doingStep = true;

            // tempSubjectLogs.Clear();

            // pre-advance resolution
            var weaponSimulationAssignmentClockTicked = scenarioState.weaponSimulationAssignmentClock.Step(deltaSeconds) > 0;
            if (weaponSimulationAssignmentClockTicked) // The clock is not limited to Weapon allocation though
            {
                foreach ((var meShipLogs, var otherShipLogs) in GetOpposeSidePairs())
                {
                    var solver = new WeaponTargetAssignmentSolver();
                    solver.Solve(
                        meShipLogs.Where(s => s.doctrine.GetFireAutomaticType() == AutomaticType.Automatic),
                        otherShipLogs
                    );

                    var planner = new LowLevelCoursePlanner();
                    planner.Plan(
                        // meShipLogs.Where(s => s.doctrine.GetManeuverAutomaticType() == AutomaticType.Automatic),
                        meShipLogs.Where(s => s.doctrine.GetManeuverAutomaticType() == AutomaticType.Automatic && s.GetControlRoot().doctrine.GetManeuverAutomaticType() == AutomaticType.Automatic),
                        otherShipLogs,
                        CoreParameter.Instance.extrapolateSeconds
                    ); // Extrapolate 360s
                }

                var activceShipLogs = shipLogs.Where(s =>
                    s.mapState == MapState.Deployed 
                    && s.operationalState == ShipOperationalState.Operational
                    // && s.speedKnots > 0
                ).ToList();

                var leadShipLogs = activceShipLogs.Where(s =>
                    s.GetEffectiveControlMode() == ControlMode.Independent &&
                    s.doctrine.GetManeuverAutomaticType() == AutomaticType.Automatic
                ).ToList();

                foreach(var g in shipLogsOnMap.GroupBy(s => s.GetControlRoot()))
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

                foreach(var shipLog in leadShipLogs)
                {
                    var checker = ObstacleAvoidChecker.Extract(shipLog);
                    var newDesiredHeadingDeg = checker.Check();
                    shipLog.preCollsionAvoiding = newDesiredHeadingDeg != shipLog.desiredHeadingDeg;
                    shipLog.desiredHeadingDeg = newDesiredHeadingDeg;
                }


            }

            // Reset Formation - zero speed is detached automatically, "children" reset their targets according to detached unit's previous command.
            ProcessZeroSpeedFormationAdjustment(); // TODO: Is it too frequenct to do it every step?

            // Advance
            scenarioState.Step(deltaSeconds);

            foreach (var shipLog in shipLogsOnMap)
                shipLog.StepProcessTurn(deltaSeconds); // update heading

            foreach (var shipLog in shipLogsOnMap)
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
            foreach(var shipLog in shipLogs.Where(s =>
                s.mapState == MapState.Deployed
                && s.operationalState == ShipOperationalState.Operational
                // && s.speedKnots > 0
                && s.doctrine.GetManeuverAutomaticType() == AutomaticType.Automatic
            ))
            {
                var checker = ObstacleAvoidChecker.Extract(shipLog, true);
                var newDesiredHeadingDeg = checker.Check();
                shipLog.preCollsionAvoiding = newDesiredHeadingDeg != shipLog.desiredHeadingDeg;
                shipLog.desiredHeadingDeg = newDesiredHeadingDeg;
            }

            foreach (var shipLog in shipLogsOnMap)
                shipLog.StepProcessSpeed(deltaSeconds); // update speed

            foreach (var shipLog in shipLogsOnMap)
                shipLog.StepTryMoveToNewPosition(deltaSeconds); // update position

            // PrecalculationContext.Instance.gunneryFireContext.Calculate();
            using (GunneryFireContext.Begin())
            {
                foreach (var shipLog in shipLogsOnMap)
                    shipLog.StepBatteryStatus(deltaSeconds); // gunnery resolution
            }

            foreach (var shipLog in shipLogsOnMap)
                shipLog.StepDamageResolution(deltaSeconds);


            foreach (var launchedTorpedo in launchedTorpedosOnMap)
                launchedTorpedo.StepMoveToNewPosition(deltaSeconds);

            using (TorpedoAttackContext.Begin())
            {
                foreach (var shipLog in shipLogsOnMap)
                {
                    shipLog.StepTorpedoSector(deltaSeconds);
                }
            }

            foreach (var shipLog in shipLogsOnMap)
                shipLog.StepLogging();

            scenarioState.doingStep = false;
        }

        public IEnumerable<ShipLog> shipLogsOnMap => shipLogs.Where(x => x.mapState == MapState.Deployed);
        public IEnumerable<LaunchedTorpedo> launchedTorpedosOnMap => launchedTorpedos.Where(x => x.mapState == MapState.Deployed);

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