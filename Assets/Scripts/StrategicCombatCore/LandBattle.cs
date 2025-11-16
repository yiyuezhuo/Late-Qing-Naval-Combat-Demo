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
        public float currentStrengthLoss;
        public float accumulatedStrengthLoss;

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

    public class LandBattle : IObjectIdLabeled
    {
        public string objectId { get; set; }
        public IEnumerable<IObjectIdLabeled> GetSubObjects()
        {
            yield break;
        }

        public bool end;
        public bool attackerVictory;

        public LandBattleSideState attacker = new();
        public LandBattleSideState defender = new();
        public XY cellXY = new();

        public SideState GetAttacker() => EntityManager.Instance.Get<SideState>(attacker.sideId);
        public SideState GetDefender() => EntityManager.Instance.Get<SideState>(defender.sideId);
        public Cell GetCell() => StrategicGameState.Instance.cellMatrix[cellXY.x, cellXY.y];
        public (Cell, SideState, SideState) GetKey() => (GetCell(), GetAttacker(), GetDefender());

    }
}