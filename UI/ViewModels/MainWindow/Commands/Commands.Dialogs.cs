using ReactiveUI;
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

        /// <summary>
        /// Opens the settings dialog
        /// </summary>
        private async Task OpenSettingsAsync()
        {
            var result = await _uiService.ShowSettingsDialogAsync();

            if (result)
            {
                SetStatus("Settings saved");
                await RefreshModsAsync();
            }
        }
    }
}