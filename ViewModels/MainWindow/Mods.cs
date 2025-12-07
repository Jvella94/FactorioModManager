using Avalonia.Media.Imaging;
using FactorioModManager.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;

namespace FactorioModManager.ViewModels.MainWindow
{

    public partial class MainWindowVM
    {
        private bool _isRefreshing = false;

        public async Task RefreshModsAsync()
        {
            if (_isRefreshing)
            {
                LogService.LogDebug("RefreshModsAsync already running, skipping duplicate call");
                return;
            }

            _isRefreshing = true;

            await Task.Run(async () =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    StatusText = "Refreshing mods...";
                });

                try
                {
                    var modsDirectory = ModPathHelper.GetModsDirectory();
                    LogService.LogDebug($"Looking for mods in: {modsDirectory}");
                    LogService.LogDebug($"Directory exists: {Directory.Exists(modsDirectory)}");

                    var loadedMods = _modService.LoadAllMods();
                    LogService.LogDebug($"Loaded {loadedMods.Count} mods from disk");

                    var loadedGroups = _groupService.LoadGroups();
                    var apiKey = _settingsService.GetApiKey();

                    var modNames = loadedMods.Select(m => m.Info.Name).ToList();
                    _metadataService.EnsureModsExist(modNames);

                    // FIXED: Use InvokeAsync and await to ensure Mods collection is populated before continuing
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        Mods.Clear();
                        Authors.Clear();
                        _authorModCounts.Clear();

                        var authorCounts = new Dictionary<string, int>();
                        var allDependencies = new HashSet<string>();

                        LogService.LogDebug($"Processing {loadedMods.Count} mods...");

                        foreach (var (info, isEnabled, lastUpdated, thumbnailPath) in loadedMods)
                        {
                            var modVm = new ModViewModel
                            {
                                Name = info.Name,
                                Title = info.Title ?? info.Name,
                                Version = info.Version,
                                Author = info.Author,
                                Description = info.Description ?? "",
                                IsEnabled = isEnabled,
                                Dependencies = info.Dependencies,
                                LastUpdated = lastUpdated,
                                ThumbnailPath = thumbnailPath,
                                Category = _metadataService.GetCategory(info.Name),
                                SourceUrl = _metadataService.GetSourceUrl(info.Name),
                                HasUpdate = _metadataService.GetHasUpdate(info.Name), // ADDED: Load persisted update status
                                LatestVersion = _metadataService.GetLatestVersion(info.Name) // ADDED: Load persisted version
                            };

                            foreach (var dep in info.Dependencies)
                            {
                                var depName = dep.Split(DependencySeparators, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                                if (!string.IsNullOrEmpty(depName))
                                {
                                    allDependencies.Add(depName);
                                }
                            }

                            var group = loadedGroups.FirstOrDefault(g => g.ModNames.Contains(modVm.Title));
                            if (group != null)
                            {
                                modVm.GroupName = group.Name;
                            }

                            Mods.Add(modVm);

                            if (!string.IsNullOrEmpty(info.Author))
                            {
                                if (!authorCounts.TryGetValue(info.Author, out var count))
                                {
                                    count = 0;
                                }
                                authorCounts[info.Author] = count + 1;
                            }
                        }

                        LogService.LogDebug($"Added {Mods.Count} mods to collection");

                        foreach (var mod in Mods)
                        {
                            if (mod.Category?.Equals("internal", StringComparison.OrdinalIgnoreCase) == true)
                            {
                                mod.IsUnusedInternal = !allDependencies.Contains(mod.Name);
                            }
                        }

                        var sortedMods = Mods.OrderByDescending(m => m.LastUpdated ?? DateTime.MinValue).ToList();
                        Mods.Clear();
                        foreach (var mod in sortedMods)
                        {
                            Mods.Add(mod);
                        }

                        _authorModCounts = authorCounts;
                        var sortedAuthors = authorCounts
                            .OrderByDescending(kvp => kvp.Value)
                            .Select(kvp => $"{kvp.Key} ({kvp.Value})")
                            .ToList();

                        foreach (var author in sortedAuthors)
                        {
                            Authors.Add(author);
                        }

                        Groups.Clear();
                        foreach (var group in loadedGroups)
                        {
                            var groupVm = new ModGroupViewModel
                            {
                                Name = group.Name,
                                Description = group.Description,
                                ModNames = group.ModNames
                            };
                            UpdateGroupStatus(groupVm);
                            Groups.Add(groupVm);
                        }

                        SelectedAuthorFilter = null;
                        AuthorSearchText = string.Empty;
                        UpdateFilteredAuthors();
                        UpdateFilteredMods();

                        StatusText = $"Loaded {Mods.Count} mods and {Groups.Count} groups";
                        LogService.LogDebug($"Status: {StatusText}");
                    });

                    // NOW the Mods collection is guaranteed to be populated
                    LogService.LogDebug($"Mods.Count after UI population: {Mods.Count}");

                    // Fetch metadata for mods that need it
                    await FetchMissingMetadataAsync(apiKey);

