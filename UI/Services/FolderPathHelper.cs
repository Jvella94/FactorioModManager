using System;
using System.IO;
using System.Runtime.InteropServices;

namespace FactorioModManager.Services
{
    public static class FolderPathHelper
    {
        private static string GetFactorioBaseModPath()
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
            Path.Combine(GetFactorioBaseModPath(), "mods");

        public static string GetModListPath() =>
            Path.Combine(GetModsDirectory(), "mod-list.json");

        public static string GetPlayerDataPath() =>
            Path.Combine(GetFactorioBaseModPath(), "player-data.json");

        /// <summary>
        /// Attempts to detect the Factorio executable path based on the mods directory location
        /// </summary>
        public static string? DetectFactorioExecutable()
        {
            // Try to find from common locations based on OS
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return DetectWindowsFactorio();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return DetectLinuxFactorio();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return DetectMacOSFactorio();
            }

            return null;
        }

        private static string? DetectWindowsFactorio()
        {
            // Common Windows paths
            var searchPaths = new[]
            {
                // Steam installations
                @"C:\Program Files (x86)\Steam\steamapps\common\Factorio\bin\x64\factorio.exe",
                @"C:\Program Files\Steam\steamapps\common\Factorio\bin\x64\factorio.exe",

                // Standalone installations
                @"C:\Program Files\Factorio\bin\x64\factorio.exe",
                @"C:\Program Files (x86)\Factorio\bin\x64\factorio.exe",

                // Custom Steam library on other drives
                @"D:\SteamLibrary\steamapps\common\Factorio\bin\x64\factorio.exe",
                @"D:\Steam\steamapps\common\Factorio\bin\x64\factorio.exe",
                @"E:\SteamLibrary\steamapps\common\Factorio\bin\x64\factorio.exe",
                @"E:\Steam\steamapps\common\Factorio\bin\x64\factorio.exe",
            };

            foreach (var path in searchPaths)
            {
                if (File.Exists(path))
                    return path;
            }

            // Try to find Steam libraries automatically
            var steamPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "Steam");

            if (Directory.Exists(steamPath))
            {
                var steamFactorio = Path.Combine(steamPath, @"steamapps\common\Factorio\bin\x64\factorio.exe");
                if (File.Exists(steamFactorio))
                    return steamFactorio;
            }

            return null;
        }

        private static string? DetectLinuxFactorio()
        {
            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            var searchPaths = new[]
            {
                // Steam installation
                Path.Combine(homeDir, ".steam/steam/steamapps/common/Factorio/bin/x64/factorio"),
                Path.Combine(homeDir, ".local/share/Steam/steamapps/common/Factorio/bin/x64/factorio"),

                // Standalone installation
                Path.Combine(homeDir, ".factorio/bin/x64/factorio"),
                "/opt/factorio/bin/x64/factorio",
                "/usr/share/games/factorio/bin/x64/factorio",
                "/usr/local/games/factorio/bin/x64/factorio",
            };

            foreach (var path in searchPaths)
            {
                if (File.Exists(path))
                    return path;
            }

            return null;
        }

        private static string? DetectMacOSFactorio()
        {
            var searchPaths = new[]
            {
                // Standard installation
                "/Applications/factorio.app/Contents/MacOS/factorio",

                // Steam installation
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Library/Application Support/Steam/steamapps/common/Factorio/factorio.app/Contents/MacOS/factorio"
                ),
            };

            foreach (var path in searchPaths)
            {
                if (File.Exists(path))
                    return path;
            }

            return null;
        }

        /// <summary>
        /// Gets the default executable name based on current OS
        /// </summary>
        public static string GetExecutableName()
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "factorio.exe"
                : "factorio";
        }
    }
}