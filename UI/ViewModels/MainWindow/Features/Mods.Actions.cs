using FactorioModManager.Models;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using static FactorioModManager.Constants;

namespace FactorioModManager.ViewModels.MainWindow
{
    public partial class MainWindowViewModel
    {
        private async void ToggleMod(ModViewModel? mod)
        {
            if (mod == null) return;
            if (_togglingMod) return;

            _togglingMod = true;
            var targetEnabled = mod.IsEnabled; // Checkbox binding already set this

            // Prevent toggling while Factorio is running (use service)
            if (_factorioLauncher.IsFactorioRunning())
            {
                // Revert UI checkbox state
                mod.IsEnabled = !targetEnabled;
                SetStatus("Cannot toggle mods while Factorio is running.", LogLevel.Warning);
                await _uiService.ShowMessageAsync(
                    "Factorio is running",
                    "Mods cannot be toggled while Factorio is running. Please close the game and try again.");
                _togglingMod = false;
                return;
            }

            if (targetEnabled)
            {
                // Enabling: ensure dependencies are satisfied (now transitive)
                var transitiveNames = GetTransitiveMandatoryDependencyNames(mod);

                // Classify transitive deps into installed enabled, installed disabled, missing
                var installedEnabledDeps = new List<ModViewModel>();
                var installedDisabledDeps = new List<ModViewModel>();
                var missingDeps = new List<string>();

                foreach (var name in transitiveNames)
                {
                    var found = FindModByName(name);
                    if (found == null)
                        missingDeps.Add(name);
                    else if (found.IsEnabled)
                        installedEnabledDeps.Add(found);
                    else
                        installedDisabledDeps.Add(found);
                }

                // Build the set of names that will be enabled (main mod + all installed-but-disabled transitive deps)
                var namesToEnable = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    mod.Name
                };
                foreach (var d in installedDisabledDeps)
                    namesToEnable.Add(d.Name);

                // Check incompatible mods against all mods that will be enabled
                var incompatibleMods = _allMods.Where(m =>
                    m.IsEnabled &&
                    m.Dependencies != null &&
                    DependencyHelper.GetIncompatibleDependencies(m.Dependencies)
                        .Any(d => namesToEnable.Contains(d))
                ).ToList();

                if (missingDeps.Count > 0)
                {
                    // Block enable – missing deps
                    mod.IsEnabled = false;
                    SetStatus($"Cannot enable {mod.Title}: missing dependencies: {string.Join(", ", missingDeps)}", LogLevel.Warning);
                    await _uiService.ShowMessageAsync(
                        "Missing Dependencies",
                        $"The following mandatory dependencies for {mod.Title} are not installed:\n\n" +
                        string.Join("\n", missingDeps) + "\n\nInstall or add them before enabling this mod.");
                    _togglingMod = false;
                    return;
                }

                // Build a summary of actions: dependencies to enable and incompatible mods to disable
                var willEnable = installedDisabledDeps.Select(m => $"{m.Title} ({m.Name})").ToList();
                var willDisable = incompatibleMods.Select(m => $"{m.Title} ({m.Name})").ToList();

                if (willEnable.Count == 0 && willDisable.Count == 0)
                {
                    // Nothing special to do – proceed
                }
                else
                {
                    var parts = new List<string>();
                    if (willEnable.Count > 0)
                        parts.Add("The following mandatory dependencies will be enabled:\n\n" + string.Join("\n", willEnable));
                    if (willDisable.Count > 0)
                        parts.Add("The following currently enabled incompatible mods will be disabled:\n\n" + string.Join("\n", willDisable));

                    var summary = string.Join("\n\n", parts);
                    var confirmAll = await _uiService.ShowConfirmationAsync(
                        "Confirm Dependency Changes",
                        $"Enabling {mod.Title} will make the following changes:\n\n" + summary + "\n\nProceed?",
                        null);

                    if (!confirmAll)
                    {
                        mod.IsEnabled = false;
                        SetStatus($"Cancelled enabling {mod.Title}.", LogLevel.Warning);
                        _togglingMod = false;
                        return;
                    }

                    // Apply disables first
                    foreach (var inc in incompatibleMods)
                    {
                        inc.IsEnabled = false;
                        _modService.ToggleMod(inc.Name, false);
                    }

                    // Then enable transitive installed-disabled dependencies (full set)
                    if (installedDisabledDeps.Count > 0)
                    {
                        var toEnable = installedDisabledDeps.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);

                        // Build adjacency and indegree for Kahn's algorithm
                        var indegree = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                        var adj = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

                        foreach (var name in toEnable.Keys)
                        {
                            indegree[name] = 0;
                            adj[name] = [];
                        }

                        // For each node, look at its mandatory dependencies; add edge dep -> node when dep is also in toEnable
                        foreach (var kv in toEnable)
                        {
                            var nodeName = kv.Key;
                            var nodeVm = kv.Value;
                            var deps = DependencyHelper.GetMandatoryDependencies(nodeVm.Dependencies);
                            foreach (var rawDep in deps)
                            {
                                var depName = DependencyHelper.ExtractDependencyName(rawDep);
                                if (string.IsNullOrEmpty(depName)) continue;
                                if (!toEnable.ContainsKey(depName)) continue; // dependency already enabled or external

                                // edge depName -> nodeName
                                adj[depName].Add(nodeName);
                                indegree[nodeName]++;
                            }
                        }

                        // Kahn's algorithm
                        var queue = new Queue<string>(indegree.Where(kv => kv.Value == 0).Select(kv => kv.Key));
                        var enabledOrder = new List<string>();

                        while (queue.Count > 0)
                        {
                            var cur = queue.Dequeue();
                            enabledOrder.Add(cur);

                            foreach (var succ in adj[cur])
                            {
                                indegree[succ]--;
                                if (indegree[succ] == 0)
                                    queue.Enqueue(succ);
                            }
                        }

                        // If cycle exists, enable remaining nodes in arbitrary order
                        var remaining = indegree.Where(kv => kv.Value > 0).Select(kv => kv.Key).ToList();
                        if (remaining.Count > 0)
                        {
                            enabledOrder.AddRange(remaining);
                        }

                        // Now enable in computed order
                        foreach (var name in enabledOrder)
                        {
                            if (!toEnable.TryGetValue(name, out var vm)) continue;
                            vm.IsEnabled = true;
                            _modService.ToggleMod(vm.Name, true);
                        }
                    }
                }

