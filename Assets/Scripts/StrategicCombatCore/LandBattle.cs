using System;
using System.Collections.Generic;
using System.Linq;
using CoreUtils;
using NavalCombatCore;

namespace StrategicCombatCore
{

    public partial class LandBattleUnitState // mainly logging for loss of engaged units
    {
        public string unitId;
        public int currentStrengthLoss;
        public int accumulatedStrengthLoss;
        public int currentStrengthKill; // kill
        public int accumulatedStrengthKill;
        // Current Strength is accessed from the concrete LandUnit

        // frozen attributes
        public int endStrength;
        // public bool end;

        public LandUnit GetLandUnit() => EntityManager.Instance.Get<LandUnit>(unitId);

        public void StepResetState()
        {
            currentStrengthLoss = 0;
            currentStrengthKill = 0;
        }

        // public int GetStrength() => end ? endStrength : EntityManager.Instance.Get<LandUnit>(unitId)?.strength ?? 0;
    }

    public partial class LandBattleSideState
    {
        public string sideId;
        public List<LandBattleUnitState> unitStates = new();
        // public float globalTacticalModifier; // TODO: Represent it in Hex instead of Battle?

        // frozen attributes
        public string currentLeaderId; // set in dynamic resolve
        public Country currentCountry;
        // public bool end;

        public SideState GetSide() => EntityManager.Instance.Get<SideState>(sideId);
        public Leader GetLeader() => EntityManager.Instance.Get<Leader>(currentLeaderId);
        public float GetTotalCurrentStrengthLoss() => unitStates.Sum(u => u.currentStrengthLoss);
        public float GetTotalAccumulatedStrengthLoss() => unitStates.Sum(u => u.accumulatedStrengthLoss);

        // static string Localize(string key, params object[] args) => ServiceLocator.Get<ILocalizeService>().Get(key, args);

        public LazyLocalizedString GetSummary()
        {
            var currentStrength = unitStates.Sum(u => u.endStrength);
            var lossStrength = unitStates.Sum(u => u.accumulatedStrengthLoss);
            var commitStrength = currentStrength + lossStrength;
            return LazyLocalizedString.MakeTemplate(
                "Commit: {0}, Loss: {1}, Remain: {2}",
                LazyLocalizedString.MakeRaw(commitStrength),
                LazyLocalizedString.MakeRaw(lossStrength),
                LazyLocalizedString.MakeRaw(currentStrength)
            );
        }
    }


    public partial class LandBattle : IObjectIdLabeled
    {
        public string objectId { get; set; }
        public IEnumerable<IObjectIdLabeled> GetSubObjects()
        {
            yield break;
        }

        public bool end;
        public bool attackerVictory;
        public float attackerSituation; // global tactical modifier from viewpoint of the attacker
        public DateTime beginDateTime;
        public DateTime endDateTime;

        public LandBattleSideState attacker = new();
        public LandBattleSideState defender = new();
        public XY cellXY = new();

        public SideState GetAttacker() => EntityManager.Instance.Get<SideState>(attacker.sideId);
        public SideState GetDefender() => EntityManager.Instance.Get<SideState>(defender.sideId);
        // public Cell GetCell() => StrategicGameState.Instance.cellMatrix[cellXY.x, cellXY.y];
        public Cell GetCell() => cellXY.GetCell();
        public (Cell, SideState, SideState) GetKey() => (GetCell(), GetAttacker(), GetDefender());

        public LandBattleSideStateDynamic GetAttackerDynamic()
        {
            LandBattleSideStateDynamic ret = new();
            ret.Initialize(GetCell(), attacker, this);

            return ret;
        }

        public LandBattleSideStateDynamic GetDefenderDynamic()
        {
            LandBattleSideStateDynamic ret = new();
            ret.Initialize(GetCell(), defender, this);

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
                
                var subCombat = initiative.GenerateSubCombatAsInitiative(passive, attackerInitiative);
                if(subCombat == null)
                {
                    break;
                }

                ServiceLocator.Get<ILoggerService>().Log($"{dynamics[0]} vs {dynamics[1]}: {subCombat}");

                subCombat.Resolve();
                initiative.chance -= subCombat.chanceUsage;
                // foreach(var dynamic in dynamics)
                // {
                //     dynamic.chance -= subCombat.chanceUsage;
                // }
            }

            // Process initiative disengagement - (attacker is switched to passive, defender is switched to Disengaged)
            if(attackerSituation >= 1f) // defender retreat
            {
                def.RetreatFromDefend();
                // end = true;
                // GoToEnd();
            }
            else if(attackerSituation <= -1)
            {
                atk.StopAttack();
                // end = true;
                // GoToEnd();
            }

            // Update log states
            UpdateLogState(attacker, atk);
            UpdateLogState(defender, def);

            endDateTime = StrategicGameState.Instance.scenarioState.dateTime;
        }

        void UpdateLogState(LandBattleSideState landBattleSideState, LandBattleSideStateDynamic landBattleSideStateDyanmic)
        {
            landBattleSideState.currentLeaderId = landBattleSideStateDyanmic.battleLeader?.objectId;
            landBattleSideState.currentCountry = landBattleSideStateDyanmic.country;

            foreach(var landUnitBundle in landBattleSideStateDyanmic.landUnitBundles)
            {
                landUnitBundle.battleUnitState.endStrength = landUnitBundle.landUnit.strength;
            }
        }

        public void GoToEnd()
        {
            end = true;
            // endDateTime = StrategicGameState.Instance.scenarioState.dateTime;

            // var attackerDynamic = GetAttackerDynamic();
            // var defenderDynamic = GetDefenderDynamic();

            // attacker.currentLeaderId = attackerDynamic.battleLeader.objectId;
            // defender.currentLeaderId = defenderDynamic.battleLeader.objectId;
            
            // attacker.currentCountry = attackerDynamic.country;
            // defender.currentCountry = defenderDynamic.country;

            // attacker.end = true;
            // defender.end = true;

            // foreach(var landUnitBundle in attackerDynamic.landUnitBundles.Concat(defenderDynamic.landUnitBundles))
            // {
            //     landUnitBundle.battleUnitState.endStrength = landUnitBundle.landUnit.strength;
            //     landUnitBundle.battleUnitState.end = true;
            // }
            // foreach(var unitState in attacker.unitStates.Concat(defender.unitStates))
            // {
            //     unitState.end = true;
            // }
        }

        // public LazyLocalizedString GetSummary()
        // {
        //     var vicDesc = attackerVictory ? "Attacker Victory" : "Defender Victory";
        //     return LazyLocalizedString.MakeTemplate(
        //                 "Land battle end: {0} {1} ({2}) vs {3} ({4})",
        //                 StrategicGameState.Instance.GetCellNameLazyStr(cellXY),
        //                 LazyLocalizedString.MakeGlobalStringShort(attacker.name),
        //                 attacker.GetSummary(),
        //                 LazyLocalizedString.MakeGlobalStringShort(name),
        //                 defender.GetSummary()
        //             );
        // }
    }
}