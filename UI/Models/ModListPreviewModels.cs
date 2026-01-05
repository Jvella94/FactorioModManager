using System.Collections.Generic;

namespace FactorioModManager.Models
{
    public class ModListPreviewItem
    {
        public string Name { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public bool CurrentEnabled { get; set; }
        public bool TargetEnabled { get; set; }
        public string? CurrentVersion { get; set; }
        public string? TargetVersion { get; set; }
        public List<string> InstalledVersions { get; set; } = [];
    }

    public class ModListPreviewResult
    {
        public string Name { get; set; } = string.Empty;
        public bool ApplyEnabled { get; set; }
        public string? ApplyVersion { get; set; }
    }
}