using System.Collections.Generic;
using CoreUtils;
using Unity.Properties;
using UnityEngine.UIElements;
using UnityEngine;
using NavalCombatCore;
using UnityEngine.InputSystem.Utilities;
using Unity.Collections;
using System.Xml.Serialization;


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

        [CreateProperty]
        public int cellInfoGroupCount => StrategicGameState.Instance.hexInfoMap.GetValueOrDefault((x, y))?.strategicGroupReferences?.Count ?? 0;
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

        [CreateProperty]
        public int xProp
        {
            get => x;
            set => x = value;
        }

        [CreateProperty]
        public int yProp
        {
            get => y;
            set => y = value;
        }

        [CreateProperty]
        public bool isXYEditable => deployState == DeployState.Independent;

        [CreateProperty]
        public StrategicGroupReference strategicGroupReferenceProp => strategicGroupReference;

        [CreateProperty]
        public string nameLink
        {
            get
            {
                var rawName = name.GetMergedName();
                if (rawName == null)
                    rawName = "_";
                return $"<link=\"nameLink\"><color=#40a0ff><u>{rawName}</u></color></link>";
            }
        }

        [XmlIgnore]
        [CreateProperty]
        public DeployState deployStateProp
        {
            get => deployState;
            set => SetDeployState(value);
        }

    }

    public partial class StrategicGroupReference
    {
        [CreateProperty]
        public string name => Get()?.name?.mergedName ?? "[Not Set or Invalid]";
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
                // TODO: Move casting to interface method
                if (obj is ShipLog shipLog)
                {
                    return shipLog?.namedShip?.name?.mergedName ?? "[Undefined or Invalid ShipLog]";
                }
                if (obj is StrategicGroup group)
                {
                    return group?.name?.mergedName ?? "[Undefined or Invalid StrategicGroup]";
                }
                if (obj is LandUnit landUnit)
                {
                    return landUnit?.name?.mergedName ?? "[Undefined or Invalid LandUnit]";
                }
                return "[Undefined or Invalid Unknown Type]";
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
                // if (obj is StrategicGroup group)
                // {
                //     return group.typeIcon;
                // }
                return null;
            }
        }

        [CreateProperty]
        public bool isShip
        {
            get
            {
                var obj = Get();
                return obj is ShipLog;
            }
        }

        [CreateProperty]
        public StyleBackground icon2
        {
            get
            {
                var obj = Get();
                if (obj == null)
                    return null;
                if (obj is StrategicGroup group)
                {
                    return group.typeIcon;
                }
                return null;
            }
        }

        [CreateProperty]
        public string sizeStr
        {
            get
            {
                var obj = Get();
                if (obj is StrategicGroup group)
                {
                    return group.sizeStr;
                }
                return "";
            }
        }

        [CreateProperty]
        public string desc1
        {
            get
            {
                var obj = Get();
                if (obj == null)
                    return "";
                // TODO: Move casting to interface method
                if (obj is ShipLog shipLog)
                {
                    var tons = shipLog?.shipClass?.displacementTons;
                    var type = shipLog?.shipClass?.type;
                    var crews = shipLog?.shipClass?.complementMen;
                    var className = shipLog?.shipClass?.name.mergedName;

                    return $"{type}, {tons} tons, {crews} men, (class: {className})";
                }
                if (obj is StrategicGroup group)
                {
                    return $"{group.type}, {group.combinedSubUnitSize} sub units";
                }
                if (obj is LandUnit landUnit)
                {
                    return $"{landUnit.stregnth} men";
                }
                return "";
            }
        }

        [CreateProperty]
        public string desc2
        {
            get
            {
                var obj = Get();
                if (obj == null)
                    return "";

                if (obj is ShipLog shipLog)
                {
                    var maxSpeed = shipLog.GetMaxSpeedKnots();
                    return $"{shipLog.mapState}, {shipLog.operationalState}, {maxSpeed} kts, DP: {shipLog.damagePoint} / {shipLog.shipClass.damagePoint}";
                }

                return "";
            }
        }

    }

    public partial class WeaponRecord
    {
        [CreateProperty]
        public string name => Get()?.name?.mergedName ?? "[Undefined or Invalid]";

    }

    public partial class LandUnitTemplate
    {
        [CreateProperty]
        public int weaponStrength => GetWeaponStrength();

        [CreateProperty]
        public int weaponGuns => GetWeaponGuns();
    }

    public partial class LandUnit
    {
        [CreateProperty]
        public string landUnitTemplateName => GetLandUnitTemplate()?.name?.mergedName ?? "[Undefined or Invalid]";
    }

}