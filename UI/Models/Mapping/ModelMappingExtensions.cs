using FactorioModManager.Models.API;
using FactorioModManager.Models.Domain;
using FactorioModManager.Models.DTO;
using FactorioModManager.Services.API;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FactorioModManager.Models.Mapping
{
    /// <summary>
    /// Extension methods for mapping between different model types
    /// </summary>
    public static class ModelMappingExtensions
    {
        // ============================================
        // API Models → DTOs
        // ============================================

        public static ModDetailsShortDTO ToShortDTO(this ModDetailsShort apiModel)
        {
            return new ModDetailsShortDTO(
                apiModel.Name,
                apiModel.Title,
                apiModel.Category,
                apiModel.DownloadsCount,
                [.. apiModel.Releases.Select(r => r.ToDTO())]
            );
        }

        public static ModDetailsFullDTO ToFullDTO(this ModDetailsFull apiModel)
        {
            return new ModDetailsFullDTO(
                apiModel.Name,
                apiModel.Title,
                apiModel.Category,
                apiModel.DownloadsCount,
                [.. apiModel.Releases.Select(r => r.ToDTO())],
                apiModel.Changelog,
                apiModel.SourceUrl,
                apiModel.Homepage
            );
        }

        public static ReleaseDTO ToDTO(this ModRelease apiRelease)
        {
            return new ReleaseDTO(
                apiRelease.Version,
                apiRelease.ReleasedAt,
                apiRelease.DownloadUrl,
                apiRelease.InfoJson?.FactorioVersion ?? "2.0"
            );
        }

        // ============================================
        // ModInfo → Domain Model
        // ============================================

        public static Mod ToDomainModel(
            this ModInfo modInfo,
            bool isEnabled,
            DateTime? lastUpdated,
            string filePath,
            string? thumbnailPath = null)
        {
            return new Mod
            {
                Name = modInfo.Name,
                Version = modInfo.Version,
                Title = modInfo.DisplayTitle,
                Author = modInfo.Author,
                Description = modInfo.Description,
                Homepage = modInfo.Homepage,
                FactorioVersion = modInfo.FactorioVersion,
                Dependencies = modInfo.Dependencies,
                IsEnabled = isEnabled,
                LastUpdated = lastUpdated,
                FilePath = filePath,
                ThumbnailPath = thumbnailPath
            };
        }

        // ============================================
        // Domain Model → ModInfo
        // ============================================

        public static ModInfo ToModInfo(this Mod domainModel)
        {
            return new ModInfo
            {
                Name = domainModel.Name,
                Version = domainModel.Version,
                Title = domainModel.Title,
                Author = domainModel.Author,
                Description = domainModel.Description,
                Homepage = domainModel.Homepage,
                FactorioVersion = domainModel.FactorioVersion,
                Dependencies = [.. domainModel.Dependencies]
            };
        }

        // ============================================
        // API Update Info → Domain
        // ============================================

        public static ModUpdateInfo ToUpdateInfo(
            this ModRelease release,
            string modName,
            string modTitle,
            string currentVersion)
        {
            return new ModUpdateInfo(
                modName,
                modTitle,
                currentVersion,
                release.Version,
                release.ReleasedAt
            );
        }

        // ============================================
        // Batch Conversions
        // ============================================

        public static List<ReleaseDTO> ToReleaseDTOs(this IEnumerable<ModRelease> releases)
        {
            return [.. releases.Select(r => r.ToDTO())];
        }

        public static List<Mod> ToDomainModels(
            this IEnumerable<(ModInfo Info, bool IsEnabled, DateTime? LastUpdated, string? ThumbnailPath, string FilePath)> modData)
        {
            return [.. modData.Select(m => m.Info.ToDomainModel(m.IsEnabled, m.LastUpdated, m.FilePath, m.ThumbnailPath))];
        }
    }
}