using FactorioModManager.Services.Settings;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FactorioModManager.Models.Domain
{
    public interface IModDependencyValidator
    {
        ValidationResult ValidateDependencies(ModInfo mod, IEnumerable<ModInfo> installedMods);

        List<string> GetMissingMandatoryDeps(ModInfo mod, IEnumerable<ModInfo> installedMods);

        List<string> GetIncompatibleMods(ModInfo mod, IEnumerable<ModInfo> installedMods);

        List<string> GetDisabledDependencies(ModInfo mod, IEnumerable<ModInfo> installedMods, Dictionary<string, bool> enabledStates);

        List<string> GetMissingBuiltInDependencies(ModInfo mod);
    }

    public record ValidationResult(
        bool IsValid,
        List<string> MissingMandatory,
        List<string> DisabledDependencies,
        List<string> IncompatibleMods,
        List<string> MissingBuiltIn
    );

    public class ModDependencyValidator(ISettingsService settingsService) : IModDependencyValidator
    {
        private readonly ISettingsService _settingsService = settingsService;

        public ValidationResult ValidateDependencies(ModInfo mod, IEnumerable<ModInfo> installedMods)
        {
            var installedModsList = installedMods.ToList();
            var enabledStates = new Dictionary<string, bool>();

            var missingMandatory = GetMissingMandatoryDeps(mod, installedModsList);
            var incompatibleMods = GetIncompatibleMods(mod, installedModsList);
            var disabledDeps = GetDisabledDependencies(mod, installedModsList, enabledStates);
            var missingBuiltIn = GetMissingBuiltInDependencies(mod);

            var isValid = missingMandatory.Count == 0 &&
                         incompatibleMods.Count == 0 &&
                         disabledDeps.Count == 0 &&
                         missingBuiltIn.Count == 0;

            return new ValidationResult(
                isValid,
                missingMandatory,
                disabledDeps,
                incompatibleMods,
                missingBuiltIn
            );
        }

        public List<string> GetMissingMandatoryDeps(ModInfo mod, IEnumerable<ModInfo> installedMods)
        {
            var mandatoryDeps = Constants.DependencyHelper.GetMandatoryDependencies(mod.Dependencies);
            var installedModNames = new HashSet<string>(
                installedMods.Select(m => m.Name),
                StringComparer.OrdinalIgnoreCase
            );

            return [.. mandatoryDeps
                .Where(dep => !Constants.DependencyHelper.IsGameDependency(dep))
                .Where(dep => !installedModNames.Contains(dep))];
        }

        public List<string> GetIncompatibleMods(ModInfo mod, IEnumerable<ModInfo> installedMods)
        {
            var incompatibleDeps = Constants.DependencyHelper.GetIncompatibleDependencies(mod.Dependencies);
            var installedModNames = new HashSet<string>(
                installedMods.Select(m => m.Name),
                StringComparer.OrdinalIgnoreCase
            );

            return [.. incompatibleDeps.Where(dep => installedModNames.Contains(dep))];
        }

        public List<string> GetDisabledDependencies(
            ModInfo mod,
            IEnumerable<ModInfo> installedMods,
            Dictionary<string, bool> enabledStates)
        {
            var mandatoryDeps = Constants.DependencyHelper.GetMandatoryDependencies(mod.Dependencies);
            var installedModsList = installedMods.ToList();

            return [.. mandatoryDeps
                .Where(dep => !Constants.DependencyHelper.IsGameDependency(dep))
                .Where(dep =>
                {
                    var installedMod = installedModsList.FirstOrDefault(
                        m => m.Name.Equals(dep, StringComparison.OrdinalIgnoreCase)
                    );
                    if (installedMod == null) return false;

                    return enabledStates.TryGetValue(installedMod.Name, out var enabled) && !enabled;
                })];
        }

        public List<string> GetMissingBuiltInDependencies(ModInfo mod)
        {
            var missingBuiltIn = new List<string>();
            var mandatoryDeps = Constants.DependencyHelper.GetMandatoryDependencies(mod.Dependencies);

            foreach (var dep in mandatoryDeps)
            {
                switch (dep.ToLowerInvariant())
                {
                    case "base":
                        // Always available
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
    }
}