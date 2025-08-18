using System.Collections.Generic;
using CoreUtils;
using Unity.Properties;
using UnityEngine.UIElements;
using UnityEngine;
using Unity.VisualScripting;
using NavalCombatCore;

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

    public static class StyleConstants
    {
        public static Dictionary<Country, Color> countryColorMap = new()
        {
            {Country.China, Color.yellow},
            {Country.Japan, Color.white},
            {Country.Britain, Color.red},
            {Country.France, Color.purple},
            {Country.Russia, Color.green},
            {Country.UnitedState, Color.blue},
            {Country.Spain, Color.darkOrange},
            {Country.Germany, Color.black},
            {Country.Italy, Color.greenYellow},
            {Country.Austria, Color.silver},
            {Country.Turkey, Color.darkGreen},
            {Country.Holland, Color.pink},
        };
    }

    public partial class StrategicGroup
    {
        [CreateProperty]
        public string sizeStr => GetSizeStr();

        [CreateProperty]
        public int combinedSubUnitSize => GetCombinedSubUnitSize();

        [CreateProperty]
        public Color countryColor => StyleConstants.countryColorMap.GetValueOrDefault(country, Color.gray);

        [CreateProperty]
        public StyleBackground typeIcon => UnityWebRequestImageReader.Instance.FetchStyleBackground($"{Application.streamingAssetsPath}/Pictures/GroupTypeIcons/{type}.png");
    }

    public partial class StrategicGroupMemberReference
    {
        [CreateProperty]
        public string name
        {
            get
            {
                var obj = Get();
                if (obj == null)
                    return "[Undefined or Invalid]";
                if (obj is ShipLog shipLog)
                {
                    return shipLog?.namedShip?.name?.mergedName;
                }
                if (obj is StrategicGroup group)
                {
                    return group?.name?.mergedName;
                }
                return "[Undefined or Invalid]";
            }
        }

        [CreateProperty]
        public StyleBackground icon
        {
            get
            {
                var obj = Get();
                if (obj == null)
                    return null;
                if (obj is ShipLog shipLog)
                {
                    return shipLog?.shipClass?.portraitStyleBackground ?? null;
                }
                if (obj is StrategicGroup group)
                {
                    return group.typeIcon;
                }
                return null;
            }
        }
    }
}