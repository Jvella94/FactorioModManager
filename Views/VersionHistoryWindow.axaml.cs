using System.Collections.Generic;
using System.Diagnostics;
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
            this.FindControl<DataGrid>("VersionGrid")!.ItemsSource = releases;
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
                }
                catch { }
            }
        }
    }
}
