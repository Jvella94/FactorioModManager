using FactorioModManager.Models;
using FactorioModManager.Services.API;
using FactorioModManager.Services.Infrastructure;
using FactorioModManager.Services.Settings;
using FactorioModManager.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FactorioModManager.Services.Mods
{
    public interface IModDependencyResolver
    {
        Task<DependencyResolution> ResolveForInstallAsync(
            string modName,
            IEnumerable<ModViewModel> installedMods);

        Task<DependencyResolution> ResolveForUpdateAsync(
            string modName,
            string version,
            IEnumerable<ModViewModel> installedMods);

        bool ValidateMandatoryDependencies(List<string> dependencies, IEnumerable<ModViewModel> installedMods);
    }

    public sealed class DependencyResolution
    {
        public bool Proceed { get; set; }
        public bool InstallEnabled { get; set; }
        public List<ModViewModel> ModsToEnable { get; } = [];
        public List<ModViewModel> ModsToDisable { get; } = [];
        public List<string> MissingDependenciesToInstall { get; } = [];
    }

    public class ModDependencyResolver(
        IUIService uiService,
        ILogService logService,
        IFactorioApiService factorioApiService,
        ISettingsService settingsService) : IModDependencyResolver
    {
        private readonly IUIService _uiService = uiService;
        private readonly ILogService _logService = logService;
        private readonly IFactorioApiService _factorioApiService = factorioApiService;
        private readonly ISettingsService _settingsService = settingsService;

        public async Task<DependencyResolution> ResolveForInstallAsync(
         string modName,
         IEnumerable<ModViewModel> installedMods)
        {
            var visitedMods = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var dependencyTree = new Dictionary<string, List<string>>();
            var resolution = await ResolveDependenciesRecursivelyAsync(modName, installedMods, visitedMods, dependencyTree);

            // Build the tree message
            var treeMessage = BuildDependencyTreeMessage(dependencyTree, resolution);

            // Present the summary to the user
            if (treeMessage.Length > 0)
            {
                resolution.Proceed = await _uiService.ShowConfirmationAsync("Confirm dependencies to be installed", treeMessage, null);
            }
            return resolution;
        }

        public async Task<DependencyResolution> ResolveForUpdateAsync(
            string modName,
            string version,
            IEnumerable<ModViewModel> installedMods)
        {
            // For updates, we're more lenient - just check if dependencies exist
            var result = new DependencyResolution
            {
                Proceed = true,
                InstallEnabled = true
            };

            // This would require fetching mod info for the specific version
            // For now, simplified implementation
            return await Task.FromResult(result);
        }

        public bool ValidateMandatoryDependencies(List<string> dependencies, IEnumerable<ModViewModel> installedMods)
        {
            var mandatoryDeps = Constants.DependencyHelper.GetMandatoryDependencies(dependencies);
            var (missing, _) = ClassifyDependencies(mandatoryDeps, installedMods);

            return missing.Count == 0;
        }

        private (List<string> Missing, List<ModViewModel> Disabled) ClassifyDependencies(
            IReadOnlyList<string> dependencies,
            IEnumerable<ModViewModel> installedMods)
        {
            var missing = new List<string>();
            var disabled = new List<ModViewModel>();
            var installedModsList = installedMods.ToList();

            foreach (var depName in dependencies)
            {
                var depMod = installedModsList.FirstOrDefault(
                    m => m.Name.Equals(depName, StringComparison.OrdinalIgnoreCase));

                if (Constants.DependencyHelper.IsGameDependency(depName))
                {
                    if (Constants.DependencyHelper.IsDLCDependency(depName) && _settingsService.GetHasSpaceAgeDLC() == false)
                    {
                        _logService.Log($"Missing required DLC for dependency: {depName}", LogLevel.Warning);
                    }
                    else
                    {
                        _logService.Log($"Skipping game dependency: {depName}", LogLevel.Info);
                        continue;
                    }
                }

                if (depMod == null)
                {
                    missing.Add(depName);
                }
                else if (!depMod.IsEnabled)
                {
                    disabled.Add(depMod);
                }
            }

            return (missing, disabled);
        }

        private async Task<DependencyResolution> ResolveDependenciesRecursivelyAsync(
            string modName,
            IEnumerable<ModViewModel> installedMods,
            HashSet<string> visitedMods,
            Dictionary<string, List<string>> dependencyTree)
        {
            // Prevent infinite loops in circular dependencies
            if (visitedMods.Contains(modName))
            {
                _logService.Log($"Skipping already visited mod: {modName}", LogLevel.Info);
                return new DependencyResolution { Proceed = true, InstallEnabled = true };
            }

            visitedMods.Add(modName);

            var result = new DependencyResolution
            {
                Proceed = true,
                InstallEnabled = true
            };

            try
            {
                // Fetch mod details from the API
                _logService.LogDebug($"Fetching details for mod: {modName}");
                var modDetails = await _factorioApiService.GetModDetailsFullAsync(modName);
                if (modDetails?.Releases == null || modDetails.Releases.Count == 0)
                {
                    _logService.Log($"No release details found for {modName}", LogLevel.Warning);
                    return new DependencyResolution { Proceed = false, InstallEnabled = false };
                }

                // Get the latest release
                var latestRelease = modDetails.Releases
                    .OrderByDescending(r => r.ReleasedAt)
                    .FirstOrDefault();

                if (latestRelease == null || latestRelease.Dependencies == null)
                {
                    _logService.Log($"No dependencies found for {modName}", LogLevel.Info);
                    return result;
                }

                // Classify dependencies
                var mandatoryDeps = Constants.DependencyHelper.GetMandatoryDependencies(latestRelease.Dependencies);
                var incompatibleDeps = Constants.DependencyHelper.GetIncompatibleDependencies(latestRelease.Dependencies);

                _logService.Log($"Mandatory dependencies for {modName}: {string.Join(", ", mandatoryDeps)}", LogLevel.Info);
                _logService.Log($"Incompatible dependencies for {modName}: {string.Join(", ", incompatibleDeps)}", LogLevel.Info);

                var (missingDeps, disabledDeps) = ClassifyDependencies(mandatoryDeps, installedMods);
                var incompatibleLoaded = installedMods
                    .Where(m => m.IsEnabled && incompatibleDeps.Contains(m.Name))
                    .ToList();

                // Add missing dependencies to the tree
                if (!dependencyTree.TryGetValue(modName, out List<string>? value))
                {
                    value = [];
                    dependencyTree[modName] = value;
                }

                // Use a HashSet to ensure unique entries in MissingDependenciesToInstall
                var uniqueDependencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var missingDep in missingDeps)
                {
                    _logService.Log($"Found missing dependency: {missingDep}", LogLevel.Info);
                    value.Add(missingDep);

                    // Add the missing dependency to the HashSet
                    if (uniqueDependencies.Add(missingDep))
                    {
                        result.MissingDependenciesToInstall.Add(missingDep);
                    }

                    // Recursively resolve dependencies for the missing dependency
                    var depResolution = await ResolveDependenciesRecursivelyAsync(missingDep, installedMods, visitedMods, dependencyTree);
                    if (!depResolution.Proceed)
                    {
                        result.Proceed = false;
                        return result;
                    }

                    // Add unique dependencies from the recursive result
                    foreach (var dep in depResolution.MissingDependenciesToInstall)
                    {
                        if (uniqueDependencies.Add(dep))
                        {
                            result.MissingDependenciesToInstall.Add(dep);
                        }
                    }

                    result.ModsToEnable.AddRange(depResolution.ModsToEnable);
                    result.ModsToDisable.AddRange(depResolution.ModsToDisable);
                }

                // Aggregate disabled dependencies
                if (disabledDeps.Count > 0)
                {
                    _logService.Log($"Disabled dependencies for {modName}: {string.Join(", ", disabledDeps.Select(m => m.Title))}", LogLevel.Warning);
                    result.ModsToEnable.AddRange(disabledDeps);
                }

                // Aggregate incompatible mods
                if (incompatibleLoaded.Count > 0)
                {
                    _logService.Log($"Incompatible mods for {modName}: {string.Join(", ", incompatibleLoaded.Select(m => m.Title))}", LogLevel.Warning);
                    result.ModsToDisable.AddRange(incompatibleLoaded);
                }
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error resolving dependencies for {modName}: {ex.Message}", ex);
                result.Proceed = false;
            }

            return result;
        }

        private static string BuildDependencyTreeMessage(
             Dictionary<string, List<string>> dependencyTree,
             DependencyResolution resolution)
        {
            var message = new System.Text.StringBuilder();

            if (dependencyTree.Count == 0)
            {
                message.AppendLine("No dependencies were found.");
                return message.ToString();
            }

            void AppendTree(string modName, int level)
            {
                var indent = new string(' ', level * 4);
                message.AppendLine($"{indent}- {modName}");

                if (dependencyTree.TryGetValue(modName, out var dependencies))
                {
                    foreach (var dependency in dependencies)
                    {
                        AppendTree(dependency, level + 1);
                    }
                }
            }

            message.AppendLine("Dependency Tree:");
            AppendTree(dependencyTree.Keys.First(), 0);

            if (resolution.ModsToEnable.Count > 0)
            {
                message.AppendLine("\nDisabled Dependencies:");
                foreach (var mod in resolution.ModsToEnable)
                {
                    message.AppendLine($"- {mod.Title}");
                }
            }

            if (resolution.ModsToDisable.Count > 0)
            {
                message.AppendLine("\nIncompatible Mods:");
                foreach (var mod in resolution.ModsToDisable)
                {
                    message.AppendLine($"- {mod.Title}");
                }
            }

            return message.ToString();
        }
    }
}