using Avalonia.Platform.Storage;
using FactorioModManager.Models;
using FactorioModManager.Services.Infrastructure;
using ReactiveUI;
using System;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;

namespace FactorioModManager.ViewModels.MainWindow
{
    public partial class MainWindowViewModel
    {
        public ReactiveCommand<Unit, Unit> CreateGroupCommand { get; private set; } = null!;
        public ReactiveCommand<ModGroupViewModel, Unit> ToggleGroupCommand { get; private set; } = null!;
        public ReactiveCommand<Unit, Unit> AddToGroupCommand { get; private set; } = null!;
        public ReactiveCommand<Unit, Unit> AddMultipleToGroupCommand { get; private set; } = null!;
        public ReactiveCommand<Unit, Unit> RemoveFromGroupCommand { get; private set; } = null!;
        public ReactiveCommand<Unit, Unit> RemoveMultipleFromGroupCommand { get; private set; } = null!;
        public ReactiveCommand<DeleteGroupRequest, Unit> DeleteGroupCommand { get; private set; } = null!;
        public ReactiveCommand<ModGroupViewModel, Unit> RenameGroupCommand { get; private set; } = null!;
        public ReactiveCommand<ModGroupViewModel, Unit> ConfirmRenameGroupCommand { get; private set; } = null!;
        public ReactiveCommand<Unit, Unit> ToggleGroupsCommand { get; private set; } = null!;
        public ReactiveCommand<Unit, Unit> CreateModListCommand { get; private set; } = null!;
        public ReactiveCommand<string, Unit> DeleteModListCommand { get; private set; } = null!;
        public ReactiveCommand<string, Unit> ApplyModListCommand { get; private set; } = null!;
        public ReactiveCommand<(string oldName, string newName), Unit> RenameModListCommand { get; private set; } = null!;
        public ReactiveCommand<CustomModList, Unit> StartRenameModListCommand { get; private set; } = null!;
        public ReactiveCommand<CustomModList, Unit> ConfirmRenameModListCommand { get; private set; } = null!;

        // New commands for import/export
        public ReactiveCommand<Unit, Unit> ImportListCommand { get; private set; } = null!;

        public ReactiveCommand<string, Unit> ExportListCommand { get; private set; } = null!;

