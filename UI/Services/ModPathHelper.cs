using System;
using System.IO;
using System.Runtime.InteropServices;

namespace FactorioModManager.Services
{
    public static class ModPathHelper
    {
        private static string GetFactorioBasePath()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Factorio"
                );
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".factorio"
                );
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Library", "Application Support", "factorio"
                );
            }
            throw new PlatformNotSupportedException("Unsupported operating system");
        }

        public static string GetModsDirectory() =>
            Path.Combine(GetFactorioBasePath(), "mods");

        public static string GetModListPath() =>
            Path.Combine(GetModsDirectory(), "mod-list.json");

        public static string GetPlayerDataPath() =>
            Path.Combine(GetFactorioBasePath(), "player-data.json");
    }
}