using ReactiveUI;
using System;
using System.Linq;

namespace FactorioModManager.ViewModels.MainWindow
{
    public partial class MainWindowVM
    {
        private void UpdateFilteredMods()
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                FilteredMods.Clear();
                var filtered = Mods.Where(ModMatchesFilters);

                foreach (var mod in filtered)
                {
                    FilteredMods.Add(mod);
                }

                this.RaisePropertyChanged(nameof(ModCountText));
            });
        }

        private bool ModMatchesFilters(ModViewModel m)
        {
            if (!ShowDisabled && !m.IsEnabled) return false;

            if (!string.IsNullOrEmpty(SearchText) &&
                !m.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                return false;

            if (!string.IsNullOrEmpty(SelectedAuthorFilter))
            {
                var authorName = SelectedAuthorFilter.Split('(')[0].Trim();
                if (m.Author != authorName) return false;
            }

            if (FilterBySelectedGroup && SelectedGroup != null)
            {
                if (!SelectedGroup.ModNames.Contains(m.Title))
                    return false;
            }

            return true;
        }

        private void UpdateFilteredAuthors()
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                FilteredAuthors.Clear();
                var searchLower = AuthorSearchText?.ToLower() ?? "";

                foreach (var author in Authors)
                {
                    if (string.IsNullOrEmpty(searchLower) ||
                        author.Contains(searchLower, StringComparison.OrdinalIgnoreCase))
                    {
                        FilteredAuthors.Add(author);
                    }
                }
            });
        }
    }
}
