using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using FactorioModManager.Services;

namespace FactorioModManager.Views
{
    public partial class VersionHistoryWindow : Window
    {
        public VersionHistoryWindow()
        {
            InitializeComponent();
        }

        public VersionHistoryWindow(string modTitle, List<ModRelease> releases) : this()
        {
            this.FindControl<TextBlock>("ModTitle")!.Text = $"{modTitle} - Version History";

            // FIXED: Sort by ReleasedAt descending (newest first)
            var sortedReleases = releases.OrderByDescending(r => r.ReleasedAt).ToList();
            this.FindControl<DataGrid>("VersionGrid")!.ItemsSource = sortedReleases;
        }

        private void OpenDownloadLink(object? sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string url && !string.IsNullOrEmpty(url))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = $"https://mods.factorio.com{url}",
                        UseShellExecute = true
                    });
                    LogService.Instance.Log($"Opened download link: {url}");
                }
                catch
                {
                    LogService.Instance.Log($"Failed to open download link: {url}");
                }
            }
        }
    }
}
