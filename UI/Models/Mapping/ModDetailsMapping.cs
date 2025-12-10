using System.Collections.Generic;
using System.Linq;
using FactorioModManager.Models.API;
using FactorioModManager.Models.DTO;

namespace FactorioModManager.Models.Mapping
{
    /// <summary>
    /// Mapping helpers between API models and internal DTOs.
    /// </summary>
    public static class ModDetailsMapping
    {
        public static ModDetailsShortDTO ToShortDTO(this ModDetailsShort details)
        {
            return new ModDetailsShortDTO(
                Name: details.Name,
                Title: details.Title,
                Category: details.Category,
                DownloadsCount: details.DownloadsCount,
                Releases: [.. details.Releases.Select(r => r.ToDTO())]
            );
        }

        public static ModDetailsFullDTO ToFullDTO(this ModDetailsFull details)
        {
            return new ModDetailsFullDTO(
                Name: details.Name,
                Title: details.Title,
                Category: details.Category,
                DownloadsCount: details.DownloadsCount,
                Releases: [.. details.Releases.Select(r => r.ToDTO())],
                Changelog: details.Changelog,
                SourceUrl: details.SourceUrl,
                Homepage: details.Homepage
            );
        }

        public static ReleaseDTO ToDTO(this ModRelease release)
        {
            return new ReleaseDTO(
                Version: release.Version,
                ReleasedAt: release.ReleasedAt,
                DownloadUrl: release.DownloadUrl,
                FactorioVersion: release.InfoJson?.FactorioVersion ?? "2.0"
            );
        }

        public static List<ReleaseDTO> ToDTO(this IEnumerable<ModRelease> releases)
        {
            return [.. releases.Select(r => r.ToDTO())];
        }
    }
}