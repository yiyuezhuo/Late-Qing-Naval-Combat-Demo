using System;
using System.Collections.Generic;
using System.Linq;
using CoreUtils;

namespace NavalCombatCore
{
    public static class NavalCombatCoreUtils
    {
        public static float CalibrateSurviveProb(float prob1, float seconds1, float seconds2) // (0.5, 120, 1) will convert 50% / turn to p / second
        {
            // (1-Prob2)^(Seconds1/Seconds2) = (1-Prob1)
            // Prob2 = 1 - (1-Prob1)^(Seconds2/Seconds1)
            return (float)(1 - Math.Pow(1 - prob1, seconds2 / seconds1));
        }

        public static float CalibrateSurviceProbFromTurnProb(float probTurn, float deltaSeconds)
        {
            return CalibrateSurviveProb(probTurn, 120, deltaSeconds);
        }
    }

    public static class ShipGroupOrderUtils
    {
        public static List<ShipGroup> GetShipGroupsInOobOrder(NavalGameState state)
        {
            return GetShipGroupsInOobOrder(state?.shipGroups, objectId => EntityManager.Instance.Get<IShipGroupMember>(objectId));
        }

        public static List<ShipGroup> GetTopLevelShipGroupsInOobOrder(NavalGameState state)
        {
            return GetTopLevelShipGroupsInOobOrder(state?.shipGroups);
        }

        public static List<ShipGroup> GetTopLevelShipGroupsInOobOrder(IReadOnlyList<ShipGroup> shipGroups)
        {
            if (shipGroups == null)
                return new List<ShipGroup>();

            return shipGroups.Where(group => group != null && string.IsNullOrEmpty(group.parentObjectId)).ToList();
        }

        public static List<ShipGroup> GetShipGroupsInOobOrder(IReadOnlyList<ShipGroup> shipGroups, Func<string, IShipGroupMember> resolver)
        {
            var orderedGroups = new List<ShipGroup>();
            if (shipGroups == null || resolver == null)
                return orderedGroups;

            void Visit(ShipGroup group)
            {
                if (group == null)
                    return;

                orderedGroups.Add(group);
                foreach (var childObjectId in group.childrenObjectIds ?? Enumerable.Empty<string>())
                {
                    var childGroup = resolver(childObjectId) as ShipGroup;
                    if (childGroup != null)
                        Visit(childGroup);
                }
            }

            foreach (var rootGroup in shipGroups.Where(group => group != null && string.IsNullOrEmpty(group.parentObjectId)))
            {
                Visit(rootGroup);
            }

            return orderedGroups;
        }
    }
}
