using System;
using System.Collections.Generic;
using System.Linq;
using CoreUtils;

namespace StrategicCombatCore
{

    public class LandBattleUnitState // mainly logging for loss of engaged units
    {
        public string unitId;
        public int currentStrengthLoss;
        public int accumulatedStrengthLoss;
        // Current Strength is accessed from the concrete LandUnit

        public LandUnit GetLandUnit() => EntityManager.Instance.Get<LandUnit>(unitId);

    }

    public class LandBattleSideState
    {
        public string sideId;
        public List<LandBattleUnitState> unitStates = new();
        public float globalTacticalModifier; // TODO: Represent it in Hex instead of Battle?

        public SideState GetSide() => EntityManager.Instance.Get<SideState>(sideId);
        public float GetTotalCurrentStrengthLoss() => unitStates.Sum(u => u.currentStrengthLoss);
        public float GetTotalAccumulatedStrengthLoss() => unitStates.Sum(u => u.accumulatedStrengthLoss);
    }

    public partial class LandBattleSideStateDynamic // Not serializable helper, generate it when needed
    {
        public class StrategicGroupBundle
        {
            public StrategicGroup group;

            public float commandUsageFlatten;
            public float commandUsage;
            
            public float currentLayerChanceCostModifier;
            public float accumulatedChanceCostModifier; // also average

            public float directLayerTacticalModifier;
            public float averageTacticalModifier;

            // TODO: Maintain children relationship?

            public StrategicGroupBundle parent; // Top Groups which are not leading group would be attached to the leading group in the bundle layer.

            // public float GetChanceCostModifier() => StrategicGroup.GetChanceCostModifier(commandUsage, group.GetCommandCapacity(), group.GetLeaderSkillLevel());
        }

        public partial class LandUnitBundle
        {
            public LandUnit landUnit;
            // Reference LandBattleUnitState?
            public LandBattleUnitState battleUnitState; 

            public StrategicGroupBundle parent;

            public float chanceCostModifier;
            public float tacticalModifier;

            public void DetermineModifiers()
            {
                if(parent != null)
                {
                    tacticalModifier = parent.directLayerTacticalModifier;
                    var pt = parent;
                    while(pt != null)
                    {
                        chanceCostModifier += pt.currentLayerChanceCostModifier;
                        pt = pt.parent;
                    }
                }
            }
        }

        public Cell cell;
        public StrategicGroupBundle leadingGroupBundle = new(); // group has highest flatten command cost
        public List<StrategicGroupBundle> topGroupBundles = new();
        public List<LandUnitBundle> landUnitBundles = new();

        public Leader battleLeader;
        public Country country;

        LandBattleSideState battleSideState;
        Dictionary<string, LandBattleUnitState> idToBattleUnitState;

        public void Initialize(Cell cell, LandBattleSideState battleSideState)
        {
            this.cell = cell;
            this.battleSideState = battleSideState;
            idToBattleUnitState = battleSideState.unitStates.ToDictionary(s => s.unitId, s => s);

            var topGroups = cell.StrategicGroupReferences.Select(r => r.Get()).Where(g => 
                g.deployState == StrategicGroup.DeployState.Independent &&
                g.posture != StrategicGroup.GroupPostureType.Disengaged &&
                g.type != StrategicGroup.Type.Fleet &&
                g.side == battleSideState.GetSide()
            );

            topGroupBundles = topGroups.Select(_Scan).ToList();

            var maxCommandUsageDirect = topGroupBundles.Max(b => b.commandUsageFlatten);
            leadingGroupBundle = topGroupBundles.First(b => b.commandUsageFlatten == maxCommandUsageDirect);

            // Recalculate the leading group's command properties
            
            var leadingGroupAccWeight = (leadingGroupBundle.accumulatedChanceCostModifier - leadingGroupBundle.currentLayerChanceCostModifier) * leadingGroupBundle.commandUsageFlatten;
            var leadingGroupTacWeight = leadingGroupBundle.averageTacticalModifier * leadingGroupBundle.commandUsageFlatten;
            foreach(var groupBundle in topGroupBundles)
            {
                if(groupBundle != leadingGroupBundle)
                {
                    groupBundle.parent = leadingGroupBundle;

                    leadingGroupBundle.commandUsage += groupBundle.commandUsage / 3;
                    leadingGroupBundle.commandUsageFlatten += groupBundle.commandUsageFlatten;

                    leadingGroupAccWeight += groupBundle.commandUsageFlatten * groupBundle.accumulatedChanceCostModifier;
                    leadingGroupTacWeight += groupBundle.commandUsageFlatten * groupBundle.averageTacticalModifier;
                }
            }

            leadingGroupBundle.currentLayerChanceCostModifier = StrategicGroup.GetChanceCostModifier(
                leadingGroupBundle.commandUsage, leadingGroupBundle.group.GetCommandCapacity(), leadingGroupBundle.group.GetLeaderSkillLevel()
            );
            leadingGroupBundle.accumulatedChanceCostModifier = leadingGroupAccWeight / leadingGroupBundle.commandUsageFlatten + leadingGroupBundle.currentLayerChanceCostModifier;
            leadingGroupBundle.directLayerTacticalModifier = StrategicGroup.GetTacticalModifier(
                leadingGroupBundle.commandUsage, leadingGroupBundle.group.GetCommandCapacity(), leadingGroupBundle.group.GetLeaderSkillLevel()
            );
            leadingGroupBundle.averageTacticalModifier = leadingGroupTacWeight / leadingGroupBundle.commandUsageFlatten;

            battleLeader = leadingGroupBundle.group.leaderReference.Get();
            country = leadingGroupBundle.group.side.countries.FirstOrDefault();

            foreach(var landUnitBundle in landUnitBundles)
            {
                landUnitBundle.DetermineModifiers();
            }
        }

