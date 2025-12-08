using FactorioModManager.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FactorioModManager.Services
{
    public interface IModService
    {
        List<(ModInfo Info, bool IsEnabled, DateTime? LastUpdated, string? ThumbnailPath, string FilePath)> LoadAllMods();
        void ToggleMod(string modName, bool enabled);
        void RemoveMod(string modName);
        string GetModsDirectory();
        Task DownloadVersionAsync(string modName, string version, string v);
        void DeleteVersion(string modName, string version);
        void RefreshInstalledCounts(string modName);
        HashSet<string> GetInstalledVersions(string modName);
        void RefreshInstalledVersions(string modName);
    }
}
