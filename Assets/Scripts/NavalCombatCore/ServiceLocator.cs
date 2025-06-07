using System;
using System.Collections.Generic;

namespace NavalCombatCore
{
    public class MaskCheckResult
    {
        public bool isMasked;
        public object maskedObject;
        public string message;
    }

    // IMaskProvider should look at NavalGameState's data, such as location of ships and size, to determine if LOS is masked.
    // The object which is at src location would not block LOS.
    public interface IMaskCheckService
    {
        MaskCheckResult Check(LatLon src, LatLon dst);
        MaskCheckResult Check(ShipLog observer, ShipLog target);
    }


    public class FallbackMaskChecker : IMaskCheckService
    {
        public MaskCheckResult Check(LatLon src, LatLon dst) => new();
        public MaskCheckResult Check(ShipLog observer, ShipLog target) => new();
    }

    public interface ILoggerService
    {
        void Log(string message);
    }

    public class FallbackLogger : ILoggerService
    {
        public void Log(string message)
        {
            System.Console.WriteLine(message);
        }
    }

    public static class ServiceLocator
    {
        static Dictionary<Type, object> services = new()
        {
            {typeof(ILoggerService), new FallbackLogger()},
            {typeof(IMaskCheckService), new FallbackMaskChecker()}
        };

        public static T Get<T>() where T : class
        {
            var type = typeof(T);
            if (services.ContainsKey(type))
            {
                return (T)services[type];
            }
            return null;
        }

        public static void Register<T>(T service) where T : class
        {
            var type = typeof(T);
            var currentValue = Get<T>();
            if (currentValue != null)
            {
                var logger = Get<ILoggerService>();
                logger.Log($"Overriding service: {currentValue} -> {service}");
            }
            services[type] = service;
        }
    }
}