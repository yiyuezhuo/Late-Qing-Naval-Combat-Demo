using CoreUtils;
using Unity.Properties;
using UnityEngine.UIElements;

namespace StrategicCombatCore
{
    public partial class DepartmentPosition
    {
        [CreateProperty]
        public string leaderName => EntityManager.Instance.Get<Leader>(objectId)?.name?.GetMergedName() ?? "[Not Defined or Invalid]";

        [CreateProperty]
        public StyleBackground leaderPortrait => EntityManager.Instance.Get<Leader>(objectId)?.portraitReference?.pictureStyleBackground ?? null;
    }

    public partial class Cell
    {
        [CreateProperty]
        public string brief => $"({x}, {y}), {terrain}";
    }
}