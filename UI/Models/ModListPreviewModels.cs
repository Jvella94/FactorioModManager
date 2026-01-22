using System.Collections.Generic;

namespace FactorioModManager.Models
{
    public class ModListPreviewItem
    {
        public string Name { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public bool CurrentStatus { get; set; }
        public bool NewStatus { get; set; }
        public string? CurrentVersion { get; set; }
        public string? ListedVersion { get; set; }
        public List<string> InstalledVersions { get; set; } = [];
    }

    public class ModListPreviewResult
    {
        public string Name { get; set; } = string.Empty;
        public bool Enabled { get; set; }
        public string? Version { get; set; }
    }
}