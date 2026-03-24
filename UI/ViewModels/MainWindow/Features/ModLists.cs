using FactorioModManager.Models;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FactorioModManager.ViewModels.MainWindow
{
    public partial class MainWindowViewModel
    {
        // Expand/collapse state for the left-side sections (UI-only)
        private bool _areGroupsExpanded = true;

        private bool _areModListsExpanded = true;

        public bool AreGroupsExpanded
        {
            get => _areGroupsExpanded;
            set => this.RaiseAndSetIfChanged(ref _areGroupsExpanded, value);
        }

        public bool AreModListsExpanded
        {
            get => _areModListsExpanded;
            set => this.RaiseAndSetIfChanged(ref _areModListsExpanded, value);
        }

        /// <summary>
        /// Creates a new mod list and immediately enters rename mode and selects it.
        /// Persists the new list via the ModListService.
        /// </summary>
        private void CreateModList()
        {
            var listName = $"New List {ModLists.Count + 1}";
            var newList = new CustomModList
            {
                Name = listName,
                Description = "Snapshot",
                Entries = []
            };
            foreach (var m in _allMods.Where(m => m.IsEnabled))
            {
                newList.Entries.Add(new ModListEntry { Name = m.Name, Enabled = m.IsEnabled, Version = m.Version });
            }
            try
            {
                _modListService.AddList(newList);
            }
            catch
            {
                // non-fatal; still add to UI so user can edit
            }

            // Add to observable collection and start inline rename
            ModLists.Add(newList);
            newList.IsRenaming = true;
            newList.EditedName = newList.Name;
            newList.RequestRenameFocus = true;

            // select the new list so inline buttons / editor receive focus
            SelectedModList = newList;
            SetStatus($"Created mod list: {listName}");
        }

        private async Task ApplyModList(string name)
        {
            var list = ModLists.FirstOrDefault(l => l.Name == name);
            if (list == null) return;

            // Build preview items
            var previewItems = new List<(string Name, string Title, bool CurrentEnabled, bool TargetEnabled, string? CurrentVersion, string? TargetVersion, List<string> InstalledVersions)>();
            foreach (var vm in _allMods)
            {
                var entry = list.Entries.FirstOrDefault(e => e.Name.Equals(vm.Name, StringComparison.OrdinalIgnoreCase));
                var target = entry?.Enabled ?? false;
                var targetVersion = entry?.Version;
                var installedVersions = new List<string>();
                previewItems.Add((vm.Name, vm.Title, vm.IsEnabled, target, vm.Version, targetVersion, installedVersions));
            }

            // Show preview dialog on UI thread
            var owner = _uiService.GetMainWindow();

            // Build strongly-typed preview items for UI service
            var previewModels = new List<ModListPreviewItem>();
            foreach (var vm in _allMods)
            {
                var entry = list.Entries.FirstOrDefault(e => e.Name.Equals(vm.Name, StringComparison.OrdinalIgnoreCase));
                var target = entry?.Enabled ?? false;
                var targetVersion = entry?.Version;

                // Populate installed versions using ModVersionManager so preview can show dropdowns
                List<string> installedVersions = [];
                try
                {
                    var installed = _modVersionManager?.GetInstalledVersions(vm.Name);
                    if (installed != null)
                        installedVersions = installed;
                }
                catch (Exception ex)
                {
                    _logService.LogError($"Failed to get installed versions for {vm.Name}: {ex.Message}", ex);
                }

                previewModels.Add(new ModListPreviewItem
                {
                    Name = vm.Name,
                    Title = vm.Title,
                    CurrentStatus = vm.IsEnabled,
                    NewStatus = target,
                    CurrentVersion = vm.Version,
                    ListedVersion = targetVersion,
                    InstalledVersions = installedVersions
                });
            }
            previewModels = [.. previewModels.OrderBy(m => !m.NewStatus)];
            var result = await _uiService.ShowModListPreviewAsync(previewModels, name, owner);
            if (result == null) return; // cancelled

            // Prepare activation candidates (name, version) directly from the preview dialog results.
            var activationCandidates = result.Where(r => !string.IsNullOrEmpty(r.Version) && r.Enabled)
                                     .Select(r => (r.Name, Version: r.Version!))
                                     .ToList();

            var skipActivations = false;
            HashSet<string>? allowedActivations = null;

            if (activationCandidates.Count > 0)
            {
                // If Factorio is running, offer to apply without activations to avoid corrupting active files
                if (_factorioLauncher != null && _factorioLauncher.IsFactorioRunning())
                {
                    var proceed = await _uiService.ShowConfirmationAsync(
                        "Factorio is running",
                        "Factorio appears to be running. Active version changes require Factorio to be closed.\n\nChoose 'Apply without activations' to apply enabled/disabled changes only, or 'Cancel' to abort.",
                        owner,
                        yesButtonText: "Apply without activations",
                        noButtonText: "Cancel",
                        yesButtonColor: "#FFA000",
                        noButtonColor: "#3A3A3A");

                    if (!proceed)
                        return;

                    // User chose to proceed but without activations
                    skipActivations = true;
                }

                // Use the preview dialog's selections as the authoritative activation list.
                // All activation candidates returned by the preview are treated as allowed activations
                // unless activations were skipped due to Factorio running.
                if (!skipActivations)
                {
                    allowedActivations = new HashSet<string>(activationCandidates.Select(a => a.Name), StringComparer.OrdinalIgnoreCase);
                }
            }

            // Apply according to user's selections
            foreach (var r in result)
            {
                var vm = _allMods.FirstOrDefault(m => m.Name.Equals(r.Name, StringComparison.OrdinalIgnoreCase));
                if (vm == null) continue;

                // Apply enabled state
                if (vm.IsEnabled != r.Enabled)
                {
                    vm.IsEnabled = r.Enabled;
                    _modService.ToggleMod(vm.Name, r.Enabled);
                }

                // Apply version if requested (and non-null) AND the target is enabled
                if (!string.IsNullOrEmpty(r.Version) && r.Enabled)
                {
                    try
                    {
                        // Set SelectedMod so SetActiveVersion operates on correct VM
                        SelectedMod = vm;

                        // Only persist/apply version if activations aren't skipped and this mod was approved in the activation dialog (if shown)
                        var shouldApplyVersion = !skipActivations && (allowedActivations == null || allowedActivations.Contains(vm.Name));
                        if (shouldApplyVersion)
                        {
                            _modService.SaveModState(vm.Name, enabled: true, version: r.Version);

                            // Activate the selected version (updates FilePath, Version, etc.)
                            await SetActiveVersion(r.Version);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Preserve behavior: log and continue
                        _logService.LogError($"Failed to apply version for {vm.Name}: {ex.Message}", ex);
                    }
                }
                else if (!string.IsNullOrEmpty(r.Version) && !r.Enabled)
                {
                    // If target is disabled but a version was supplied, still persist version if desired
                    try
                    {
                        _modService.SaveModState(vm.Name, enabled: false, version: r.Version);
                    }
                    catch (Exception ex)
                    {
                        _logService.LogError($"Failed to save mod state for {vm.Name}: {ex.Message}", ex);
                    }
                }
            }

            SetStatus($"Applied mod list: {name}");
        }

        private void DeleteModList(string name)
        {
            _modListService.DeleteList(name);
            var item = ModLists.FirstOrDefault(l => l.Name == name);
            if (item != null) ModLists.Remove(item);
            SetStatus($"Deleted mod list: {name}");
        }

        private void RenameModList(string oldName, string newName)
        {
            var item = ModLists.FirstOrDefault(l => l.Name == oldName);
            if (item == null) return;
            var updated = new CustomModList { Name = newName, Description = item.Description, Entries = item.Entries };
            _modListService.UpdateList(oldName, updated);
            item.Name = newName;
            SetStatus($"Renamed mod list from '{oldName}' to '{newName}'");
        }

        private static void StartRenameModList(CustomModList? list)
        {
            if (list == null) return;
            list.IsRenaming = true;
            list.EditedName = list.Name;
        }

        private void ConfirmRenameModList(CustomModList? list)
        {
            if (list == null) return;
            if (string.IsNullOrWhiteSpace(list.EditedName)) return;

            var oldName = list.Name;
            var newName = list.EditedName.Trim();
            if (oldName == newName)
            {
                list.IsRenaming = false;
                return;
            }

            RenameModList(oldName, newName);
            list.IsRenaming = false;
        }
    }
}