                    // Check for updates once per session
                    var lastCheck = _settingsService.GetLastUpdateCheck();
                    if (!lastCheck.HasValue || (DateTime.UtcNow - lastCheck.Value).TotalHours >= 1)
                    {
                        await CheckForUpdatesAsync(apiKey);
                        _settingsService.SetLastUpdateCheck(DateTime.UtcNow);
                    }
                }
                catch (Exception ex)
                {
                    LogService.LogDebug($"ERROR in RefreshModsAsync: {ex}");
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        StatusText = $"Error: {ex.Message}";
                    });
                }
                finally
                {
                    _isRefreshing = false;
                    LogService.LogDebug("=== RefreshModsAsync completed ===");
                }
            });
        }

        private async Task FetchMissingMetadataAsync(string? apiKey)
        {
            var modsSnapshot = Mods.ToList();

            var modsNeedingMetadata = modsSnapshot.Where(m =>
                _metadataService.NeedsCategoryCheck(m.Name) ||
                _metadataService.NeedsSourceUrlCheck(m.Name)).ToList();

            if (modsNeedingMetadata.Count == 0) return;

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                StatusText = $"Fetching metadata for {modsNeedingMetadata.Count} mods...";
            });

            var currentIndex = 0;
            foreach (var mod in modsNeedingMetadata)
            {
                currentIndex++;

                // ADDED: Update progress
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    StatusText = $"Fetching metadata ({currentIndex}/{modsNeedingMetadata.Count}): {mod.Title}";
                });

                try
                {
                    var details = await _apiService.GetModDetailsAsync(mod.Name, apiKey);

                    if (details != null)
                    {
                        if (_metadataService.NeedsCategoryCheck(mod.Name))
                        {
                            _metadataService.UpdateCategory(mod.Name, details.Category);
                            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                            {
                                mod.Category = details.Category;
                            });
                        }

                        if (_metadataService.NeedsSourceUrlCheck(mod.Name))
                        {
                            _metadataService.UpdateSourceUrl(mod.Name, details.SourceUrl, wasChecked: true);
                            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                            {
                                mod.SourceUrl = details.SourceUrl;
                            });
                        }
                    }
                    else
                    {
                        _metadataService.MarkAsChecked(mod.Name);
                    }

                    await Task.Delay(100);
                }
                catch (Exception ex)
                {
                    LogService.LogDebug($"Error fetching metadata for {mod.Name}: {ex.Message}");
                    _metadataService.MarkAsChecked(mod.Name);
                }
            }

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                StatusText = $"Metadata update complete - {modsNeedingMetadata.Count} mods processed";
            });
        }


        private async Task CheckForUpdatesAsync(string? apiKey, int hoursAgo = 1)
        {
            LogService.Instance.Log($"Checking for updates from the last {hoursAgo} hour(s)...");

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                StatusText = "Fetching recently updated mods...";
            });

            try
            {
                var recentlyUpdatedModNames = await _apiService.GetRecentlyUpdatedModsAsync(hoursAgo, apiKey);
                LogService.Instance.Log($"Found {recentlyUpdatedModNames.Count} recently updated mods on portal");

                if (recentlyUpdatedModNames.Count == 0)
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        StatusText = "No recently updated mods found";
                    });
                    return;
                }

                var modsSnapshot = Mods.ToList();
                var installedRecentlyUpdated = modsSnapshot
                    .Where(m => recentlyUpdatedModNames.Contains(m.Name))
                    .ToList();

                LogService.Instance.Log($"Checking {installedRecentlyUpdated.Count} of your installed mods for updates");

                var updateCount = 0;
                var currentIndex = 0;

                foreach (var mod in installedRecentlyUpdated)
                {
                    currentIndex++;

                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        StatusText = $"Checking updates ({currentIndex}/{installedRecentlyUpdated.Count}): {mod.Title}";
                    });

                    try
                    {
                        var details = await _apiService.GetModDetailsAsync(mod.Name, apiKey);
                        if (details?.Releases != null && details.Releases.Count > 0)
                        {
                            var latestRelease = details.Releases
                                .OrderByDescending(r => r.ReleasedAt)
                                .FirstOrDefault();
                            if (latestRelease != null)
                            {
                                var latestVersion = latestRelease.Version;

                                if (CompareVersions(latestVersion, mod.Version) > 0)
                                {
                                    _metadataService.UpdateLatestVersion(mod.Name, latestVersion, hasUpdate: true);

                                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                                    {
                                        mod.HasUpdate = true;
                                        mod.LatestVersion = latestVersion;
                                    });
                                    updateCount++;
                                    LogService.Instance.Log($"Update available for {mod.Title}: {mod.Version} → {latestVersion}");
                                }
                                else
                                {
                                    // ADDED: Clear update flag if mod is up to date
                                    _metadataService.UpdateLatestVersion(mod.Name, latestVersion, hasUpdate: false);
                                }
                            }
                        }

                        await Task.Delay(100);
                    }
                    catch (Exception ex)
                    {
                        LogService.LogDebug($"Error checking updates for {mod.Name}: {ex.Message}");
                    }
                }

                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (updateCount > 0)
                    {
                        StatusText = $"Found {updateCount} mod update(s) available";
                        LogService.Instance.Log($"Update check complete: {updateCount} updates found");

                        // Sort mods to show updates at top
                        SortModsWithUpdatesFirst();
                    }
                    else
                    {
                        StatusText = "All mods are up to date";
                        LogService.Instance.Log("All mods are up to date");
                    }
                });
            }
            catch (Exception ex)
            {
                LogService.Instance.Log($"Error during update check: {ex.Message}");
                LogService.LogDebug($"Error in CheckForUpdatesAsync: {ex}");
            }
        }

        private static int CompareVersions(string v1, string v2)
        {
            var parts1 = v1.Split('.').Select(int.Parse).ToArray();
            var parts2 = v2.Split('.').Select(int.Parse).ToArray();

            for (int i = 0; i < Math.Max(parts1.Length, parts2.Length); i++)
            {
                var p1 = i < parts1.Length ? parts1[i] : 0;
                var p2 = i < parts2.Length ? parts2[i] : 0;

                if (p1 > p2) return 1;
                if (p1 < p2) return -1;
            }

            return 0;
        }

        private void SortModsWithUpdatesFirst()
        {
            var sorted = Mods
                .OrderByDescending(m => m.HasUpdate)
                .ThenByDescending(m => m.LastUpdated ?? DateTime.MinValue)
                .ToList();

            Mods.Clear();
            foreach (var mod in sorted)
            {
                Mods.Add(mod);
            }

            UpdateFilteredMods();
        }



        private static async Task LoadThumbnailAsync(ModViewModel mod)
        {
            if (string.IsNullOrEmpty(mod.ThumbnailPath))
            {
                mod.Thumbnail = null;
                return;
            }

            await Task.Run(() =>
            {
                try
                {
                    Bitmap? thumbnail = null;

                    if (mod.ThumbnailPath.Contains('|'))
                    {
                        var parts = mod.ThumbnailPath.Split('|');
                        using var archive = ZipFile.OpenRead(parts[0]);
                        var entry = archive.GetEntry(parts[1]);
                        if (entry != null)
                        {
                            using var stream = entry.Open();
                            using var memStream = new MemoryStream();
                            stream.CopyTo(memStream);
                            memStream.Position = 0;
                            thumbnail = new Bitmap(memStream);
                        }
                    }
                    else if (File.Exists(mod.ThumbnailPath))
                    {
                        thumbnail = new Bitmap(mod.ThumbnailPath);
                    }

                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        mod.Thumbnail = thumbnail;
                    });
                }
                catch (Exception ex)
                {
                    LogService.LogDebug($"Error loading thumbnail: {ex.Message}");
                }
            });
        }

        private void ToggleMod(ModViewModel mod)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (mod != null)
                {
                    mod.IsEnabled = !mod.IsEnabled;
                    _modService.ToggleMod(mod.Name, mod.IsEnabled);

                    foreach (var group in Groups)
                    {
                        UpdateGroupStatus(group);
                    }

                    StatusText = $"{mod.Title} {(mod.IsEnabled ? "enabled" : "disabled")}";
                }
            });
        }

        private void RemoveMod(ModViewModel mod)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (mod != null)
                {
                    Mods.Remove(mod);
                    UpdateFilteredMods();
                    StatusText = $"Removed {mod.Title}";
                }
            });
        }

        private async Task OpenChangelogAsync()
        {
            if (SelectedMod == null) return;

            await Task.Run(async () =>
            {
                try
                {
                    var apiKey = _settingsService.GetApiKey();
                    var details = await _apiService.GetModDetailsAsync(SelectedMod.Name, apiKey);

                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        if (!string.IsNullOrEmpty(details?.Changelog))
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
                catch (Exception ex)
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
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
                try
                {
                    var apiKey = _settingsService.GetApiKey();
                    var details = await _apiService.GetModDetailsAsync(SelectedMod.Name, apiKey);

                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        if (details?.Releases != null && details.Releases.Count > 0)
                        {
                            var window = new Views.VersionHistoryWindow(SelectedMod.Title, details.Releases);
                            window.Show();
                            StatusText = "Version history opened";
                        }
                        else
                        {
                            StatusText = "No version history available";
                        }
                    });
                }
                catch (Exception ex)
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        StatusText = $"Error fetching version history: {ex.Message}";
                    });
                }
            });
        }

        private async Task OpenSettingsAsync()
        {
            await Task.Run(() =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
                {
                    var settingsWindow = new Views.SettingsWindow(_settingsService);

                    var owner = Avalonia.Application.Current?.ApplicationLifetime
                        is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                        ? desktop.MainWindow : null;

                    if (owner != null)
                    {
                        var result = await settingsWindow.ShowDialog<bool>(owner);
                        if (result)
                        {
                            StatusText = "Settings saved";
                        }
                    }
                    else
                    {
                        settingsWindow.Show();
                        StatusText = "Settings window opened";
                    }
                });
            });
        }
    }
}
