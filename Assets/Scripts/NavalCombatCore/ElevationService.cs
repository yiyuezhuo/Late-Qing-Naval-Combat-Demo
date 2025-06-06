

namespace NavalCombatCore
{
    public interface IElevationProvider
    {
        public float GetElevation(LatLon latLon);
    }

    public class FallbackElevationProvider : IElevationProvider
    {
        public float GetElevation(LatLon latLon)
        {
            return 0;
        }
    }

    public class ElevationService
    {
        public IElevationProvider elevationProvider = new FallbackElevationProvider();

        static ElevationService instance = new ElevationService();
        public static ElevationService Instance
        {
            get => instance;
        }

        public float GetElevation(LatLon latLon)
        {
            return elevationProvider.GetElevation(latLon);
        }
    }
}