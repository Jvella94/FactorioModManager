using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace FactorioModManager
{
    /// <summary>
    /// Application-wide constants for Factorio Mod Manager
    /// </summary>
    public static class Constants
    {
        public const string AboutMessage = "Factorio Mod Manager\nVersion 1.0.0\n\n" +
                                     "A modern mod manager for Factorio.\n\n" +
                                     "Features:\n" +
                                     "• Manage and organize mods\n" +
                                     "• Check for updates\n" +
                                     "• Group management\n" +
                                     "• Download from Mod Portal";

        private static readonly Lazy<Bitmap> _lazyPlaceholder = new(() =>
        {
            try
            {
                var uri = new Uri("avares://FactorioModManager/Assets/FMM.png");
                return new Bitmap(AssetLoader.Open(uri));
            }
            catch (Exception)
            {
                return new WriteableBitmap(
                    new PixelSize(1, 1),
                    new Vector(96, 96),
                    PixelFormat.Bgra8888,
                    AlphaFormat.Premul);
            }
        });

        public static Bitmap LoadPlaceholderThumbnail() => _lazyPlaceholder.Value;

        /// <summary>
        /// Official Factorio game dependencies
        /// </summary>
        public static class GameDependencies
        {
            /// <summary>
            /// List of all official game dependencies that come with Factorio
            /// </summary>
            public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
            {
                "base",
                "space-age",
                "quality",
                "elevated-rails"
            };

            /// <summary>
            /// Checks if a dependency name is an official game dependency
            /// </summary>
            /// <param name="dependencyName">The dependency name to check</param>
            /// <returns>True if it's a game dependency, false otherwise</returns>
            public static bool IsGameDependency(string dependencyName)
            {
                return All.Contains(dependencyName);
            }
        }

        /// <summary>
        /// Character separators used for parsing strings
        /// </summary>
        public static class Separators
        {
            /// <summary>
            /// Characters used to separate dependency names from version constraints
            /// Example: "base >= 2.0" splits into ["base", "2.0"]
            /// </summary>
            public static readonly char[] Dependency = [' ', '>', '<', '=', '!', '?', '(', ')'];
        }

        /// <summary>
        /// URLs for Factorio mod portal and API
        /// </summary>
        public static class Urls
        {
            /// <summary>
            /// Base URL for the Factorio mod portal
            /// </summary>
            public const string ModPortalBase = "https://mods.factorio.com";

            /// <summary>
            /// Gets the mod portal URL for a specific mod
            /// </summary>
            /// <param name="modName">The internal name of the mod</param>
            /// <returns>Full URL to the mod's portal page</returns>
            public static string GetModUrl(string modName) => $"{ModPortalBase}/mod/{modName}";

            /// <summary>
            /// Gets the authenticated download URL for a mod
            /// </summary>
            /// <param name="downloadPath">The download path from the API</param>
            /// <param name="username">Factorio username</param>
            /// <param name="token">Authentication token</param>
            /// <returns>Full download URL with authentication parameters</returns>
            public static string GetModDownloadUrl(string downloadPath, string username, string token)
                => $"{ModPortalBase}{downloadPath}?username={Uri.EscapeDataString(username)}&token={Uri.EscapeDataString(token)}";
        }

        /// <summary>
        /// Throttle delays for UI operations (in milliseconds)
        /// </summary>
        public static class Throttle
        {
            /// <summary>
            /// Delay for mod search text input (300ms)
            /// </summary>
            public const int SearchMs = 300;

            /// <summary>
            /// Delay for author search text input (200ms)
            /// </summary>
            public const int AuthorSearchMs = 200;
        }

        /// <summary>
        /// File system related constants
        /// </summary>
        public static class FileSystem
        {
            /// <summary>
            /// File pattern for mod files (*.zip)
            /// </summary>
            public const string ModFilePattern = "*.zip";

            /// <summary>
            /// Name of the mod info file (info.json)
            /// </summary>
            public const string InfoJsonFileName = "info.json";

            /// <summary>
            /// Name of the mod list file (mod-list.json)
            /// </summary>
            public const string ModListFileName = "mod-list.json";

            public const string ModSettingsFolder = "mod-settings";
        }

        /// <summary>
        /// UI-related constants
        /// </summary>
        public static class UI
        {
            /// <summary>
            /// Default group name for mods without a group
            /// </summary>
            public const string DefaultGroupName = "N/A";

            /// <summary>
            /// Buffer size for file operations (8KB)
            /// </summary>
            public const int BufferSize = 8192;
        }

        /// <summary>
        /// Cache lifetime settings
        /// </summary>
        public static class Cache
        {
            /// <summary>
            /// How long to cache API responses (5 minutes)
            /// </summary>
            public static readonly TimeSpan ApiCacheLifetime = TimeSpan.FromMinutes(5);

            /// <summary>
            /// How long to cache mod metadata (90 days)
            /// </summary>
            public static readonly TimeSpan MetadataCacheLifetime = TimeSpan.FromDays(90);
        }

        public static class VersionHelper
        {
            /// <summary>
            /// Compares two version strings
            /// Returns: 1 if v1 > v2, -1 if v1 < v2, 0 if equal
            /// </summary>
            public static int CompareVersions(string v1, string v2)
            {
                if (string.IsNullOrWhiteSpace(v1) || string.IsNullOrWhiteSpace(v2))
                    return string.Compare(v1, v2, StringComparison.Ordinal);

                // Try semantic versioning first
                if (NuGetVersion.TryParse(v1, out var version1) &&
                    NuGetVersion.TryParse(v2, out var version2))
                {
                    return version1.CompareTo(version2);
                }

                // Fallback to string comparison
                return string.Compare(v1, v2, StringComparison.Ordinal);
            }

            /// <summary>
            /// Checks if version1 is newer than version2
            /// </summary>
            public static bool IsNewerVersion(string version1, string version2)
            {
                return CompareVersions(version1, version2) > 0;
            }
        }

        public static class DependencyHelper
        {
            private static readonly HashSet<string> GameDependencies = new(StringComparer.OrdinalIgnoreCase)
        {
            "base",
            "space-age",
            "quality",
            "elevated-rails"
        };

            /// <summary>
            /// Extracts the dependency name from a dependency string
            /// Example: "? base >= 1.0" -> "base"
            /// </summary>
            public static string ExtractDependencyName(string dependency)
            {
                if (string.IsNullOrWhiteSpace(dependency))
                    return string.Empty;

                return dependency
                    .Split(Constants.Separators.Dependency, StringSplitOptions.RemoveEmptyEntries)
                    .FirstOrDefault()?.Trim() ?? string.Empty;
            }

            /// <summary>
            /// Checks if a dependency is optional
            /// </summary>
            public static bool IsOptionalDependency(string dependency)
            {
                var trimmed = dependency.TrimStart();
                return trimmed.StartsWith('?') || dependency.Contains("(?)");
            }

            /// <summary>
            /// Checks if a dependency is a conflict dependency
            /// </summary>
            public static bool IsConflictDependency(string dependency)
            {
                var trimmed = dependency.TrimStart();
                return trimmed.StartsWith('!') || dependency.Contains("(!)");
            }

            /// <summary>
            /// Checks if a dependency is a game/base dependency
            /// </summary>
            public static bool IsGameDependency(string dependencyName)
            {
                return GameDependencies.Contains(dependencyName);
            }

            /// <summary>
            /// Gets all mandatory (required) dependencies from a mod
            /// </summary>
            public static List<string> GetMandatoryDependencies(IEnumerable<string> dependencies)
            {
                return [.. dependencies
                    .Where(dep => !IsOptionalDependency(dep) && !IsConflictDependency(dep))
                    .Select(ExtractDependencyName)
                    .Where(name => !string.IsNullOrEmpty(name) && !IsGameDependency(name))];
            }
        }

        public static class JsonHelper
        {
            public static readonly JsonSerializerOptions CamelCase = new()
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            public static readonly JsonSerializerOptions IndentedOnly = new()
            {
                WriteIndented = true,
            };
        }
    }
}