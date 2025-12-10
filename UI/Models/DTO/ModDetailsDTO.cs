using System;
using System.Collections.Generic;

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
}