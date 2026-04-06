using System;
using System.Collections.Generic;
using System.Linq;
using CoreUtils;
using NavalCombatCore;
using YYZ;

namespace StrategicCombatCore
{
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
            public LandBattleSideStateDynamic sideStateDynamic;
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

            public float GetAttackerWeight(bool isGlobalAttacker)
            {
                return landUnit.GetLethality(isGlobalAttacker);
                // return landUnit.strength; // TODO: Apply suppression modifier
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
                public LandBattleSubCombat subCombat;
                public bool isGlobalAttacker;
                public bool isLocalInitiative;
                public LandUnitBundle landUnitBundle;
                public int commitStrength;

                public int GetCommitableStrength() => landUnitBundle.landUnit.strength;
                public float GetCommitPercent() => commitStrength / landUnitBundle.landUnit.strength;
                public LandUnit landUnit => landUnitBundle.landUnit;
                public float GetLethality() => landUnitBundle.landUnit.GetLethality(isGlobalAttacker);
                
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

                    landUnitBundle.battleUnitState.currentStrengthKill += inflictLoss;
                    landUnitBundle.battleUnitState.accumulatedStrengthKill += inflictLoss;
                    target.InflictStrengthLoss(inflictLoss);
                    target.InflictEffectivenessLoss(inflictLossF);

                    // Handle breakthrough which push the situation (progression)
                    if(isLocalInitiative && landUnit.suppression < 0.5f && landUnit.morale > 0.5f && target.landUnit.suppression > 0.5f && target.landUnit.morale < 0.5f)
                    {
                        var chancePercent = subCombat.chanceUsage / Math.Max(1, target.landUnitBundle.sideStateDynamic.maxChance);
                        var landBattle = landUnitBundle.sideStateDynamic.landBattle;
                        if(isGlobalAttacker)
                        {
                            landBattle.attackerSituation += chancePercent;
                        }
                        else
                        {
                            landBattle.attackerSituation -= chancePercent;
                        }
                        landBattle.attackerSituation = Math.Clamp(landBattle.attackerSituation, -1, 1);
                    }
                }

                public void InflictStrengthLoss(int inflictLoss)
                {
                    commitStrength -= inflictLoss;
                    landUnit.strength -= inflictLoss;
                    landUnitBundle.battleUnitState.accumulatedStrengthLoss += inflictLoss;
                    landUnitBundle.battleUnitState.currentStrengthLoss += inflictLoss;
                }

                class EffectivessModifier
                {
                    public float suppression;
                    public float morale;
                    public float fatigue;
                }

                static Dictionary<LandUnitQuality, EffectivessModifier> landUnitQualityModifierMap = new()
                {
                    {LandUnitQuality.Abysmal, new(){suppression=1f, morale=0.7f, fatigue=0.4f}},
                    {LandUnitQuality.Inferior, new(){suppression=0.5f, morale=0.5f, fatigue=0.3f}},
                    {LandUnitQuality.BelowAverage, new(){suppression=0.25f, morale=0.3f, fatigue=0.2f}},
                    {LandUnitQuality.Average, new(){suppression=0, morale=0, fatigue=0}},
                    {LandUnitQuality.Superior, new(){suppression=-0.1f, morale=-0.2f, fatigue=-0.2f}},
                    {LandUnitQuality.Elite, new(){suppression=-0.2f, morale=-0.5f, fatigue=-0.3f}},
                };