                // All checks passed – persist main mod
                _modService.ToggleMod(mod.Name, true);
                SetStatus($"{mod.Title} enabled");
            }
            else
            {
                // Disabling: check dependents
                var dependents = GetDependents(mod.Name)
                    .Where(m => m.IsEnabled)
                    .ToList();

                if (dependents.Count > 0)
                {
                    var message =
                        $"The following mods depend on {mod.Title} and are currently enabled:\n\n" +
                        string.Join("\n", dependents.Select(m => m.Title)) +
                        "\n\nDo you want to disable them as well?";

                    var disableDeps = await _uiService.ShowConfirmationAsync(
                        "Dependent Mods",
                        message,
                        null);

                    if (!disableDeps)
                    {
                        // Cancel disable
                        mod.IsEnabled = true;
                        SetStatus($"Cancelled disabling {mod.Title} due to dependent mods.", LogLevel.Warning);
                        _togglingMod = false;
                        return;
                    }

                    foreach (var dep in dependents)
                    {
                        dep.IsEnabled = false;
                        _modService.ToggleMod(dep.Name, false);
                    }
                }

                _modService.ToggleMod(mod.Name, false);
                SetStatus($"{mod.Title} disabled");
            }

            // Update groups and UI
            foreach (var group in Groups.Where(g => g.ModNames.Contains(mod.Title)))
                UpdateGroupStatus(group);

