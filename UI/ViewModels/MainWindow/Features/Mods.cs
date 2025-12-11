using FactorioModManager.Models;
using System;
using System.Collections.Generic;
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
                await _uiService.InvokeAsync(() =>
                {
                    SetStatus("Refreshing mods...");
                });

                try
                {
                    var loadedMods = _modService.LoadAllMods();
                    var loadedGroups = _groupService.LoadGroups();
                    var latestMods = GetLatestModVersions(loadedMods);

                    await _uiService.InvokeAsync(() =>
                    {
                        UpdateModsCache(latestMods, loadedGroups);
                        UpdateGroupsCollection(loadedGroups);
                        SetStatus($"Loaded {AllModsCount} unique mods and {Groups.Count} groups");
                    });

                    await CheckForAlreadyDownloadedUpdatesAsync();
                    await FetchMissingMetadataAsync();
                    await CheckForUpdatesIfNeededAsync();
                }
                catch (Exception ex)
                {
                    await _uiService.InvokeAsync(() =>
                    {
                        HandleError(ex, "Refresh Mods");
                    });
                }
                finally
                {
                    _isRefreshing = false;
                    _logService.LogDebug("=== RefreshModsAsync completed ===");
                }
            });
        }

        private static List<(ModInfo Info, bool IsEnabled, DateTime? LastUpdated, string? ThumbnailPath, string FilePath)>
            GetLatestModVersions(
                List<(ModInfo Info, bool IsEnabled, DateTime? LastUpdated, string? ThumbnailPath, string FilePath)> loadedMods)
        {
            return [.. loadedMods
                .GroupBy(m => m.Info.Name)
                .Select(g => g.OrderByDescending(m => m.Info.Version).First())];
        }

        /// <summary>
        /// Updates the mods cache with loaded data
        /// </summary>
        private void UpdateModsCache(
            List<(ModInfo Info, bool IsEnabled, DateTime? LastUpdated, string? ThumbnailPath, string FilePath)> latestMods,
            List<ModGroup> loadedGroups)
        {
            var allDependencies = new HashSet<string>();
            var modViewModels = new List<ModViewModel>();

            foreach (var (info, isEnabled, lastUpdated, thumbnailPath, filePath) in latestMods)
            {
                var modVm = CreateModViewModel(info, isEnabled, lastUpdated, thumbnailPath, filePath, loadedGroups);

                // Track dependencies
                foreach (var dep in info.Dependencies)
                {
                    var depName = DependencyHelper.ExtractDependencyName(dep);
                    if (!string.IsNullOrEmpty(depName))
                    {
                        allDependencies.Add(depName);
                    }
                }

                LoadModVersions(modVm);
                modViewModels.Add(modVm);
            }

            // Mark unused internal mods
            foreach (var mod in modViewModels)
            {
                if (mod.Category?.Equals("internal", StringComparison.OrdinalIgnoreCase) == true)
                {
                    mod.IsUnusedInternal = !allDependencies.Contains(mod.Name);
                }
            }

            // ✅ Simple collection update - no disposal during active rendering
            _allMods.Clear();
            foreach (var mod in modViewModels)
            {
                _allMods.Add(mod);
            }

            // Update authors list
            UpdateAuthorsList();

            // Trigger filter
            ApplyModFilter();
        }

        /// <summary>
        /// Updates the authors list based on current mods
        /// </summary>
        private void UpdateAuthorsList()
        {
            var authorCounts = _allMods
                .Where(m => !string.IsNullOrEmpty(m.Author))
                .GroupBy(m => m.Author)
                .Select(g => ($"{g.Key} ({g.Count()})", g.Count()))
                .OrderByDescending(x => x.Item2)
                .Select(x => x.Item1)
                .ToList();

            _authors.Clear();
            foreach (var author in authorCounts)
            {
                _authors.Add(author);
            }

            ApplyAuthorFilter();
        }

        private ModViewModel CreateModViewModel(
            Models.ModInfo info,
            bool isEnabled,
            DateTime? lastUpdated,
            string? thumbnailPath,
            string filePath,
            List<ModGroup> loadedGroups)
        {
            var modVm = new ModViewModel()
            {
                Name = info.Name,
                Title = info.DisplayTitle ?? info.Name,
                Version = info.Version,
                Author = info.Author,
                Description = info.Description ?? string.Empty,
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

            var group = loadedGroups.FirstOrDefault(g => g.ModNames.Contains(modVm.Title));
            if (group != null)
            {
                modVm.GroupName = group.Name;
            }

            return modVm;
        }

        private void UpdateGroupsCollection(List<ModGroup> loadedGroups)
        {
            // Clear groups (don't dispose - they'll be GC'd)
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
        }

        private async Task LoadThumbnailAsync(ModViewModel mod)
        {
            if (string.IsNullOrEmpty(mod.ThumbnailPath))
            {
                // Already returns placeholder from getter
                return;
            }

            try
            {
                // ✅ FIX: Use IThumbnailCache instead of IModService
                var thumbnail = await _thumbnailCache.LoadThumbnailAsync(mod.ThumbnailPath);
                await _uiService.InvokeAsync(() =>
                {
                    mod.Thumbnail = thumbnail ?? LoadPlaceholderThumbnail();
                });
            }
            catch (Exception ex)
            {
                HandleError(ex, $"Error loading thumbnail for {mod.Name}: {ex.Message}");
                // Thumbnail getter will return placeholder automatically
            }
        }

        private async Task CheckForUpdatesIfNeededAsync()
        {
            var lastCheck = _settingsService.GetLastModUpdateCheck();
            if (!lastCheck.HasValue || (DateTime.UtcNow - lastCheck.Value).TotalHours >= 1)
            {
                if (lastCheck.HasValue)
                    _logService.Log($"Checking for updates on Portal since {lastCheck.Value}");

                var hours = lastCheck.HasValue ? (DateTime.UtcNow - lastCheck.Value).Hours : 1;
                _settingsService.SetLastModUpdateCheck(DateTime.UtcNow);
                await CheckForUpdatesAsync(hours);
            }
        }

        private async Task ViewDependentsAsync(ModViewModel? mod)
        {
            if (mod == null)
            {
                SetStatus("No mod selected", LogLevel.Warning);
                return;
            }

            var targetName = mod.Name;
            if (string.IsNullOrWhiteSpace(targetName))
                return;

            // ✅ Query from _allMods collection
            var dependents = GetDependents(targetName);

            if (!dependents.Any())
            {
                await _uiService.ShowMessageAsync(
                    "Mods depending on this",
                    $"No other loaded mods declare a dependency on '{mod.Title}'.");
                return;
            }

            var list = string.Join(Environment.NewLine,
                dependents.Select(m => $"- {m.Title} ({m.Name})"));

            await _uiService.ShowMessageAsync(
                "Mods depending on this",
                $"The following mods depend on '{mod.Title}':{Environment.NewLine}{Environment.NewLine}{list}");
        }

        // Find installed mods by name (case-insensitive)
        private ModViewModel? FindMod(string name) =>
            _allMods.FirstOrDefault(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        // All mods that depend on a given mod (mandatory dependency)
        private IEnumerable<ModViewModel> GetDependents(string modName)
        {
            return _allMods.Where(mod =>
                mod.Dependencies.Any(dependency =>
                {
                    var dependencyName = DependencyHelper.ExtractDependencyName(dependency);
                    return dependencyName.Equals(modName, StringComparison.OrdinalIgnoreCase) &&
                           !DependencyHelper.IsOptionalDependency(dependency);
                }));
        }

        // Mods that conflict with the given mod (incompatible dependencies)
        private List<ModViewModel> GetIncompatibleMods(ModViewModel mod) =>
            [.. _allMods.Where(m =>
                m.IsEnabled &&
                m.Dependencies != null &&
                DependencyHelper.GetIncompatibleDependencies(m.Dependencies)
                    .Any(d => d.Equals(mod.Name, StringComparison.OrdinalIgnoreCase)))];

        /// <summary>
        /// Classifies dependencies into installed (enabled/disabled) and missing,
        /// filtering out game dependencies that can't be installed from the portal
        /// </summary>
        private (List<ModViewModel> installedEnabled,
         List<ModViewModel> installedDisabled,
         List<string> missing) ClassifyDependencies(IReadOnlyList<string> dependencyNames)
        {
            var installedEnabled = new List<ModViewModel>();
            var installedDisabled = new List<ModViewModel>();
            var missing = new List<string>();

            foreach (var depName in dependencyNames)
            {
                // ✅ FIX: Skip game dependencies (base, space-age, quality, elevated-rails)
                if (DependencyHelper.IsGameDependency(depName))
                {
                    continue;
                }

                var dep = FindModByName(depName);
                if (dep == null)
                {
                    missing.Add(depName);
                }
                else if (dep.IsEnabled)
                {
                    installedEnabled.Add(dep);
                }
                else
                {
                    installedDisabled.Add(dep);
                }
            }

            return (installedEnabled, installedDisabled, missing);
        }

        /// <summary>
        /// Fetches missing metadata (category, source URL) from API
        /// </summary>
        private async Task FetchMissingMetadataAsync()
        {
            // Take a snapshot to avoid collection modification issues
            var modsSnapshot = _allMods.ToList();
            var modsNeedingMetadata = modsSnapshot.Where(m =>
                _metadataService.NeedsMetadaUpdate(m.Name)).ToList();

            if (modsNeedingMetadata.Count == 0)
                return;

            await _uiService.InvokeAsync(() =>
            {
                SetStatus($"Fetching metadata for {modsNeedingMetadata.Count} mods...");
            });

            var currentIndex = 0;
            foreach (var mod in modsNeedingMetadata)
            {
                currentIndex++;
                await _uiService.InvokeAsync(() =>
                {
                    SetStatus($"Fetching metadata ({currentIndex}/{modsNeedingMetadata.Count}): {mod.Title}");
                });

                try
                {
                    var details = await _apiService.GetModDetailsFullAsync(mod.Name);
                    if (details != null)
                    {
                        // Update both category and source URL in a single operation
                        _metadataService.UpdateAllPortalMetadata(mod.Name, details.Category, details.SourceUrl);
                        await _uiService.InvokeAsync(() =>
                        {
                            mod.Category = details.Category;
                            mod.SourceUrl = details.SourceUrl;
                        });
                    }
                    else
                    {
                        _logService.LogWarning($"No full portal details for mod {mod.Name}");
                        _metadataService.CreateBaseMetadata(mod.Name);
                    }

                    await Task.Delay(100); // Rate limiting
                }
                catch (Exception ex)
                {
                    HandleError(ex, _errorMessageService.GetTechnicalMessage(ex));
                }
            }

            await _uiService.InvokeAsync(() =>
            {
                SetStatus("Metadata update complete");
            });
        }
    }
}