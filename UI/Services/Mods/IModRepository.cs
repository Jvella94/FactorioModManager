using FactorioModManager.Models;
using System;
using System.Collections.Generic;

namespace FactorioModManager.Services.Mods
{
    public interface IModRepository
    {
        List<(ModInfo Info, bool IsEnabled, DateTime? LastUpdated, string? ThumbnailPath, string FilePath)> LoadAllMods();
        ModInfo? ReadModInfo(string filePath);
        void SaveModState(string modName, bool enabled);
        Dictionary<string, bool> LoadEnabledStates();
        void SaveEnabledStates(Dictionary<string, bool> states);
    }
}