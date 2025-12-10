using Avalonia.Controls;
using Avalonia.Threading;
using FactorioModManager.Services.Settings;
using FactorioModManager.Views.Dialogs;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace FactorioModManager.Services.Infrastructure
{
    public class AvaloniaUIService(ILogService logService) : IUIService
    {
        private readonly ILogService _logService = logService;

        public void Post(Action action)
        {
            Dispatcher.UIThread.Post(action);
        }

        public Task InvokeAsync(Action action)
        {
            return Dispatcher.UIThread.InvokeAsync(action).GetTask();
        }

        public Task<T> InvokeAsync<T>(Func<T> func)
        {
            return Dispatcher.UIThread.InvokeAsync(func).GetTask();
        }

        public async Task ShowMessageAsync(string title, string message, Window? parentWindow = null)
        {
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var owner = parentWindow ?? GetMainWindow();
                if (owner != null)
                {
                    var messageBoxWindow = new MessageBoxDialog(title, message);
                    await messageBoxWindow.ShowDialog(owner);
                    _logService.LogDebug($"Messagebox shown with: [{title}] {message}");
                }
            });
        }

        /// <summary>
        /// Shows a basic confirmation dialog with default styling
        /// </summary>
        public async Task<bool> ShowConfirmationAsync(string title, string message, Window? parentWindow = null)
        {
            return await ShowConfirmationAsync(title, message, parentWindow, "Yes", "No", null, null);
        }

        /// <summary>
        /// Shows a customizable confirmation dialog with custom button text and colors
        /// </summary>
        public async Task<bool> ShowConfirmationAsync(
            string title,
            string message,
            Window? parentWindow = null,
            string yesButtonText = "Yes",
            string noButtonText = "No",
            string? yesButtonColor = null,
            string? noButtonColor = null)
        {
            return await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var owner = parentWindow ?? GetMainWindow();
                if (owner == null)
                    return false;

                var confirmDialog = new ConfirmationDialog(
                    title,
                    message,
                    yesButtonText,
                    noButtonText,
                    yesButtonColor,
                    noButtonColor);

                var result = await confirmDialog.ShowDialog(owner);
                _logService.LogDebug($"Confirmation dialog [{title}]: {result}");
                return result;
            });
        }

        public void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
                _logService.Log($"Opened URL: {url}");
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error opening URL: {ex.Message}", ex);
                throw;
            }
        }

        public void OpenFolder(string path)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
                _logService.Log($"Opened folder: {path}");
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error opening folder: {ex.Message}", ex);
                throw;
            }
        }

        public Window? GetMainWindow()
        {
            return Avalonia.Application.Current?.ApplicationLifetime
                is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;
        }

        public async Task<bool> ShowSettingsDialogAsync()
        {
            return await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var settingsService = ServiceContainer.Instance.Resolve<ISettingsService>();
                var loggingService = ServiceContainer.Instance.Resolve<ILogService>();
                var dialog = new Views.SettingsWindow(settingsService, loggingService);
                var owner = GetMainWindow();
                if (owner != null)
                {
                    return await dialog.ShowDialog<bool>(owner);
                }
                return false;
            });
        }

        public async Task<(bool Success, int Hours)> ShowUpdateCheckDialogAsync()
        {
            return await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var dialog = new Views.UpdateCheckDialog();
                var owner = GetMainWindow();
                if (owner != null)
                {
                    return await dialog.ShowDialog<(bool, int)>(owner);
                }
                return (false, 0);
            });
        }

        public async Task<(bool Success, string? Data, bool IsUrl)> ShowInstallModDialogAsync()
        {
            return await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var dialog = new Views.InstallModDialog();
                var owner = GetMainWindow();
                if (owner != null)
                {
                    return await dialog.ShowDialog<(bool, string?, bool)>(owner);
                }
                return (false, null, false);
            });
        }

        public Task ShowChangelogAsync(string modTitle, string changelog)
        {
            return Dispatcher.UIThread.InvokeAsync(() =>
            {
                var window = new Views.ChangelogWindow(modTitle, changelog);
                window.Show();
            }).GetTask();
        }

        public Task ShowVersionHistoryAsync(string modTitle, string modName,
            System.Collections.Generic.List<Models.DTO.ReleaseDTO> releases)
        {
            return Dispatcher.UIThread.InvokeAsync(() =>
            {
                var window = new Views.VersionHistoryWindow(modTitle, modName, releases);
                window.Show();
            }).GetTask();
        }
    }
}