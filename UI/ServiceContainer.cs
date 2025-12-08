using FactorioModManager.Services;
using FactorioModManager.Services.API;
using FactorioModManager.Services.Infrastructure;
using FactorioModManager.ViewModels.MainWindow;
using System;
using System.Collections.Generic;
using System.Net.Http;

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

            var httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
            RegisterSingleton(httpClient);

            // Core Services
            RegisterSingleton<ILogService>(new LogService());
            RegisterSingleton<ISettingsService>(new SettingsService(Resolve<ILogService>()));
            RegisterSingleton<IModGroupService>(new ModGroupService(Resolve<ILogService>()));
            RegisterSingleton<IModMetadataService>(new ModMetadataService());

            // API Services - wrap with caching
            var apiService = new FactorioApiService(Resolve<HttpClient>(), Resolve<ILogService>());
            var cachedApiService = new CachedFactorioApiService(apiService, Resolve<ILogService>());
            RegisterSingleton<IFactorioApiService>(cachedApiService);

            RegisterSingleton<IModService>(new ModService(
                Resolve<ISettingsService>(),
                Resolve<ILogService>(),
                Resolve<IFactorioApiService>()
            ));

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

        public void Dispose()
        {
            foreach (var singleton in _singletons.Values)
            {
                if (singleton is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            _singletons.Clear();
            _factories.Clear();
        }
    }
}