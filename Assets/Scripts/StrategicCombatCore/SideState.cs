using CoreUtils;
using System.Collections.Generic;
using NavalCombatCore;
using System.Linq;

namespace StrategicCombatCore
{
    public enum DiplomacyState
    {
        Neutral,
        War
    }

    public partial class DiplomacyRelation
    {
        public string sideObjectId;
        public float relationValue;
        public DiplomacyState state;

        public SideState GetSideState()
        {
            return EntityManager.Instance.Get<SideState>(sideObjectId);
        }
    }


    public class SideState : IObjectIdLabeled
    {
        public string objectId { get; set; }
        public IEnumerable<IObjectIdLabeled> GetSubObjects()
        {
            yield break;
        }
        public GlobalString name = new();
        public List<Country> countries = new();
        public List<DiplomacyRelation> diplomacyRelations = new();
        public float victoryPoints;
        public bool recommended = false;
        public bool supplyAutomation = true;
        public bool automaticalTransportAssetBalance = true;
        public bool automaticArmyOperation = false;
        public bool automaticNavyOperation = false;
        public string remark;
        public StrategicPowerInfluenceMapCache powerInfluenceMapCache = new();

        public AmmunitionLoadoutWeightRecord defaultAmmunitionLoadoutWeightRecord = new();
        public List<AmmunitionLoadoutWeightRecord> extraAmmunitionLoadoutWeightRecords = new();

        public override string ToString()
        {
            return $"SideState({name.mergedName})";
        }

        public ResetDamageExpenditureStateContext GetResetDamageExpenditureStateContext()
        {
            var merged = extraAmmunitionLoadoutWeightRecords.Append(defaultAmmunitionLoadoutWeightRecord).ToList();
            return new()
            {
                ammunitionLoadoutWeightRecords = merged
            };
        }
    }
}