            this.RaisePropertyChanged(nameof(EnabledCountText));
            _togglingMod = false;
        }

        private async void RemoveMod(ModViewModel? mod)
        {
            if (mod == null)
                return;

            // Confirmation for uninstalling the selected mod
            var confirmUninstall = await _uiService.ShowConfirmationAsync(
                "Uninstall Mod",
                $"Are you sure you want to uninstall {mod.Title}?",
                null,
                "Uninstall",
                "Cancel");

            if (!confirmUninstall)
            {
                SetStatus($"Uninstall cancelled for {mod.Title}", LogLevel.Warning);
                return;
            }

            // Collect candidate dependency names that may become unused when these mods are removed
            var candidateDeps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Who depends on this mod?
            var dependents = GetDependents(mod.Name).ToList();

            if (dependents.Count > 0)
            {
                var message =
                    $"{mod.Title} is required by the following mods:\n\n" +
                    string.Join("\n", dependents.Select(m => m.Title)) +
                    "\n\nDo you want to uninstall these mods as well? If not, they will be disabled.";

                var removeAll = await _uiService.ShowConfirmationAsync(
                    "Mod In Use",
                    message,
                    null);

                if (!removeAll)
                {
                    _logService.Log($"Disabling mods needed by {mod.Title}", LogLevel.Warning);
                    foreach (var dep in dependents)
                    {
                        if (dep.IsEnabled) ToggleMod(dep);
                    }
                }
                else
                {
                    // Remove dependents first
                    foreach (var dep in dependents)
                    {
                        // Add this dependent's declared dependencies as candidates to re-evaluate
                        if (dep.Dependencies != null)
                        {
                            foreach (var raw in dep.Dependencies)
                            {
                                var dn = DependencyHelper.ExtractDependencyName(raw);
                                if (!string.IsNullOrEmpty(dn)) candidateDeps.Add(dn);
                            }
                        }

                        if (dep.FilePath == null)
                        {
                            _logService.LogWarning("Filepath not found for mod {modName}, cannot remove mod.");
                            await _uiService.ShowMessageAsync($"Cannot find mod file", $"The file path was not found for mod {dep.Name} so it could not be removed. Disabling it for further investigation.");
                            if (dep.IsEnabled) ToggleMod(dep);
                            continue;
                        }
                        _allMods.Remove(dep);
                        _modService.RemoveMod(dep.Name, dep.FilePath);
                    }
                }
            }

            // Add main mod's declared dependencies as candidates as well
            if (mod.Dependencies != null)
            {
                foreach (var raw in mod.Dependencies)
                {
                    var dn = DependencyHelper.ExtractDependencyName(raw);
                    if (!string.IsNullOrEmpty(dn)) candidateDeps.Add(dn);
                }
            }

            if (mod.FilePath == null)
            {
                _logService.LogWarning("Filepath not found for mod {modName}, cannot remove mod.");
                await _uiService.ShowMessageAsync($"Cannot find mod file", $"The file path was not found for mod {mod.Name} so it could not be removed. Disabling it for further investigation.");
                if (mod.IsEnabled) ToggleMod(mod);
            }
            else
            {
                // Remove selected mod
                _allMods.Remove(mod);
                _modService.RemoveMod(mod.Name, mod.FilePath);
            }

            // If a batch aggregation is running, merge candidates for a single recompute later.
            if (_batchCandidateDeps != null)
            {
                foreach (var dn in candidateDeps)
                    _batchCandidateDeps.Add(dn);
            }
            else
            {
                // Recompute only candidate internal mods that may have become unused
                RecomputeUnusedInternalFlagsForCandidates(candidateDeps);
            }
            ApplyModFilter();
            this.RaisePropertyChanged(nameof(EnabledCountText));
            SetStatus($"Removed {mod.Title}");
        }

        // Collect transitive mandatory dependency names for a mod (excluding game deps)
        private HashSet<string> GetTransitiveMandatoryDependencyNames(ModViewModel mod)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var stack = new Stack<string>();

            // Start with direct mandatory dependencies (by raw strings)
            var direct = DependencyHelper.GetMandatoryDependencies(mod.Dependencies);
            foreach (var d in direct)
            {
                if (!string.IsNullOrWhiteSpace(d) && !DependencyHelper.IsGameDependency(d))
                    stack.Push(d);
            }

            while (stack.Count > 0)
            {
                var name = stack.Pop();
                if (string.IsNullOrWhiteSpace(name)) continue;
                if (result.Contains(name)) continue;

                result.Add(name);

                // Find the mod by name to inspect its own dependencies
                var vm = FindModByName(name);
                if (vm == null) continue;

                var deps = DependencyHelper.GetMandatoryDependencies(vm.Dependencies);
                foreach (var d in deps)
                {
                    if (string.IsNullOrWhiteSpace(d)) continue;
                    if (DependencyHelper.IsGameDependency(d)) continue;
                    if (!result.Contains(d)) stack.Push(d);
                }
            }

            return result;
        }
    }
}