using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using FactorioModManager.Services;
using FactorioModManager.Services.Infrastructure;
using FactorioModManager.Services.Mods;
using FactorioModManager.Views;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Diagnostics;
using System.Net.Http;

namespace FactorioModManager
{
    public partial class App : Application
    {
        public static IServiceProvider ServicesProvider { get; private set; } = null!;

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            // Configure services
            var services = new ServiceCollection();
            ConfigureServices(services);
            ServicesProvider = services.BuildServiceProvider();

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                try
                {
                    // Initialize service container
                    var container = ServiceContainer.Instance;
                    var logservice = container.Resolve<ILogService>();
                    // Auto-prune logs older than 30 days on startup
                    logservice.PruneOldLogs(30);
                    // Log startup
                    logservice.Log("Application starting...");

                    desktop.MainWindow = ServicesProvider.GetRequiredService<MainWindow>();

                    // Shutdown when main window closes
                    desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error during initialization: {ex.Message}", ex);
                    throw;
                }
            }

            base.OnFrameworkInitializationCompleted();
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            // Infrastructure Services (Singleton - live for app lifetime)
            services.AddSingleton<HttpClient>();
            services.AddSingleton<ILogService, LogService>();
            services.AddSingleton<IUIService, AvaloniaUIService>();

            // Domain Services (Singleton)
            services.AddSingleton<ISettingsService, SettingsService>();
            services.AddSingleton<IModGroupService, ModGroupService>();
            services.AddSingleton<IModMetadataService, ModMetadataService>();
            services.AddSingleton<IModService, ModService>();
            services.AddSingleton<IDownloadService, DownloadService>();

            // API Services (Singleton with caching)
            services.AddSingleton<Services.API.IFactorioApiService, Services.API.CachedFactorioApiService>();

            // ViewModels (Transient - new instance each time)
            services.AddTransient<ViewModels.MainWindow.MainWindowViewModel>();

            // Windows (Transient)
            services.AddTransient<MainWindow>();
            services.AddTransient<SettingsWindow>();
        }
    }
}