        private void InitializeGroupCommands()
        {
            CreateGroupCommand = ReactiveCommand.Create(CreateGroup);
            ToggleGroupCommand = ReactiveCommand.Create<ModGroupViewModel>(ToggleGroup);
            AddToGroupCommand = ReactiveCommand.Create(AddToGroup);
            AddMultipleToGroupCommand = ReactiveCommand.Create(AddMultipleToGroup);
            RemoveFromGroupCommand = ReactiveCommand.Create(RemoveFromGroup);
            RemoveMultipleFromGroupCommand = ReactiveCommand.Create(RemoveMultipleFromGroup);
            DeleteGroupCommand = ReactiveCommand.Create<DeleteGroupRequest>(req =>
                DeleteGroupInternal(req.Group, req.Owner));
            RenameGroupCommand = ReactiveCommand.Create<ModGroupViewModel>(StartRenameGroup);
            ConfirmRenameGroupCommand = ReactiveCommand.Create<ModGroupViewModel>(ConfirmRenameGroup);
            // Toggle groups panel visibility
            ToggleGroupsCommand = ReactiveCommand.Create(() =>
            {
                // ToggleButton two-way binding will already update AreGroupsVisible before the Command runs.
                // Do not invert here — just persist the current value.
                try { _settingsService.SetShowGroupsPanel(AreGroupsVisible); } catch { }
            });

            // Mod list commands (create/load/delete)
            CreateModListCommand = ReactiveCommand.Create(CreateModList);
            DeleteModListCommand = ReactiveCommand.Create<string>(name => DeleteModList(name));
            // Apply: if name parameter is null/empty, open mod list picker
            ApplyModListCommand = ReactiveCommand.CreateFromTask<string>(async name =>
            {
                var targetName = name;
                if (string.IsNullOrEmpty(targetName))
                {
                    // If no name passed, show pick dialog with available lists
                    var names = ModLists.Select(l => l.Name).ToList();
                    if (names.Count == 0)
                    {
                        SetStatus("No saved mod lists available", LogLevel.Warning);
                        return;
                    }
                    var pick = await _uiService.ShowPickModListAsync(names, "Apply Mod List", _uiService.GetMainWindow());
                    if (string.IsNullOrEmpty(pick))
                        return;
                    targetName = pick;
                }

                await ApplyModList(targetName);
            });
            RenameModListCommand = ReactiveCommand.Create<(string oldName, string newName)>(tuple => RenameModList(tuple.oldName, tuple.newName));
            StartRenameModListCommand = ReactiveCommand.Create<CustomModList>(list => StartRenameModList(list));
            ConfirmRenameModListCommand = ReactiveCommand.Create<CustomModList>(list => ConfirmRenameModList(list));

            // Import/Export commands using StorageProvider API
            ImportListCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                var owner = _uiService.GetMainWindow();
                if (owner == null)
                {
                    SetStatus("Cannot open file dialog: main window not available", LogLevel.Warning);
                    return;
                }

                var provider = owner.StorageProvider;
                if (provider == null)
                {
                    SetStatus("No storage provider available", LogLevel.Warning);
                    return;
                }

                var options = new FilePickerOpenOptions
                {
                    Title = "Import Mod List",
                    AllowMultiple = false,
                    FileTypeFilter =
                    [
                        new FilePickerFileType("JSON") { Patterns = ["*.json"], MimeTypes = ["application/json"] }
                    ]
                };

                var result = await provider.OpenFilePickerAsync(options);
                if (result == null || result.Count == 0) return;

                var file = result[0];
                var sourcePath = file.Path.LocalPath;
                if (string.IsNullOrEmpty(sourcePath))
                {
                    // If no local path (virtual file), try copy to temp then import
                    try
                    {
                        var tmp = Path.GetTempFileName();
                        await using (var read = await file.OpenReadAsync())
                        await using (var fs = File.Create(tmp))
                        {
                            await read.CopyToAsync(fs);
                        }
                        sourcePath = tmp;
                    }
                    catch (Exception ex)
                    {
                        SetStatus($"Failed to access picked file: {ex.Message}", LogLevel.Error);
                        return;
                    }
                }

                // Read candidate list from file without importing so we can inspect name
                CustomModList? candidate = null;
                try
                {
                    var json = File.ReadAllText(sourcePath);
                    candidate = System.Text.Json.JsonSerializer.Deserialize<CustomModList>(json, Constants.JsonOptions.CaseInsensitive);
                    if (candidate == null)
                    {
                        SetStatus("Invalid mod list file", LogLevel.Error);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    SetStatus($"Failed to read list file: {ex.Message}", LogLevel.Error);
                    return;
                }

                // If a list with same name exists, prompt for behavior; otherwise import directly
                var existingConflict = ModLists.FirstOrDefault(l => string.Equals(l.Name, candidate.Name, StringComparison.OrdinalIgnoreCase));
                var behaviorEnum = ImportBehavior.Keep;
                if (existingConflict != null)
                {
                    var behavior = await _uiService.ShowImportBehaviorDialogAsync(candidate, owner);
                    if (behavior == null) return;
                    behaviorEnum = behavior.Value;
                }

                // Now apply chosen behavior
                if (existingConflict == null || behaviorEnum == ImportBehavior.Keep)
                {
                    var imported = _modListService.ImportListFromFile(sourcePath);
                    if (imported == null)
                    {
                        SetStatus("Failed to import list", LogLevel.Error);
                        return;
                    }

                    ModLists.Add(imported);
                    var notifier = ServiceContainer.Instance.Resolve<INotificationService>();
                    notifier.Show("Import Successful", $"Imported list: {imported.Name}", NotificationType.Success);
                    SetStatus($"Imported list: {imported.Name}");
                    return;
                }

                // Overwrite
                if (behaviorEnum == ImportBehavior.Overwrite)
                {
                    // Deserialize candidate already done; update existing with candidate content
                    try
                    {
                        _modListService.UpdateList(existingConflict.Name, candidate);
                        // reload in-memory list
                        existingConflict.Entries = candidate.Entries;
                        existingConflict.Description = candidate.Description;
                        var notifier = ServiceContainer.Instance.Resolve<INotificationService>();
                        notifier.Show("Import Successful", $"Imported and overwrote list: {existingConflict.Name}", NotificationType.Success);
                        SetStatus($"Imported and overwrote list: {existingConflict.Name}");
                        return;
                    }
                    catch (Exception ex)
                    {
                        SetStatus($"Failed to overwrite list: {ex.Message}", LogLevel.Error);
                        return;
                    }
                }

                // Merge
                if (behaviorEnum == ImportBehavior.Merge)
                {
                    try
                    {
                        var map = existingConflict.Entries.ToDictionary(e => e.Name, e => e, StringComparer.OrdinalIgnoreCase);
                        if (candidate.Entries != null)
                        {
                            foreach (var e in candidate.Entries)
                            {
                                map[e.Name] = e;
                            }
                        }

                        existingConflict.Entries = [.. map.Values];
                        _modListService.UpdateList(existingConflict.Name, existingConflict);
                        var notifier = ServiceContainer.Instance.Resolve<INotificationService>();
                        notifier.Show("Import Successful", $"Merged imported list into: {existingConflict.Name}", NotificationType.Success);
                        SetStatus($"Merged imported list into existing: {existingConflict.Name}");
                        return;
                    }
                    catch (Exception ex)
                    {
                        SetStatus($"Failed to merge list: {ex.Message}", LogLevel.Error);
                        return;
                    }
                }
            });

