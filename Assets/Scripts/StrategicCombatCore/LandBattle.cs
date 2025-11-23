using System;
using System.Collections.Generic;
using System.Linq;
using CoreUtils;
using NavalCombatCore;

namespace StrategicCombatCore
{

    public class LandBattleUnitState // mainly logging for loss of engaged units
    {
        public string unitId;
        public int currentStrengthLoss;
        public int accumulatedStrengthLoss;
        // Current Strength is accessed from the concrete LandUnit

        public LandUnit GetLandUnit() => EntityManager.Instance.Get<LandUnit>(unitId);

        public void StepResetState()
        {
            currentStrengthLoss = 0;
        }
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

            public void StepResetState()
            {
                battleUnitState.StepResetState();
            }

            public float GetTargetWeight()
            {
                return landUnit.strength; // TODO: apply suppression modifier and other non-weapon/strength modifier
            }

            public float GetAttackerWeight()
            {
                return landUnit.strength; // TODO: Apply suppression modifier
            }

            public override string ToString()
            {
                return $"LandUnitBundle({landUnit})";
            }

        }

        public class LandBattleSubCombat // Not serializable but should be converted to some sort of loggable object or provide log info to indicate what happened
        {
            public class RoleBundle
            {
                public LandUnitBundle landUnitBundle;
                public int commitStrength;

                public int GetCommitableStrength() => landUnitBundle.landUnit.strength;
                public float GetCommitPercent() => commitStrength / landUnitBundle.landUnit.strength;
                public LandUnit landUnit => landUnitBundle.landUnit;
                public float GetLethality() => landUnitBundle.landUnit.GetLethality();
                
                public float GetEqHexWidth() => commitStrength / menPerEqHex;
                public float GetMenPerEqHex() => menPerEqHex;

                // SB Style parameters
                public static float menPerEqHex = 40;
                // SB-Style combat value
                public static float lowCombatValue = 6;
                public static float highCombatValue = 60;
                public static float combatValueBase = 10000;

                public void Fire(RoleBundle target) // A SB style Fire (3 fires for a SB turn)
                {
                    var leth = GetLethality();
                    // var tgtEqHexWidth = target.GetEqHexWidth();
                    // var lethPerHex = leth / tgtEqHexWidth;
                    var combatValue = RandomUtils.NextFloat(lowCombatValue, highCombatValue);
                    // var inflictLossF = tgtEqHexWidth * lethPerHex * combatValue / combatValueBase * (menPerEqHex / 10);
                    var inflictLossF = leth * combatValue / combatValueBase * (target.GetMenPerEqHex() / 10);
                    var inflictLoss = Math.Min(target.commitStrength, RandomUtils.RandomRoundToInt(inflictLossF));
                    // TODO: Apply effect to suppression, fatigue, effectiveness and etc.

                    ServiceLocator.Get<ILoggerService>().Log($"Fire: {this} -> {target}: {inflictLoss}");

                    target.InflictStrengthLoss(inflictLoss);
                }

                public void InflictStrengthLoss(int inflictLoss)
                {
                    commitStrength -= inflictLoss;
                    landUnit.strength -= inflictLoss;
                    landUnitBundle.battleUnitState.accumulatedStrengthLoss += inflictLoss;
                    landUnitBundle.battleUnitState.currentStrengthLoss += inflictLoss;
                }

                public override string ToString()
                {
                    return $"RoleBundle({landUnitBundle})";
                }
            }

            public RoleBundle attacker;
            public RoleBundle target;
            public float chanceUsage;

            public float distanceMeter = 200;

            public static int standardFiringCount = 3;

            public void Resolve()
            {
                for(int i=0; i<standardFiringCount; i++)
                {
                    // Check distance to handle artillery bombardment
                    attacker.Fire(target);
                    target.Fire(attacker);
                }
            }

            public override string ToString()
            {
                return $"LandBattleSubCombat({attacker} vs {target}, chanceUsage={chanceUsage})";
            }
        }


        public Cell cell;
        public StrategicGroupBundle leadingGroupBundle = new(); // group has highest flatten command cost
        public List<StrategicGroupBundle> topGroupBundles = new();
        public List<LandUnitBundle> landUnitBundles = new();

        public Leader battleLeader;
        public Country country;

        public float maxChance;
        public float chance;

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

            foreach(var landUnitBundle in landUnitBundles)
            {
                landUnitBundle.DetermineModifiers();
            }

