

using System.Numerics;

namespace NavalCombatCore
{
    public interface IElevationProvider
    {
        public float GetElevation(LatLon latLon);
    }

    public struct ShoreFieldSample
    {
        public float distancePixels;
        public Vector2 gradient;
    }

    public interface IShoreFieldProvider : IElevationProvider
    {
        bool HasValidROIShoreField();
        bool TrySampleROIShoreField(LatLon latLon, out ShoreFieldSample sample);
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
        public IShoreFieldProvider shoreFieldProvider => elevationProvider as IShoreFieldProvider;

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
