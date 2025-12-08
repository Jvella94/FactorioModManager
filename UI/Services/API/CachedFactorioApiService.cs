using FactorioModManager.Models.API;
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
        private readonly Dictionary<string, CacheEntry<ModDetailsShort>> _modDetailsShortCache = [];
        private readonly Dictionary<string, CacheEntry<ModDetailsFull>> _modDetailsFullCache = [];
        private readonly Dictionary<string, CacheEntry<List<string>>> _recentModsCache = [];
        private readonly Lock _lock = new();

        public async Task<ModDetailsShort?> GetModDetailsAsync(string modName)
        {
            lock (_lock)
            {
                if (_modDetailsShortCache.TryGetValue(modName, out var cached))
                {
                    if (DateTime.Now - cached.Timestamp < Constants.Cache.ApiCacheLifetime)
                    {
                        LogService.Instance.LogDebug($"Using cached mod details for {modName}");
                        return cached.Value;
                    }
                    else
                    {
                        _modDetailsShortCache.Remove(modName);
                    }
                }
            }

            var result = await _inner.GetModDetailsAsync(modName);

            if (result != null)
            {
                lock (_lock)
                {
                    _modDetailsShortCache[modName] = new CacheEntry<ModDetailsShort>
                    {
                        Value = result,
                        Timestamp = DateTime.Now
                    };
                }
            }

            return result;
        }

        public async Task<ModDetailsFull?> GetModDetailsFullAsync(string modName)
        {
            lock (_lock)
            {
                if (_modDetailsFullCache.TryGetValue(modName, out var cached))
                {
                    if (DateTime.Now - cached.Timestamp < Constants.Cache.ApiCacheLifetime)
                    {
                        LogService.Instance.LogDebug($"Using cached mod details for {modName}");
                        return cached.Value;
                    }
                    else
                    {
                        _modDetailsFullCache.Remove(modName);
                    }
                }
            }

            var result = await _inner.GetModDetailsFullAsync(modName);

            if (result != null)
            {
                lock (_lock)
                {
                    _modDetailsFullCache[modName] = new CacheEntry<ModDetailsFull>
                    {
                        Value = result,
                        Timestamp = DateTime.Now
                    };
                }
            }

            return result;
        }

        public async Task<List<string>> GetRecentlyUpdatedModsAsync(int hoursAgo)
        {
            var cacheKey = $"{hoursAgo}";

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

            var result = await _inner.GetRecentlyUpdatedModsAsync(hoursAgo);

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
                _modDetailsShortCache.Clear();
                _modDetailsFullCache.Clear();
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