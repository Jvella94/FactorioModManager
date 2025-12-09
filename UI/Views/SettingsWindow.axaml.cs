using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using FactorioModManager.Services;
using FactorioModManager.ViewModels.Dialogs;
using FactorioModManager.Views.Base;
using System;

namespace FactorioModManager.Views
{
    public partial class SettingsWindow : DialogWindowBase<(bool Success, string? Data, bool IsUrl)>
    {
        private readonly SettingsWindowViewModel _viewModel;

        public SettingsWindow() : this(ServiceContainer.Instance.Resolve<ISettingsService>())
        {
        }

        public SettingsWindow(ISettingsService settingsService)
        {
            InitializeComponent();
            _viewModel = new SettingsWindowViewModel(settingsService);
            DataContext = _viewModel;

            _viewModel.SaveCommand.Subscribe(_ => Close(true));
            _viewModel.CancelCommand.Subscribe(_ => Close(false));
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