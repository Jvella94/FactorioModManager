using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using FactorioModManager.Services;

namespace FactorioModManager.ViewModels.MainWindow
{
    public partial class MainWindowVM
    {
        private async Task DownloadUpdateAsync(ModViewModel? mod)
        {
            if (mod == null || !mod.HasUpdate || string.IsNullOrEmpty(mod.LatestVersion))
            {
                return;
            }

            await Task.Run(async () =>
            {
                try
                {
                    LogService.Instance.Log($"Starting update for {mod.Title} from {mod.Version} to {mod.LatestVersion}");

                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        StatusText = $"Downloading update for {mod.Title}...";
                    });

                    var apiKey = _settingsService.GetApiKey();
                    var modDetails = await _apiService.GetModDetailsAsync(mod.Name, apiKey);

                    if (modDetails?.Releases == null)
                    {
                        LogService.Instance.Log($"Failed to fetch release details for {mod.Name}");
                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            StatusText = $"Failed to fetch update details for {mod.Title}";
                        });
                        return;
                    }

                    var latestRelease = modDetails.Releases
                        .OrderByDescending(r => r.ReleasedAt)
                        .FirstOrDefault();

                    if (latestRelease == null || string.IsNullOrEmpty(latestRelease.DownloadUrl))
                    {
                        LogService.Instance.Log($"No download URL found for {mod.Name}");
                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            StatusText = $"No download URL available for {mod.Title}";
                        });
                        return;
                    }

                    // Download the mod
                    var downloadUrl = $"https://mods.factorio.com{latestRelease.DownloadUrl}";
                    var modsDirectory = ModPathHelper.GetModsDirectory();
                    var archiveDirectory = Path.Combine(modsDirectory, "_archived");

                    if (!Directory.Exists(archiveDirectory))
                    {
                        Directory.CreateDirectory(archiveDirectory);
                    }

                    var newFileName = $"{mod.Name}_{latestRelease.Version}.zip";
                    var newFilePath = Path.Combine(modsDirectory, newFileName);

                    // Download file
                    using (var httpClient = new HttpClient())
                    {
                        if (!string.IsNullOrEmpty(apiKey))
                        {
                            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
                        }

                        LogService.Instance.Log($"Downloading from {downloadUrl}");
                        var response = await httpClient.GetAsync(downloadUrl);

                        if (!response.IsSuccessStatusCode)
                        {
                            LogService.Instance.Log($"Download failed: {response.StatusCode}");
                            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                            {
                                StatusText = $"Download failed for {mod.Title}";
                            });
                            return;
                        }

                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            StatusText = $"Saving update for {mod.Title}...";
                        });

                        var content = await response.Content.ReadAsByteArrayAsync();
                        await File.WriteAllBytesAsync(newFilePath, content);
                    }

                    LogService.Instance.Log($"Downloaded to {newFilePath}");

                    // Archive old version
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        StatusText = $"Archiving old version of {mod.Title}...";
                    });

                    var oldFiles = Directory.GetFiles(modsDirectory, $"{mod.Name}_*.zip")
                        .Where(f => !f.Equals(newFilePath, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    var oldFolders = Directory.GetDirectories(modsDirectory, $"{mod.Name}_*").ToList();

                    foreach (var oldFile in oldFiles)
                    {
                        var archivePath = Path.Combine(archiveDirectory, Path.GetFileName(oldFile));
                        File.Move(oldFile, archivePath, overwrite: true);
                        LogService.Instance.Log($"Archived {Path.GetFileName(oldFile)}");
                    }

                    foreach (var oldFolder in oldFolders)
                    {
                        var archivePath = Path.Combine(archiveDirectory, Path.GetFileName(oldFolder));
                        if (Directory.Exists(archivePath))
                        {
                            Directory.Delete(archivePath, recursive: true);
                        }
                        Directory.Move(oldFolder, archivePath);
                        LogService.Instance.Log($"Archived folder {Path.GetFileName(oldFolder)}");
                    }

                    LogService.Instance.Log($"Successfully updated {mod.Title} to version {latestRelease.Version}");

                    // ADDED: Clear the update flag since we just updated
                    _metadataService.ClearUpdate(mod.Name);
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        StatusText = $"Update complete for {mod.Title}. Refreshing...";
                    });

                    // Refresh mods list
                    await Task.Delay(1000);
                    await RefreshModsAsync();

                }
                catch (Exception ex)
                {
                    LogService.Instance.Log($"Error updating {mod?.Title}: {ex.Message}");
                    LogService.LogDebug($"Update error details: {ex}");

                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        StatusText = $"Error updating {mod?.Title}: {ex.Message}";
                    });
                }
            });
        }
    }
}
