using System;
using System.Collections.Generic;

namespace FactorioModManager.Models.Domain
{
    /// <summary>
    /// Domain model representing an installed mod
    /// </summary>
    public record Mod
    {
        public required string Name { get; init; }
        public required string Version { get; init; }
        public required string Title { get; init; }
        public required string Author { get; init; }
        public string? Description { get; init; }
        public string? Homepage { get; init; }
        public string FactorioVersion { get; init; } = "2.0";
        public IReadOnlyList<string> Dependencies { get; init; } = [];
        public bool IsEnabled { get; init; }
        public DateTime? LastUpdated { get; init; }
        public string FilePath { get; init; } = string.Empty;
        public string? ThumbnailPath { get; init; }

        // Computed properties
        public bool HasDependencies => Dependencies.Count > 0;
        public IReadOnlyList<string> MandatoryDependencies =>
            Constants.DependencyHelper.GetMandatoryDependencies(Dependencies);
    }
}