using Avalonia.Media.Imaging;
using FactorioModManager.Services.Infrastructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;


namespace FactorioModManager.Services.Mods
{
    public interface IThumbnailCache
    {
        Task<Bitmap?> LoadThumbnailAsync(string thumbnailPath);
        void ClearCache();
        (int CacheSize, int Hits, int Misses, double HitRate) GetCacheStats();
    }

    public class ThumbnailCache(ILogService logService) : IThumbnailCache
    {
        private readonly ILogService _logService = logService;
        private readonly Dictionary<string, WeakReference<Bitmap>> _cache = [];
        private int _cacheHits = 0;
        private int _cacheMisses = 0;

        public async Task<Bitmap?> LoadThumbnailAsync(string thumbnailPath)
        {
            if (string.IsNullOrEmpty(thumbnailPath))
                return null;

            // Check cache first
            if (_cache.TryGetValue(thumbnailPath, out var weakRef))
            {
                if (weakRef.TryGetTarget(out var cachedThumbnail))
                {
                    _cacheHits++;
                    _logService.LogDebug($"Thumbnail cache hit: {thumbnailPath}");
                    return cachedThumbnail;
                }
                else
                {
                    // Weak reference was collected, remove from cache
                    _cache.Remove(thumbnailPath);
                }
            }

            _cacheMisses++;

            return await Task.Run(() =>
            {
                try
                {
                    Bitmap? thumbnail = null;

                    // Load from zip or file
                    if (thumbnailPath.Contains('|'))
                    {
                        var parts = thumbnailPath.Split('|');
                        if (parts.Length == 2)
                        {
                            thumbnail = LoadThumbnailFromZip(parts[0], parts[1]);
                        }
                    }
                    else if (File.Exists(thumbnailPath))
                    {
                        thumbnail = new Bitmap(thumbnailPath);
                    }

                    // Cache the loaded thumbnail with weak reference
                    if (thumbnail != null)
                    {
                        _cache[thumbnailPath] = new WeakReference<Bitmap>(thumbnail);
                        _logService.LogDebug($"Cached thumbnail: {thumbnailPath}");
                    }

                    return thumbnail;
                }
                catch (Exception ex)
                {
                    _logService.LogError($"Error loading thumbnail from {thumbnailPath}: {ex.Message}", ex);
                    return null;
                }
            });
        }

        public void ClearCache()
        {
            _cache.Clear();
            _cacheHits = 0;
            _cacheMisses = 0;
            _logService.Log("Thumbnail cache cleared");
        }

        public (int CacheSize, int Hits, int Misses, double HitRate) GetCacheStats()
        {
            var total = _cacheHits + _cacheMisses;
            var hitRate = total > 0 ? (_cacheHits / (double)total) * 100 : 0;
            return (_cache.Count, _cacheHits, _cacheMisses, hitRate);
        }

        private Bitmap? LoadThumbnailFromZip(string zipPath, string entryPath)
        {
            try
            {
                using var archive = ZipFile.OpenRead(zipPath);
                var entry = archive.GetEntry(entryPath);

                if (entry != null)
                {
                    using var stream = entry.Open();
                    using var memStream = new MemoryStream();
                    stream.CopyTo(memStream);
                    memStream.Position = 0;
                    return new Bitmap(memStream);
                }
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error loading thumbnail from zip {zipPath}: {ex.Message}", ex);
            }

            return null;
        }
    }
}