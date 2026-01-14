using System.Collections.Generic;
using System;


namespace YYZ
{
    public interface ILoggerService
    {
        void Log(string message);
        void LogWarning(string message);
        void LogError(string message);
    }

    public class FallbackLogger : ILoggerService
    {
        public void Log(string message)
        {
            System.Console.WriteLine(message);
        }

        public void LogWarning(string message)
        {
            System.Console.WriteLine("[Warn]:" + message);
        }

        public void LogError(string message)
        {
            System.Console.WriteLine("[Error]:" + message);
        }
    }

    public interface ILocalizeService
    {
        string Get(string key, params object[] args);
        string GetFor(object obj);
        string GetEnum<T>(T enumValue);
    }

    public class FallbackLocalizeService : ILocalizeService
    {
        public string Get(string key, params object[] args) => string.Format(key, args);
        public string GetFor(object obj) => Get(obj.ToString());
        public string GetEnum<T>(T enumValue) => GetFor(enumValue);
    }

    public static class ServiceLocator
    {
        static Dictionary<Type, object> services = new()
        {
            {typeof(ILoggerService), new FallbackLogger()},
            {typeof(ILocalizeService), new FallbackLocalizeService()}
            // {typeof(IMaskCheckService), new FallbackMaskChecker()}
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

    public static class YDebug
    {
        public static void Log(string log) => ServiceLocator.Get<ILoggerService>().Log(log);
        public static void LogWarning(string log) => ServiceLocator.Get<ILoggerService>().LogWarning(log);
        public static void LogError(string log) => ServiceLocator.Get<ILoggerService>().LogError(log);
    }
}