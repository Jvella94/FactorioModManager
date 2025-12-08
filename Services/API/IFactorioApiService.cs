using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FactorioModManager.Services.API
{
    public interface IFactorioApiService
    {
        Task<ModDetails?> GetModDetailsAsync(string modName, string? apiKey);
        Task<List<string>> GetRecentlyUpdatedModsAsync(int hoursAgo, string? apiKey);
        void ClearCache(); 
    }

    public class ModDetails
    {
        public string Name { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Category { get; set; }
        public string? SourceUrl { get; set; }
        public string? Homepage { get; set; }
        public string? Changelog { get; set; }
        public int DownloadsCount { get; set; }
        public List<ModRelease> Releases { get; set; } = [];
    }

    public class ModRelease
    {
        public string Version { get; set; } = string.Empty;
        public string? DownloadUrl { get; set; }
        public DateTime ReleasedAt { get; set; }
    }
}
