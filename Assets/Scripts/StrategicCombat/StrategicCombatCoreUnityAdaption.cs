using CoreUtils;
using Unity.Properties;

namespace StrategicCombatCore
{
    public partial class DepartmentPosition
    {
        [CreateProperty]
        public string leaderName => EntityManager.Instance.Get<Leader>(objectId)?.name?.GetMergedName() ?? "[Not Defined or Invalid]";
    }
}