using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FactorioModManager.Services.Infrastructure;
using FactorioModManager.ViewModels;
using FactorioModManager.ViewModels.MainWindow;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive.Linq;

namespace FactorioModManager.Views
{
    public partial class MainWindow : Window
    {
        private readonly MouseNavigationHandler _mouseNavHandler;
        private readonly IUIService _uiService;

        public MainWindow()
        {
            // Set DataContext from DI
            DataContext = ServiceContainer.Instance.Resolve<MainWindowViewModel>();
            _uiService = ServiceContainer.Instance.Resolve<IUIService>();

            _mouseNavHandler = new MouseNavigationHandler(this);
            using var stream = AssetLoader.Open(
                new Uri("avares://FactorioModManager/Assets/FMM.png"));
            Icon = new WindowIcon(new Bitmap(stream));
            InitializeComponent();
        }

        private async void Window_Loaded(object? sender, RoutedEventArgs e)
        {
            if (DataContext is MainWindowViewModel vm)
            {
                await vm.RefreshModsCommand.Execute();
                vm.InitializeStartupTasks();
            }
        }

        private void OpenLogs(object? sender, RoutedEventArgs e)
        {
            var logWindow = new LogWindow(ServiceContainer.Instance.Resolve<ILogService>(), ServiceContainer.Instance.Resolve<IUIService>());
            logWindow.Show();
        }

        private async void ShowAbout_Click(object? sender, RoutedEventArgs e)
        {
            var aboutMessage = Constants.AboutMessage;

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
            ModDetailsScroller.ScrollToHome();
        }

        private void CancelRename(object? sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is ModGroupViewModel group)
            {
                group.IsRenaming = false;
            }
        }

        private async void OpenModFile(object? sender, RoutedEventArgs e)
        {
            if (DataContext is MainWindowViewModel vm && vm.SelectedMod?.FilePath is string filePath)
            {
                try
                {
                    // Support both zip file paths and mod directory paths.
                    if (File.Exists(filePath))
                    {
                        // Reveal file in system file manager with the file selected when possible
                        _uiService.RevealFile(filePath);
                        return;
                    }

                    if (Directory.Exists(filePath))
                    {
                        // Directory - open folder in file manager
                        _uiService.OpenFolder(filePath);
                        return;
                    }

                    await _uiService.ShowMessageAsync("Error", $"File or directory not found: {filePath}", this);
                }
                catch (Exception ex)
                {
                    await _uiService.ShowMessageAsync("Error", $"Failed to view mod file: {ex.Message}", this);
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

        /// <summary>
        /// Handles focus when renaming groups.
        /// </summary>
        /// <param name="sender">The source of the property change event. Must be a StackPanel representing the group edit panel.</param>
        /// <param name="e">An AvaloniaPropertyChangedEventArgs instance containing information about the changed property, including
        /// its new value.</param>
        private void GroupEditPanel_OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (sender is not StackPanel panel)
                return;

            if (e.Property == IsVisibleProperty &&
                e.NewValue is bool isVisible &&
                isVisible)
            {
                // Panel just became visible (IsRenaming == true)
                var textBox = panel.GetVisualDescendants().OfType<TextBox>().FirstOrDefault();
                if (textBox?.DataContext is ModGroupViewModel groupVm && groupVm.IsRenaming)
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        textBox.Focus();
                        textBox.SelectAll();
                    });
                }
            }
        }

        /// <summary>
        /// Handles focus when creating groups.
        /// </summary>
        /// <param name="sender">The source of the event, expected to be a <see cref="TextBox"/> representing the group name edit box.</param>
        /// <param name="e">The event data containing information about the visual tree attachment.</param>
        private void GroupNameEditBox_OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            if (sender is not TextBox textBox)
                return;

            // DataContext is ModGroupViewModel
            if (textBox.DataContext is ModGroupViewModel groupVm && groupVm.IsRenaming)
            {
                // Defer one tick so layout is fully ready
                Dispatcher.UIThread.Post(() =>
                {
                    textBox.Focus();
                    textBox.SelectAll();
                });
            }
        }

        private void GroupNameEditBox_OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (sender is not TextBox textBox ||
                textBox.DataContext is not ModGroupViewModel groupVm ||
                DataContext is not MainWindowViewModel mainVm)
                return;

            switch (e.Key)
            {
                case Key.Enter:
                    // Call ConfirmRenameGroup via its command if you exposed one,
                    // or directly if it's internal:
                    mainVm.ConfirmRenameGroupCommand.Execute(groupVm).Subscribe();
                    e.Handled = true;
                    break;

                case Key.Escape:
                    groupVm.IsRenaming = false;
                    groupVm.EditedName = groupVm.Name; // reset
                    e.Handled = true;
                    break;
            }
        }

        private void DeleteGroup_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is not Button button ||
                button.DataContext is not ModGroupViewModel groupVm ||
                DataContext is not MainWindowViewModel mainVm)
                return;

            var request = new DeleteGroupRequest(groupVm, this);
            mainVm.DeleteGroupCommand.Execute(request).Subscribe();
        }
    }
}