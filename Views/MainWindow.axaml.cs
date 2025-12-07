using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using FactorioModManager.Services;
using FactorioModManager.ViewModels;
using FactorioModManager.ViewModels.MainWindow;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace FactorioModManager.Views
{
    public partial class MainWindow : Window
    {
        private MainWindowVM? ViewModel => DataContext as MainWindowVM;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainWindowVM();
        }

        private void Window_Loaded(object? sender, RoutedEventArgs e)
        {
            if (ViewModel != null)
            {
                _ = ViewModel.RefreshModsAsync();
            }
        }

        private void CancelRename(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is ModGroupViewModel group)
            {
                group.IsEditing = false;
            }
        }

        private void DataGrid_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (sender is DataGrid dataGrid && DataContext is MainWindowVM viewModel)
            {
                viewModel.SelectedMods.Clear();

                if (dataGrid.SelectedItems != null)
                {
                    foreach (var item in dataGrid.SelectedItems.OfType<ModViewModel>())
                    {
                        viewModel.SelectedMods.Add(item);
                    }
                }
            }
        }

        private void AuthorBox_OnGotFocus(object? sender, GotFocusEventArgs e)
        {
            if (sender is AutoCompleteBox autoCompleteBox &&
                string.IsNullOrEmpty(autoCompleteBox.Text))
            {
                // Open dropdown when focused and empty
                autoCompleteBox.IsDropDownOpen = true;
            }
        }

        private void OpenLogs(object? sender, RoutedEventArgs e)
        {
            var logWindow = new LogWindow();
            logWindow.Show();
        }

        private void OpenModFile(object? sender, RoutedEventArgs e)
        {
            if (ViewModel?.SelectedMod == null) return;

            var modPath = ModPathHelper.GetModsDirectory();
            var modName = ViewModel.SelectedMod.Name;

            // Try to find the mod file (zip or folder)
            var zipFile = System.IO.Directory.GetFiles(modPath, $"{modName}*.zip").FirstOrDefault();
            var folder = System.IO.Directory.GetDirectories(modPath, $"{modName}*").FirstOrDefault();

            var targetPath = zipFile ?? folder;

            if (!string.IsNullOrEmpty(targetPath))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = targetPath,
                        UseShellExecute = true
                    });
                    LogService.Instance.Log($"Opened mod file: {targetPath}");
                }
                catch (System.Exception ex)
                {
                    LogService.Instance.Log($"Error opening mod file: {ex.Message}");
                }
            }
        }

        private void Window_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space && ViewModel?.SelectedMod != null)
            {
                ViewModel.ToggleModCommand.Execute(ViewModel.SelectedMod);
                e.Handled = true;
            }
        }

        private async void ShowAbout_Click(object? sender, RoutedEventArgs e)
        {
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown";
            var messageBox = new Window
            {
                Title = "About Factorio Mod Manager",
                Width = 400,
                Height = 200,
                CanResize = false,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new StackPanel
                {
                    Margin = new Avalonia.Thickness(20),
                    Spacing = 10,
                    Children =
            {
                new TextBlock { Text = "Factorio Mod Manager", FontSize = 18, FontWeight = Avalonia.Media.FontWeight.Bold },
                new TextBlock { Text = $"Version {version}" },
                new TextBlock { Text = "A modern mod manager for Factorio", TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                new Button { Content = "Close", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, Margin = new Avalonia.Thickness(0, 20, 0, 0) }
            }
                }
            };

            var closeButton = (Button)((StackPanel)messageBox.Content).Children[3];
            closeButton.Click += (s, e) => messageBox.Close();

            await messageBox.ShowDialog(this);
        }


    }
}
