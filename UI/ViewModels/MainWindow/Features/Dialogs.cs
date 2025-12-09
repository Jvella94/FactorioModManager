using FactorioModManager.Models;
using System.Threading.Tasks;

namespace FactorioModManager.ViewModels.MainWindow
{
    public partial class MainWindowViewModel
    {
        /// <summary>
        /// Opens the changelog for the selected mod
        /// </summary>
        private async Task OpenChangelogAsync()
        {
            if (SelectedMod == null)
            {
                SetStatus("No mod selected", LogLevel.Warning);
                return;
            }

            await Task.Run(async () =>
            {
                await _uiService.InvokeAsync(() =>
                {
                    SetStatus("Fetching changelog...");
                });

                try
                {
                    var details = await _apiService.GetModDetailsFullAsync(SelectedMod.Name);

                    await _uiService.InvokeAsync(async () =>
                    {
                        if (details?.Changelog != null)
                        {
                            await _uiService.ShowChangelogAsync(SelectedMod.Title, details.Changelog);
                            SetStatus("Changelog opened");
                        }
                        else
                        {
                            SetStatus("No changelog available", LogLevel.Warning);
                        }
                    });
                }
                catch (System.Exception ex)
                {
                    await _uiService.InvokeAsync(() =>
                    {
                        SetStatus($"Error fetching changelog: {ex.Message}", LogLevel.Error);
                    });
                    _logService.LogError($"Error fetching changelog: {ex.Message}", ex);
                }
            });
        }

        /// <summary>
        /// Opens the version history for the selected mod
        /// </summary>
        private async Task OpenVersionHistoryAsync()
        {
            if (SelectedMod == null)
            {
                SetStatus("No mod selected", LogLevel.Warning);
                return;
            }

            await Task.Run(async () =>
            {
                await _uiService.InvokeAsync(() =>
                {
                    SetStatus("Fetching version history...");
                });

                try
                {
                    var details = await _apiService.GetModDetailsAsync(SelectedMod.Name);

                    await _uiService.InvokeAsync(async () =>
                    {
                        if (details?.Releases != null && details.Releases.Count > 0)
                        {
                            await _uiService.ShowVersionHistoryAsync(
                                SelectedMod.Title,
                                SelectedMod.Name,
                                details.Releases);
                            SetStatus("Version history opened");
                        }
                        else
                        {
                            SetStatus("No version history available", LogLevel.Warning);
                        }
                    });
                }
                catch (System.Exception ex)
                {
                    await _uiService.InvokeAsync(() =>
                    {
                        SetStatus($"Error fetching version history: {ex.Message}", LogLevel.Error);
                    });
                    _logService.LogError($"Error fetching version history: {ex.Message}", ex);
                }
            });
        }
    }
}