                public void InflictEffectivenessLoss(float inflictLossF)
                {
                    if(landUnit.strength == 0)
                        return;

                    var modifier = landUnitQualityModifierMap[landUnit.GetLandUnitTemplate()?.quality ?? LandUnitQuality.Average];

                    var suppressionDelta = (inflictLossF * 50 / landUnit.strength) * (1 + modifier.suppression);
                    var moraleDelta = - (inflictLossF * 10f / landUnit.strength) * (1 + modifier.morale);
                    var fatigueDelta = (inflictLossF * 5f / landUnit.strength + 0.025f * GetCommitPercent()) * (1 + modifier.fatigue);
                    
                    landUnit.suppression = Math.Min(1, landUnit.suppression + suppressionDelta);
                    landUnit.morale = Math.Max(0, landUnit.morale + moraleDelta);
                    landUnit.fatigue = Math.Min(1, landUnit.fatigue + fatigueDelta);
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

        public LandBattle landBattle;
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

        public void Initialize(Cell cell, LandBattleSideState battleSideState, LandBattle landBattle)
        {
            this.landBattle = landBattle;
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

            foreach(var subordinateRef in parent.directMemberReferences)
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
                        sideStateDynamic = this,
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

        public float chancePercent => chance / maxChance;

        public LandUnitBundle RollSubCombatTarget()
        {
            var validLandUnitBundles = landUnitBundles.Where(b => b.landUnit.strength > 0).ToList();
            if(validLandUnitBundles.Count == 0)
                return null;
            var weights = validLandUnitBundles.Select(b => b.GetTargetWeight()).ToList();
            if (weights.Sum() <= 0)
                return null;
            return RandomUtils.Sample(validLandUnitBundles, weights);
        }

        public LandUnitBundle RollSubCombatAttacker(bool isGlobalAttacker)
        {
            var validLandUnitBundles = landUnitBundles.Where(b => b.landUnit.strength > 0).ToList();
            if(validLandUnitBundles.Count == 0)
                return null;
            var weights = validLandUnitBundles.Select(b => b.GetAttackerWeight(isGlobalAttacker)).ToList();
            if (weights.Sum() <= 0)
                return null;
            return RandomUtils.Sample(validLandUnitBundles, weights);
        }

        float GetRefCommitOdd(float attackerSituation)
        {
            if(attackerSituation >= 0)
            {
                if(RandomUtils.NextFloat() > 0.5f * (1 - attackerSituation))
                {
                    return RandomUtils.NextFloat() * 1 + 1 + attackerSituation;
                }
                else
                {
                    return 1 / (RandomUtils.NextFloat() * (1 - attackerSituation) + 1 );
                }
            }
            return 1 / GetRefCommitOdd(-attackerSituation);
        }

        public LandBattleSubCombat GenerateSubCombatAsInitiative(LandBattleSideStateDynamic other, bool attackerInitiative)
        {
            var target = new LandBattleSubCombat.RoleBundle()
            {
                isGlobalAttacker=!attackerInitiative,
                isLocalInitiative=false,
                landUnitBundle=other.RollSubCombatTarget()
            };
            if(target.landUnitBundle == null)
                return null;

            var attacker = new LandBattleSubCombat.RoleBundle()
            {
                isGlobalAttacker=attackerInitiative,
                isLocalInitiative=true,
                landUnitBundle=RollSubCombatAttacker(attackerInitiative) // TODO: Introduce postive correlation for history engagement?
            };
            if(attacker.landUnitBundle == null)
                return null;

            var attackerSituation = Math.Clamp(landBattle.attackerSituation, -1, 1);

            // var refCommitOdd = RandomUtils.NextFloat() * 1 + 1; // 1:1 ~ 2:1
            // if(RandomUtils.NextFloat() <= 0.5f)
            // {
            //     refCommitOdd = 1 / refCommitOdd;
            // }

            var refCommitOdd = GetRefCommitOdd(attackerSituation);

            var attackerCommitableStrength = attacker.GetCommitableStrength();
            var targetCommitableStrength = target.GetCommitableStrength();

            var attackerCommitStrength = (int)Math.Floor(Math.Min(attackerCommitableStrength, targetCommitableStrength * refCommitOdd));
            var targetCommitStrength = (int)Math.Floor(attackerCommitStrength / refCommitOdd);

            attacker.commitStrength = Math.Max(1, attackerCommitStrength);
            target.commitStrength = Math.Max(1, targetCommitStrength);

            var subCombat = new LandBattleSubCombat()
            {
                attacker=attacker,
                target=target,
                chanceUsage=Math.Min(attackerCommitStrength, targetCommitStrength) // TODO: Use more detailed method
            };

            // FIXME: Code smell...
            attacker.subCombat = subCombat;
            target.subCombat = subCombat;

            return subCombat;
        }

        public void StopAttack() // used by attacker
        {
            foreach(var groupBundle in topGroupBundles)
            {
                var group = groupBundle.group;
                group.StartStopLandAttack();
            }
        }

        public void RetreatFromDefend() // used by defender
        {
            foreach(var groupBundle in topGroupBundles)
            {
                var group = groupBundle.group;
                if (group.IsBase())
                    continue;

                group.StartRetreatFromLandDefend();
                // group.posture = StrategicGroup.GroupPostureType.Disengaged;
                // group.restoredHours = 48; // TODO: It's questionable to "return" to Active state sometimes, looks like we should separated those types of states.
            }
        }

        public override string ToString()
        {
            return $"LandBattleSideStateDynamic({country}, {cell}, {chance})";
        }
    }
}
