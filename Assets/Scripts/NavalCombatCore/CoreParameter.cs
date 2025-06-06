using Unity.VisualScripting;

namespace NavalCombatCore
{
    public class CoreParameter
    {
        static CoreParameter instance = new CoreParameter();
        public static CoreParameter Instance => instance;

        public bool checkLandCollision = true;
        public bool checkShipCollision = true; 
    } 
}