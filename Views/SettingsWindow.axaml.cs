using Avalonia.Controls;
using Avalonia.Interactivity;
using FactorioModManager.Services;

namespace FactorioModManager.Views
{
    public partial class SettingsWindow : Window
    {
        private readonly SettingsService _settingsService = null!;

        public SettingsWindow()
        {
            InitializeComponent();
        }

        public SettingsWindow(SettingsService settingsService) : this()
        {
            _settingsService = settingsService;
            this.FindControl<TextBox>("ApiKeyBox")!.Text = _settingsService.GetApiKey();
        }

        private void SaveSettings(object? sender, RoutedEventArgs e)
        {
            var apiKey = this.FindControl<TextBox>("ApiKeyBox")!.Text;
            _settingsService.SetApiKey(string.IsNullOrWhiteSpace(apiKey) ? null : apiKey);
            Close(true);
        }

        private void CancelSettings(object? sender, RoutedEventArgs e)
        {
            Close(false);
        }
    }
}
