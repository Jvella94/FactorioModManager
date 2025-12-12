using System;
using System.Collections.Generic;

namespace FactorioModManager.Models.DTO
{
    public abstract record ModDetailsBaseDTO(
    string Name,
    string Title,
    string? Category,
    int DownloadsCount
    );

    public record ModDetailsShortDTO(
       string Name,
       string Title,
       string? Category,
       int DownloadsCount,
       List<ShortReleaseDTO> Releases
   ) : ModDetailsBaseDTO(Name, Title, Category, DownloadsCount);

    public record ModDetailsFullDTO(
        string Name,
        string Title,
        string? Category,
        int DownloadsCount,
        List<FullReleaseDTO> Releases,
        string? Changelog,
        string? SourceUrl,
        string? Homepage
    ) : ModDetailsBaseDTO(Name, Title, Category, DownloadsCount);

    public record ShortReleaseDTO(
         string Version,
         DateTime ReleasedAt,
         string DownloadUrl,
         string FactorioVersion
     );

    public record FullReleaseDTO(
         string Version,
         DateTime ReleasedAt,
         string DownloadUrl,
         string FactorioVersion,
        List<string>? Dependencies
     ) : ShortReleaseDTO(Version, ReleasedAt, DownloadUrl, FactorioVersion);
}