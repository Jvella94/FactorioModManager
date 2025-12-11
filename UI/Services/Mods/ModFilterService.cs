using FactorioModManager.ViewModels;
using FactorioModManager.ViewModels.MainWindow;
using System.Collections.Generic;
using System.Linq;
using System;

namespace FactorioModManager.Services.Mods
{
    public interface IModFilterService
    {
        List<ModViewModel> ApplyFilter(IEnumerable<ModViewModel> allMods,
            string? searchText,
            string? selectedAuthorFilter,
            ModGroupViewModel? selectedGroup,
            bool showOnlyUnusedInternals,
            bool showOnlyPendingUpdates);
    }

    public class ModFilterService : IModFilterService
    {
        public List<ModViewModel> ApplyFilter(IEnumerable<ModViewModel> allMods,
            string? searchText,
            string? selectedAuthorFilter,
            ModGroupViewModel? selectedGroup,
            bool showOnlyUnusedInternals,
            bool showOnlyPendingUpdates)
        {
            var query = allMods.AsQueryable();

            if (!string.IsNullOrEmpty(searchText))
            {
                query = query.Where(m => m.Title != null && m.Title.Contains(searchText, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrEmpty(selectedAuthorFilter))
            {
                var parenIndex = selectedAuthorFilter.IndexOf('(');
                var authorName = parenIndex > 0 ? selectedAuthorFilter[..parenIndex].Trim() : selectedAuthorFilter.Trim();
                query = query.Where(m => string.Equals(m.Author, authorName, StringComparison.OrdinalIgnoreCase));
            }

            if (selectedGroup is not null)
            {
                query = query.Where(m => string.Equals(m.GroupName, selectedGroup.Name, StringComparison.OrdinalIgnoreCase));
            }

            if (showOnlyUnusedInternals)
            {
                query = query.Where(m => m.IsUnusedInternal);
            }

            if (showOnlyPendingUpdates)
            {
                query = query.Where(m => m.HasUpdate);
            }

            return [.. query.OrderByDescending(m => m.LastUpdated ?? DateTime.MinValue)];
        }
    }
}