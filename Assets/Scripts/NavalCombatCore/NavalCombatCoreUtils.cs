using System;

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
}