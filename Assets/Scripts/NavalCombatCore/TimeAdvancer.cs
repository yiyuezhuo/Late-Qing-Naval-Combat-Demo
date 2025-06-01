
namespace NavalCombatCore
{
    public class SimulationTimeAdvancer
    {
        public float pulseLengthSeconds = 1; // Step length
        public float simulationRateRatio = 30; // 30 => simulation is faster than real time x30. CMO's value: x1, x2, x5, x15, 1 fire, 2 fire.

        static SimulationTimeAdvancer _instance;
        public static SimulationTimeAdvancer Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new();
                }
                return _instance;
            }
        }

        public void Advance1Pulse()
        {
            NavalGameState.Instance.Step(pulseLengthSeconds);
        }

        public void AdvanceSimulationSeconds(float simulationSeconds)
        {
            while (simulationSeconds < pulseLengthSeconds)
            {
                Advance1Pulse();
                simulationSeconds -= pulseLengthSeconds;
            }
            NavalGameState.Instance.Step(simulationSeconds);
        }

        public void AdvanceRealSeconds(float realSeconds)
        {
            AdvanceSimulationSeconds(realSeconds * simulationRateRatio);
        }
    }
}