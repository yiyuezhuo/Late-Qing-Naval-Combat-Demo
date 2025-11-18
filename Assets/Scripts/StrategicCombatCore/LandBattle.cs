using System;
using System.Collections.Generic;
using System.Linq;
using CoreUtils;

namespace StrategicCombatCore
{
    // public class LandBattleSideState
    // {
    //     public SideState side;
    //     // log is record instead of derived dynamically (since we want to record initial value).
    //     public float strengthCommitted;
    //     public float strengthLost;
    //     public float globalTacticalModifier;
    //     public List<string> groupIds = new();
    // }

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

    public partial class LandBattleSideStateDynamic // Not serializable, generate it when needed
    {
        public class StrategicGroupBundle
        {
            public StrategicGroup group;

            public float commandUsageDirect;
            public float commandUsage;
            public float accumulatedChanceCostModifier;
            public float currentLayerChanceCostModifier;
        }

        public partial class LandUnitBundle
        {
            public LandUnit landUnit;
            // Reference LandBattleUnitState?
            public LandBattleUnitState battleUnitState; 
        }

        public Cell cell;
        public StrategicGroupBundle leadingGroupBundle; // group has highest flatten command cost
        public List<StrategicGroupBundle> topGroupBundles;
        public List<LandUnitBundle> landUnitBundles;
        public Leader battleLeader;
        public Country country;
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

        // public StrategicGroup GetTopGroup(LandBattleSideState )
        // {
        //     // Top group is the group has highest direct command pts.
        //     // Top group's leader is the shown top operational leader, while other groups (if any) are "attached" to the top group for calculation of chance cost.

        // }

        public LandBattleSideStateDynamic CollectDynamicSideState(LandBattleSideState battleSideState)
        {
            var cell = GetCell();
            var topGroupBundles = cell.StrategicGroupReferences.Select(r => r.Get()).Where(g => 
                g.deployState == StrategicGroup.DeployState.Independent &&
                g.posture != StrategicGroup.GroupPostureType.Disengaged &&
                g.type != StrategicGroup.Type.Fleet &&
                g.side == battleSideState.GetSide()
            ).Select(g =>
            {
                var (usageDirect, usage, accCostMod, currentLayerCostMod) = g.GetAverageAccumulatedChanceCostModifier();
                return new LandBattleSideStateDynamic.StrategicGroupBundle()
                {
                    group=g,
                    commandUsageDirect=usageDirect,
                    commandUsage=usage,
                    accumulatedChanceCostModifier=accCostMod,
                    currentLayerChanceCostModifier=currentLayerCostMod
                };
            }).ToList();

            // External logic should ensure there's at least a group.
            var maxCommandUsageDirect = topGroupBundles.Max(b => b.commandUsageDirect);
            var leadingGroupBundle = topGroupBundles.First(b => b.commandUsageDirect == maxCommandUsageDirect);

            var idToBattleUnitState = battleSideState.unitStates.ToDictionary(s => s.unitId, s=>s);

            var landUnitBundles = new List<LandBattleSideStateDynamic.LandUnitBundle>();

            foreach(var groupBundle in topGroupBundles)
            {
                landUnitBundles.AddRange(
                    groupBundle.group.WalkGroupMembers<LandUnit>().Select(landUnit =>
                    {
                        if(!idToBattleUnitState.TryGetValue(landUnit.objectId, out var battleUnitState))
                        {
                            battleUnitState = new(){
                                unitId=landUnit.objectId
                            };
                            idToBattleUnitState[landUnit.objectId] = battleUnitState;
                            battleSideState.unitStates.Add(battleUnitState);
                        }
                        return new LandBattleSideStateDynamic.LandUnitBundle()
                        {
                            landUnit=landUnit,
                            battleUnitState=battleUnitState
                        };
                    })
                );
            }

            var battleLeader = leadingGroupBundle.group.leaderReference.Get();

            var country = leadingGroupBundle.group.side.countries.FirstOrDefault();

            return new()
            {
                cell=cell,
                leadingGroupBundle=leadingGroupBundle,
                topGroupBundles=topGroupBundles,
                landUnitBundles=landUnitBundles,
                battleLeader=battleLeader,
                country=country
            };
        }

    }
}