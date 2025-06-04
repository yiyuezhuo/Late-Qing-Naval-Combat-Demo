using System.Collections.Generic;

namespace NavalCombatCore
{
    public interface IWTAObject
    {
        float EvaluateArmorScore();
        float EvaluateSurvivability();
        float EvaluateBatteryFirepower();
        float EvaluateTorpedoThreatScore();
        float EvaluateRapidFiringFirepower();
        float EvaluateFirepowerScore();
        float EvaluateGeneralScore();
    }

    public class WeaponTargetAssignmentSolver // WTA Solver
    {
        // The primary goal is to reduce hositile potential firepower effectiveness. Thus:
        // 1. Fire Suppression: Deliver minimal firepower to enemy to create "under-fire" debuff to decrease their current fire projection.
        // 2. Mission Kill: Prior to attack low-survibility platform with high firepower.
        // 3. Prevent Concenertation: 
        // 4. Firepower stickiness: Firing plaform tend to fire at the same target to prevent goal change debuf and visual appearance.

        public void Solve(List<ShipLog> shooters, List<ShipLog> targets)
        {

        }
    }

}