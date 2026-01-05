using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FactorioModManager.Models;

namespace FactorioModManager.ViewModels.MainWindow.UpdateHandlers
{
    public sealed class SingleDependencyHandler(IUpdateHost host) : IDependencyHandler
    {
        public bool SuppressDependencyPrompts => false;

        public async Task<bool> PrepareAsync(List<ModViewModel> mods, Dictionary<string, string> plannedUpdates, IProgressReporter progress)
        {
            // Delegate to batch handler so the prepare logic remains the same; only the prompt suppression flag differs.
            var batch = new BatchDependencyHandler(host);
            return await batch.PrepareAsync(mods, plannedUpdates, progress);
        }
    }

    public sealed class BatchDependencyHandler(IUpdateHost host) : IDependencyHandler
    {
        public bool SuppressDependencyPrompts => true;

        public async Task<bool> PrepareAsync(List<ModViewModel> mods, Dictionary<string, string> plannedUpdates, IProgressReporter progress)
        {
            var aggregatedMissing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var combinedEnable = new Dictionary<string, ModViewModel>(StringComparer.OrdinalIgnoreCase);
            var combinedDisable = new Dictionary<string, ModViewModel>(StringComparer.OrdinalIgnoreCase);
            var updateResolutions = new List<(ModViewModel Target, Services.Mods.DependencyResolution Resolution)>();

            foreach (var mod in mods)
            {
                var resolution = await host.DependencyFlow.ResolveForUpdateAsync(mod.Name, mod.LatestVersion!, host.AllMods, plannedUpdates);
                if (!resolution.Proceed)
                {
                    await host.SetStatusAsync("Update cancelled.");
                    return false;
                }

                updateResolutions.Add((mod, resolution));

                if (mod.IsEnabled)
                {
                    foreach (var toEnable in resolution.ModsToEnable)
                        if (!combinedEnable.ContainsKey(toEnable.Name)) combinedEnable[toEnable.Name] = toEnable;
                }

                foreach (var toDisable in resolution.ModsToDisable)
                    if (!combinedDisable.ContainsKey(toDisable.Name)) combinedDisable[toDisable.Name] = toDisable;

                foreach (var missing in resolution.MissingDependenciesToInstall)
                    aggregatedMissing.Add(missing);
            }

            if (aggregatedMissing.Count > 0)
            {
                var previewBuilder = new System.Text.StringBuilder();
                foreach (var (targetMod, resolution) in updateResolutions)
                {
                    var (updateRes, message) = await host.DependencyFlow.BuildUpdatePreviewAsync(targetMod.Name, targetMod.LatestVersion!, host.AllMods, plannedUpdates);
                    previewBuilder.AppendLine(message);
                    previewBuilder.AppendLine();

                    if (targetMod.IsEnabled)
                    {
                        foreach (var e in updateRes.ModsToEnable)
                            if (!combinedEnable.ContainsKey(e.Name)) combinedEnable[e.Name] = e;
                    }

                    foreach (var d in updateRes.ModsToDisable)
                        if (!combinedDisable.ContainsKey(d.Name)) combinedDisable[d.Name] = d;

                    foreach (var missing in updateRes.MissingDependenciesToInstall)
                        aggregatedMissing.Add(missing);
                }

                var combinedMessage = previewBuilder.ToString();
                var confirmCombined = await host.ConfirmDependencyInstallAsync(
                    false,
                    "Install Missing Dependencies",
                    combinedMessage,
                    "Install",
                    "Skip");

                if (!confirmCombined)
                {
                    await host.SetStatusAsync("Update cancelled: missing dependencies not installed", LogLevel.Warning);
                    return false;
                }

                // Apply enable/disable decisions now
                foreach (var kv in combinedEnable.Values)
                {
                    var vm = host.AllMods.FirstOrDefault(m => m.Name == kv.Name);
                    if (vm != null && !vm.IsEnabled)
                    {
                        vm.IsEnabled = true;
                        host.ToggleMod(vm.Name, true);
                    }
                }

                foreach (var kv in combinedDisable.Values)
                {
                    var vm = host.AllMods.FirstOrDefault(m => m.Name == kv.Name);
                    if (vm != null && vm.IsEnabled)
                    {
                        vm.IsEnabled = false;
                        host.ToggleMod(vm.Name, false);
                    }
                }

                var depsToInstall = aggregatedMissing.Where(d => host.AllMods.FirstOrDefault(m => m.Name.Equals(d, StringComparison.OrdinalIgnoreCase)) == null).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

                try { await progress.BeginAsync(mods.Count + depsToInstall.Count); } catch { await progress.BeginAsync(mods.Count); }

                foreach (var depName in depsToInstall)
                {
                    var installResult = await host.InstallModAsync(depName);
                    if (!installResult.Success)
                    {
                        host.LogService.LogWarning($"Failed to install dependency {depName}: {installResult.Error}");
                        await host.SetStatusAsync($"Failed to install dependency {depName}: {installResult.Error}", LogLevel.Warning);
                        return false; // Abort the whole Update All
                    }

                    progress.Increment();
                    await Task.Delay(200);
                }

                try { await host.ForceRefreshAffectedModsAsync(depsToInstall); } catch (Exception ex) { host.LogService.LogDebug($"ForceRefreshAffectedModsAsync failed: {ex.Message}"); }

                return true;
            }

            await progress.BeginAsync(mods.Count);
            return true;
        }
    }
}