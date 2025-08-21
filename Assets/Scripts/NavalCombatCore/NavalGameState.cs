using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using System.Xml;
using System.IO;
using System.Linq;
using MathNet.Numerics;
using CoreUtils;

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

    public class NavalGameState : AbstractGameState
    {
        public List<ShipGroup> shipGroups = new();
        public ScenarioState scenarioState = new();
        public List<LaunchedTorpedo> launchedTorpedos = new();

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
            // EntityManager.Instance.Reset();

            // foreach (var leader in leaders)
            // {
            //     EntityManager.Instance.Register(leader, null);
            // }
            // foreach (var shipClasses in shipClasses)
            // {
            //     EntityManager.Instance.Register(shipClasses, null);
            // }
            // foreach (var namedShip in namedShips)
            // {
            //     EntityManager.Instance.Register(namedShip, null);
            // }
            // foreach (var shipLog in shipLogs)
            // {
            //     EntityManager.Instance.Register(shipLog, null);
            // }

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

        public void ProcessZeroSpeedFormationAdjustment()
        {
            // var immobilizedShipLogs = shipLogs.Where(shipLog => shipLog.mapState != MapState.Deployed || shipLog.GetMaxSpeedKnots() <= 4).ToHashSet(); // `<= 4` => cannot to turn => impossible to main formation

            // var immoblizedShipLogToChildrens = immobilizedShipLogs.ToDictionary(x => x, x => new List<ShipLog>());
            // foreach (var shipLog in shipLogsOnMap)
            // {
            //     var (controlMode, controlTarget) = shipLog.GetControlModeAndTargetInlucdeNonMap();
            //     if (controlTarget != null && immobilizedShipLogs.Contains(controlTarget))
            //     {
            //         immoblizedShipLogToChildrens[controlTarget].Add(shipLog);
            //     }
            // }

            // foreach (var immobilizedShipLog in immobilizedShipLogs)
            // {
            //     var children = immoblizedShipLogToChildrens[immobilizedShipLog];
            //     if (children.Count > 0)
            //     {
            //         var newAnchor = children[0];

            //         newAnchor.controlMode = immobilizedShipLog.controlMode;
            //         newAnchor.followDistanceYards = immobilizedShipLog.followDistanceYards;
            //         newAnchor.followedTargetObjectId = immobilizedShipLog.followedTargetObjectId;
            //         newAnchor.relativeToTargetDistanceYards = immobilizedShipLog.relativeToTargetDistanceYards;
            //         newAnchor.relativeTargetObjectId = immobilizedShipLog.relativeTargetObjectId;
            //         newAnchor.relativeToTargetAzimuth = immobilizedShipLog.relativeToTargetAzimuth;

            //         foreach (var otherChild in children.Skip(1))
            //         {
            //             otherChild.followedTargetObjectId = newAnchor.objectId;
            //             otherChild.relativeTargetObjectId = newAnchor.objectId;
            //         }
            //     }

            //     if (immobilizedShipLog.mapState == MapState.Deployed)
            //         immobilizedShipLog.controlMode = ControlMode.Independent; // Auto Detach
            // }

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

            // pre-advance resolution
            if (scenarioState.weaponSimulationAssignmentClock.Step(deltaSeconds) > 0)
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
                        meShipLogs.Where(s => s.doctrine.GetManeuverAutomaticType() == AutomaticType.Automatic),
                        otherShipLogs,
                        360
                    ); // Extrapolate 360s
                }
            }

            // Reset Formation - zero speed is detached automatically, "children" reset their targets according to detached unit's previous command.
            ProcessZeroSpeedFormationAdjustment();

            // Advance
            scenarioState.Step(deltaSeconds);

            foreach (var shipLog in shipLogsOnMap)
                shipLog.StepProcessTurn(deltaSeconds); // update heading

            foreach (var shipLog in shipLogsOnMap)
                shipLog.StepProcessControl(); // set desired heading / desired speed

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
    }
}