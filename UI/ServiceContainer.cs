using FactorioModManager.Models.Domain;
using FactorioModManager.Services;
using FactorioModManager.Services.API;
using FactorioModManager.Services.Infrastructure;
using FactorioModManager.Services.Mods;
using FactorioModManager.Services.Platform;
using FactorioModManager.Services.Settings;
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
            RegisterSingleton<IErrorMessageService>(new ErrorMessageService());
            RegisterSingleton<IErrorMapper>(new ErrorMapper());
            RegisterSingleton<ILogService>(new LogService(Resolve<IErrorMessageService>()));
            RegisterSingleton<IUIService>(new AvaloniaUIService(Resolve<ILogService>()));

            // Register a singleton DownloadProgressViewModel so it is shared application-wide
            var downloadProgressSingleton = new DownloadProgressViewModel(Resolve<IUIService>());
            RegisterSingleton<IDownloadProgress>(downloadProgressSingleton);

            var httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
            RegisterSingleton(httpClient);

            // Core Settings
            RegisterSingleton<ISettingsService>(new SettingsService(Resolve<ILogService>()));

            // Register platform service (Avalonia) so ViewModels can use it via DI
            RegisterSingleton<IPlatformService>(new AvaloniaPlatformService(Resolve<IUIService>()));

            // Settings Adapters
            RegisterSingleton<IModPathSettings>(new ModPathSettings(Resolve<ISettingsService>()));
            RegisterSingleton<IApiCredentials>(new ApiCredentials(Resolve<ISettingsService>()));
            RegisterSingleton<IFactorioEnvironment>(new FactorioEnvironment(
                Resolve<ISettingsService>(),
                Resolve<ILogService>()));

            RegisterSingleton<IModGroupService>(new ModGroupService(Resolve<ILogService>()));
            RegisterSingleton<IModMetadataService>(new ModMetadataService(Resolve<ILogService>()));
            RegisterSingleton<IThumbnailCache>(new ThumbnailCache(Resolve<ILogService>()));

            RegisterSingleton<IAppUpdateChecker>(new AppUpdateChecker(
                Resolve<ILogService>(),
                Resolve<HttpClient>(),
                Resolve<IUIService>()));
            RegisterSingleton<IDownloadService>(new DownloadService(
                Resolve<ISettingsService>(),
                Resolve<ILogService>(),
                Resolve<HttpClient>()));

            // API Services
            var apiService = new FactorioApiService(Resolve<HttpClient>(), Resolve<ILogService>());
            var cachedApiService = new CachedFactorioApiService(apiService, Resolve<ILogService>());
            RegisterSingleton<IFactorioApiService>(cachedApiService);

            RegisterSingleton<IModMetadataFetcher>(new ModMetadataFetcher(
             Resolve<IFactorioApiService>(),
             Resolve<IModMetadataService>(),
             Resolve<IThumbnailCache>(),
             Resolve<IUIService>(),
             Resolve<ILogService>()));

            // Mod Services
            RegisterSingleton<IModVersionManager>(new ModVersionManager(
                Resolve<ILogService>(),
                Resolve<IModPathSettings>(),
                Resolve<IDownloadService>()));

            RegisterSingleton<IDependencyFlow>(new DependencyFlow(
                Resolve<ILogService>(),
                Resolve<IFactorioApiService>(),
                Resolve<ISettingsService>()));

            RegisterSingleton<IModRepository>(new ModRepository(
                Resolve<ILogService>(),
                Resolve<IModPathSettings>()));

            RegisterSingleton<IModService>(new ModService(
                Resolve<IModRepository>(),
                Resolve<ILogService>()));

            RegisterSingleton<IModUpdateService>(new ModUpdateService(
                Resolve<IFactorioApiService>(),
                Resolve<IModMetadataService>(),
                Resolve<IModPathSettings>(),
                Resolve<ILogService>()));

            // Mod refresh service
            RegisterSingleton<IModRefreshService>(new ModRefreshService(
                Resolve<IModService>(),
                Resolve<IModGroupService>(),
                Resolve<IModMetadataService>(),
                Resolve<ILogService>()));

            RegisterSingleton<IModFilterService>(new ModFilterService());

            // Launcher
            RegisterSingleton<IFactorioLauncher>(new FactorioLauncher(
                Resolve<IFactorioEnvironment>(),
                Resolve<ILogService>(),
                Resolve<ISettingsService>()));

            // ViewModels
            // Create with parameterless constructor (partial viewmodels handle wiring)
            RegisterFactory(() => new MainWindowViewModel(
                Resolve<IModService>(),
                Resolve<IModGroupService>(),
                Resolve<IFactorioApiService>(),
                Resolve<IModMetadataService>(),
                Resolve<ISettingsService>(),
                Resolve<IUIService>(),
                Resolve<ILogService>(),
                Resolve<IDownloadService>(),
                Resolve<IErrorMessageService>(),
                Resolve<IAppUpdateChecker>(),
                Resolve<IDependencyFlow>(),
                Resolve<IModVersionManager>(),
                Resolve<IFactorioLauncher>(),
                Resolve<IThumbnailCache>(),
                Resolve<IModFilterService>(),
                Resolve<IDownloadProgress>()
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