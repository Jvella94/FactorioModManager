using FactorioModManager.Services;
using FactorioModManager.Services.API;
using FactorioModManager.Services.Infrastructure;
using FactorioModManager.ViewModels.MainWindow;
using System;
using System.Collections.Generic;

namespace FactorioModManager
{
    public class ServiceContainer
    {
        private static ServiceContainer? _instance;
        public static ServiceContainer Instance => _instance ??= new ServiceContainer();

        private readonly Dictionary<Type, object> _singletons = [];
        private readonly Dictionary<Type, Func<object>> _factories = [];

        private ServiceContainer()
        {
            RegisterServices();
        }

        private void RegisterServices()
        {
            // Infrastructure
            RegisterSingleton<IUIService>(new AvaloniaUIService());

            // Core Services
            RegisterSingleton<ISettingsService>(new SettingsService());
            RegisterSingleton<ILogService>(new LogService());
            RegisterSingleton<IModGroupService>(new ModGroupService());
            RegisterSingleton<IModMetadataService>(new ModMetadataService());

            RegisterSingleton<IModService>(new ModService(
                Resolve<ISettingsService>(),
                Resolve<ILogService>()
            ));

            // API Services - wrap with caching
            var apiService = new FactorioApiService();
            var cachedApiService = new CachedFactorioApiService(apiService);
            RegisterSingleton<IFactorioApiService>(cachedApiService);

            // ViewModels
            RegisterFactory(() => new MainWindowViewModel(
                Resolve<IModService>(),
                Resolve<IModGroupService>(),
                Resolve<IFactorioApiService>(),
                Resolve<IModMetadataService>(),
                Resolve<ISettingsService>(),
                Resolve<IUIService>(),
                Resolve<ILogService>()
            ));
        }

        public void RegisterSingleton<T>(T instance) where T : class
        {
            _singletons[typeof(T)] = instance;
        }

        public void RegisterFactory<T>(Func<T> factory) where T : class
        {
            _factories[typeof(T)] = () => factory();
        }

        public T Resolve<T>() where T : class
        {
            var type = typeof(T);

            if (_singletons.TryGetValue(type, out var singleton))
            {
                return (T)singleton;
            }

            if (_factories.TryGetValue(type, out var factory))
            {
                return (T)factory();
            }

            throw new InvalidOperationException($"Service of type {type.Name} is not registered");
        }

        /// <summary>
        /// Check if a service is registered
        /// </summary>
        public bool IsRegistered<T>() where T : class
        {
            var type = typeof(T);
            return _singletons.ContainsKey(type) || _factories.ContainsKey(type);
        }
    }
}