        // Determine Hierarchy & command usage (true & flatten)
        StrategicGroupBundle _Scan(StrategicGroup parent)
        {
            var parentBundle = new StrategicGroupBundle()
            {
                group=parent
            };

            var accCostModWeight = 0f;
            // var averageTacWeight = 0f;

            var sumLandUnitCommandUsage = 0f;
            var groupTacWeight = 0f;

            foreach(var subordinateRef in parent.subordinatesCombined)
            {
                var subordinate = subordinateRef.Get();
                if(subordinate is LandUnit landUnit)
                {
                    if(!idToBattleUnitState.TryGetValue(landUnit.objectId, out var battleUnitState))
                    {
                        battleUnitState = new(){
                            unitId=landUnit.objectId
                        };
                        idToBattleUnitState[landUnit.objectId] = battleUnitState;
                        battleSideState.unitStates.Add(battleUnitState);
                    }
                    var landUnitBundle = new LandUnitBundle()
                    {
                        landUnit = landUnit,
                        battleUnitState=battleUnitState,
                        parent = parentBundle
                    };

                    landUnitBundles.Add(landUnitBundle);

                    var landUnitCommandUsage = landUnit.GetCurrentCommandUsage();

                    parentBundle.commandUsageFlatten += landUnitCommandUsage;
                    parentBundle.commandUsage += landUnitCommandUsage;
                    sumLandUnitCommandUsage += landUnitCommandUsage;
                }
                else if(subordinate is StrategicGroup g)
                {
                    if(g.deployState == StrategicGroup.DeployState.Combined)
                    {
                        var subGroupBundle = _Scan(g);
                        subGroupBundle.parent = parentBundle;

                        parentBundle.commandUsageFlatten += subGroupBundle.commandUsageFlatten;
                        parentBundle.commandUsage += subGroupBundle.commandUsage / 3;

                        accCostModWeight += subGroupBundle.commandUsageFlatten * subGroupBundle.accumulatedChanceCostModifier;

                        groupTacWeight += subGroupBundle.commandUsageFlatten * subGroupBundle.averageTacticalModifier;
                    }
                }
            }

            parentBundle.currentLayerChanceCostModifier = StrategicGroup.GetChanceCostModifier(
                parentBundle.commandUsage, parent.GetCommandCapacity(), parent.GetLeaderSkillLevel()
            );
            parentBundle.accumulatedChanceCostModifier = accCostModWeight / parentBundle.commandUsageFlatten + parentBundle.currentLayerChanceCostModifier;

            parentBundle.directLayerTacticalModifier = StrategicGroup.GetTacticalModifier(
                parentBundle.commandUsage, parent.GetCommandCapacity(), parent.GetLeaderSkillLevel()
            );
            parentBundle.averageTacticalModifier = (parentBundle.directLayerTacticalModifier * sumLandUnitCommandUsage + groupTacWeight) / parentBundle.commandUsageFlatten;

            return parentBundle;
        }
    }

