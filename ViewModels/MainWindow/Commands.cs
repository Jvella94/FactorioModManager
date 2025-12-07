using FactorioModManager.Services;
using ReactiveUI;
using System;
using System.Reactive;
using System.Threading.Tasks;

namespace FactorioModManager.ViewModels.MainWindow
{
    public partial class MainWindowVM
    {
        public ReactiveCommand<Unit, Unit> RefreshModsCommand { get; private set; } = null!;
        public ReactiveCommand<Unit, Unit> InstallModCommand { get; private set; } = null!;
        public ReactiveCommand<Unit, Unit> OpenModFolderCommand { get; private set; } = null!;
        public ReactiveCommand<ModViewModel, Unit> ToggleModCommand { get; private set; } = null!;
        public ReactiveCommand<ModViewModel, Unit> RemoveModCommand { get; private set; } = null!;
        public ReactiveCommand<Unit, Unit> CreateGroupCommand { get; private set; } = null!;
        public ReactiveCommand<ModGroupViewModel, Unit> ToggleGroupCommand { get; private set; } = null!;
        public ReactiveCommand<Unit, Unit> AddToGroupCommand { get; private set; } = null!;
        public ReactiveCommand<Unit, Unit> AddMultipleToGroupCommand { get; private set; } = null!;
        public ReactiveCommand<Unit, Unit> RemoveFromGroupCommand { get; private set; } = null!;
        public ReactiveCommand<Unit, Unit> RemoveMultipleFromGroupCommand { get; private set; } = null!;
        public ReactiveCommand<ModGroupViewModel, Unit> DeleteGroupCommand { get; private set; } = null!;
        public ReactiveCommand<ModGroupViewModel, Unit> RenameGroupCommand { get; private set; } = null!;
        public ReactiveCommand<ModGroupViewModel, Unit> ConfirmRenameGroupCommand { get; private set; } = null!;
        public ReactiveCommand<Unit, Unit> OpenModPortalCommand { get; private set; } = null!;
        public ReactiveCommand<Unit, Unit> OpenSourceUrlCommand { get; private set; } = null!;
        public ReactiveCommand<Unit, Unit> OpenChangelogCommand { get; private set; } = null!;
        public ReactiveCommand<Unit, Unit> OpenVersionHistoryCommand { get; private set; } = null!;
        public ReactiveCommand<string, Unit> NavigateToDependencyCommand { get; private set; } = null!;
        public ReactiveCommand<Unit, Unit> NavigateBackCommand { get; private set; } = null!;
        public ReactiveCommand<Unit, Unit> NavigateForwardCommand { get; private set; } = null!;
        public ReactiveCommand<Unit, Unit> OpenSettingsCommand { get; private set; } = null!;
        public ReactiveCommand<Unit, Unit> CheckUpdatesCustomCommand { get; private set; } = null!;
        public ReactiveCommand<ModViewModel?, Unit> DownloadUpdateCommand { get; private set; } = null!;
        public ReactiveCommand<ModViewModel?, Unit> DeleteOldVersionCommand { get; private set; } = null!;

        private void InitializeCommands()
        {
            RefreshModsCommand = ReactiveCommand.CreateFromTask(RefreshModsAsync);
            InstallModCommand = ReactiveCommand.Create(InstallMod);
            OpenModFolderCommand = ReactiveCommand.Create(OpenModFolder);
            ToggleModCommand = ReactiveCommand.Create<ModViewModel>(ToggleMod);
            RemoveModCommand = ReactiveCommand.Create<ModViewModel>(RemoveMod);
            CreateGroupCommand = ReactiveCommand.Create(CreateGroup);
            ToggleGroupCommand = ReactiveCommand.Create<ModGroupViewModel>(ToggleGroup);
            AddToGroupCommand = ReactiveCommand.Create(AddToGroup);
            AddMultipleToGroupCommand = ReactiveCommand.Create(AddMultipleToGroup);
            RemoveFromGroupCommand = ReactiveCommand.Create(RemoveFromGroup);
            RemoveMultipleFromGroupCommand = ReactiveCommand.Create(RemoveMultipleFromGroup);
            DeleteGroupCommand = ReactiveCommand.Create<ModGroupViewModel>(DeleteGroup);
            RenameGroupCommand = ReactiveCommand.Create<ModGroupViewModel>(StartRenameGroup);
            ConfirmRenameGroupCommand = ReactiveCommand.Create<ModGroupViewModel>(ConfirmRenameGroup);
            OpenModPortalCommand = ReactiveCommand.Create(OpenModPortal);
            OpenSourceUrlCommand = ReactiveCommand.Create(OpenSourceUrl);
            OpenChangelogCommand = ReactiveCommand.CreateFromTask(OpenChangelogAsync);
            OpenVersionHistoryCommand = ReactiveCommand.CreateFromTask(OpenVersionHistoryAsync);

            var canNavigateBack = this.WhenAnyValue(x => x.CanNavigateBack);
            var canNavigateForward = this.WhenAnyValue(x => x.CanNavigateForward);

            NavigateBackCommand = ReactiveCommand.Create(NavigateBack, canNavigateBack);
            NavigateForwardCommand = ReactiveCommand.Create(NavigateForward, canNavigateForward);
            NavigateToDependencyCommand = ReactiveCommand.Create<string>(NavigateToDependency);

            OpenSettingsCommand = ReactiveCommand.CreateFromTask(OpenSettingsAsync);
            CheckUpdatesCustomCommand = ReactiveCommand.CreateFromTask(CheckUpdatesCustomAsync);
            DownloadUpdateCommand = ReactiveCommand.CreateFromTask<ModViewModel?>(DownloadUpdateAsync);
            DeleteOldVersionCommand = ReactiveCommand.Create<ModViewModel?>(DeleteOldVersion);
        }

        private void InstallMod()
        {
            StatusText = "Install mod feature not yet implemented";
        }

        private void OpenModFolder()
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    var path = ModPathHelper.GetModsDirectory();
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = path,
                        UseShellExecute = true
                    });
                    StatusText = $"Opened: {path}";
                }
                catch (Exception ex)
                {
                    StatusText = $"Error opening folder: {ex.Message}";
                }
            });
        }

        private void OpenModPortal()
        {
            if (SelectedMod != null)
            {
                try
                {
                    var url = $"https://mods.factorio.com/mod/{SelectedMod.Name}";
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
                    StatusText = $"Opened mod portal for {SelectedMod.Title}";
                }
                catch (Exception ex)
                {
                    StatusText = $"Error opening browser: {ex.Message}";
                }
            }
        }

        private void OpenSourceUrl()
        {
            if (SelectedMod != null && !string.IsNullOrEmpty(SelectedMod.SourceUrl))
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = SelectedMod.SourceUrl,
                        UseShellExecute = true
                    });
                    StatusText = $"Opened source URL for {SelectedMod.Title}";
                }
                catch (Exception ex)
                {
                    StatusText = $"Error opening browser: {ex.Message}";
                }
            }
        }

        private async Task CheckUpdatesCustomAsync()
        {
            await Task.Run(() =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
                {
                    var dialog = new Views.UpdateCheckDialog();
                    var owner = Avalonia.Application.Current?.ApplicationLifetime
                        is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                        ? desktop.MainWindow : null;

                    if (owner != null)
                    {
                        var result = await dialog.ShowDialog<bool>(owner);
                        if (result)
                        {
                            await CheckForUpdatesAsync(_settingsService.GetApiKey(), dialog.Hours);
                        }
                    }
                });
            });
        }
    }
}
