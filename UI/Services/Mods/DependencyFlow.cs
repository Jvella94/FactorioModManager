using FactorioModManager.Models;
using FactorioModManager.Services.API;
using FactorioModManager.Services.Infrastructure;
using FactorioModManager.Services.Settings;
using FactorioModManager.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FactorioModManager.Services.Mods
{
    public sealed class DependencyResolution
    {
        public bool Proceed { get; set; }
        public bool InstallEnabled { get; set; }
        public List<ModViewModel> ModsToEnable { get; } = [];
        public List<ModViewModel> ModsToDisable { get; } = [];
        public List<string> MissingDependenciesToInstall { get; } = [];
    }

    public interface IDependencyFlow
    {
        Task<DependencyResolution> ResolveForInstallAsync(string modName, IEnumerable<ModViewModel> installedMods);

        // Single planned-updates-aware update resolver (plannedUpdates may be null)
        Task<DependencyResolution> ResolveForUpdateAsync(string modName, string version, IEnumerable<ModViewModel> installedMods, IDictionary<string, string>? plannedUpdates = null);

        bool ValidateMandatoryDependencies(List<string> dependencies, IEnumerable<ModViewModel> installedMods);

        List<string> GetMissingMandatoryDepsForModInfo(ModInfo mod, IEnumerable<ModInfo> installedMods);

        List<string> GetDisabledDependenciesForModInfo(ModInfo mod, IEnumerable<ModInfo> installedMods, Dictionary<string, bool> enabledStates);

        List<string> GetMissingBuiltInDependenciesForModInfo(ModInfo mod);

        // Build a preview of the dependency tree and resolution for installing a mod without showing UI.
        // Returns the resolution and the formatted tree/message so callers can aggregate confirmations.
        Task<(DependencyResolution Resolution, string Message)> BuildInstallPreviewAsync(string modName, IEnumerable<ModViewModel> installedMods);

        Task<(DependencyResolution Resolution, string Message)> BuildUpdatePreviewAsync(string modName, string version, IEnumerable<ModViewModel> installedMods, IDictionary<string, string>? plannedUpdates = null);
    }

    // Centralized dependency resolution and helper service
    public class DependencyFlow(
        ILogService logService,
        IFactorioApiService factorioApiService,
        ISettingsService settingsService) : IDependencyFlow
    {
        private readonly ILogService _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        private readonly IFactorioApiService _factorioApiService = factorioApiService ?? throw new ArgumentNullException(nameof(factorioApiService));
        private readonly ISettingsService _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

        public async Task<DependencyResolution> ResolveForInstallAsync(string modName, IEnumerable<ModViewModel> installedMods)
        {
            var visitedMods = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var dependencyTree = new Dictionary<string, List<string>>();
            var edgeLabels = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            var resolution = await ResolveDependenciesRecursivelyAsync(modName, installedMods, visitedMods, dependencyTree, edgeLabels, plannedUpdates: null);
            // Do not show UI here. Caller should use BuildInstallPreviewAsync to obtain the message and show confirmation once.
            return resolution;
        }

        public async Task<DependencyResolution> ResolveForUpdateAsync(string modName, string version, IEnumerable<ModViewModel> installedMods, IDictionary<string, string>? plannedUpdates = null)
        {
            var result = new DependencyResolution { Proceed = true, InstallEnabled = true };

            try
            {
                _logService.LogDebug($"DependencyFlow: fetching details for {modName}@{version}");
                var modDetails = await _factorioApiService.GetModDetailsFullAsync(modName);
                if (modDetails?.Releases == null || modDetails.Releases.Count == 0)
                    return new DependencyResolution { Proceed = false, InstallEnabled = false };

                var target = modDetails.Releases.FirstOrDefault(r => string.Equals(r.Version, version, StringComparison.OrdinalIgnoreCase));
                if (target == null)
                {
                    _logService.Log($"DependencyFlow: version {version} not found for {modName}", LogLevel.Warning);
                    return new DependencyResolution { Proceed = false, InstallEnabled = false };
                }

                var dependencyTree = new Dictionary<string, List<string>>();
                var edgeLabels = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
                var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                var mandatoryParsed = Constants.DependencyHelper.GetMandatoryDependenciesWithConstraints(target.Dependencies);
                var incompatibleDeps = Constants.DependencyHelper.GetIncompatibleDependencies(target.Dependencies);

                var installedList = installedMods.ToList();
                var missing = new List<(string Name, string? Op, string? Version)>();
                var disabled = new List<ModViewModel>();

                // helper to get effective installed version (apply plannedUpdates override if present)
                string? GetEffectiveVersion(string name)
                {
                    var inst = installedList.FirstOrDefault(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                    if (inst == null) return null;
                    if (plannedUpdates != null && plannedUpdates.TryGetValue(inst.Name, out var planned))
                        return planned;
                    return inst.Version;
                }

                foreach (var dep in mandatoryParsed)
                {
                    var name = dep.Name;
                    if (Constants.DependencyHelper.IsGameDependency(name))
                    {
                        if (Constants.DependencyHelper.IsDLCDependency(name) && !_settingsService.GetHasSpaceAgeDLC())
                        {
                            _logService.Log($"Missing required DLC for dependency: {name}", LogLevel.Warning);
                        }
                        else
                        {
                            _logService.Log($"Skipping game dependency: {name}", LogLevel.Info);
                            continue;
                        }
                    }

                    var installed = installedList.FirstOrDefault(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                    var effectiveVersion = GetEffectiveVersion(name);
                    if (installed == null)
                        missing.Add((name, dep.VersionOperator, dep.Version));
                    else if (!installed.IsEnabled)
                        disabled.Add(installed);
                    else if (!Constants.DependencyHelper.SatisfiesVersionConstraint(effectiveVersion, dep.VersionOperator, dep.Version))
                        missing.Add((name, dep.VersionOperator, dep.Version));
                }

                var incompatibleLoaded = installedMods.Where(m => m.IsEnabled && incompatibleDeps.Contains(m.Name)).ToList();

                if (!dependencyTree.TryGetValue(modName, out var list))
                {
                    list = [];
                    dependencyTree[modName] = list;
                }

                var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var m in missing)
                {
                    // Avoid adding the mod itself as its own dependency (handle malformed metadata/cycles)
                    if (string.Equals(m.Name, modName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    list.Add(m.Name);

                    // store display label for this edge
                    if (!edgeLabels.TryGetValue(modName, out var map))
                    {
                        map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        edgeLabels[modName] = map;
                    }
                    map[m.Name] = FormatDependencyDisplay(m.Op, m.Version);

                    if (unique.Add(m.Name))
                        result.MissingDependenciesToInstall.Add(m.Name);

                    var sub = await ResolveDependenciesRecursivelyAsync(m.Name, installedMods, visited, dependencyTree, edgeLabels, plannedUpdates);
                    if (!sub.Proceed)
                    {
                        result.Proceed = false;
                        return result;
                    }

                    foreach (var s in sub.MissingDependenciesToInstall)
                    {
                        if (unique.Add(s))
                            result.MissingDependenciesToInstall.Add(s);
                    }

                    result.ModsToEnable.AddRange(sub.ModsToEnable);
                    result.ModsToDisable.AddRange(sub.ModsToDisable);
                }

                // Determine whether the target mod is enabled — affects enable/disable decisions
                var targetInstalled = installedList.FirstOrDefault(m => m.Name.Equals(modName, StringComparison.OrdinalIgnoreCase));
                var targetIsEnabled = targetInstalled?.IsEnabled ?? true;

                // Only enable disabled dependencies if the target mod is currently enabled
                if (disabled.Count > 0 && targetIsEnabled)
                    result.ModsToEnable.AddRange(disabled);

                // Only consider disabling incompatible mods if the target mod is currently enabled
                if (targetIsEnabled && incompatibleLoaded.Count > 0)
                    result.ModsToDisable.AddRange(incompatibleLoaded);

                // Do not show UI here. Caller should use BuildUpdatePreviewAsync to obtain the message and show confirmation once.
            }
            catch (Exception ex)
            {
                _logService.LogError($"DependencyFlow: error resolving update dependencies for {modName}@{version}: {ex.Message}", ex);
                result.Proceed = false;
            }

            return result;
        }

        public bool ValidateMandatoryDependencies(List<string> dependencies, IEnumerable<ModViewModel> installedMods)
        {
            var mandatoryParsed = Constants.DependencyHelper.GetMandatoryDependenciesWithConstraints(dependencies);
            var installedList = installedMods.ToList();

            foreach (var dep in mandatoryParsed)
            {
                var depName = dep.Name;
                if (Constants.DependencyHelper.IsGameDependency(depName))
                    continue;

                var installed = installedList.FirstOrDefault(m => m.Name.Equals(depName, StringComparison.OrdinalIgnoreCase));
                if (installed == null)
                    return false;

                if (!Constants.DependencyHelper.SatisfiesVersionConstraint(installed.Version, dep.VersionOperator, dep.Version))
                    return false;
            }

            return true;
        }

        public List<string> GetMissingMandatoryDepsForModInfo(ModInfo mod, IEnumerable<ModInfo> installedMods)
        {
            var mandatoryParsed = Constants.DependencyHelper.GetMandatoryDependenciesWithConstraints(mod.Dependencies);
            var installedList = installedMods.ToList();

            var missing = new List<string>();
            foreach (var dep in mandatoryParsed)
            {
                var name = dep.Name;
                if (Constants.DependencyHelper.IsGameDependency(name))
                    continue;

                var installed = installedList.FirstOrDefault(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (installed == null)
                {
                    missing.Add(name);
                    continue;
                }

                if (!Constants.DependencyHelper.SatisfiesVersionConstraint(installed.Version, dep.VersionOperator, dep.Version))
                {
                    missing.Add(name);
                }
            }

            return missing;
        }

        public List<string> GetDisabledDependenciesForModInfo(ModInfo mod, IEnumerable<ModInfo> installedMods, Dictionary<string, bool> enabledStates)
        {
            var mandatoryParsed = Constants.DependencyHelper.GetMandatoryDependenciesWithConstraints(mod.Dependencies);
            var installedModsList = installedMods.ToList();

            var disabled = new List<string>();
            foreach (var dep in mandatoryParsed)
            {
                var name = dep.Name;
                if (Constants.DependencyHelper.IsGameDependency(name))
                    continue;

                var installedMod = installedModsList.FirstOrDefault(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (installedMod == null) continue;

                if (!Constants.DependencyHelper.SatisfiesVersionConstraint(installedMod.Version, dep.VersionOperator, dep.Version))
                    continue;

                if (enabledStates.TryGetValue(installedMod.Name, out var enabled) && !enabled)
                    disabled.Add(name);
            }

            return disabled;
        }

        public List<string> GetMissingBuiltInDependenciesForModInfo(ModInfo mod)
        {
            var missingBuiltIn = new List<string>();
            var mandatoryParsed = Constants.DependencyHelper.GetMandatoryDependenciesWithConstraints(mod.Dependencies);

            foreach (var parsed in mandatoryParsed)
            {
                var dep = parsed.Name;
                switch (dep.ToLowerInvariant())
                {
                    case "base":
                        break;

                    case "space-age":
                    case "quality":
                    case "elevated-rails":
                        if (!_settingsService.GetHasSpaceAgeDLC())
                        {
                            if (!missingBuiltIn.Contains("Space Age DLC"))
                                missingBuiltIn.Add("Space Age DLC (includes Quality & Elevated Rails)");
                        }
                        break;
                }
            }

            return missingBuiltIn;
        }

        public async Task<(DependencyResolution Resolution, string Message)> BuildInstallPreviewAsync(string modName, IEnumerable<ModViewModel> installedMods)
        {
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var dependencyTree = new Dictionary<string, List<string>>();
            var edgeLabels = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            var resolution = await ResolveDependenciesRecursivelyAsync(modName, installedMods, visited, dependencyTree, edgeLabels, plannedUpdates: null);
            var message = BuildDependencyTreeMessage(dependencyTree, edgeLabels, resolution);
            return (resolution, message);
        }

        public async Task<(DependencyResolution Resolution, string Message)> BuildUpdatePreviewAsync(string modName, string version, IEnumerable<ModViewModel> installedMods, IDictionary<string, string>? plannedUpdates = null)
        {
            var result = new DependencyResolution { Proceed = true, InstallEnabled = true };
            try
            {
                var modDetails = await _factorioApiService.GetModDetailsFullAsync(modName);
                if (modDetails?.Releases == null || modDetails.Releases.Count == 0)
                    return (new DependencyResolution { Proceed = false, InstallEnabled = false }, string.Empty);

                var target = modDetails.Releases.FirstOrDefault(r => string.Equals(r.Version, version, StringComparison.OrdinalIgnoreCase));
                if (target == null)
                    return (new DependencyResolution { Proceed = false, InstallEnabled = false }, string.Empty);

                var dependencyTree = new Dictionary<string, List<string>>();
                var edgeLabels = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
                var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                var mandatoryParsed = Constants.DependencyHelper.GetMandatoryDependenciesWithConstraints(target.Dependencies);
                var incompatibleDeps = Constants.DependencyHelper.GetIncompatibleDependencies(target.Dependencies);

                var installedList = installedMods.ToList();
                var missing = new List<(string Name, string? Op, string? Version)>();
                var disabled = new List<ModViewModel>();

                string? GetEffectiveVersion(string name)
                {
                    var inst = installedList.FirstOrDefault(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                    if (inst == null) return null;
                    if (plannedUpdates != null && plannedUpdates.TryGetValue(inst.Name, out var planned))
                        return planned;
                    return inst.Version;
                }

                foreach (var dep in mandatoryParsed)
                {
                    var name = dep.Name;
                    if (Constants.DependencyHelper.IsGameDependency(name))
                    {
                        if (Constants.DependencyHelper.IsDLCDependency(name) && !_settingsService.GetHasSpaceAgeDLC())
                        {
                            _logService.Log($"Missing required DLC for dependency: {name}", LogLevel.Warning);
                        }
                        else
                        {
                            _logService.Log($"Skipping game dependency: {name}", LogLevel.Info);
                            continue;
                        }
                    }

                    var installed = installedList.FirstOrDefault(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                    var effectiveVersion = GetEffectiveVersion(name);
                    if (installed == null)
                        missing.Add((name, dep.VersionOperator, dep.Version));
                    else if (!installed.IsEnabled)
                        disabled.Add(installed);
                    else if (!Constants.DependencyHelper.SatisfiesVersionConstraint(effectiveVersion, dep.VersionOperator, dep.Version))
                        missing.Add((name, dep.VersionOperator, dep.Version));
                }

                if (!dependencyTree.TryGetValue(modName, out var list))
                {
                    list = [];
                    dependencyTree[modName] = list;
                }

                var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var m in missing)
                {
                    if (string.Equals(m.Name, modName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    list.Add(m.Name);

                    if (!edgeLabels.TryGetValue(modName, out var map))
                    {
                        map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        edgeLabels[modName] = map;
                    }
                    map[m.Name] = FormatDependencyDisplay(m.Op, m.Version);

                    if (unique.Add(m.Name))
                        result.MissingDependenciesToInstall.Add(m.Name);

                    var sub = await ResolveDependenciesRecursivelyAsync(m.Name, installedMods, visited, dependencyTree, edgeLabels, plannedUpdates);
                    if (!sub.Proceed)
                    {
                        result.Proceed = false;
                        return (result, string.Empty);
                    }

                    foreach (var s in sub.MissingDependenciesToInstall)
                    {
                        if (unique.Add(s))
                            result.MissingDependenciesToInstall.Add(s);
                    }

                    result.ModsToEnable.AddRange(sub.ModsToEnable);
                    result.ModsToDisable.AddRange(sub.ModsToDisable);
                }

                // Determine if target mod is enabled (affects enable/disable decisions)
                var targetInstalled = installedList.FirstOrDefault(m => m.Name.Equals(modName, StringComparison.OrdinalIgnoreCase));
                var targetIsEnabled = targetInstalled?.IsEnabled ?? true;
                if (disabled.Count > 0 && targetIsEnabled)
                    result.ModsToEnable.AddRange(disabled);

                if (targetIsEnabled && incompatibleDeps.Count > 0)
                    result.ModsToDisable.AddRange(installedMods.Where(m => m.IsEnabled && incompatibleDeps.Contains(m.Name)));

                // Build message body
                var body = BuildDependencyTreeMessage(dependencyTree, edgeLabels, result);

                // Compute current installed version for header (if present)
                var currentVersion = installedList.FirstOrDefault(m => m.Name.Equals(modName, StringComparison.OrdinalIgnoreCase))?.Version ?? "(not installed)";
                var header = $"Update: {modDetails.Title} ({currentVersion} -> {version}){Environment.NewLine}";
                var message = header + body;
                return (result, message);
            }
            catch (Exception ex)
            {
                _logService.LogError($"DependencyFlow: error building update preview for {modName}@{version}: {ex.Message}", ex);
                return (new DependencyResolution { Proceed = false, InstallEnabled = false }, string.Empty);
            }
        }

        private async Task<DependencyResolution> ResolveDependenciesRecursivelyAsync(
            string modName,
            IEnumerable<ModViewModel> installedMods,
            HashSet<string> visitedMods,
            Dictionary<string, List<string>> dependencyTree,
            Dictionary<string, Dictionary<string, string>> edgeLabels,
            IDictionary<string, string>? plannedUpdates)
        {
            if (visitedMods.Contains(modName))
            {
                _logService.Log($"DependencyFlow: skipping already visited mod: {modName}", LogLevel.Info);
                return new DependencyResolution { Proceed = true, InstallEnabled = true };
            }

            visitedMods.Add(modName);

            var result = new DependencyResolution { Proceed = true, InstallEnabled = true };

            try
            {
                var modDetails = await _factorioApiService.GetModDetailsFullAsync(modName);
                if (modDetails?.Releases == null || modDetails.Releases.Count == 0)
                {
                    _logService.Log($"DependencyFlow: no release details found for {modName}", LogLevel.Warning);
                    return new DependencyResolution { Proceed = false, InstallEnabled = false };
                }

                var latestRelease = modDetails.Releases.OrderByDescending(r => r.ReleasedAt).FirstOrDefault();
                if (latestRelease == null || latestRelease.Dependencies == null)
                {
                    _logService.Log($"DependencyFlow: no dependencies found for {modName}", LogLevel.Info);
                    return result;
                }

                var mandatoryParsed = Constants.DependencyHelper.GetMandatoryDependenciesWithConstraints(latestRelease.Dependencies);
                var incompatibleDeps = Constants.DependencyHelper.GetIncompatibleDependencies(latestRelease.Dependencies);

                var installedModsList = installedMods.ToList();
                var missingDeps = new List<(string Name, string? Op, string? Version)>();
                var disabledDeps = new List<ModViewModel>();

                string? GetEffectiveVersion(string name)
                {
                    var inst = installedModsList.FirstOrDefault(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                    if (inst == null) return null;
                    if (plannedUpdates != null && plannedUpdates.TryGetValue(inst.Name, out var planned))
                        return planned;
                    return inst.Version;
                }

                foreach (var dep in mandatoryParsed)
                {
                    var depName = dep.Name;
                    if (Constants.DependencyHelper.IsGameDependency(depName))
                    {
                        if (Constants.DependencyHelper.IsDLCDependency(depName) && !_settingsService.GetHasSpaceAgeDLC())
                        {
                            _logService.Log($"Missing required DLC for dependency: {depName}", LogLevel.Warning);
                        }
                        else
                        {
                            _logService.Log($"Skipping game dependency: {depName}", LogLevel.Info);
                            continue;
                        }
                    }

                    var depMod = installedModsList.FirstOrDefault(m => m.Name.Equals(depName, StringComparison.OrdinalIgnoreCase));
                    var effectiveVersion = GetEffectiveVersion(depName);
                    if (depMod == null)
                        missingDeps.Add((depName, dep.VersionOperator, dep.Version));
                    else if (!depMod.IsEnabled)
                        disabledDeps.Add(depMod);
                    else if (!Constants.DependencyHelper.SatisfiesVersionConstraint(effectiveVersion, dep.VersionOperator, dep.Version))
                        missingDeps.Add((depName, dep.VersionOperator, dep.Version));
                }

                var incompatibleLoaded = installedMods.Where(m => m.IsEnabled && incompatibleDeps.Contains(m.Name)).ToList();

                if (!dependencyTree.TryGetValue(modName, out var value))
                {
                    value = [];
                    dependencyTree[modName] = value;
                }

                var uniqueDependencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var missingDep in missingDeps)
                {
                    // Skip if a mod's dependencies incorrectly include itself to avoid infinite/circular entries
                    if (string.Equals(missingDep.Name, modName, StringComparison.OrdinalIgnoreCase))
                    {
                        _logService.Log($"DependencyFlow: skipping own mod entry as dependency: {missingDep.Name}", LogLevel.Info);
                        continue;
                    }

                    // Skip if this dependency is already in the visited chain to prevent cycles
                    if (visitedMods.Contains(missingDep.Name))
                    {
                        _logService.Log($"DependencyFlow: skipping already visited dependency to avoid cycle: {missingDep.Name}", LogLevel.Info);
                        continue;
                    }

                    _logService.Log($"DependencyFlow: found missing dependency: {missingDep.Name}", LogLevel.Info);
                    value.Add(missingDep.Name);

                    // store edge label
                    if (!edgeLabels.TryGetValue(modName, out var map))
                    {
                        map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        edgeLabels[modName] = map;
                    }
                    map[missingDep.Name] = FormatDependencyDisplay(missingDep.Op, missingDep.Version);

                    if (uniqueDependencies.Add(missingDep.Name))
                        result.MissingDependenciesToInstall.Add(missingDep.Name);

                    var depResolution = await ResolveDependenciesRecursivelyAsync(missingDep.Name, installedMods, visitedMods, dependencyTree, edgeLabels, plannedUpdates);
                    if (!depResolution.Proceed)
                    {
                        result.Proceed = false;
                        return result;
                    }

                    foreach (var dep in depResolution.MissingDependenciesToInstall)
                    {
                        if (uniqueDependencies.Add(dep))
                            result.MissingDependenciesToInstall.Add(dep);
                    }

                    result.ModsToEnable.AddRange(depResolution.ModsToEnable);
                    result.ModsToDisable.AddRange(depResolution.ModsToDisable);
                }

                if (disabledDeps.Count > 0)
                    result.ModsToEnable.AddRange(disabledDeps);

                // Only consider disabling incompatible mods if the current target mod is enabled
                var targetInstalled = installedModsList.FirstOrDefault(m => m.Name.Equals(modName, StringComparison.OrdinalIgnoreCase));
                var targetIsEnabled = targetInstalled?.IsEnabled ?? true;
                if (targetIsEnabled && incompatibleLoaded.Count > 0)
                    result.ModsToDisable.AddRange(incompatibleLoaded);
            }
            catch (Exception ex)
            {
                _logService.LogError($"DependencyFlow: error resolving dependencies for {modName}: {ex.Message}", ex);
                result.Proceed = false;
            }

            return result;
        }

        private static string BuildDependencyTreeMessage(Dictionary<string, List<string>> dependencyTree, Dictionary<string, Dictionary<string, string>> edgeLabels, DependencyResolution resolution)
        {
            var message = new StringBuilder();
            // If there are no entries or only a single root with no children, report no dependencies
            if (dependencyTree.Count == 0 || (dependencyTree.Count == 1 && dependencyTree.Values.First().Count == 0))
            {
                message.AppendLine("No missing dependencies were found.");
                return message.ToString();
            }

            void AppendChildren(string parent, int level)
            {
                var indent = new string(' ', level * 4);
                if (!dependencyTree.TryGetValue(parent, out var deps) || deps == null)
                    return;

                foreach (var d in deps)
                {
                    var label = string.Empty;
                    if (edgeLabels != null && edgeLabels.TryGetValue(parent, out var map) && map != null && map.TryGetValue(d, out var lbl))
                        label = $" {lbl}";

                    // Print child on its own indented line including label
                    message.AppendLine($"{indent}- {d}{label}");

                    // Recurse into this child's children
                    AppendChildren(d, level + 1);
                }
            }

            void AppendTree(string modName, int level)
            {
                var indent = new string(' ', level * 4);
                message.AppendLine($"{indent}- {modName}");
                AppendChildren(modName, level + 1);
            }

            // Pick a root that actually has dependencies if possible
            var root = dependencyTree.Keys.First();
            var candidateWithDeps = dependencyTree.FirstOrDefault(kv => kv.Value != null && kv.Value.Count > 0);
            if (!string.IsNullOrEmpty(candidateWithDeps.Key))
                root = candidateWithDeps.Key;

            message.AppendLine("Dependency Tree:");
            AppendTree(root, 0);

            if (resolution.ModsToEnable.Count > 0)
            {
                message.AppendLine();
                message.AppendLine("Disabled Dependencies to be Enabled:");
                foreach (var m in resolution.ModsToEnable)
                    message.AppendLine($"- {m.Title}");
            }

            if (resolution.ModsToDisable.Count > 0)
            {
                message.AppendLine();
                message.AppendLine("Incompatible Mods to be Disabled:");
                foreach (var m in resolution.ModsToDisable)
                    message.AppendLine($"- {m.Title}");
            }

            return message.ToString();
        }

        private static string FormatDependencyDisplay(string? op, string? version)
        {
            if (string.IsNullOrEmpty(version))
                return string.Empty;

            var useOp = string.IsNullOrEmpty(op) ? "=" : op;
            return $"{useOp} {version}";
        }
    }
}