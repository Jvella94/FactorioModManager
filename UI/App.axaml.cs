using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using FactorioModManager.Services.Infrastructure;
using FactorioModManager.Views;
using System;

namespace FactorioModManager
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                try
                {
                    // Initialize service container
                    var container = ServiceContainer.Instance;

                    // Auto-prune logs older than 30 days on startup
                    LogService.Instance.PruneOldLogs(30);
                    // Log startup
                    LogService.Instance.Log("Application starting...");

                    desktop.MainWindow = new MainWindow();
                }
                catch (Exception ex)
                {
                    LogService.Instance.LogError($"Error during initialization: {ex.Message}");
                    throw;
                }
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}