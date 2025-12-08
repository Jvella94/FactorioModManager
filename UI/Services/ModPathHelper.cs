using System;
using System.IO;
using System.Runtime.InteropServices;

namespace FactorioModManager.Services
{
    public static class ModPathHelper
    {
        public static string GetModsDirectory()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Windows: %appdata%\Factorio\mods
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                return Path.Combine(appData, "Factorio", "mods");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // Linux: ~/.factorio/mods
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                return Path.Combine(home, ".factorio", "mods");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // macOS: ~/Library/Application Support/factorio/mods
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                return Path.Combine(home, "Library", "Application Support", "factorio", "mods");
            }

            throw new PlatformNotSupportedException("Unsupported operating system");
        }

        public static string GetModListPath()
        {
            var modsDir = GetModsDirectory();
            return Path.Combine(modsDir, "mod-list.json");
        }

        public static string GetPlayerDataPath()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Windows: %appdata%\Factorio\player-data.json
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                return Path.Combine(appData, "Factorio", "player-data.json");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // Linux: ~/.factorio/player-data.json
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                return Path.Combine(home, ".factorio", "player-data.json");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // macOS: ~/Library/Application Support/factorio/player-data.json
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                return Path.Combine(home, "Library", "Application Support", "factorio", "player-data.json");
            }

            throw new PlatformNotSupportedException("Unsupported operating system");
        }
    }
}