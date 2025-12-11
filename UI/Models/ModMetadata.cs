using System;
using System.Collections.Generic;

namespace FactorioModManager.Models
{
    public class ModMetadata
    {
        public string ModName { get; set; } = string.Empty;
        public string? Category { get; set; }
        public DateTime? CreatedOn { get; set; }
        public bool HasUpdate { get; set; }
        public DateTime? LastUpdateCheck { get; set; }
        public string? LatestVersion { get; set; }
        public string? SourceUrl { get; set; }
        public long? SizeOnDiskBytes { get; set; }
    }

    public class ModMetadataCollection
    {
        public List<ModMetadata> Metadata { get; set; } = [];
    }
}