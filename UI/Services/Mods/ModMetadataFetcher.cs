using FactorioModManager.Services.API;
using FactorioModManager.Services.Infrastructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FactorioModManager.Services.Mods
{
    public interface IModMetadataFetcher
    {
        Task FetchMissingMetadataAsync(IEnumerable<ViewModels.ModViewModel> modsSnapshot);
    }

    public class ModMetadataFetcher(
        IFactorioApiService apiService,
        IModMetadataService metadataService,
        IThumbnailCache thumbnailCache,
        IUIService uiService,
        ILogService logService) : IModMetadataFetcher
    {
        private readonly IFactorioApiService _apiService = apiService;
        private readonly IModMetadataService _metadataService = metadataService;
        private readonly IThumbnailCache _thumbnailCache = thumbnailCache;
        private readonly IUIService _uiService = uiService;
        private readonly ILogService _logService = logService;

        public async Task FetchMissingMetadataAsync(IEnumerable<ViewModels.ModViewModel> modsSnapshot)
        {
            var modsNeedingMetadata = modsSnapshot
                .Where(m => _metadataService.NeedsMetadaUpdate(m.Name))
                .ToList();

            if (modsNeedingMetadata.Count == 0)
                return;

            var currentIndex = 0;
            foreach (var mod in modsNeedingMetadata)
            {
                currentIndex++;
                try
                {
                    // Fetch details
                    var details = await _apiService.GetModDetailsFullAsync(mod.Name);
                    if (details != null)
                    {
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
                }
                catch (Exception ex)
                {
                    _logService.LogError($"Error fetching metadata for {mod.Name}", ex);
                }

                // compute size on disk
                if (mod.FilePath is not null)
                {
                    try
                    {
                        var size = ComputeSizeOnDisk(mod.FilePath);
                        _metadataService.UpdateSizeOnDisk(mod.Name, size);
                        await _uiService.InvokeAsync(() => mod.SizeOnDiskBytes = size);
                    }
                    catch (Exception ex)
                    {
                        _logService.LogWarning($"Failed to compute size for {mod.Name}: {ex.Message}");
                    }
                }
                else
                {
                }

                // Try to load thumbnail (fire and forget)
                if (!string.IsNullOrEmpty(mod.ThumbnailPath))
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var thumbnail = await _thumbnailCache.LoadThumbnailAsync(mod.ThumbnailPath);
                            await _uiService.InvokeAsync(() => mod.Thumbnail = thumbnail);
                        }
                        catch (Exception ex)
                        {
                            _logService.LogWarning($"Thumbnail load failed for {mod.Name}: {ex.Message}");
                        }
                    });
                }

                await Task.Delay(100); // rate limiting
            }
        }

        private static long ComputeSizeOnDisk(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path))
                    return 0;

                if (File.Exists(path))
                {
                    var fi = new FileInfo(path);
                    return fi.Length;
                }

                if (Directory.Exists(path))
                {
                    long total = 0;
                    foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                    {
                        try
                        {
                            var fi = new FileInfo(file);
                            total += fi.Length;
                        }
                        catch
                        {
                            // Skip files we cannot access
                        }
                    }
                    return total;
                }
            }
            catch
            {
                // Swallow and return 0 on failure
            }

            return 0;
        }
    }
}