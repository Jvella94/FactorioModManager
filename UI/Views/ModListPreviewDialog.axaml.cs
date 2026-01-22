using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using FactorioModManager.Models;
using System.Collections.Generic;
using System.Linq;

namespace FactorioModManager.Views
{
    public partial class ModListPreviewDialog : Window
    {
        public record PreviewResult(string Name, bool ApplyEnabled, string? ApplyVersion);

        private readonly List<PreviewItem> _items;

        // Public parameterless constructor required for XAML/URI loader
        public ModListPreviewDialog()
        {
            InitializeComponent();
            _items = [];
        }

        public ModListPreviewDialog(List<ModListPreviewItem> items, string listName)
        {
            InitializeComponent();

            Title = $"Preview: {listName}";

            _items = [.. items.Select(i => new PreviewItem
            {
                Name = i.Name,
                Title = i.Title,
                CurrentEnabled = i.CurrentStatus,
                ApplyEnabled = i.NewStatus,
                CurrentVersion = i.CurrentVersion,
                ApplyVersion = i.ListedVersion,
                AvailableVersions = i.InstalledVersions ?? []
            })];

            // Header row
            var headerGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("3*,80,80,100,150"), Margin = new Thickness(6, 6, 6, 0) };
            var hdrTitle = new TextBlock { Text = "Title", FontWeight = FontWeight.Bold, VerticalAlignment = VerticalAlignment.Center };
            var hdrCurrent = new TextBlock { Text = "Current", FontWeight = FontWeight.Bold, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            var hdrNew = new TextBlock { Text = "New", FontWeight = FontWeight.Bold, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            var hdrExpected = new TextBlock { Text = "Expected Version", FontWeight = FontWeight.Bold, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            var hdrInstalled = new TextBlock { Text = "Installed Version(s)", FontWeight = FontWeight.Bold, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };

            Grid.SetColumn(hdrTitle, 0);
            Grid.SetColumn(hdrCurrent, 1);
            Grid.SetColumn(hdrNew, 2);
            Grid.SetColumn(hdrExpected, 3);
            Grid.SetColumn(hdrInstalled, 4);

            headerGrid.Children.Add(hdrTitle);
            headerGrid.Children.Add(hdrCurrent);
            headerGrid.Children.Add(hdrNew);
            headerGrid.Children.Add(hdrExpected);
            headerGrid.Children.Add(hdrInstalled);

            PreviewPanel.Children.Add(headerGrid);

            // Manually build UI rows into PreviewPanel
            foreach (var it in _items)
            {
                // Columns: Title, CurrentEnabled, ApplyEnabled, CurrentVersion, VersionSelector
                var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("3*,80,80,100,150"), Margin = new Thickness(6) };

                var title = new TextBlock { Text = it.Title, VerticalAlignment = VerticalAlignment.Center };
                var current = new CheckBox { IsChecked = it.CurrentEnabled, IsEnabled = false, HorizontalAlignment = HorizontalAlignment.Center };
                var apply = new CheckBox { IsChecked = it.ApplyEnabled, HorizontalAlignment = HorizontalAlignment.Center };
                var currVer = new TextBlock { Text = it.CurrentVersion ?? string.Empty, VerticalAlignment = VerticalAlignment.Center };

                Grid.SetColumn(title, 0);
                Grid.SetColumn(current, 1);
                Grid.SetColumn(apply, 2);
                Grid.SetColumn(currVer, 3);

                grid.Children.Add(title);
                grid.Children.Add(current);
                grid.Children.Add(apply);
                grid.Children.Add(currVer);

                // Version selector: show ComboBox only if multiple versions available, otherwise static text
                if (it.AvailableVersions != null && it.AvailableVersions.Count > 1)
                {
                    var combo = new ComboBox { ItemsSource = it.AvailableVersions, SelectedItem = it.ApplyVersion };
                    Grid.SetColumn(combo, 4);
                    grid.Children.Add(combo);
                    it.ApplyCombo = combo;
                }
                else
                {
                    var verText = new TextBlock { Text = it.ApplyVersion ?? (it.AvailableVersions?.FirstOrDefault() ?? string.Empty), VerticalAlignment = VerticalAlignment.Center };
                    Grid.SetColumn(verText, 4);
                    grid.Children.Add(verText);
                    // Keep ApplyCombo null when static
                }

                // store references
                it.ApplyCheck = apply;

                PreviewPanel.Children.Add(grid);
            }

            CancelBtn.Click += (_, __) => Close(null);
            ApplyBtn.Click += (_, __) =>
            {
                var res = _items.Select(v => new PreviewResult(v.Name, v.ApplyCheck.IsChecked == true, v.ApplyCombo?.SelectedItem as string ?? v.ApplyVersion)).ToList();
                Close(res);
            };
        }

        public class PreviewItem
        {
            public string Name { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;
            public bool CurrentEnabled { get; set; }
            public bool ApplyEnabled { get; set; }
            public string? CurrentVersion { get; set; }
            public string? ApplyVersion { get; set; }
            public List<string> AvailableVersions { get; set; } = [];

            // UI refs
            public CheckBox ApplyCheck { get; set; } = null!;

            public ComboBox? ApplyCombo { get; set; }
        }
    }
}