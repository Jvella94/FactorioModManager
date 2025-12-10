using Avalonia.Controls;
using FactorioModManager.Domain;
using FactorioModManager.Models;
using FactorioModManager.Services.Infrastructure;
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
            ModInfo modInfo,
            IEnumerable<ModViewModel> installedMods);

        Task<DependencyResolution> ResolveForUpdateAsync(
            string modName,
            string version,
            IEnumerable<ModViewModel> installedMods);

        bool ValidateMandatoryDependencies(ModInfo modInfo, IEnumerable<ModViewModel> installedMods);
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
        IModDependencyValidator validator,
        IUIService uiService,
        ILogService logService) : IModDependencyResolver
    {
        private readonly IModDependencyValidator _validator = validator;
        private readonly IUIService _uiService = uiService;
        private readonly ILogService _logService = logService;

        public async Task<DependencyResolution> ResolveForInstallAsync(
            ModInfo modInfo,
            IEnumerable<ModViewModel> installedMods)
        {
            var result = new DependencyResolution
            {
                Proceed = true,
                InstallEnabled = true
            };

            var modTitle = modInfo.Title ?? modInfo.Name;
            var deps = modInfo.Dependencies as IReadOnlyList<string>;
            var mandatoryRaw = Constants.DependencyHelper.GetMandatoryDependencies(deps);

            // Check built-in dependencies
            var missingBuiltIn = _validator.GetMissingBuiltInDependencies(modInfo);
            if (missingBuiltIn.Count > 0)
            {
                var msg = $"The mod {modTitle} requires official content that is not detected:\n\n" +
                         string.Join("\n", missingBuiltIn) +
                         "\n\nThese cannot be installed via the mod portal.";

                await _uiService.ShowMessageAsync("Missing Official Content", msg);
                result.Proceed = false;
                return result;
            }

            // Filter out game dependencies
            var mandatoryDeps = mandatoryRaw
                .Where(d => !Constants.DependencyHelper.GameDependencies.Contains(d, StringComparer.OrdinalIgnoreCase))
                .ToList();

            var incompatibleDeps = Constants.DependencyHelper.GetIncompatibleDependencies(deps);

            // Check missing and disabled dependencies
            var (missingDeps, installedDisabledDeps) = ClassifyDependencies(mandatoryDeps, installedMods);

            // Check incompatible mods
            var incompatibleLoaded = installedMods
                .Where(m => m.IsEnabled && incompatibleDeps.Contains(m.Name))
                .ToList();

            // Handle missing dependencies
            if (missingDeps.Count > 0)
            {
                var installMissing = await _uiService.ShowConfirmationAsync(
                    "Missing Dependencies",
                    $"The following mandatory dependencies for {modTitle} are not installed:\n\n" +
                    string.Join("\n", missingDeps) +
                    "\n\nDo you want to install these dependencies as well?",
                    null);

                if (!installMissing)
                {
                    var installDisabled = await _uiService.ShowConfirmationAsync(
                        "Install Disabled?",
                        $"{modTitle} will not be loadable without these dependencies.\n\n" +
                        "Install the mod disabled instead?",
                        null);

                    if (!installDisabled)
                    {
                        result.Proceed = false;
                        return result;
                    }

                    result.InstallEnabled = false;
                }
                else
                {
                    result.MissingDependenciesToInstall.AddRange(missingDeps);
                }
            }

            // Handle disabled dependencies
            if (installedDisabledDeps.Count > 0 && result.InstallEnabled)
            {
                var enableDeps = await _uiService.ShowConfirmationAsync(
                    "Enable Dependencies",
                    $"The following mandatory dependencies for {modTitle} are installed but disabled:\n\n" +
                    string.Join("\n", installedDisabledDeps.Select(m => m.Title)) +
                    "\n\nEnable them now?",
                    null);

                if (enableDeps)
                {
                    result.ModsToEnable.AddRange(installedDisabledDeps);
                }
                else
                {
                    var installDisabled = await _uiService.ShowConfirmationAsync(
                        "Install Disabled?",
                        $"{modTitle} will be installed disabled.\n\nContinue?",
                        null);

                    if (!installDisabled)
                    {
                        result.Proceed = false;
                        return result;
                    }

                    result.InstallEnabled = false;
                }
            }

            // Handle incompatible mods
            if (incompatibleLoaded.Count > 0 && result.InstallEnabled)
            {
                var disableIncompatibles = await _uiService.ShowConfirmationAsync(
                    "Incompatible Mods",
                    $"The following mods are incompatible with {modTitle}:\n\n" +
                    string.Join("\n", incompatibleLoaded.Select(m => m.Title)) +
                    "\n\nDisable them?",
                    null);

                if (disableIncompatibles)
                {
                    result.ModsToDisable.AddRange(incompatibleLoaded);
                }
                else
                {
                    result.InstallEnabled = false;
                }
            }

            return result;
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

        public bool ValidateMandatoryDependencies(ModInfo modInfo, IEnumerable<ModViewModel> installedMods)
        {
            var mandatoryDeps = Constants.DependencyHelper.GetMandatoryDependencies(modInfo.Dependencies);
            var (missing, _) = ClassifyDependencies(mandatoryDeps, installedMods);

            return missing.Count == 0;
        }

        private static (List<string> Missing, List<ModViewModel> Disabled) ClassifyDependencies(
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
    }
}