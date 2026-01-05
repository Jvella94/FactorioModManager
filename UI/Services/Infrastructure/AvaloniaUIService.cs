using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using FactorioModManager.Models;
using FactorioModManager.Services.Settings;
using FactorioModManager.Views.Dialogs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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

        public void OpenFile(string path)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
                _logService.Log($"Opened file: {path}");
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error opening file: {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// Reveal a file in the system file manager with the file selected where supported.
        /// Uses platform-specific invocation:
        /// - Windows: explorer /select,
        /// - macOS: open -R
        /// - Linux: try common file managers that support selecting a file; fall back to xdg-open/gio
        /// </summary>
        public void RevealFile(string path)
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    var args = $"/select,\"{path}\"";
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = args,
                        UseShellExecute = true
                    });
                    _logService.Log($"Revealed file in Explorer: {path}");
                    return;
                }

                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    var args = $"-R \"{path}\"";
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "open",
                        Arguments = args,
                        UseShellExecute = true
                    });
                    _logService.Log($"Revealed file in Finder: {path}");
                    return;
                }

                // Linux / other: try common file managers that support selecting a file
                var fileManagers = new[] { "nautilus", "dolphin", "nemo", "thunar", "caja" };

                foreach (var fm in fileManagers)
                {
                    var exe = FindExecutableInPath(fm);
                    if (exe is null)
                        continue;

                    var psi = new ProcessStartInfo
                    {
                        FileName = exe,
                        Arguments = $"--select \"{path}\"",
                        UseShellExecute = true
                    };

                    if (TryStartProcess(psi))
                    {
                        _logService.Log($"Revealed file using {fm}: {path}");
                        return;
                    }
                }

                // Fallback: open containing folder with xdg-open or gio
                var dir = Path.GetDirectoryName(path) ?? path;
                var opener = FindExecutableInPath("xdg-open") ?? FindExecutableInPath("gio");
                if (opener != null)
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = opener,
                        Arguments = $"\"{dir}\"",
                        UseShellExecute = true
                    };

                    if (TryStartProcess(psi))
                    {
                        _logService.Log($"Opened containing folder: {dir}");
                        return;
                    }
                }

                _logService.LogWarning($"No suitable file manager found to reveal file: {path}");
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error revealing file: {ex.Message}", ex);
                throw;
            }
        }

        private static IClassicDesktopStyleApplicationLifetime? GetDesktopLifetime()
        {
            return Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        }

        public Window? GetMainWindow()
        {
            return GetDesktopLifetime()?.MainWindow;
        }

        public async Task<bool> ShowSettingsDialogAsync()
        {
            return await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var desktop = GetDesktopLifetime();
                var owner = GetMainWindow();

                // If settings window already open, focus and bring to front
                if (desktop != null)
                {
                    var existing = desktop.Windows.OfType<Views.SettingsWindow>().FirstOrDefault();
                    if (existing != null)
                    {
                        existing.Activate();
                        existing.Topmost = true;
                        existing.Topmost = false;
                        existing.Focus();
                        return false; // already open
                    }
                }

                var settingsService = ServiceContainer.Instance.Resolve<ISettingsService>();
                var loggingService = ServiceContainer.Instance.Resolve<ILogService>();
                var dialog = new Views.SettingsWindow(settingsService, loggingService);
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
                var desktop = GetDesktopLifetime();
                var owner = GetMainWindow();

                if (desktop != null)
                {
                    var existing = desktop.Windows.OfType<Views.UpdateCheckDialog>().FirstOrDefault();
                    if (existing != null)
                    {
                        existing.Activate();
                        existing.Topmost = true;
                        existing.Topmost = false;
                        existing.Focus();
                        return (false, 0);
                    }
                }

                var dialog = new Views.UpdateCheckDialog();
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

        public async Task ShowChangelogAsync(string modTitle, string changelog)
        {
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var desktop = GetDesktopLifetime();
                var owner = GetMainWindow();

                // If a changelog window for this mod exists, bring to front
                if (desktop != null)
                {
                    var existing = desktop.Windows.OfType<Views.ChangelogWindow>().FirstOrDefault(w => w.ModTitle?.Text?.StartsWith(modTitle) == true);
                    if (existing != null)
                    {
                        existing.Activate();
                        existing.Topmost = true;
                        existing.Topmost = false;
                        existing.Focus();
                        return; // already focused
                    }
                }

                var window = new Views.ChangelogWindow(modTitle, changelog);
                window.Show();
            });
        }

        public async Task ShowVersionHistoryAsync(string modTitle, string modName,
            System.Collections.Generic.List<Models.DTO.ShortReleaseDTO> releases)
        {
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var desktop = GetDesktopLifetime();
                var owner = GetMainWindow();

                if (desktop != null)
                {
                    var existing = desktop.Windows.OfType<Views.VersionHistoryWindow>().FirstOrDefault(w => (w.DataContext as ViewModels.Dialogs.VersionHistoryViewModel)?.ModName == modName);
                    if (existing != null)
                    {
                        existing.Activate();
                        existing.Topmost = true;
                        existing.Topmost = false;
                        existing.Focus();
                        return; // already focused
                    }
                }

                var window = new Views.VersionHistoryWindow(modTitle, modName, releases);
                window.Show();
            });
        }

        public async Task SetClipboardTextAsync(string text)
        {
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                try
                {
                    var top = TopLevel.GetTopLevel(GetMainWindow());
                    if (top?.Clipboard is not null)
                    {
                        await top.Clipboard.SetTextAsync(text);
                    }
                }
                catch (Exception ex)
                {
                    _logService.LogError($"Clipboard error: {ex.Message}", ex);
                }
            });
        }

        private static string? FindExecutableInPath(string name)
        {
            var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            var paths = path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

            string[] exts;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var pathext = Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE";
                exts = pathext.Split(';', StringSplitOptions.RemoveEmptyEntries);
            }
            else
            {
                exts = [string.Empty];
            }

            foreach (var dir in paths)
            {
                foreach (var ext in exts)
                {
                    var candidate = Path.Combine(dir, name + ext);
                    try
                    {
                        if (File.Exists(candidate))
                            return candidate;
                    }
                    catch
                    {
                        // ignore directories we can't access
                    }
                }
            }

            return null;
        }

        private static bool TryStartProcess(ProcessStartInfo psi)
        {
            try
            {
                Process.Start(psi);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<List<ModListPreviewResult>?> ShowModListPreviewAsync(List<ModListPreviewItem> items, string listName, Window? parentWindow = null)
        {
            return await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var owner = parentWindow ?? GetMainWindow();
                if (owner == null) return null;

                var dialog = new Views.ModListPreviewDialog(items, listName);
                var res = await dialog.ShowDialog<List<Views.ModListPreviewDialog.PreviewResult>>(owner);
                if (res == null) return null;
                return res.Select(r => new ModListPreviewResult { Name = r.Name, ApplyEnabled = r.ApplyEnabled, ApplyVersion = r.ApplyVersion }).ToList();
            });
        }

        public async Task<string?> ShowPickModListAsync(List<string> listNames, string? title = null, Window? parentWindow = null)
        {
            return await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var owner = parentWindow ?? GetMainWindow();
                if (owner == null) return null;

                var dlg = new Window { Title = title ?? "Select Mod List", Width = 420, Height = 360 };
                var root = new Grid { RowDefinitions = new RowDefinitions("*,Auto"), Margin = new Thickness(8) };

                var listBox = new ListBox { ItemsSource = listNames }; // single-click selection supported by default
                listBox.DoubleTapped += (_, __) => { if (listBox.SelectedItem != null) dlg.Close(); };

                // Support Enter key to accept selection
                listBox.KeyDown += (s, e) =>
                {
                    if (e.Key == Key.Enter && listBox.SelectedItem != null)
                    {
                        dlg.Close();
                    }
                };

                root.Children.Add(listBox);
                Grid.SetRow(listBox, 0);

                var panel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right, Spacing = 8 };
                var cancel = new Button { Content = "Cancel", Width = 100 };
                var ok = new Button { Content = "OK", Width = 100 };
                panel.Children.Add(cancel);
                panel.Children.Add(ok);

                root.Children.Add(panel);
                Grid.SetRow(panel, 1);

                var tcs = new TaskCompletionSource<string?>();

                cancel.Click += (_, __) => { tcs.SetResult(null); dlg.Close(); };
                ok.Click += (_, __) => { tcs.SetResult(listBox.SelectedItem as string); dlg.Close(); };

                // Close also signals selection when user double-clicked or pressed Enter
                dlg.Closed += (_, __) => { if (!tcs.Task.IsCompleted) tcs.SetResult(listBox.SelectedItem as string); };

                await dlg.ShowDialog(owner);
                return await tcs.Task;
            });
        }

        public async Task<List<string>?> ShowActivationConfirmationAsync(string title, string message, List<(string Name, string Version)> items, Window? parentWindow = null)
        {
            return await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var owner = parentWindow ?? GetMainWindow();
                if (owner == null) return null;

                var dlg = new Window
                {
                    Title = title,
                    Width = 600,
                    Height = 400
                };

                var root = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto"), Margin = new Thickness(8) };

                var header = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4) };
                root.Children.Add(header);
                Grid.SetRow(header, 0);

                var scroll = new ScrollViewer { VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto };
                var stack = new StackPanel { Spacing = 4 };

                var checkboxes = new List<CheckBox>();
                foreach (var it in items)
                {
                    var cb = new CheckBox { Content = $"{it.Name}@{it.Version}", IsChecked = true };
                    checkboxes.Add(cb);
                    stack.Children.Add(cb);
                }

                scroll.Content = stack;
                root.Children.Add(scroll);
                Grid.SetRow(scroll, 1);

                var btnPanel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right, Spacing = 8 };
                var cancel = new Button { Content = "Cancel", Width = 100 };
                var ok = new Button { Content = "Apply", Width = 100, Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#D32F2F")), Foreground = Avalonia.Media.Brushes.White };
                btnPanel.Children.Add(cancel);
                btnPanel.Children.Add(ok);

                root.Children.Add(btnPanel);
                Grid.SetRow(btnPanel, 2);

                dlg.Content = root;

                var tcs = new TaskCompletionSource<List<string>?>();

                cancel.Click += (_, __) => { tcs.SetResult(null); dlg.Close(); };
                ok.Click += (_, __) =>
                {
                    var selected = checkboxes.Where(cb => cb.IsChecked == true).Select(cb => cb.Content?.ToString() ?? string.Empty).ToList();
                    tcs.SetResult(selected);
                    dlg.Close();
                };

                await dlg.ShowDialog(owner);
                return await tcs.Task;
            });
        }
    }
}