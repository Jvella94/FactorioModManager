using FactorioModManager.Models.API;
using FactorioModManager.Models.DTO;
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
    public class CachedFactorioApiService(IFactorioApiService innerService) : IFactorioApiService, IDisposable
    {
        private readonly IFactorioApiService _inner = innerService ?? throw new ArgumentNullException(nameof(innerService));
        private readonly Dictionary<string, CacheEntry<ModDetailsShortDTO>> _modDetailsShortCache = [];
        private readonly Dictionary<string, CacheEntry<ModDetailsFullDTO>> _modDetailsFullCache = [];
        private readonly Dictionary<string, CacheEntry<List<string>>> _recentModsCache = [];
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private bool _disposed;

        public async Task<ModDetailsShortDTO?> GetModDetailsAsync(string modName)
        {
            // Check cache first
            await _semaphore.WaitAsync();
            try
            {
                if (_modDetailsShortCache.TryGetValue(modName, out var cached))
                {
                    if (DateTime.UtcNow - cached.Timestamp < Constants.Cache.ApiCacheLifetime)
                    {
                        LogService.Instance.LogDebug($"Using cached mod details for {modName}");
                        return cached.Value;
                    }
                    // Cache expired, remove it
                    _modDetailsShortCache.Remove(modName);
                }
            }
            finally
            {
                _semaphore.Release();
            }

            // Fetch from API (outside lock to allow parallel requests for different mods)
            var result = await _inner.GetModDetailsAsync(modName);

            // Update cache with result
            if (result != null)
            {
                await _semaphore.WaitAsync();
                try
                {
                    _modDetailsShortCache[modName] = new CacheEntry<ModDetailsShortDTO>
                    {
                        Value = result,
                        Timestamp = DateTime.UtcNow
                    };
                }
                finally
                {
                    _semaphore.Release();
                }
            }

            return result;
        }

        public async Task<ModDetailsFullDTO?> GetModDetailsFullAsync(string modName)
        {
            // Check cache first
            await _semaphore.WaitAsync();
            try
            {
                if (_modDetailsFullCache.TryGetValue(modName, out var cached))
                {
                    if (DateTime.UtcNow - cached.Timestamp < Constants.Cache.ApiCacheLifetime)
                    {
                        LogService.Instance.LogDebug($"Using cached full mod details for {modName}");
                        return cached.Value;
                    }
                    // Cache expired, remove it
                    _modDetailsFullCache.Remove(modName);
                }
            }
            finally
            {
                _semaphore.Release();
            }

            // Fetch from API (outside lock)
            var result = await _inner.GetModDetailsFullAsync(modName);

            // Update cache with result
            if (result != null)
            {
                await _semaphore.WaitAsync();
                try
                {
                    _modDetailsFullCache[modName] = new CacheEntry<ModDetailsFullDTO>
                    {
                        Value = result,
                        Timestamp = DateTime.UtcNow
                    };
                }
                finally
                {
                    _semaphore.Release();
                }
            }

            return result;
        }

        public async Task<List<string>> GetRecentlyUpdatedModsAsync(int hoursAgo)
        {
            var cacheKey = $"{hoursAgo}";

            // Check cache first
            await _semaphore.WaitAsync();
            try
            {
                if (_recentModsCache.TryGetValue(cacheKey, out var cached))
                {
                    if (DateTime.UtcNow - cached.Timestamp < TimeSpan.FromMinutes(10))
                    {
                        LogService.Instance.LogDebug($"Using cached recent mods list (last {hoursAgo}h)");
                        return cached.Value;
                    }
                    // Cache expired, remove it
                    _recentModsCache.Remove(cacheKey);
                }
            }
            finally
            {
                _semaphore.Release();
            }

            // Fetch from API (outside lock)
            var result = await _inner.GetRecentlyUpdatedModsAsync(hoursAgo);

            // Update cache with result
            await _semaphore.WaitAsync();
            try
            {
                _recentModsCache[cacheKey] = new CacheEntry<List<string>>
                {
                    Value = result,
                    Timestamp = DateTime.UtcNow
                };
            }
            finally
            {
                _semaphore.Release();
            }

            return result;
        }

        public void ClearCache()
        {
            _semaphore.Wait(); // Synchronous wait for void method
            try
            {
                _modDetailsShortCache.Clear();
                _modDetailsFullCache.Clear();
                _recentModsCache.Clear();
            }
            finally
            {
                _semaphore.Release();
            }

            LogService.Instance.Log("API cache cleared");
        }

        private class CacheEntry<T>
        {
            public T Value { get; set; } = default!;
            public DateTime Timestamp { get; set; }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _semaphore?.Dispose();
            GC.SuppressFinalize(this);
            _disposed = true;
        }
    }
}