    public class LandBattle : IObjectIdLabeled
    {
        public string objectId { get; set; }
        public IEnumerable<IObjectIdLabeled> GetSubObjects()
        {
            yield break;
        }

        public bool end;
        public bool attackerVictory;
        public DateTime beginDateTime;
        public DateTime endDateTime;

        public LandBattleSideState attacker = new();
        public LandBattleSideState defender = new();
        public XY cellXY = new();

        public SideState GetAttacker() => EntityManager.Instance.Get<SideState>(attacker.sideId);
        public SideState GetDefender() => EntityManager.Instance.Get<SideState>(defender.sideId);
        public Cell GetCell() => StrategicGameState.Instance.cellMatrix[cellXY.x, cellXY.y];
        public (Cell, SideState, SideState) GetKey() => (GetCell(), GetAttacker(), GetDefender());

        public LandBattleSideStateDynamic GetAttackerDynamic()
        {
            LandBattleSideStateDynamic ret = new();
            ret.Initialize(GetCell(), attacker);
            return ret;
        }

        public LandBattleSideStateDynamic GetDefenderDynamic()
        {
            LandBattleSideStateDynamic ret = new();
            ret.Initialize(GetCell(), defender);
            return ret;
        }

        // public StrategicGroup GetTopGroup(LandBattleSideState )
        // {
        //     // Top group is the group has highest direct command pts.
        //     // Top group's leader is the shown top operational leader, while other groups (if any) are "attached" to the top group for calculation of chance cost.

        // }

        // public LandBattleSideStateDynamic CollectDynamicSideState(LandBattleSideState battleSideState)
        // {
        //     var cell = GetCell();
        //     var topGroupBundles = cell.StrategicGroupReferences.Select(r => r.Get()).Where(g => 
        //         g.deployState == StrategicGroup.DeployState.Independent &&
        //         g.posture != StrategicGroup.GroupPostureType.Disengaged &&
        //         g.type != StrategicGroup.Type.Fleet &&
        //         g.side == battleSideState.GetSide()
        //     ).Select(g =>
        //     {
        //         var (usageDirect, usage, accCostMod, currentLayerCostMod) = g.GetAverageAccumulatedChanceCostModifier();
        //         return new LandBattleSideStateDynamic.StrategicGroupBundle()
        //         {
        //             group=g,
        //             commandUsageFlatten=usageDirect,
        //             commandUsage=usage,
        //             accumulatedChanceCostModifier=accCostMod,
        //             currentLayerChanceCostModifier=currentLayerCostMod
        //         };
        //     }).ToList();

        //     // External logic should ensure there's at least a group.
        //     var maxCommandUsageDirect = topGroupBundles.Max(b => b.commandUsageFlatten);
        //     var leadingGroupBundle = topGroupBundles.First(b => b.commandUsageFlatten == maxCommandUsageDirect);

        //     var idToBattleUnitState = battleSideState.unitStates.ToDictionary(s => s.unitId, s=>s);

        //     var landUnitBundles = new List<LandBattleSideStateDynamic.LandUnitBundle>();

        //     foreach(var groupBundle in topGroupBundles)
        //     {
        //         landUnitBundles.AddRange(
        //             groupBundle.group.WalkGroupMembers<LandUnit>().Select(landUnit =>
        //             {
        //                 if(!idToBattleUnitState.TryGetValue(landUnit.objectId, out var battleUnitState))
        //                 {
        //                     battleUnitState = new(){
        //                         unitId=landUnit.objectId
        //                     };
        //                     idToBattleUnitState[landUnit.objectId] = battleUnitState;
        //                     battleSideState.unitStates.Add(battleUnitState);
        //                 }
        //                 return new LandBattleSideStateDynamic.LandUnitBundle()
        //                 {
        //                     landUnit=landUnit,
        //                     battleUnitState=battleUnitState
        //                 };
        //             })
        //         );
        //     }

        //     var battleLeader = leadingGroupBundle.group.leaderReference.Get();

        //     var country = leadingGroupBundle.group.side.countries.FirstOrDefault();

        //     return new()
        //     {
        //         cell=cell,
        //         leadingGroupBundle=leadingGroupBundle,
        //         topGroupBundles=topGroupBundles,
        //         landUnitBundles=landUnitBundles,
        //         battleLeader=battleLeader,
        //         country=country
        //     };
        // }
    }
}