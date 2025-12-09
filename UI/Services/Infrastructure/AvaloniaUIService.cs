using Avalonia.Controls;
using Avalonia.Threading;
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

        public async Task ShowMessageAsync(string title, string message)
        {
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var window = GetMainWindow();
                if (window != null)
                {
                    var messageBoxWindow = new MessageBoxDialog(title, message);
                    await messageBoxWindow.ShowDialog(window);
                }
            });
            _logService.LogDebug($"Messagbox shown with: [{title}] {message}");
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
                var dialog = new Views.SettingsWindow(settingsService);
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

        /// <summary>
        /// ✅ Shows a confirmation dialog with Yes/No buttons
        /// </summary>
        public async Task<bool> ShowConfirmationAsync(string title, string message)
        {
            return await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var window = GetMainWindow();
                if (window == null)
                    return false;

                var confirmWindow = new Window
                {
                    Title = title,
                    Width = 400,
                    Height = 200,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    CanResize = false
                };

                var textBlock = new Avalonia.Controls.TextBlock
                {
                    Text = message,
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    Margin = new Avalonia.Thickness(20)
                };

                var result = false;

                var yesButton = new Button
                {
                    Content = "Yes",
                    Width = 80,
                    Margin = new Avalonia.Thickness(0, 10, 10, 0)
                };

                var noButton = new Button
                {
                    Content = "No",
                    Width = 80,
                    Margin = new Avalonia.Thickness(10, 10, 0, 0)
                };

                yesButton.Click += (s, e) =>
                {
                    result = true;
                    confirmWindow.Close();
                };

                noButton.Click += (s, e) =>
                {
                    result = false;
                    confirmWindow.Close();
                };

                var buttonPanel = new StackPanel
                {
                    Orientation = Avalonia.Layout.Orientation.Horizontal,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    Children = { yesButton, noButton }
                };

                var stack = new StackPanel
                {
                    Children = { textBlock, buttonPanel }
                };

                confirmWindow.Content = stack;

                await confirmWindow.ShowDialog(window);

                return result;
            });
        }
    }
}