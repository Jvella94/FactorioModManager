using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using FactorioModManager.Infrastructure; // Added
using FactorioModManager.Services;
using FactorioModManager.ViewModels.Dialogs;
using FactorioModManager.Views.Base;
using System;
using System.Threading.Tasks;

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
        }

        private async void BrowseModsPath(object? sender, RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
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

        private async void SaveSettings(object? sender, RoutedEventArgs e)
        {
            if (_viewModel.Validate())
            {
                _viewModel.SaveCommand.Execute().Subscribe();
                Close(true);
            }
            else
            {
                await ShowValidationError("Please select a valid mods directory.");
            }
        }

        private void CancelSettings(object? sender, RoutedEventArgs e)
        {
            Close(false);
        }

#pragma warning disable CA1859 // Use concrete types when possible for improved performance
        private Task ShowValidationError(string message)
#pragma warning restore CA1859 // Use concrete types when possible for improved performance
        {
            var dialog = new Dialogs.MessageBoxDialog("Validation Error", message);
            return dialog.ShowDialog(this);
        }
    }
}
