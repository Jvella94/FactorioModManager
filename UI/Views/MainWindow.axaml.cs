using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FactorioModManager.Models;
using FactorioModManager.Services;
using FactorioModManager.Services.Infrastructure;
using FactorioModManager.Services.Settings;
using FactorioModManager.ViewModels;
using FactorioModManager.ViewModels.MainWindow;
using FactorioModManager.Views.Controls;
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
        private readonly ISettingsService _settingsService;

        // Hold prototypes for category/size columns so we can re-insert them
        private DataGridColumn? _categoryColumnPrototype;

        private DataGridColumn? _sizeColumnPrototype;
        private int _categoryColumnOriginalIndex = -1;
        private int _sizeColumnOriginalIndex = -1;

        public MainWindow()
        {
            // Set DataContext from DI
            DataContext = ServiceContainer.Instance.Resolve<MainWindowViewModel>();
            _uiService = ServiceContainer.Instance.Resolve<IUIService>();
            _settingsService = ServiceContainer.Instance.Resolve<ISettingsService>();

            _mouseNavHandler = new MouseNavigationHandler(this);
            using var stream = AssetLoader.Open(
                new Uri("avares://FactorioModManager/Assets/FMM.png"));
            Icon = new WindowIcon(new Bitmap(stream));
            InitializeComponent();

            // Capture and apply persisted groups width to the column on startup
            if (DataContext is MainWindowViewModel vm)
            {
                try
                {
                    var grid = this.FindControl<Grid>("MainContentGrid");
                    if (grid != null)
                    {
                        // Use VM's effective lengths so collapsed state is respected on startup
                        grid.ColumnDefinitions[0].Width = vm.EffectiveGroupsColumnGridLength;
                        grid.ColumnDefinitions[1].Width = vm.EffectiveSplitterGridLength;
                    }
                }
                catch { }

                // Capture DataGrid columns prototypes before we remove them
                try
                {
                    var modGrid = this.FindControl<DataGrid>("ModGrid");
                    if (modGrid != null)
                    {
                        // Find category and size columns by Tag
                        for (int i = 0; i < modGrid.Columns.Count; i++)
                        {
                            var col = modGrid.Columns[i];
                            var tag = col.Tag?.ToString() ?? string.Empty;
                            if (string.Equals(tag, "CategoryColumn", StringComparison.OrdinalIgnoreCase))
                            {
                                _categoryColumnPrototype = col;
                                _categoryColumnOriginalIndex = i;
                            }
                            else if (string.Equals(tag, "SizeColumn", StringComparison.OrdinalIgnoreCase))
                            {
                                _sizeColumnPrototype = col;
                                _sizeColumnOriginalIndex = i;
                            }
                        }

                        // Apply initial visibility according to settings service (if available)
                        var showCategory = _settingsService.GetShowCategoryColumn();
                        var showSize = _settingsService.GetShowSizeColumn();
                        ApplyColumnVisibility(modGrid, showCategory, showSize);

                        // Subscribe to changes for live updates
                        _settingsService.ShowCategoryColumnChanged += () => ApplyColumnVisibility(modGrid, _settingsService.GetShowCategoryColumn(), _settingsService.GetShowSizeColumn());
                        _settingsService.ShowSizeColumnChanged += () => ApplyColumnVisibility(modGrid, _settingsService.GetShowCategoryColumn(), _settingsService.GetShowSizeColumn());
                    }
                }
                catch { }
            }
        }

        private void ApplyColumnVisibility(DataGrid modGrid, bool showCategory, bool showSize)
        {
            // Category
            try
            {
                var hasCategory = modGrid.Columns.Any(c => string.Equals(c.Tag?.ToString(), "CategoryColumn", StringComparison.OrdinalIgnoreCase));
                if (!showCategory && hasCategory)
                {
                    var col = modGrid.Columns.First(c => string.Equals(c.Tag?.ToString(), "CategoryColumn", StringComparison.OrdinalIgnoreCase));
                    // store prototype if not stored
                    _categoryColumnPrototype ??= col;
                    // Store original index if unknown
                    if (_categoryColumnOriginalIndex < 0)
                        _categoryColumnOriginalIndex = modGrid.Columns.IndexOf(col);

                    modGrid.Columns.Remove(col);
                }
                else if (showCategory && !hasCategory && _categoryColumnPrototype != null)
                {
                    var insertIndex = _categoryColumnOriginalIndex >= 0 ? Math.Min(_categoryColumnOriginalIndex, modGrid.Columns.Count) : modGrid.Columns.Count;
                    modGrid.Columns.Insert(insertIndex, _categoryColumnPrototype);
                }
            }
            catch { }

            // Size
            try
            {
                var hasSize = modGrid.Columns.Any(c => string.Equals(c.Tag?.ToString(), "SizeColumn", StringComparison.OrdinalIgnoreCase));
                if (!showSize && hasSize)
                {
                    var col = modGrid.Columns.First(c => string.Equals(c.Tag?.ToString(), "SizeColumn", StringComparison.OrdinalIgnoreCase));
                    _sizeColumnPrototype ??= col;
                    if (_sizeColumnOriginalIndex < 0)
                        _sizeColumnOriginalIndex = modGrid.Columns.IndexOf(col);

                    modGrid.Columns.Remove(col);
                }
                else if (showSize && !hasSize && _sizeColumnPrototype != null)
                {
                    var insertIndex = _sizeColumnOriginalIndex >= 0 ? Math.Min(_sizeColumnOriginalIndex, modGrid.Columns.Count) : modGrid.Columns.Count;
                    modGrid.Columns.Insert(insertIndex, _sizeColumnPrototype);
                }
            }
            catch { }
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
            if (sender is Button button)
            {
                // Support both group and mod-list rename cancel
                if (button.DataContext is ModGroupViewModel group)
                {
                    group.IsRenaming = false;
                }
                else if (button.DataContext is CustomModList list)
                {
                    list.IsRenaming = false;
                    list.EditedName = list.Name;
                }
            }
        }

        private async void OpenModFile(object? sender, RoutedEventArgs e)
        {
            if (DataContext is MainWindowViewModel vm && vm.SelectedMod is { } selectedMod)
            {
                try
                {
                    // Prefer the file/directory for the selected version in the details panel when available
                    string? filePath = null;
                    var selVersion = selectedMod.SelectedVersion;

                    if (!string.IsNullOrEmpty(selVersion))
                    {
                        try
                        {
                            var idx = selectedMod.AvailableVersions.IndexOf(selVersion);
                            if (idx >= 0 && idx < selectedMod.VersionFilePaths.Count && !string.IsNullOrEmpty(selectedMod.VersionFilePaths[idx]))
                            {
                                filePath = selectedMod.VersionFilePaths[idx];
                            }
                            else
                            {
                                var modsDirectory = FolderPathHelper.GetModsDirectory();
                                var zipPath = Path.Combine(modsDirectory, $"{selectedMod.Name}_{selVersion}.zip");
                                var dirPath = Path.Combine(modsDirectory, $"{selectedMod.Name}_{selVersion}");
                                if (File.Exists(zipPath)) filePath = zipPath;
                                else if (Directory.Exists(dirPath)) filePath = dirPath;
                                else
                                {
                                    // Fallback: try to find any matching zip by name
                                    try
                                    {
                                        var files = Directory.GetFiles(modsDirectory, $"{selectedMod.Name}_*.zip");
                                        var match = files.FirstOrDefault(f => Path.GetFileNameWithoutExtension(f).EndsWith($"_{selVersion}", StringComparison.OrdinalIgnoreCase));
                                        if (match != null) filePath = match;
                                    }
                                    catch { }
                                }
                            }
                        }
                        catch { }
                    }

                    // Fall back to the selected mod's FilePath (active version)
                    if (string.IsNullOrEmpty(filePath))
                        filePath = selectedMod.FilePath;

                    if (string.IsNullOrEmpty(filePath))
                    {
                        await _uiService.ShowMessageAsync("Error", "File or directory not found.", this);
                        return;
                    }

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
        /// Handles focus when renaming groups or mod lists.
        /// </summary>
        private void GroupEditPanel_OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (sender is not StackPanel panel)
                return;

            if (e.Property == IsVisibleProperty && e.NewValue is bool isVisible && isVisible)
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
                    return;
                }

                if (textBox?.DataContext is CustomModList listVm && listVm.IsRenaming)
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
        /// Handles focus when creating groups or editing mod list names (attached to TextBox in XAML).
        /// </summary>
        private void GroupNameEditBox_OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            if (sender is not TextBox textBox)
                return;

            // DataContext may be ModGroupViewModel or CustomModList
            if (textBox.DataContext is ModGroupViewModel groupVm && groupVm.IsRenaming)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    textBox.Focus();
                    textBox.SelectAll();
                });
                return;
            }

            if (textBox.DataContext is CustomModList listVm && listVm.IsRenaming)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    textBox.Focus();
                    textBox.SelectAll();
                });
            }
        }

        private void GroupNameEditBox_OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (sender is not TextBox textBox || DataContext is not MainWindowViewModel mainVm)
                return;

            // Mod group renaming
            if (textBox.DataContext is ModGroupViewModel groupVm)
            {
                switch (e.Key)
                {
                    case Key.Enter:
                        mainVm.ConfirmRenameGroupCommand.Execute(groupVm).Subscribe();
                        e.Handled = true;
                        break;

                    case Key.Escape:
                        groupVm.IsRenaming = false;
                        groupVm.EditedName = groupVm.Name; // reset
                        e.Handled = true;
                        break;
                }

                return;
            }

            // Mod list renaming
            if (textBox.DataContext is CustomModList listVm)
            {
                switch (e.Key)
                {
                    case Key.Enter:
                        mainVm.ConfirmRenameModListCommand.Execute(listVm).Subscribe();
                        e.Handled = true;
                        break;

                    case Key.Escape:
                        listVm.IsRenaming = false;
                        listVm.EditedName = listVm.Name;
                        e.Handled = true;
                        break;
                }
            }
        }

        private void DeleteGroup_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is not Button button || DataContext is not MainWindowViewModel mainVm)
                return;

            // Support deleting groups only (mod list delete handled by command binding)
            if (button.DataContext is ModGroupViewModel groupVm)
            {
                var request = new DeleteGroupRequest(groupVm, this);
                mainVm.DeleteGroupCommand.Execute(request).Subscribe();
            }
        }

        private void GridSplitter_DragDelta(object? sender, VectorEventArgs e)
        {
            if (DataContext is not MainWindowViewModel vm) return;

            var grid = this.FindControl<Grid>("MainContentGrid");
            if (grid == null) return;

            try
            {
                var col = grid.ColumnDefinitions[0];
                double currentWidth = 0;
                try
                {
                    dynamic gl = col.Width;
                    currentWidth = (double)gl.Value;
                }
                catch
                {
                    currentWidth = vm.GroupsColumnWidth;
                }

                var newWidth = Math.Max(80, currentWidth + e.Vector.X);
                vm.GroupsColumnWidth = newWidth;

                // update column width immediately
                grid.ColumnDefinitions[0].Width = new GridLength(newWidth, GridUnitType.Pixel);
            }
            catch { }
        }

        private void GroupsListBox_OnDoubleTapped(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not MainWindowViewModel vm) return;
            if (sender is not ListBox lb) return;

            var item = lb.SelectedItem as ModGroupViewModel;
            vm.ToggleActiveFilter(item);
        }

        // ----- New: centralized rename helpers and handlers -----

        private void RenameEditor_SaveClicked(object? sender, EventArgs e)
        {
            if (sender is RenameEditor editor)
            {
                SharedRenameSave(editor);
            }
        }

        private void RenameEditor_CancelClicked(object? sender, EventArgs e)
        {
            if (sender is RenameEditor editor)
            {
                SharedRenameCancel(editor);
            }
        }

        private void SharedRenameSave(RenameEditor editor)
        {
            // Find nearest DataContext (could be ModGroupViewModel or CustomModList)
            var vm = editor.DataContext;
            if (vm == null) return;

            if (vm is ModGroupViewModel groupVm && DataContext is MainWindowViewModel mainVm)
            {
                mainVm.ConfirmRenameGroupCommand.Execute(groupVm).Subscribe();
                // remove visual highlight
                editor.SetHighlight(false);
            }
            else if (vm is CustomModList listVm && DataContext is MainWindowViewModel mm)
            {
                mm.ConfirmRenameModListCommand.Execute(listVm).Subscribe();
                editor.SetHighlight(false);
            }
        }

        private static void SharedRenameCancel(RenameEditor editor)
        {
            var vm = editor.DataContext;
            if (vm == null) return;

            if (vm is ModGroupViewModel groupVm)
            {
                groupVm.IsRenaming = false;
                groupVm.EditedName = groupVm.Name;
                editor.SetHighlight(false);
            }
            else if (vm is CustomModList listVm)
            {
                listVm.IsRenaming = false;
                listVm.EditedName = listVm.Name;
                editor.SetHighlight(false);
            }
        }
    }
}