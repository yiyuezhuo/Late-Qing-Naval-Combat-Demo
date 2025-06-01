using System;

namespace NavalCombatCore
{
    public static class MeasureUtils
    {
        public static float MoveAngleTowards(float current, float target, float step)
        {
            // Normalize angles to 0-360 range
            current = NormalizeAngle(current);
            target = NormalizeAngle(target);
            
            // Calculate both possible directional differences (clockwise and counter-clockwise)
            float diff = target - current;
            float absDiff = Math.Abs(diff);
            
            // If direct difference is greater than 180°, choose the other direction
            if (absDiff > 180f)
            {
                if (diff > 0)
                {
                    diff -= 360f; // Take the shorter path clockwise
                }
                else
                {
                    diff += 360f; // Take the shorter path counter-clockwise
                }
            }
            
            // Calculate actual movement (not exceeding step size)
            float move = Math.Sign(diff) * Math.Min(Math.Abs(diff), step);
            
            // Apply movement and normalize result
            float result = current + move;
            return NormalizeAngle(result);
        }

        // Helper function: Normalize angle to 0-360 range
        public static float NormalizeAngle(float angle)
        {
            angle %= 360f; // Reduce to base value
            if (angle < 0)
            {
                angle += 360f; // Convert negative angles to positive equivalents
            }
            return angle;
        }


    }
}