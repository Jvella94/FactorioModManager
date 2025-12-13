using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace FactorioModManager.Models
{
    /// <summary>
    /// Represents mod metadata from info.json file
    /// </summary>
    public class ModInfo
    {
        private const string _placeholderTitle = "[MOD DISPLAY NAME]";

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; init; } = string.Empty;

        [JsonIgnore]
        public string DisplayTitle => Title != _placeholderTitle ? Title : Name;

        [JsonPropertyName("author")]
        public string Author { get; set; } = string.Empty;

        [JsonPropertyName("contact")]
        public string? Contact { get; set; }

        [JsonPropertyName("homepage")]
        public string? Homepage { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("factorio_version")]
        public string FactorioVersion { get; set; } = "2.0";

        [JsonPropertyName("dependencies")]
        public List<string> Dependencies { get; set; } = [];

        // ✨ NEW: Computed properties for better domain modeling
        [JsonIgnore]
        public bool HasDependencies => Dependencies.Count > 0;

        [JsonIgnore]
        public IReadOnlyList<string> MandatoryDependencies =>
            Constants.DependencyHelper.GetMandatoryDependencies(Dependencies);

        [JsonIgnore]
        public IReadOnlyList<string> OptionalDependencies =>
            [.. Dependencies
                .Where(Constants.DependencyHelper.IsOptionalDependency)
                .Select(Constants.DependencyHelper.ExtractDependencyName)];

        [JsonIgnore]
        public IReadOnlyList<string> IncompatibleDependencies =>
            Constants.DependencyHelper.GetIncompatibleDependencies(Dependencies);

        // ✨ NEW: Validation
        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(Name) &&
                   !string.IsNullOrWhiteSpace(Version) &&
                   !string.IsNullOrWhiteSpace(Title);
        }

        public Result Validate()
        {
            if (string.IsNullOrWhiteSpace(Name))
                return Result.Fail("Mod name is required", ErrorCode.InvalidModFormat);

            if (string.IsNullOrWhiteSpace(Version))
                return Result.Fail("Mod version is required", ErrorCode.InvalidModFormat);

            if (string.IsNullOrWhiteSpace(Title))
                return Result.Fail("Mod title is required", ErrorCode.InvalidModFormat);

            return Result.Ok();
        }
    }
}