            ExportListCommand = ReactiveCommand.CreateFromTask<string>(async name =>
            {
                var owner = _uiService.GetMainWindow();
                if (owner == null)
                {
                    SetStatus("Cannot open save dialog: main window not available", LogLevel.Warning);
                    return;
                }

                var provider = owner.StorageProvider;
                if (provider == null)
                {
                    SetStatus("No storage provider available", LogLevel.Warning);
                    return;
                }

                var options = new FilePickerSaveOptions
                {
                    Title = "Export Mod List",
                    // attempt to set suggested filename via reflection for providers that support it
                    FileTypeChoices =
                    [
                        new FilePickerFileType("JSON") { Patterns = ["*.json"], MimeTypes = ["application/json"] }
                    ]
                };

                // Try to suggest a filename by reflection; also create a temp file with desired name as a fallback hint on some platforms
                var suggested = MakeSafeFileName(name) + ".json";
                var tempPath = Path.Combine(Path.GetTempPath(), suggested);
                try
                {
                    if (!File.Exists(tempPath)) File.WriteAllText(tempPath, string.Empty);
                }
                catch { }

                try
                {
                    var prop = options.GetType().GetProperty("SuggestedFileName") ?? options.GetType().GetProperty("DefaultFileName");
                    if (prop != null && prop.CanWrite)
                        prop.SetValue(options, suggested);
                }
                catch { }

                var storageFile = await provider.SaveFilePickerAsync(options);

                try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }

                if (storageFile == null) return;

                // Get source file path from service
                var sourcePath = _modListService.GetListFilePath(name);
                if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
                {
                    SetStatus($"Source list file missing: {name}", LogLevel.Error);
                    return;
                }

                try
                {
                    await using var src = File.OpenRead(sourcePath);
                    await using var dst = await storageFile.OpenWriteAsync();
                    await src.CopyToAsync(dst);
                    await dst.FlushAsync();
                }
                catch (Exception ex)
                {
                    SetStatus($"Export failed: {ex.Message}", LogLevel.Error);
                    return;
                }

                var notifier2 = ServiceContainer.Instance.Resolve<INotificationService>();
                notifier2.Show("Export Successful", $"Exported list '{name}' to {storageFile.Name}", NotificationType.Success);
                SetStatus($"Exported list to {storageFile.Name}");
            });

            // Also persist width when it changes (listen to property changes) but only when panel is visible
            this.WhenAnyValue(x => x.GroupsColumnWidth, x => x.AreGroupsVisible)
                .Throttle(TimeSpan.FromMilliseconds(250))
                .Subscribe(tuple =>
                {
                    var (w, visible) = tuple;
                    if (visible)
                    {
                        try { _settingsService.SetGroupsColumnWidth(w); } catch { }
                    }
                });
        }

        private static string MakeSafeFileName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            name = name.Trim();
            if (string.IsNullOrEmpty(name)) name = "list";
            if (name.Length > 80) name = name[..80];
            return name;
        }
    }
}