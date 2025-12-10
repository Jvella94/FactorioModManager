using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using FactorioModManager.Models;
using FactorioModManager.Services;
using FactorioModManager.Services.Infrastructure;
using FactorioModManager.ViewModels.Dialogs;
using FactorioModManager.Views.Base;
using System;
using System.IO;

namespace FactorioModManager.Views
{
    public partial class SettingsWindow : DialogWindowBase<(bool Success, string? Data, bool IsUrl)>
    {
        private readonly SettingsWindowViewModel _viewModel;
        private readonly ILogService _logService;

        public SettingsWindow() : this(ServiceContainer.Instance.Resolve<ISettingsService>(), ServiceContainer.Instance.Resolve<ILogService>())
        {
        }

        public SettingsWindow(ISettingsService settingsService, ILogService logService)
        {
            InitializeComponent();
            _viewModel = new SettingsWindowViewModel(settingsService);
            DataContext = _viewModel;

            _viewModel.SaveCommand.Subscribe(_ => Close(true));
            _viewModel.CancelCommand.Subscribe(_ => Close(false));
            _logService = logService;
        }

        private static readonly string[] _options = ["factorio"];

        private async void BrowseModsPath(object? sender, RoutedEventArgs e)
        {
            var topLevel = GetTopLevel(this);
            if (topLevel == null) return;

            var folderPickerOptions = new FolderPickerOpenOptions
            {
                Title = "Select Factorio Mods Folder",
                AllowMultiple = false
            };

            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(folderPickerOptions);
            if (folders.Count > 0)
            {
                _viewModel.ModsPath = folders[0].Path.LocalPath;
            }
        }

        private async void BrowseFactorioPath_Click(object sender, RoutedEventArgs e)
        {
            var storageProvider = StorageProvider;

            var file = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select Factorio Executable",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("Factorio Executable")
                    {
                        Patterns = OperatingSystem.IsWindows()
                            ? ["factorio.exe"]
                            : _options },
                    new FilePickerFileType("All Files") { Patterns = ["*"] }
                ]
            });

            if (file != null && file.Count > 0)
            {
                var path = file[0].Path.LocalPath;
                FactorioPathTextBox.Text = path;
                UpdateFactorioPathStatus(path);
            }
        }

        private void AutoDetectFactorioPath_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var detectedPath = FolderPathHelper.DetectFactorioExecutable();

                if (!string.IsNullOrEmpty(detectedPath))
                {
                    FactorioPathTextBox.Text = detectedPath;
                    UpdateFactorioPathStatus(detectedPath);
                    FactorioPathStatus.Foreground = Avalonia.Media.Brushes.LightGreen;
                    FactorioPathStatus.Text = $"✓ Auto-detected: {detectedPath}";
                    _logService.Log($"Auto-detected Factorio at: {detectedPath}", LogLevel.Info);
                }
                else
                {
                    FactorioPathStatus.Foreground = Avalonia.Media.Brushes.Orange;
                    FactorioPathStatus.Text = "⚠ Could not auto-detect Factorio. Please browse manually.";
                    _logService.Log("Failed to auto-detect Factorio executable", LogLevel.Warning);
                }
            }
            catch (Exception ex)
            {
                FactorioPathStatus.Foreground = Avalonia.Media.Brushes.Red;
                FactorioPathStatus.Text = $"❌ Error: {ex.Message}";
                _logService.LogError("Auto-detect error", ex);
            }
        }

        private async void TestFactorioPath_Click(object sender, RoutedEventArgs e)
        {
            var path = FactorioPathTextBox.Text?.Trim();

            if (string.IsNullOrEmpty(path))
            {
                path = FolderPathHelper.DetectFactorioExecutable();
            }

            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                FactorioPathStatus.Foreground = Avalonia.Media.Brushes.Red;
                FactorioPathStatus.Text = "❌ Invalid path or file does not exist";
                return;
            }

            try
            {
                var processInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(path) ?? string.Empty
                };

                System.Diagnostics.Process.Start(processInfo);

                FactorioPathStatus.Foreground = Avalonia.Media.Brushes.LightGreen;
                FactorioPathStatus.Text = "✓ Factorio launched successfully!";
                _logService.Log($"Test launch successful: {path}", LogLevel.Info);
            }
            catch (Exception ex)
            {
                FactorioPathStatus.Foreground = Avalonia.Media.Brushes.Red;
                FactorioPathStatus.Text = $"❌ Launch failed: {ex.Message}";
                _logService.LogError("Test launch failed", ex);
            }
        }

        private void UpdateFactorioPathStatus(string? path)
        {
            if (string.IsNullOrEmpty(path))
            {
                FactorioPathStatus.Foreground = Avalonia.Media.Brushes.Gray;
                FactorioPathStatus.Text = "ℹ Path will be auto-detected when launching Factorio";
            }
            else if (File.Exists(path))
            {
                FactorioPathStatus.Foreground = Avalonia.Media.Brushes.LightGreen;
                FactorioPathStatus.Text = $"✓ Valid executable found";
            }
            else
            {
                FactorioPathStatus.Foreground = Avalonia.Media.Brushes.Red;
                FactorioPathStatus.Text = "❌ File does not exist at this path";
            }
        }
    }
}