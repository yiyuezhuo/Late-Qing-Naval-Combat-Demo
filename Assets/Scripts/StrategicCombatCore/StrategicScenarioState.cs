using System;

namespace StrategicCombatCore
{
    public partial class StrategicScenarioState
    {
        public DateTime dateTime = new DateTime(1894, 7, 25, 0, 0, 0, DateTimeKind.Utc); // The battle of Pundo, Note: UTC => Local +8, so standard time of strategic is defined in 8:00 am
        public bool enableFogOfWar; // false => god eye's view, true => pick a side as current's view, is it actually a view state?
        public string fogOfWarViewerSideObjectId;

        public string pendingNavalCombatId;
    }
}