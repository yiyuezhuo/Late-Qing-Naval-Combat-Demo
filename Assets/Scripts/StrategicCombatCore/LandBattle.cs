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