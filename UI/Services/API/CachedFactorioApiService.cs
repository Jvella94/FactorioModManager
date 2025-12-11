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
    public class CachedFactorioApiService(IFactorioApiService innerService, ILogService logService) : IFactorioApiService, IDisposable
    {
        private readonly IFactorioApiService _inner = innerService ?? throw new ArgumentNullException(nameof(innerService));
        private readonly ILogService _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        private readonly Dictionary<string, CacheEntry<ModDetailsShortDTO>> _modDetailsShortCache = [];
        private readonly Dictionary<string, CacheEntry<ModDetailsFullDTO>> _modDetailsFullCache = [];
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private bool _disposed;

        public async Task<ModDetailsShortDTO?> GetModDetailsAsync(string modName)
        {
            // Check cache WITHOUT holding lock across API call
            await _semaphore.WaitAsync();
            try
            {
                if (_modDetailsShortCache.TryGetValue(modName, out var cached))
                {
                    if (DateTime.UtcNow - cached.Timestamp < Constants.Cache.ApiCacheLifetime)
                    {
                        _logService.LogDebug($"Using cached mod details for {modName}");
                        return cached.Value;
                    }
                    _modDetailsShortCache.Remove(modName);
                }
            }
            finally
            {
                _semaphore.Release(); // Release BEFORE slow API call
            }

            var result = await _inner.GetModDetailsAsync(modName);

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
            await _semaphore.WaitAsync();
            try
            {
                if (_modDetailsFullCache.TryGetValue(modName, out var cached))
                {
                    if (DateTime.UtcNow - cached.Timestamp < Constants.Cache.ApiCacheLifetime)
                    {
                        _logService.LogDebug($"Using cached full mod details for {modName}");
                        return cached.Value;
                    }
                    _modDetailsFullCache.Remove(modName);
                }
            }
            finally
            {
                _semaphore.Release();
            }

            var result = await _inner.GetModDetailsFullAsync(modName);

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

        // Updated signature to include isManual and forward
        public async Task<List<string>> GetRecentlyUpdatedModsAsync(int hoursAgo, bool isManual = false)
        {
            return await _inner.GetRecentlyUpdatedModsAsync(hoursAgo, isManual);
        }

        /// <summary>
        /// Downloads are not cached - pass through to inner service
        /// </summary>
        public Task DownloadModAsync(
            string downloadUrl,
            string destinationPath,
            IProgress<(long bytesDownloaded, long? totalBytes)>? progress = null,
            CancellationToken cancellationToken = default)
        {
            // Pass through - don't cache downloads
            return _inner.DownloadModAsync(downloadUrl, destinationPath, progress, cancellationToken);
        }

        public void ClearCache()
        {
            _semaphore.Wait();
            try
            {
                _modDetailsShortCache.Clear();
                _modDetailsFullCache.Clear();
            }
            finally
            {
                _semaphore.Release();
            }

            _logService.Log("API cache cleared");
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