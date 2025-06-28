
namespace NavalCombatCore
{
    public class CoreParameter
    {
        static CoreParameter instance = new CoreParameter();
        public static CoreParameter Instance => instance;

        public bool checkLandCollision = true;
        public bool checkShipCollision = true;
        public bool checkFriendlyShipCollision = false;

        public float angleStepDeg = 18; // 360 / 18 = 20 test angles => (0, 18, 36, 54, ...)
        public float attackCoef = 1f;
        // public float defenceCoef = 1f;
        public float defenceCoef = 0.1f;
        public float globalHitCoef = 1f;

        public float automaticTorpedoFiringRangeRelaxedCoef = 2.5f;
        // public float automaticTorpedoFiringRelaxedAngle = 60; // Or dynamic resolved using standard or emergency turn?
    }
}