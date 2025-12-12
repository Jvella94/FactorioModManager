using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using FactorioModManager.Services.Infrastructure;

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
        public static string? DetectFactorioExecutable(ILogService log)
        {
            log.LogDebug("Starting Factorio executable detection");

            var root = DetectFactorioRoot(log);
            if (!string.IsNullOrEmpty(root))
            {
                log.LogDebug($"Detected Factorio root at: {root}");
                var exe = GetExecutableFromRoot(root, log);
                if (!string.IsNullOrEmpty(exe))
                    return exe;
            }

            log.LogDebug("No Factorio root detected; falling back to OS-specific search");

            return null;
        }

        /// <summary>
        /// Detects the Factorio installation root folder (the folder that contains 'data' and 'bin' or the .app bundle on macOS)
        /// </summary>
        public static string? DetectFactorioRoot(ILogService log)
        {
            log.LogDebug("DetectFactorioRoot: searching common installation roots");
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return DetectWindowsFactorio(log);
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return DetectLinuxFactorio(log);
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return DetectMacOSFactorio(log);
            else
            {
                log.LogWarning("DetectFactorioRoot: unsupported OS platform");
                return null;
            }
        }

        /// <summary>
        /// Derives the executable path from an installation root (returns null if not found)
        /// </summary>
        public static string? GetExecutableFromRoot(string root, ILogService log)
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    // macOS bundle layout
                    var candidate = Path.Combine(root, "Contents", "MacOS", GetExecutableName());
                    log.LogDebug($"GetExecutableFromRoot (macOS): checking {candidate}");
                    if (File.Exists(candidate))
                        return candidate;

                    // If root pointed directly to Contents or Resources
                    candidate = Path.Combine(root, "MacOS", GetExecutableName());
                    log.LogDebug($"GetExecutableFromRoot (macOS alt): checking {candidate}");
                    if (File.Exists(candidate))
                        return candidate;
                }
                else
                {
                    var candidate = Path.Combine(root, "bin", "x64", GetExecutableName());
                    log.LogDebug($"GetExecutableFromRoot: checking {candidate}");
                    if (File.Exists(candidate))
                        return candidate;

                    // Fallback to root/bin/<exe>
                    candidate = Path.Combine(root, "bin", GetExecutableName());
                    log.LogDebug($"GetExecutableFromRoot fallback: checking {candidate}");
                    if (File.Exists(candidate))
                        return candidate;

                    // Also check root directly
                    candidate = Path.Combine(root, GetExecutableName());
                    log.LogDebug($"GetExecutableFromRoot direct: checking {candidate}");
                    if (File.Exists(candidate))
                        return candidate;
                }
            }
            catch (Exception ex)
            {
                log.LogWarning($"Error deriving executable from root '{root}': {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Derives the data directory path from an installation root (returns null if not found)
        /// </summary>
        public static string? GetDataFromRoot(string root, ILogService log)
        {
            try
            {
                // Common location
                var candidate = Path.Combine(root, "data");
                log.LogDebug($"GetDataFromRoot: checking {candidate}");
                if (Directory.Exists(candidate))
                    return candidate;

                // macOS app bundle resources
                candidate = Path.Combine(root, "Contents", "data");
                log.LogDebug($"GetDataFromRoot (macOS): checking {candidate}");
                if (Directory.Exists(candidate))
                    return candidate;

                // Some steam layouts might embed factorio.app further down
                candidate = Path.Combine(root, "factorio.app", "Contents", "data");
                log.LogDebug($"GetDataFromRoot (alt macOS): checking {candidate}");
                if (Directory.Exists(candidate))
                    return candidate;
            }
            catch (Exception ex)
            {
                log.LogWarning($"Error deriving data dir from root '{root}': {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Attempts to resolve the Factorio "data" directory.
        /// Priority:
        /// 1. explicit configured path (if exists)
        /// 2. derive from provided exePath
        /// 3. auto-detect root and derive
        /// Returns null if not found.
        /// </summary>
        public static string? GetFactorioDataPath(ILogService log, string? configuredPath = null, string? exePath = null)
        {
            log.LogDebug($"GetFactorioDataPath: configuredPath='{configuredPath}', exePath='{exePath}'");

            if (!string.IsNullOrEmpty(configuredPath))
            {
                log.LogDebug($"Checking configured data path: {configuredPath}");
                if (Directory.Exists(configuredPath))
                {
                    log.Log($"Using configured Factorio data path: {configuredPath}");
                    return configuredPath;
                }
                log.LogWarning($"Configured Factorio data path does not exist: {configuredPath}");
            }

            string? TryFromExe(string? path)
            {
                try
                {
                    log.LogDebug($"TryFromExe: checking exe path '{path}'");
                    if (string.IsNullOrEmpty(path) || !File.Exists(path))
                    {
                        log.LogDebug($"Exe path invalid or not found: '{path}'");
                        return null;
                    }

                    var exeDir = Path.GetDirectoryName(path);
                    if (string.IsNullOrEmpty(exeDir))
                    {
                        log.LogDebug($"Could not get directory for exe path: {path}");
                        return null;
                    }

                    // Walk up to the root that should contain 'data'
                    var binFolder = Path.GetDirectoryName(exeDir);
                    var rootDir = Path.GetDirectoryName(binFolder);
                    if (string.IsNullOrEmpty(rootDir))
                    {
                        log.LogDebug($"Could not resolve root directory from exe dir: {exeDir}");
                        return null;
                    }

                    var dataDir = Path.Combine(rootDir, "data");
                    log.LogDebug($"Derived data dir from exe: {dataDir}");
                    if (Directory.Exists(dataDir))
                    {
                        log.Log($"Using data directory derived from exe: {dataDir}");
                        return dataDir;
                    }

                    // macOS app layout fallback
                    dataDir = Path.Combine(rootDir, "Contents", "data");
                    log.LogDebug($"Derived macOS-style data dir from exe: {dataDir}");
                    if (Directory.Exists(dataDir))
                    {
                        log.Log($"Using data directory derived from exe: {dataDir}");
                        return dataDir;
                    }

                    log.LogDebug($"Derived data dir does not exist: {dataDir}");
                    return null;
                }
                catch (Exception ex)
                {
                    log.LogWarning($"Exception while deriving data dir from exe '{path}': {ex.Message}");
                    return null;
                }
            }

            var fromExe = TryFromExe(exePath);
            if (!string.IsNullOrEmpty(fromExe))
                return fromExe;

            // Try detecting root and deriving data from it
            var root = DetectFactorioRoot(log);
            if (!string.IsNullOrEmpty(root))
            {
                var data = GetDataFromRoot(root, log);
                if (!string.IsNullOrEmpty(data))
                    return data;

                // If data not found directly under root, try deriving from exe within that root
                var exe = GetExecutableFromRoot(root, log);
                if (!string.IsNullOrEmpty(exe))
                {
                    fromExe = TryFromExe(exe);
                    if (!string.IsNullOrEmpty(fromExe))
                        return fromExe;
                }
            }

            log.LogWarning("Failed to locate Factorio data directory");
            return null;
        }

        private static string? DetectWindowsFactorio(ILogService log)
        {
            // Keep legacy checks for executable locations as a last resort
            log.LogDebug("DetectWindowsFactorio: searching common Windows executable paths");

            var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            var candidates = new List<string>
            {
                // Standalone installs
                Path.Combine(pf, "Factorio"),
                Path.Combine(pf86, "Factorio"),

                // Steam installs
                Path.Combine(pf86, "Steam", "steamapps", "common", "Factorio"),
                Path.Combine(pf, "Steam", "steamapps", "common", "Factorio"),

                // Common custom library locations
                "C:\\Games\\SteamLibrary\\steamapps\\common\\Factorio",
                "D:\\SteamLibrary\\steamapps\\common\\Factorio",
                "E:\\SteamLibrary\\steamapps\\common\\Factorio",
                "C:\\Games\\Steam\\steamapps\\common\\Factorio",
                "D:\\Steam\\steamapps\\common\\Factorio",
                "E:\\Steam\\steamapps\\common\\Factorio"
            };
            (bool pathFound, string? value) = CheckFolderCandidates(log, candidates);
            if (pathFound)
            {
                return value;
            }

            log.LogDebug("DetectWindowsFactorio: no folder found");
            return null;
        }

        private static string? DetectLinuxFactorio(ILogService log)
        {
            log.LogDebug("DetectLinuxFactorio: searching common Linux executable paths");
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var candidates = new List<string>
            {
                Path.Combine(home, ".factorio"),
                Path.Combine(home, ".steam", "steam", "steamapps", "common", "Factorio"),
                Path.Combine(home, ".local", "share", "Steam", "steamapps", "common", "Factorio"),
                "/opt/factorio",
                "/usr/share/games/factorio",
                "/usr/local/games/factorio"
            };
            (bool pathFound, string? value) = CheckFolderCandidates(log, candidates);
            if (pathFound)
            {
                return value;
            }

            log.LogDebug("DetectWindowsFactorio: no folder found");
            return null;
        }

        private static string? DetectMacOSFactorio(ILogService log)
        {
            log.LogDebug("DetectMacOSFactorio: searching common macOS executable paths");
            var candidates = new List<string>
            {
                "/Applications/factorio.app",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "Steam", "steamapps", "common", "Factorio", "factorio.app")
            };
            (bool pathFound, string? value) = CheckFolderCandidates(log, candidates);
            if (pathFound)
            {
                return value;
            }

            log.LogDebug("DetectMacOSFactorio: no exe found");
            return null;
        }

        private static (bool pathFound, string? value) CheckFolderCandidates(ILogService log, List<string> candidates)
        {
            foreach (var path in candidates)
            {
                try
                {
                    log.LogDebug($"Checking {path}");
                    if (Directory.Exists(path))
                    {
                        log.Log($"Found Factorio folder at: {path}");
                        return (pathFound: true, value: path);
                    }
                }
                catch (Exception ex)
                {
                    log.LogWarning($"Error checking path '{path}': {ex.Message}");
                }
            }

            return (pathFound: false, value: null);
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