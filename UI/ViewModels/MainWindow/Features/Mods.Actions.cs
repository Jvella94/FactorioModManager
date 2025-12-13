using FactorioModManager.Models;
using ReactiveUI;
using System.Linq;
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

            if (targetEnabled)
            {
                // Enabling: ensure dependencies are satisfied
                var mandatoryDeps = DependencyHelper.GetMandatoryDependencies(mod.Dependencies);
                var (installedEnabledDeps, installedDisabledDeps, missingDeps) = ClassifyDependencies(mandatoryDeps);

                // Check incompatible mods
                var incompatibleMods = GetIncompatibleMods(mod);

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

                if (incompatibleMods.Count > 0)
                {
                    // Ask if user wants to disable incompatible mods
                    var message =
                        $"The following enabled mods are incompatible with {mod.Title}:\n\n" +
                        string.Join("\n", incompatibleMods.Select(m => m.Title)) +
                        "\n\nDo you want to disable them and continue?";

                    var confirm = await _uiService.ShowConfirmationAsync(
                        "Incompatible Mods",
                        message,
                        null);

                    if (!confirm)
                    {
                        mod.IsEnabled = false;
                        SetStatus($"Cancelled enabling {mod.Title} due to incompatible mods.", LogLevel.Warning);
                        _togglingMod = false;
                        return;
                    }

                    // Disable incompatible mods
                    foreach (var inc in incompatibleMods)
                    {
                        inc.IsEnabled = false;
                        _modService.ToggleMod(inc.Name, false);
                    }
                }

                if (installedDisabledDeps.Count > 0)
                {
                    var message =
                        $"The following mandatory dependencies for {mod.Title} are installed but disabled:\n\n" +
                        string.Join("\n", installedDisabledDeps.Select(m => m.Title)) +
                        "\n\nEnable them now?";

                    var enableDeps = await _uiService.ShowConfirmationAsync(
                        "Enable Dependencies",
                        message,
                        null);

                    if (enableDeps)
                    {
                        foreach (var dep in installedDisabledDeps)
                        {
                            dep.IsEnabled = true;
                            _modService.ToggleMod(dep.Name, true);
                        }
                    }
                    else
                    {
                        // User said no – do not enable main mod
                        mod.IsEnabled = false;
                        SetStatus($"Cancelled enabling {mod.Title} because dependencies were not enabled.", LogLevel.Warning);
                        _togglingMod = false;
                        return;
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
            ApplyModFilter();
            _togglingMod = false;
        }

        private async void RemoveMod(ModViewModel? mod)
        {
            if (mod == null)
                return;

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
            ApplyModFilter();
            this.RaisePropertyChanged(nameof(EnabledCountText));
            SetStatus($"Removed {mod.Title}");
        }
    }
}