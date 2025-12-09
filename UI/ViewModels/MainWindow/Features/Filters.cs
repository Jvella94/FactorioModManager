using ReactiveUI;
using System;
using System.Linq;

namespace FactorioModManager.ViewModels.MainWindow
{
    public partial class MainWindowViewModel
    {
        /// <summary>
        /// Checks if a mod matches the current filter criteria
        /// </summary>
        private bool ModMatchesFilters(ModViewModel mod)
        {
            // Filter: Show disabled
            if (!ShowDisabled && !mod.IsEnabled)
                return false;

            // Filter: Search text
            if (!string.IsNullOrEmpty(SearchText) &&
                !mod.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                return false;

            // Filter: Author
            if (!string.IsNullOrEmpty(SelectedAuthorFilter))
            {
                var authorName = ExtractAuthorName(SelectedAuthorFilter);
                if (mod.Author != authorName)
                    return false;
            }

            // Filter: Group
            if (FilterBySelectedGroup && SelectedGroup != null)
            {
                if (!SelectedGroup.ModNames.Contains(mod.Title))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Extracts author name from "Author (count)" format
        /// </summary>
        private static string ExtractAuthorName(string authorFilter)
        {
            var parenIndex = authorFilter.IndexOf('(');
            return parenIndex > 0
                ? authorFilter[..parenIndex].Trim()
                : authorFilter.Trim();
        }
    }
}