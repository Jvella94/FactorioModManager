using FactorioModManager.Models.API;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FactorioModManager.Models.DTO
{
    public record ModDetailsShortDTO(
        string Name,
        string Title,
        string? Category,
        int DownloadsCount,
        List<ReleaseDTO> Releases
    );

    public record ModDetailsFullDTO(
        string Name,
        string Title,
        string? Category,
        int DownloadsCount,
        List<ReleaseDTO> Releases,
        string? Changelog,
        string? SourceUrl,
        string? Homepage
    ) : ModDetailsShortDTO(Name, Title, Category, DownloadsCount, Releases);

    public record ReleaseDTO(
         string Version,
         DateTime ReleasedAt,
         string DownloadUrl,
         string FactorioVersion
     );

    public static class ModDetailsExtensions
    {
        public static ModDetailsShortDTO ToShortDTO(this ModDetailsShort details)
        {
            return new ModDetailsShortDTO(
                Name: details.Name,
                Title: details.Title,
                Category: details.Category,
                DownloadsCount: details.DownloadsCount,
                Releases: [.. details.Releases.Select(r => r.ToDTO())]);
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
    }
}