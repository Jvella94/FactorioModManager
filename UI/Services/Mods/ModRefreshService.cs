using FactorioModManager.Models;
using FactorioModManager.Services.Infrastructure;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FactorioModManager.Services.Mods
{
    public interface IModRefreshService
    {
        Task<(List<(ModInfo Info, bool IsEnabled, System.DateTime? LastUpdated, string? ThumbnailPath, string FilePath)> LatestMods, List<ModGroup> Groups)> RefreshAsync();
    }

    public class ModRefreshService(IModService modService, IModGroupService groupService, IModMetadataService metadataService, ILogService logService) : IModRefreshService
    {
        private readonly IModService _modService = modService;
        private readonly IModGroupService _groupService = groupService;
        private readonly IModMetadataService _metadataService = metadataService;
        private readonly ILogService _logService = logService;

        public async Task<(List<(ModInfo Info, bool IsEnabled, System.DateTime? LastUpdated, string? ThumbnailPath, string FilePath)> LatestMods, List<ModGroup> Groups)> RefreshAsync()
        {
            // Keep this quick and off the UI thread; heavier metadata fetches can happen elsewhere
            return await Task.Run(() =>
            {
                var loadedMods = _modService.LoadAllMods();
                var loadedGroups = _groupService.LoadGroups();

                var latestMods = loadedMods
                    .GroupBy(m => m.Info.Name)
                    .Select(g => g.OrderByDescending(m => m.Info.Version).First())
                    .ToList();

                _logService.LogDebug($"ModRefreshService: loaded {latestMods.Count} latest mods and {loadedGroups.Count} groups");

                return (latestMods, loadedGroups);
            });
        }
    }
}