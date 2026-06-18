using System.Collections.Generic;
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

    public interface IOperationalRoutePlanner
    {
        bool TryBuildOperationalRoute(ShipLog shipLog, LatLon targetPosition, out List<LatLon> routePoints);
    }

    public class FallbackElevationProvider : IElevationProvider
    {
        public float GetElevation(LatLon latLon)
        {
            return 0;
        }
    }

    public class FallbackOperationalRoutePlanner : IOperationalRoutePlanner
    {
        public bool TryBuildOperationalRoute(ShipLog shipLog, LatLon targetPosition, out List<LatLon> routePoints)
        {
            routePoints = null;
            return false;
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

    public class OperationalRoutePlannerService
    {
        public IOperationalRoutePlanner routePlanner = new FallbackOperationalRoutePlanner();

        static OperationalRoutePlannerService instance = new OperationalRoutePlannerService();
        public static OperationalRoutePlannerService Instance => instance;

        public bool TryBuildOperationalRoute(ShipLog shipLog, LatLon targetPosition, out List<LatLon> routePoints)
        {
            return routePlanner.TryBuildOperationalRoute(shipLog, targetPosition, out routePoints);
        }
    }
}
