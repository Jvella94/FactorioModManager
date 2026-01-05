using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
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
                CurrentEnabled = i.CurrentEnabled,
                ApplyEnabled = i.TargetEnabled,
                CurrentVersion = i.CurrentVersion,
                ApplyVersion = i.TargetVersion,
                AvailableVersions = i.InstalledVersions ?? []
            })];

            // Manually build UI rows into PreviewPanel
            foreach (var it in _items)
            {
                var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("2*,2*,80,80,100,150"), Margin = new Thickness(6) };

                var name = new TextBlock { Text = it.Name, VerticalAlignment = VerticalAlignment.Center };
                var title = new TextBlock { Text = it.Title, VerticalAlignment = VerticalAlignment.Center };
                var current = new CheckBox { IsChecked = it.CurrentEnabled, IsEnabled = false, HorizontalAlignment = HorizontalAlignment.Center };
                var apply = new CheckBox { IsChecked = it.ApplyEnabled, HorizontalAlignment = HorizontalAlignment.Center };
                var currVer = new TextBlock { Text = it.CurrentVersion ?? string.Empty, VerticalAlignment = VerticalAlignment.Center };
                var combo = new ComboBox { ItemsSource = it.AvailableVersions, SelectedItem = it.ApplyVersion };

                Grid.SetColumn(name, 0);
                Grid.SetColumn(title, 1);
                Grid.SetColumn(current, 2);
                Grid.SetColumn(apply, 3);
                Grid.SetColumn(currVer, 4);
                Grid.SetColumn(combo, 5);

                grid.Children.Add(name);
                grid.Children.Add(title);
                grid.Children.Add(current);
                grid.Children.Add(apply);
                grid.Children.Add(currVer);
                grid.Children.Add(combo);

                // store references
                it.ApplyCheck = apply;
                it.ApplyCombo = combo;

                PreviewPanel.Children.Add(grid);
            }

            CancelBtn.Click += (_, __) => Close(null);
            ApplyBtn.Click += (_, __) =>
            {
                var res = _items.Select(v => new PreviewResult(v.Name, v.ApplyCheck.IsChecked == true, v.ApplyCombo.SelectedItem as string)).ToList();
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

            public ComboBox ApplyCombo { get; set; } = null!;
        }
    }
}