            battleLeader = leadingGroupBundle.group.leaderReference.Get();
            country = leadingGroupBundle.group.side.countries.FirstOrDefault();

            maxChance = chance = landUnitBundles.Sum(b => b.landUnit.GetChance());
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

                    var landUnitCommandUsage = landUnit.GetDirectCommandUsage();

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

        public void StepResetState() // reset attributes like "current loss" 
        {
            foreach(var landUnitBundle in landUnitBundles)
            {
                landUnitBundle.StepResetState();
            }
        }

        // public float GetChance()
        // {
        //     return landUnitBundles.Sum(b => b.landUnit.GetDirectCommandUsage()); // TODO: Use an independent value to differentiate unit-level command difficulty and chance value?
        // }

        public float chancePercent => chance / maxChance;

        public LandUnitBundle RollSubCombatTarget()
        {
            var validLandUnitBundles = landUnitBundles.Where(b => b.landUnit.strength > 0).ToList();
            if(validLandUnitBundles.Count == 0)
                return null;
            var weights = validLandUnitBundles.Select(b => b.GetTargetWeight()).ToList();
            return RandomUtils.Sample(validLandUnitBundles, weights);
        }

        public LandUnitBundle RollSubCombatAttacker()
        {
            var validLandUnitBundles = landUnitBundles.Where(b => b.landUnit.strength > 0).ToList();
            if(validLandUnitBundles.Count == 0)
                return null;
            var weights = validLandUnitBundles.Select(b => b.GetAttackerWeight()).ToList();
            return RandomUtils.Sample(validLandUnitBundles, weights);
        }

        public LandBattleSubCombat GenerateSubCombatAsInitiative(LandBattleSideStateDynamic other)
        {
            var target = new LandBattleSubCombat.RoleBundle()
            {
                landUnitBundle=other.RollSubCombatTarget()
            };
            if(target.landUnitBundle == null)
                return null;

            var attacker = new LandBattleSubCombat.RoleBundle()
            {
                landUnitBundle=RollSubCombatAttacker() // TODO: Introduce postive correlation for history engagement?
            };
            if(attacker.landUnitBundle == null)
                return null;

            var refCommitOdd = RandomUtils.NextFloat() * 1 + 1; // 1:1 ~ 2:1
            if(RandomUtils.NextFloat() <= 0.5f)
            {
                refCommitOdd = 1 / refCommitOdd;
            }

            var attackerCommitableStrength = attacker.GetCommitableStrength();
            var targetCommitableStrength = target.GetCommitableStrength();

            var attackerCommitStrength = (int)Math.Floor(Math.Min(attackerCommitableStrength, targetCommitableStrength * refCommitOdd));
            var targetCommitStrength = (int)Math.Floor(attackerCommitStrength / refCommitOdd);

            attacker.commitStrength = attackerCommitStrength;
            target.commitStrength = targetCommitStrength;

            return new()
            {
                attacker=attacker,
                target=target,
                chanceUsage=Math.Min(attackerCommitStrength, targetCommitStrength) // TODO: Use more detailed method
            };
        }

        public override string ToString()
        {
            return $"LandBattleSideStateDynamic({country}, {cell}, {chance})";
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

        static float referenceCombatIntensityPercent = 0.8f;

        public void Step()
        {
            var atk = GetAttackerDynamic();
            var def = GetDefenderDynamic();

            var dynamics = new List<LandBattleSideStateDynamic>(){atk, def};
            foreach(var dynamic in dynamics)
                dynamic.StepResetState();

            while(dynamics.Min(d => d.chance) > 0 && dynamics.Min(d => d.chancePercent) >= referenceCombatIntensityPercent)
            {
                var atkChancePercent = atk.chance / (atk.chance + def.chance);
                var attackerInitiative = RandomUtils.NextFloat() < atkChancePercent;
                var (initiative, passive) = attackerInitiative ? (atk, def) : (def, atk);
                
                var subCombat = initiative.GenerateSubCombatAsInitiative(passive);
                if(subCombat == null)
                {
                    break;
                }

                ServiceLocator.Get<ILoggerService>().Log($"{dynamics[0]} vs {dynamics[1]}: {subCombat}");

                subCombat.Resolve();
                foreach(var dynamic in dynamics)
                    dynamic.chance -= subCombat.chanceUsage;

            }
        }
    }
}