using ReactiveUI;
using System;
using System.Reactive;
using System.Threading.Tasks;

namespace FactorioModManager.ViewModels.MainWindow
{
    public partial class MainWindowViewModel
    {
        public ReactiveCommand<Unit, Unit> OpenSettingsCommand { get; private set; } = null!;

        private void InitializeDialogCommands()
        {
            OpenSettingsCommand = ReactiveCommand.CreateFromTask(OpenSettingsAsync);
        }

        private async Task OpenSettingsAsync()
        {
            await _uiService.InvokeAsync(async () =>
            {
                var dialog = new Views.SettingsWindow(_settingsService);
                var owner = Avalonia.Application.Current?.ApplicationLifetime
                    is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                    ? desktop.MainWindow : null;

                if (owner != null)
                {
                    var result = await dialog.ShowDialog<bool>(owner);
                    if (result)
                    {
                        StatusText = "Settings saved";
                        await RefreshModsAsync();
                    }
                }
            });
        }
    }
}
