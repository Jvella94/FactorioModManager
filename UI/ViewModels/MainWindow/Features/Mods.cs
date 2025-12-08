using Avalonia.Media.Imaging;
using FactorioModManager.Services.Infrastructure;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using static FactorioModManager.Constants;

namespace FactorioModManager.ViewModels.MainWindow
{
    public partial class MainWindowViewModel
    {
        private bool _isRefreshing = false;

        public async Task RefreshModsAsync()
        {
            if (_isRefreshing)
            {
                _logService.LogDebug("RefreshModsAsync already running, skipping duplicate call");
                return;
            }

            _isRefreshing = true;

            await Task.Run(async () =>
            {
                _uiService.Post(() =>
                {
                    StatusText = "Refreshing mods...";
                });

                try
                {
                    var loadedMods = _modService.LoadAllMods();
                    var loadedGroups = _groupService.LoadGroups();
                    var apiKey = _settingsService.GetApiKey();

                    // Group by name and keep only the latest version
                    var latestMods = loadedMods
                        .GroupBy(m => m.Info.Name)
                        .Select(g => g.OrderByDescending(m => m.Info.Version).First())
                        .ToList();

                    // Use InvokeAsync to WAIT for the UI thread to complete loading mods
                    await _uiService.InvokeAsync(() =>
                    {
                        Mods.Clear();
                        Authors.Clear();
                        _authorModCounts.Clear();

                        var authorCounts = new Dictionary<string, int>();
                        var allDependencies = new HashSet<string>();

                        foreach (var (info, isEnabled, lastUpdated, thumbnailPath, filePath) in latestMods)
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
                                HasUpdate = _metadataService.GetHasUpdate(info.Name),
                                LatestVersion = _metadataService.GetLatestVersion(info.Name),
                                FilePath = filePath
                            };

                            // Track all dependencies
                            foreach (var dep in info.Dependencies)
                            {
                                var depName = dep.Split(Separators.Dependency, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                                if (!string.IsNullOrEmpty(depName))
                                {
                                    allDependencies.Add(depName);
                                }
                            }

                            // Load available versions for this mod
                            LoadModVersions(modVm);

                            // Determine group
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

                        // Mark unused internal mods
                        foreach (var mod in Mods)
                        {
                            if (mod.Category?.Equals("internal", StringComparison.OrdinalIgnoreCase) == true)
                            {
                                mod.IsUnusedInternal = !allDependencies.Contains(mod.Name);
                            }
                        }

                        // Sort by LastUpdated descending
                        var sortedMods = Mods.OrderByDescending(m => m.LastUpdated ?? DateTime.MinValue).ToList();
                        Mods.Clear();
                        foreach (var mod in sortedMods)
                        {
                            Mods.Add(mod);
                        }

                        // Build author list
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
                                ModNames = group.ModNames
                            };
                            UpdateGroupStatus(groupVm);
                            Groups.Add(groupVm);
                        }

                        SelectedAuthorFilter = null;
                        AuthorSearchText = string.Empty;
                        UpdateFilteredAuthors();
                        UpdateFilteredMods();

                        this.RaisePropertyChanged(nameof(ModCountSummary));
                        this.RaisePropertyChanged(nameof(HasUnusedInternals));
                        this.RaisePropertyChanged(nameof(UnusedInternalCount));

                        StatusText = $"Loaded {Mods.Count} mods and {Groups.Count} groups";
                    });

                    // NOW mods are fully loaded - safe to check for updates
                    await CheckForAlreadyDownloadedUpdatesAsync();

                    // Fetch metadata for mods that need it
                    await FetchMissingMetadataAsync();

                    // Check for updates once per session
                    var lastCheck = _settingsService.GetLastUpdateCheck();
                    if (!lastCheck.HasValue || (DateTime.UtcNow - lastCheck.Value).TotalHours >= 1)
                    {
                        await CheckForUpdatesAsync();
                        _settingsService.SetLastUpdateCheck(DateTime.UtcNow);
                    }
                }
                catch (Exception ex)
                {
                    _logService.LogDebug($"ERROR in RefreshModsAsync: {ex}");
                    _uiService.Post(() =>
                    {
                        StatusText = $"Error: {ex.Message}";
                    });
                }
                finally
                {
                    _isRefreshing = false;
                    _logService.LogDebug("=== RefreshModsAsync completed ===");
                }
            });
        }


        private async Task FetchMissingMetadataAsync()
        {
            // Take a snapshot to avoid collection modification issues
            var modsSnapshot = Mods.ToList();

            var modsNeedingMetadata = modsSnapshot.Where(m =>
                _metadataService.NeedsCategoryCheck(m.Name) ||
                _metadataService.NeedsSourceUrlCheck(m.Name)).ToList();

            if (modsNeedingMetadata.Count == 0) return;

            _uiService.Post(() =>
            {
                StatusText = $"Fetching metadata for {modsNeedingMetadata.Count} mods...";
            });
            var currentIndex = 0;
            foreach (var mod in modsNeedingMetadata)
            {
                currentIndex++;

                // ADDED: Update progress
                _uiService.Post(() =>
                {
                    StatusText = $"Fetching metadata ({currentIndex}/{modsNeedingMetadata.Count}): {mod.Title}";
                });
                try
                {
                    var details = await _apiService.GetModDetailsFullAsync(mod.Name);
                    if (details != null)
                    {
                        if (_metadataService.NeedsCategoryCheck(mod.Name))
                        {
                            _metadataService.UpdateCategory(mod.Name, details.Category);
                            _uiService.Post(() =>
                            {
                                mod.Category = details.Category;
                            });
                        }

                        if (_metadataService.NeedsSourceUrlCheck(mod.Name))
                        {
                            _metadataService.UpdateSourceUrl(mod.Name, details.SourceUrl);
                            _uiService.Post(() =>
                            {
                                mod.SourceUrl = details.SourceUrl;
                            });
                        }
                    }
                    else
                    {
                        _metadataService.MarkAsChecked(mod.Name);
                    }

                    await Task.Delay(100); // Rate limiting
                }
                catch (Exception ex)
                {
                    _logService.LogDebug($"Error fetching metadata for {mod.Name}: {ex.Message}");
                    _metadataService.MarkAsChecked(mod.Name);
                }
            }

            _uiService.Post(() =>
            {
                StatusText = "Metadata update complete";
            });
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

        private async Task LoadThumbnailAsync(ModViewModel mod)
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

                    _uiService.Post(() =>
                    {
                        mod.Thumbnail = thumbnail;
                    });
                }
                catch (Exception ex)
                {
                    _logService.LogDebug($"Error loading thumbnail: {ex.Message}");
                }
            });
        }
    }
}
