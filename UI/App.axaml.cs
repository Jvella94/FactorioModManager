using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using FactorioModManager.Services.Infrastructure;
using FactorioModManager.Views;
using System;
using System.Diagnostics;

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
                    var logservice = container.Resolve<ILogService>();
                    // Auto-prune logs older than 30 days on startup
                    logservice.PruneOldLogs(30);
                    // Log startup
                    logservice.Log("Application starting...");

                    desktop.MainWindow = new MainWindow();

                    // Dispose service container when main window closes
                    if (desktop.MainWindow != null)
                        desktop.MainWindow.Closed += (_, __) => ServiceContainer.Instance.Dispose();

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
    }
}