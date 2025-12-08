using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using FactorioModManager.Infrastructure;
using FactorioModManager.ViewModels;
using FactorioModManager.ViewModels.MainWindow;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Linq;

namespace FactorioModManager.Views
{
    public partial class MainWindow : Window
    {
        private readonly MouseNavigationHandler _mouseNavHandler;

        public MainWindow()
        {
            InitializeComponent();
            _mouseNavHandler = new MouseNavigationHandler(this);

            // Set DataContext from DI
            DataContext = ServiceContainer.Instance.Resolve<MainWindowViewModel>();

            using var stream = AssetLoader.Open(
                new Uri("avares://FactorioModManager/Assets/FMM.png"));
            Icon = new WindowIcon(new Bitmap(stream));
        }

        private async void Window_Loaded(object? sender, RoutedEventArgs e)
        {
            if (DataContext is MainWindowViewModel vm)
            {
                await vm.RefreshModsCommand.Execute();
            }
        }

        private void OpenLogs(object? sender, RoutedEventArgs e)
        {
            var logWindow = new LogWindow();
            logWindow.Show();
        }

        private async void ShowAbout_Click(object? sender, RoutedEventArgs e)
        {
            var aboutMessage = "Factorio Mod Manager\nVersion 1.0.0\n\n" +
                             "A modern mod manager for Factorio.\n\n" +
                             "Features:\n" +
                             "• Manage and organize mods\n" +
                             "• Check for updates\n" +
                             "• Group management\n" +
                             "• Download from Mod Portal";

            var dialog = new Dialogs.MessageBoxDialog("About", aboutMessage);
            await dialog.ShowDialog(this);
        }

        private void AuthorBox_OnGotFocus(object? sender, GotFocusEventArgs e)
        {
            if (sender is AutoCompleteBox autoCompleteBox)
            {
                autoCompleteBox.IsDropDownOpen = true;
            }
        }

        private void DataGrid_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (DataContext is not MainWindowViewModel vm)
                return;

            if (sender is not DataGrid grid)
                return;

            // SelectedItem is already bound to SelectedMod via XAML

            // Sync SelectedItems -> SelectedMods collection
            var selected = grid.SelectedItems
                               .OfType<ModViewModel>()
                               .ToList();

            vm.SelectedMods = new ObservableCollection<ModViewModel>(selected);
        }

        private void CancelRename(object? sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is ModGroupViewModel group)
            {
                group.IsEditing = false;
            }
        }

        private void OpenModFile(object? sender, RoutedEventArgs e)
        {
            if (DataContext is MainWindowViewModel vm && vm.SelectedMod?.FilePath is string filePath)
            {
                try
                {
                    if (!File.Exists(filePath))
                    {
                        ShowError($"File not found: {filePath}");
                        return;
                    }

                    if (OperatingSystem.IsWindows())
                    {
                        // Open Explorer and select the file
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "explorer.exe",
                            Arguments = $"/select,\"{filePath}\"",
                            UseShellExecute = false
                        });
                    }
                    else if (OperatingSystem.IsMacOS())
                    {
                        // Reveal in Finder
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "open",
                            Arguments = $"-R \"{filePath}\"",
                            UseShellExecute = false
                        });
                    }
                    else if (OperatingSystem.IsLinux())
                    {
                        // Best-effort: open containing folder with default file manager
                        var directory = Path.GetDirectoryName(filePath) ?? ".";
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "xdg-open",
                            Arguments = $"\"{directory}\"",
                            UseShellExecute = false
                        });
                    }
                    else
                    {
                        ShowError("Opening files is not supported on this OS.");
                    }
                }
                catch (Exception ex)
                {
                    ShowError($"Failed to open mod file: {ex.Message}");
                }
            }
        }

        private async void ShowError(string message)
        {
            var dialog = new Dialogs.MessageBoxDialog("Error", message);
            await dialog.ShowDialog(this);
        }

        protected override void OnClosed(EventArgs e)
        {
            _mouseNavHandler?.Dispose();
            base.OnClosed(e);
        }
    }
}