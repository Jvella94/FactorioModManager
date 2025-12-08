using System.Threading.Tasks;

namespace FactorioModManager.ViewModels.MainWindow
{
    public partial class MainWindowViewModel
    {
        private async Task OpenChangelogAsync()
        {
            if (SelectedMod == null) return;

            await Task.Run(async () =>
            {
                _uiService.Post(() =>
                {
                    StatusText = "Fetching changelog...";
                });

                try
                {
                    var apiKey = _settingsService.GetApiKey();
                    var details = await _apiService.GetModDetailsFullAsync(SelectedMod.Name);

                    _uiService.Post(() =>
                    {
                        if (details?.Changelog != null)
                        {
                            var window = new Views.ChangelogWindow(SelectedMod.Title, details.Changelog);
                            window.Show();
                            StatusText = "Changelog opened";
                        }
                        else
                        {
                            StatusText = "No changelog available";
                        }
                    });
                }
                catch (System.Exception ex)
                {
                    _uiService.Post(() =>
                    {
                        StatusText = $"Error fetching changelog: {ex.Message}";
                    });
                }
            });
        }

        private async Task OpenVersionHistoryAsync()
        {
            if (SelectedMod == null) return;

            await Task.Run(async () =>
            {
                _uiService.Post(() =>
                {
                    StatusText = "Fetching version history...";
                });

                try
                {
                    var apiKey = _settingsService.GetApiKey();
                    var details = await _apiService.GetModDetailsAsync(SelectedMod.Name);

                    _uiService.Post(() =>
                    {
                        if (details?.Releases != null && details.Releases.Count > 0)
                        {
                            var window = new Views.VersionHistoryWindow(SelectedMod.Title, SelectedMod.Name, details.Releases);
                            window.Show();
                            StatusText = "Version history opened";
                        }
                        else
                        {
                            StatusText = "No version history available";
                        }
                    });
                }
                catch (System.Exception ex)
                {
                    _uiService.Post(() =>
                    {
                        StatusText = $"Error fetching version history: {ex.Message}";
                    });
                }
            });
        }
    }
}