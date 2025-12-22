using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using FactorioModManager.Services.Infrastructure;
using FactorioModManager.Services.Platform;
using FactorioModManager.Services.Settings;
using FactorioModManager.ViewModels.Dialogs;
using FactorioModManager.Views.Base;
using ReactiveUI;
using System;

namespace FactorioModManager.Views
{
    public partial class SettingsWindow : DialogWindowBase<(bool Success, string? Data, bool IsUrl)>
    {
        private readonly SettingsWindowViewModel _viewModel;

        public SettingsWindow() : this(ServiceContainer.Instance.Resolve<ISettingsService>(), ServiceContainer.Instance.Resolve<ILogService>())
        {
        }

        public SettingsWindow(ISettingsService settingsService, ILogService logService)
        {
            InitializeComponent();
            var platform = ServiceContainer.Instance.Resolve<IPlatformService>();
            _viewModel = new SettingsWindowViewModel(settingsService, platform, logService);
            DataContext = _viewModel;

            _viewModel.SaveCommand.Subscribe(_ => Close(true));
            _viewModel.CancelCommand.Subscribe(_ => Close(false));

            // Initialize UI fields from view model
            try
            {
                FactorioPathTextBox.Text = _viewModel.FactorioExePath;
            }
            catch { }

            try
            {
                FactorioDataPathTextBox.Text = _viewModel.FactorioDataPath;
            }
            catch { }

            // Subscribe to reactive status text changes
            _viewModel.WhenAnyValue(v => v.FactorioPathStatusText)
                      .Subscribe(text =>
                      {
                          FactorioPathStatus.Text = text ?? string.Empty;
                          if (text != null && text.StartsWith('✓')) FactorioPathStatus.Foreground = Avalonia.Media.Brushes.LightGreen;
                      });

            _viewModel.WhenAnyValue(v => v.FactorioDataPathStatusText)
                      .Subscribe(text =>
                      {
                          FactorioDataPathStatus.Text = text ?? string.Empty;
                          if (text != null && text.StartsWith('✓')) FactorioDataPathStatus.Foreground = Avalonia.Media.Brushes.LightGreen;
                      });
        }

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
    }
}