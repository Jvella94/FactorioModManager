using FactorioModManager.Services.Infrastructure;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FactorioModManager.Services.API
{
    /// <summary>
    /// Caches API responses to reduce unnecessary network calls
    /// </summary>
    public class CachedFactorioApiService(IFactorioApiService innerService) : IFactorioApiService
    {
        private readonly IFactorioApiService _inner = innerService ?? throw new ArgumentNullException(nameof(innerService));
        private readonly Dictionary<string, CacheEntry<ModDetails>> _modDetailsCache = [];
        private readonly Dictionary<string, CacheEntry<List<string>>> _recentModsCache = [];
        private readonly Lock _lock = new();

        public async Task<ModDetails?> GetModDetailsAsync(string modName, string? apiKey)
        {
            lock (_lock)
            {
                if (_modDetailsCache.TryGetValue(modName, out var cached))
                {
                    if (DateTime.Now - cached.Timestamp < Constants.Cache.ApiCacheLifetime)
                    {
                        LogService.Instance.LogDebug($"Using cached mod details for {modName}");
                        return cached.Value;
                    }
                    else
                    {
                        _modDetailsCache.Remove(modName);
                    }
                }
            }

            var result = await _inner.GetModDetailsAsync(modName, apiKey);

            if (result != null)
            {
                lock (_lock)
                {
                    _modDetailsCache[modName] = new CacheEntry<ModDetails>
                    {
                        Value = result,
                        Timestamp = DateTime.Now
                    };
                }
            }

            return result;
        }

        public async Task<List<string>> GetRecentlyUpdatedModsAsync(int hoursAgo, string? apiKey)
        {
            var cacheKey = $"{hoursAgo}_{apiKey ?? "nokey"}";

            lock (_lock)
            {
                if (_recentModsCache.TryGetValue(cacheKey, out var cached))
                {
                    if (DateTime.Now - cached.Timestamp < TimeSpan.FromMinutes(10))
                    {
                        LogService.Instance.LogDebug($"Using cached recent mods list");
                        return cached.Value;
                    }
                    else
                    {
                        _recentModsCache.Remove(cacheKey);
                    }
                }
            }

            var result = await _inner.GetRecentlyUpdatedModsAsync(hoursAgo, apiKey);

            lock (_lock)
            {
                _recentModsCache[cacheKey] = new CacheEntry<List<string>>
                {
                    Value = result,
                    Timestamp = DateTime.Now
                };
            }

            return result;
        }

        public void ClearCache()
        {
            lock (_lock)
            {
                _modDetailsCache.Clear();
                _recentModsCache.Clear();
            }
            LogService.Instance.Log("API cache cleared");
        }

        private class CacheEntry<T>
        {
            public T Value { get; set; } = default!;
            public DateTime Timestamp { get; set; }
        }
    }
}
