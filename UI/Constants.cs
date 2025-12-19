using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using FactorioModManager.Services.Infrastructure;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FactorioModManager
{
    /// <summary>
    /// Application-wide constants for Factorio Mod Manager
    /// </summary>
    public static class Constants
    {
        // About message is built at runtime using the assembly version (populated from csproj)
        public static readonly string AboutMessage = BuildAboutMessage();

        private static string GetAppVersion()
        {
            try
            {
                var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
                var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
                var verString = !string.IsNullOrEmpty(info) ? info : asm.GetName().Version?.ToString();
                if (string.IsNullOrEmpty(verString))
                    return "0.0.0";

                // Strip build metadata after '+' (e.g. 1.0.0+githash)
                var plusIdx = verString.IndexOf('+');
                if (plusIdx >= 0)
                    verString = verString[..plusIdx];

                // Preserve prerelease (after '-') but limit numeric parts to major.minor.patch
                string prerelease = string.Empty;
                var dashIdx = verString.IndexOf('-');
                if (dashIdx >= 0)
                {
                    prerelease = verString[dashIdx..];
                    verString = verString[..dashIdx];
                }

                var parts = verString.Split('.');
                if (parts.Length > 3)
                    verString = string.Join('.', parts.Take(3));

                if (!string.IsNullOrEmpty(prerelease))
                    verString += prerelease;

                return verString;
            }
            catch
            {
                return "0.0.0";
            }
        }

        private static string BuildAboutMessage()
        {
            var version = GetAppVersion();
            return "Factorio Mod Manager\nVersion " + version + "\n\n" +
                   "A modern mod manager for Factorio.\n\n" +
                   "Features:\n" +
                   "• Manage and organize mods (enable/disable, grouping)\n" +
                   "• Install from local file or URL and remove mods\n" +
                   "• Download mods and specific versions from the Factorio Mod Portal\n" +
                   "• Manage multiple installed versions (download, delete, set active)\n" +
                   "• Check for updates for installed mods and update individually or in bulk\n" +
                   "• View changelogs and version history\n" +
                   "• Dependency resolution (validate mandatory deps, view dependents, handle conflicts)\n" +
                   "• Launch Factorio directly from the app and auto-detect installation\n" +
                   "• Settings with automatic Factorio detection and configurable options\n" +
                   "• Built-in logging with viewer and automatic log pruning\n" +
                   "• Cross-platform UI (Windows/Linux/macOS) via Avalonia\n" +
                   "\n" +
                   "For more information, visit the project repository.";
        }

        private static readonly Lazy<Bitmap> _lazyPlaceholder = new(() =>
        {
            try
            {
                var uri = new Uri("avares://FactorioModManager/Assets/FMM.png");
                return new Bitmap(AssetLoader.Open(uri));
            }
            catch (Exception ex)
            {
                var loggingservice = ServiceContainer.Instance.Resolve<ILogService>();
                loggingservice?.LogError($"Issue loading bitmap: {ex.Message}", ex);
                return new WriteableBitmap(
                    new PixelSize(1, 1),
                    new Vector(96, 96),
                    PixelFormat.Bgra8888,
                    AlphaFormat.Premul);
            }
        });

        public static Bitmap LoadPlaceholderThumbnail() => _lazyPlaceholder.Value;

        /// <summary>
        /// Character separators used for parsing strings
        /// </summary>
        public static class Separators
        {
            /// <summary>
            /// Characters used to separate dependency names from version constraints
            /// Example: "base >= 2.0" splits into ["base", "2.0"]
            /// Note: space removed so mod names containing spaces are preserved; parsing relies on operators instead
            /// </summary>
            public static readonly char[] Dependency = ['>', '<', '=', '!', '?', '(', ')'];
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
            private const string _baseGameName = "base";
            private static readonly string[] _dLCNames = ["space-age", "quality", "elevated-rails"];

            /// <summary>
            /// Checks if a dependency name is an official game dependency
            /// </summary>
            /// <param name="dependencyName">The dependency name to check</param>
            /// <returns>True if it's a game dependency, false otherwise</returns>
            public static bool IsGameDependency(string dependencyName)
            {
                return dependencyName == _baseGameName || _dLCNames.Contains(dependencyName);
            }

            /// <summary>
            /// Checks if a dependency name is an official game dependency
            /// </summary>
            /// <param name="dependencyName">The dependency name to check</param>
            /// <returns>True if it's a game dependency, false otherwise</returns>
            public static bool IsDLCDependency(string dependencyName)
            {
                return _dLCNames.Contains(dependencyName);
            }

            /// <summary>
            /// Extracts the dependency name from a dependency string
            /// Example: "? base >= 1.0" -> "base"
            /// </summary>
            public static string ExtractDependencyName(string dependency)
            {
                if (string.IsNullOrWhiteSpace(dependency))
                    return string.Empty;
                // Prefer the robust parser that handles prefixes like '?', '!' and '(?)' as well as
                // names that include spaces. This prevents incorrect values such as single-character
                // results when naive splitting goes wrong.
                var parsed = ParseDependency(dependency);
                if (parsed != null)
                    return parsed.Value.Name ?? string.Empty;

                // Fallback: old behaviour (split on operator characters)
                return dependency
                    .Split(Separators.Dependency, StringSplitOptions.RemoveEmptyEntries)
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
            /// Gets all mandatory (required) dependencies from a mod
            /// </summary>
            public static IReadOnlyList<string> GetMandatoryDependencies(IReadOnlyList<string>? dependencies)
            {
                if (dependencies == null || dependencies.Count == 0)
                    return [];

                // Mandatory: no prefix OR `~` (load-order only) but not `?`, "(?)", or `!` [web:38]
                return [.. dependencies
                    .Select(ParseDependencyNameAndPrefix)
                    .Where(d => d != null && d.Value.Prefix is null or "" or "~")
                    .Select(d => d!.Value.Name)
                    .Distinct(StringComparer.OrdinalIgnoreCase)];
            }

            // NEW: incompatible dependencies (prefix `!`) [web:38]
            public static IReadOnlyList<string> GetIncompatibleDependencies(IReadOnlyList<string>? dependencies)
            {
                if (dependencies == null || dependencies.Count == 0)
                    return [];

                return [.. dependencies
                    .Select(ParseDependencyNameAndPrefix)
                    .Where(d => d != null && d.Value.Prefix == "!")
                    .Select(d => d!.Value.Name)
                    .Distinct(StringComparer.OrdinalIgnoreCase)];
            }

            // NEW: get mandatory dependencies preserving parsed constraint information
            public static IReadOnlyList<(string Raw, string? Prefix, string Name, string? VersionOperator, string? Version)> GetMandatoryDependenciesWithConstraints(IReadOnlyList<string>? dependencies)
            {
                if (dependencies == null || dependencies.Count == 0)
                    return [];

                var list = new List<(string Raw, string? Prefix, string Name, string? VersionOperator, string? Version)>();
                foreach (var raw in dependencies)
                {
                    var parsed = ParseDependency(raw);
                    if (parsed == null)
                        continue;

                    // Mandatory: no prefix OR `~` (load-order only) but not `?`, "(?)", or `!` [web:38]
                    if (parsed.Value.Prefix is null or "" or "~")
                    {
                        list.Add((raw, parsed.Value.Prefix, parsed.Value.Name, parsed.Value.VersionOperator, parsed.Value.Version));
                    }
                }

                return list;
            }

            /// <summary>
            /// Parses a raw dependency string and returns prefix, name, version operator and version.
            /// Example: "? modname >= 1.2.3" -> ("?","modname", ">=", "1.2.3")
            /// This implementation supports names containing spaces by using a regex to locate operators.
            /// </summary>
            public static (string? Prefix, string Name, string? VersionOperator, string? Version)? ParseDependency(string raw)
            {
                if (string.IsNullOrWhiteSpace(raw))
                    return null;

                var trimmed = raw.Trim();
                string? prefix = null;

                if (trimmed.StartsWith("(!)", StringComparison.Ordinal) ||
                    trimmed.StartsWith("(?)", StringComparison.Ordinal))
                {
                    prefix = trimmed[..3];
                    trimmed = trimmed[3..].TrimStart();
                }
                else if (trimmed.Length > 0 && (trimmed[0] == '!' || trimmed[0] == '?' || trimmed[0] == '~'))
                {
                    prefix = trimmed[0].ToString();
                    trimmed = trimmed[1..].TrimStart();
                }

                if (string.IsNullOrEmpty(trimmed))
                    return null;

                // Use regex to capture name (allowing spaces) and optional operator+version
                var m = GenRegex.NameOperatorAndVersionRegex().Match(trimmed);
                if (!m.Success)
                    return (prefix, trimmed, null, null);

                var name = m.Groups["name"].Value.Trim();
                var op = m.Groups["op"].Success ? m.Groups["op"].Value : null;
                var ver = m.Groups["ver"].Success ? m.Groups["ver"].Value.Trim() : null;

                return (prefix, name, op, ver);
            }

            /// <summary>
            /// Checks whether a given version satisfies the operator/version constraint.
            /// If operator or version is null, returns true.
            /// </summary>
            public static bool SatisfiesVersionConstraint(string? actualVersion, string? op, string? requiredVersion)
            {
                if (string.IsNullOrEmpty(op) || string.IsNullOrEmpty(requiredVersion))
                    return true;

                if (string.IsNullOrEmpty(actualVersion))
                    return false;

                var cmp = VersionHelper.CompareVersions(actualVersion, requiredVersion);

                return op switch
                {
                    ">" => cmp > 0,
                    ">=" => cmp >= 0,
                    "<" => cmp < 0,
                    "<=" => cmp <= 0,
                    "=" => cmp == 0,
                    _ => true,
                };
            }

            private static (string? Prefix, string Name)? ParseDependencyNameAndPrefix(string raw)
            {
                if (string.IsNullOrWhiteSpace(raw))
                    return null;

                // Reuse ParseDependency to correctly handle names with spaces
                var parsed = ParseDependency(raw);
                if (parsed == null)
                    return null;

                return (parsed.Value.Prefix, parsed.Value.Name);
            }
        }

        public static class JsonOptions
        {
            public static readonly JsonSerializerOptions CamelCase = new()
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            public static readonly JsonSerializerOptions ModList = new()
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };

            public static readonly JsonSerializerOptions IndentedOnly = new()
            {
                WriteIndented = true,
            };

            public static readonly JsonSerializerOptions ModMetaData = new()
            {
                WriteIndented = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault
            };

            public static readonly JsonSerializerOptions CaseInsensitive = new()
            {
                PropertyNameCaseInsensitive = true,
            };
        }
    }

    internal static partial class GenRegex
    {
        // Match a name that excludes operator characters, then optional operator+version
        [GeneratedRegex(@"^(?<name>[^<>=]+)\s*(?<op>>=|<=|=|>|<)?\s*(?<ver>.+)?$")]
        internal static partial Regex NameOperatorAndVersionRegex();
    }
}