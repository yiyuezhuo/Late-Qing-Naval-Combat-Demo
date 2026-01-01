using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using CoreUtils;

namespace StrategicCombatCore
{
    public class AreaState
    {
        [XmlAttribute]
        public string name;

        [XmlAttribute]
        public float posX;

        [XmlAttribute]
        public float posY;

        [XmlAttribute]
        public float scaleX;

        [XmlAttribute]
        public float scaleY;
    }

    public class AreaSystem
    {
        public PictureReference backgroundReference = new();
        public List<AreaState> areaStates = new();
    }

    public partial class StrategicScenarioState
    {
        public DateTime dateTime = new DateTime(1894, 7, 25, 0, 0, 0, DateTimeKind.Utc); // The battle of Pundo, Note: UTC => Local +8, so standard time of strategic is defined in 8:00 am
        // public bool enableFogOfWar; // false => god eye's view, true => pick a side as current's view, is it actually a view state?
        // public string fogOfWarViewerSideObjectId;

        public string pendingNavalCombatId;

        public bool enableGridSystem = true;
        public bool enableAreaSystem = false;
        public AreaSystem areaSystem = new();
        public bool enableSinoJapaneseHighCommand = true;
        public bool enableContactReportBasedNavalCombat = true;
        public bool enableVladivostokSquadronScript = false;
        public bool enableStrategicDisengagementRoll = true;

        public NavalForceEstimation.Rule.Mode estimationRuleMode = NavalForceEstimation.Rule.Mode.SinoJapaneseWar;
        public NavalForceEstimation.EstimationCategoryConfig GetEstimationCategoryConfig(NavalForceEstimation.EstimationCategory estimationCategory) => 
        
        NavalForceEstimation.Rule.Get(estimationRuleMode).estimateConfigMap[estimationCategory];
    }
}