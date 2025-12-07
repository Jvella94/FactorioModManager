using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using ReactiveUI;
using FactorioModManager.Services;
using FactorioModManager.Models;

namespace FactorioModManager.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        private ObservableCollection<ModViewModel> _mods;
        private ObservableCollection<ModGroupViewModel> _groups;
        private ObservableCollection<ModViewModel> _selectedMods;
        private ModViewModel? _selectedMod;
        private ModGroupViewModel? _selectedGroup;
        private string _searchText = string.Empty;
        private bool _showDisabled = true;
        private bool _filterBySelectedGroup = false;
        private string _statusText = "Ready";
        private string? _selectedAuthorFilter;
        private string _authorSearchText = string.Empty;
        private readonly ModService _modService;
        private readonly ModGroupService _groupService;

        public MainWindowViewModel()
        {
            _mods = new ObservableCollection<ModViewModel>();
            _groups = new ObservableCollection<ModGroupViewModel>();
            _selectedMods = new ObservableCollection<ModViewModel>();
            _modService = new ModService();
            _groupService = new ModGroupService();

            // Commands
            RefreshModsCommand = ReactiveCommand.CreateFromTask(RefreshModsAsync);
            InstallModCommand = ReactiveCommand.Create(InstallMod);
            OpenModFolderCommand = ReactiveCommand.Create(OpenModFolder);
            ToggleModCommand = ReactiveCommand.Create<ModViewModel>(ToggleMod);
            RemoveModCommand = ReactiveCommand.Create<ModViewModel>(RemoveMod);
            CreateGroupCommand = ReactiveCommand.Create(CreateGroup);
            ToggleGroupCommand = ReactiveCommand.Create<ModGroupViewModel>(ToggleGroup);
            AddToGroupCommand = ReactiveCommand.Create(AddToGroup);
            AddMultipleToGroupCommand = ReactiveCommand.Create(AddMultipleToGroup);
            RemoveFromGroupCommand = ReactiveCommand.Create(RemoveFromGroup);
            RemoveMultipleFromGroupCommand = ReactiveCommand.Create(RemoveMultipleFromGroup);
            DeleteGroupCommand = ReactiveCommand.Create<ModGroupViewModel>(DeleteGroup);
            RenameGroupCommand = ReactiveCommand.Create<ModGroupViewModel>(StartRenameGroup);
            ConfirmRenameGroupCommand = ReactiveCommand.Create<ModGroupViewModel>(ConfirmRenameGroup);
            OpenModPortalCommand = ReactiveCommand.Create(OpenModPortal);

            // Set up filtering
            this.WhenAnyValue(
                x => x.SearchText,
                x => x.ShowDisabled,
                x => x.SelectedAuthorFilter,
                x => x.SelectedGroup,
                x => x.FilterBySelectedGroup)
                .Throttle(TimeSpan.FromMilliseconds(300))
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(_ => UpdateFilteredMods());

            // Filter authors when search text changes
            this.WhenAnyValue(x => x.AuthorSearchText)
                .Throttle(TimeSpan.FromMilliseconds(200))
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(_ => UpdateFilteredAuthors());

            // Sync AuthorSearchText when SelectedAuthorFilter changes
            this.WhenAnyValue(x => x.SelectedAuthorFilter)
                .Subscribe(author =>
                {
                    if (!string.IsNullOrEmpty(author))
                    {
                        AuthorSearchText = author;
                    }
                });

            // Load thumbnail when mod selection changes
            this.WhenAnyValue(x => x.SelectedMod)
                .Subscribe(async mod =>
                {
                    if (mod != null)
                    {
                        await LoadThumbnailAsync(mod);
                    }
                });

            // Auto-load on startup
            _ = RefreshModsAsync();
        }

        public ObservableCollection<ModViewModel> Mods
        {
            get => _mods;
            set => this.RaiseAndSetIfChanged(ref _mods, value);
        }

        public ObservableCollection<ModViewModel> FilteredMods { get; } = new();

        public ObservableCollection<ModGroupViewModel> Groups
        {
            get => _groups;
            set => this.RaiseAndSetIfChanged(ref _groups, value);
        }

        public ObservableCollection<ModViewModel> SelectedMods
        {
            get => _selectedMods;
            set => this.RaiseAndSetIfChanged(ref _selectedMods, value);
        }

        public ObservableCollection<string> Authors { get; } = new();
        public ObservableCollection<string> FilteredAuthors { get; } = new();
        private Dictionary<string, int> _authorModCounts = new();

        public ModViewModel? SelectedMod
        {
            get => _selectedMod;
            set => this.RaiseAndSetIfChanged(ref _selectedMod, value);
        }

        public ModGroupViewModel? SelectedGroup
        {
            get => _selectedGroup;
            set => this.RaiseAndSetIfChanged(ref _selectedGroup, value);
        }

        public string SearchText
        {
            get => _searchText;
            set => this.RaiseAndSetIfChanged(ref _searchText, value);
        }

        public bool ShowDisabled
        {
            get => _showDisabled;
            set => this.RaiseAndSetIfChanged(ref _showDisabled, value);
        }

        public bool FilterBySelectedGroup
        {
            get => _filterBySelectedGroup;
            set => this.RaiseAndSetIfChanged(ref _filterBySelectedGroup, value);
        }

        public string? SelectedAuthorFilter
        {
            get => _selectedAuthorFilter;
            set => this.RaiseAndSetIfChanged(ref _selectedAuthorFilter, value);
        }

        public string AuthorSearchText
        {
            get => _authorSearchText;
            set => this.RaiseAndSetIfChanged(ref _authorSearchText, value);
        }

        public string StatusText
        {
            get => _statusText;
            set => this.RaiseAndSetIfChanged(ref _statusText, value);
        }

        public string ModCountText => $"Mods: {FilteredMods.Count} / {Mods.Count}";

        public ReactiveCommand<Unit, Unit> RefreshModsCommand { get; }
        public ReactiveCommand<Unit, Unit> InstallModCommand { get; }
        public ReactiveCommand<Unit, Unit> OpenModFolderCommand { get; }
        public ReactiveCommand<ModViewModel, Unit> ToggleModCommand { get; }
        public ReactiveCommand<ModViewModel, Unit> RemoveModCommand { get; }
        public ReactiveCommand<Unit, Unit> CreateGroupCommand { get; }
        public ReactiveCommand<ModGroupViewModel, Unit> ToggleGroupCommand { get; }
        public ReactiveCommand<Unit, Unit> AddToGroupCommand { get; }
        public ReactiveCommand<Unit, Unit> AddMultipleToGroupCommand { get; }
        public ReactiveCommand<Unit, Unit> RemoveFromGroupCommand { get; }
        public ReactiveCommand<Unit, Unit> RemoveMultipleFromGroupCommand { get; }
        public ReactiveCommand<ModGroupViewModel, Unit> DeleteGroupCommand { get; }
        public ReactiveCommand<ModGroupViewModel, Unit> RenameGroupCommand { get; }
        public ReactiveCommand<ModGroupViewModel, Unit> ConfirmRenameGroupCommand { get; }
        public ReactiveCommand<Unit, Unit> OpenModPortalCommand { get; }

        private async Task RefreshModsAsync()
        {
            await Task.Run(() =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    StatusText = "Refreshing mods...";
                });

                try
                {
                    var loadedMods = _modService.LoadAllMods();
                    var loadedGroups = _groupService.LoadGroups();

                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        Mods.Clear();
                        Authors.Clear();
                        _authorModCounts.Clear();

                        var authorCounts = new Dictionary<string, int>();

                        foreach (var (info, isEnabled, lastUpdated, thumbnailPath) in loadedMods)
                        {
                            var modVm = new ModViewModel
                            {
                                Name = info.Name,
                                Title = info.Title ?? info.Name,
                                Version = info.Version,
                                Author = info.Author,
                                Description = info.Description ?? "",
                                IsEnabled = isEnabled,
                                Dependencies = info.Dependencies,
                                LastUpdated = lastUpdated,
                                ThumbnailPath = thumbnailPath
                            };

                            // Determine group
                            var group = loadedGroups.FirstOrDefault(g => g.ModNames.Contains(modVm.Title));
                            if (group != null)
                            {
                                modVm.GroupName = group.Name;
                            }

                            Mods.Add(modVm);

                            // Count mods per author
                            if (!string.IsNullOrEmpty(info.Author))
                            {
                                if (!authorCounts.ContainsKey(info.Author))
                                {
                                    authorCounts[info.Author] = 0;
                                }
                                authorCounts[info.Author]++;
                            }
                        }

                        // Sort by LastUpdated descending
                        var sortedMods = Mods.OrderByDescending(m => m.LastUpdated ?? DateTime.MinValue).ToList();
                        Mods.Clear();
                        foreach (var mod in sortedMods)
                        {
                            Mods.Add(mod);
                        }

                        // Build author list sorted by count descending (no "All Authors" entry)
                        _authorModCounts = authorCounts;

                        var sortedAuthors = authorCounts
                            .OrderByDescending(kvp => kvp.Value)
                            .Select(kvp => $"{kvp.Key} ({kvp.Value})")
                            .ToList();

                        foreach (var author in sortedAuthors)
                        {
                            Authors.Add(author);
                        }

                        Groups.Clear();
                        foreach (var group in loadedGroups)
                        {
                            var groupVm = new ModGroupViewModel
                            {
                                Name = group.Name,
                                Description = group.Description,
                                ModNames = group.ModNames
                            };
                            UpdateGroupStatus(groupVm);
                            Groups.Add(groupVm);
                        }

                        // Start with empty filter
                        SelectedAuthorFilter = null;
                        AuthorSearchText = string.Empty;
                        UpdateFilteredAuthors();
                        UpdateFilteredMods();
                        StatusText = $"Loaded {Mods.Count} mods and {Groups.Count} groups";
                    });
                }
                catch (Exception ex)
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        StatusText = $"Error: {ex.Message}";
                    });
                }
            });
        }

        private void UpdateFilteredMods()
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                FilteredMods.Clear();

                var filtered = Mods.Where(m =>
                {
                    // Show disabled filter
                    if (!ShowDisabled && !m.IsEnabled) return false;

                    // Search text filter
                    if (!string.IsNullOrEmpty(SearchText) &&
                        !m.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                        return false;

                    // Author filter (extract author name from "Author (count)" format)
                    if (!string.IsNullOrEmpty(SelectedAuthorFilter))
                    {
                        var authorName = SelectedAuthorFilter.Split('(')[0].Trim();
                        if (m.Author != authorName) return false;
                    }

                    // Group filter
                    if (FilterBySelectedGroup && SelectedGroup != null)
                    {
                        if (!SelectedGroup.ModNames.Contains(m.Title))
                            return false;
                    }

                    return true;
                });

                foreach (var mod in filtered)
                {
                    FilteredMods.Add(mod);
                }

                this.RaisePropertyChanged(nameof(ModCountText));
            });
        }


        private void UpdateFilteredAuthors()
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                FilteredAuthors.Clear();

                var searchLower = AuthorSearchText?.ToLower() ?? "";

                // Always show all authors if search is empty or filter matching authors
                foreach (var author in Authors)
                {
                    if (string.IsNullOrEmpty(searchLower) ||
                        author.ToLower().Contains(searchLower))
                    {
                        FilteredAuthors.Add(author);
                    }
                }
            });
        }


        private void UpdateGroupStatus(ModGroupViewModel group)
        {
            var groupModNames = group.ModNames;
            var enabledCount = Mods.Count(m => groupModNames.Contains(m.Title) && m.IsEnabled);
            group.EnabledCount = enabledCount;
            group.TotalCount = groupModNames.Count;
        }

        private async Task LoadThumbnailAsync(ModViewModel mod)
        {
            if (string.IsNullOrEmpty(mod.ThumbnailPath))
            {
                mod.Thumbnail = null;
                return;
            }

            await Task.Run(() =>
            {
                try
                {
                    Bitmap? thumbnail = null;

                    if (mod.ThumbnailPath.Contains("|"))
                    {
                        // Load from zip
                        var parts = mod.ThumbnailPath.Split('|');
                        using var archive = ZipFile.OpenRead(parts[0]);
                        var entry = archive.GetEntry(parts[1]);
                        if (entry != null)
                        {
                            using var stream = entry.Open();
                            using var memStream = new MemoryStream();
                            stream.CopyTo(memStream);
                            memStream.Position = 0;
                            thumbnail = new Bitmap(memStream);
                        }
                    }
                    else if (File.Exists(mod.ThumbnailPath))
                    {
                        // Load from file
                        thumbnail = new Bitmap(mod.ThumbnailPath);
                    }

                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        mod.Thumbnail = thumbnail;
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading thumbnail: {ex.Message}");
                }
            });
        }

        private void InstallMod()
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                StatusText = "Installing mod...";
            });
        }

        private void OpenModFolder()
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    var path = ModPathHelper.GetModsDirectory();
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = path,
                        UseShellExecute = true
                    });
                    StatusText = $"Opened: {path}";
                }
                catch (Exception ex)
                {
                    StatusText = $"Error opening folder: {ex.Message}";
                }
            });
        }

        private void OpenModPortal()
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (SelectedMod != null)
                {
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = SelectedMod.ModPortalUrl,
                            UseShellExecute = true
                        });
                        StatusText = $"Opened mod portal for {SelectedMod.Title}";
                    }
                    catch (Exception ex)
                    {
                        StatusText = $"Error opening browser: {ex.Message}";
                    }
                }
            });
        }

        private void ToggleMod(ModViewModel mod)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (mod != null)
                {
                    mod.IsEnabled = !mod.IsEnabled;
                    _modService.ToggleMod(mod.Name, mod.IsEnabled);

                    // Update group statuses
                    foreach (var group in Groups)
                    {
                        UpdateGroupStatus(group);
                    }

                    StatusText = $"{mod.Title} {(mod.IsEnabled ? "enabled" : "disabled")}";
                }
            });
        }

        private void RemoveMod(ModViewModel mod)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (mod != null)
                {
                    Mods.Remove(mod);
                    UpdateFilteredMods();
                    StatusText = $"Removed {mod.Title}";
                }
            });
        }

        private void CreateGroup()
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                var groupName = $"New Group {Groups.Count + 1}";
                var newGroup = new ModGroup
                {
                    Name = groupName,
                    Description = "New mod group",
                    ModNames = new List<string>()
                };

                _groupService.AddGroup(newGroup);

                var groupVm = new ModGroupViewModel
                {
                    Name = newGroup.Name,
                    Description = newGroup.Description,
                    ModNames = newGroup.ModNames
                };
                UpdateGroupStatus(groupVm);
                Groups.Add(groupVm);

                StatusText = $"Created group: {groupName}";
            });
        }

        private void ToggleGroup(ModGroupViewModel group)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (group != null)
                {
                    var enableGroup = group.EnabledCount < group.TotalCount;

                    foreach (var modName in group.ModNames)
                    {
                        var mod = Mods.FirstOrDefault(m => m.Title == modName);
                        if (mod != null && mod.IsEnabled != enableGroup)
                        {
                            mod.IsEnabled = enableGroup;
                            _modService.ToggleMod(mod.Name, mod.IsEnabled);
                        }
                    }

                    UpdateGroupStatus(group);
                    StatusText = $"Group '{group.Name}' {(enableGroup ? "enabled" : "disabled")}";
                }
            });
        }

        private void AddToGroup()
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (SelectedMod != null && SelectedGroup != null)
                {
                    if (!SelectedGroup.ModNames.Contains(SelectedMod.Title))
                    {
                        SelectedGroup.ModNames.Add(SelectedMod.Title);
                        SelectedMod.GroupName = SelectedGroup.Name;

                        var groups = _groupService.LoadGroups();
                        var group = groups.FirstOrDefault(g => g.Name == SelectedGroup.Name);
                        if (group != null)
                        {
                            group.ModNames = SelectedGroup.ModNames;
                            _groupService.SaveGroups(groups);
                        }

                        UpdateGroupStatus(SelectedGroup);
                        StatusText = $"Added '{SelectedMod.Title}' to group '{SelectedGroup.Name}'";
                    }
                }
                else
                {
                    StatusText = "Select a mod and a group first";
                }
            });
        }

        private void AddMultipleToGroup()
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (SelectedMods.Count > 0 && SelectedGroup != null)
                {
                    var addedCount = 0;
                    foreach (var mod in SelectedMods)
                    {
                        if (!SelectedGroup.ModNames.Contains(mod.Title))
                        {
                            SelectedGroup.ModNames.Add(mod.Title);
                            mod.GroupName = SelectedGroup.Name;
                            addedCount++;
                        }
                    }

                    if (addedCount > 0)
                    {
                        var groups = _groupService.LoadGroups();
                        var group = groups.FirstOrDefault(g => g.Name == SelectedGroup.Name);
                        if (group != null)
                        {
                            group.ModNames = SelectedGroup.ModNames;
                            _groupService.SaveGroups(groups);
                        }

                        UpdateGroupStatus(SelectedGroup);
                        StatusText = $"Added {addedCount} mods to group '{SelectedGroup.Name}'";
                    }
                }
                else
                {
                    StatusText = "Select mods and a group first";
                }
            });
        }

        private void RemoveFromGroup()
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (SelectedMod != null && SelectedGroup != null)
                {
                    if (SelectedGroup.ModNames.Contains(SelectedMod.Title))
                    {
                        SelectedGroup.ModNames.Remove(SelectedMod.Title);
                        SelectedMod.GroupName = "N/A";

                        var groups = _groupService.LoadGroups();
                        var group = groups.FirstOrDefault(g => g.Name == SelectedGroup.Name);
                        if (group != null)
                        {
                            group.ModNames = SelectedGroup.ModNames;
                            _groupService.SaveGroups(groups);
                        }

                        UpdateGroupStatus(SelectedGroup);
                        StatusText = $"Removed '{SelectedMod.Title}' from group '{SelectedGroup.Name}'";
                    }
                }
                else
                {
                    StatusText = "Select a mod and a group first";
                }
            });
        }

        private void RemoveMultipleFromGroup()
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (SelectedMods.Count > 0 && SelectedGroup != null)
                {
                    var removedCount = 0;
                    foreach (var mod in SelectedMods.ToList())
                    {
                        if (SelectedGroup.ModNames.Contains(mod.Title))
                        {
                            SelectedGroup.ModNames.Remove(mod.Title);
                            mod.GroupName = "N/A";
                            removedCount++;
                        }
                    }

                    if (removedCount > 0)
                    {
                        var groups = _groupService.LoadGroups();
                        var group = groups.FirstOrDefault(g => g.Name == SelectedGroup.Name);
                        if (group != null)
                        {
                            group.ModNames = SelectedGroup.ModNames;
                            _groupService.SaveGroups(groups);
                        }

                        UpdateGroupStatus(SelectedGroup);
                        StatusText = $"Removed {removedCount} mods from group '{SelectedGroup.Name}'";
                    }
                }
                else
                {
                    StatusText = "Select mods and a group first";
                }
            });
        }

        private void StartRenameGroup(ModGroupViewModel group)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (group != null)
                {
                    group.IsEditing = true;
                    group.EditName = group.Name;
                }
            });
        }

        private void ConfirmRenameGroup(ModGroupViewModel group)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (group != null && !string.IsNullOrWhiteSpace(group.EditName))
                {
                    var oldName = group.Name;
                    var newName = group.EditName.Trim();

                    if (oldName != newName)
                    {
                        var groups = _groupService.LoadGroups();
                        var groupData = groups.FirstOrDefault(g => g.Name == oldName);
                        if (groupData != null)
                        {
                            groupData.Name = newName;
                            _groupService.SaveGroups(groups);

                            group.Name = newName;

                            // Update mods that reference this group
                            foreach (var mod in Mods.Where(m => m.GroupName == oldName))
                            {
                                mod.GroupName = newName;
                            }

                            StatusText = $"Renamed group to '{newName}'";
                        }
                    }

                    group.IsEditing = false;
                }
            });
        }

        private void DeleteGroup(ModGroupViewModel group)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (group != null)
                {
                    _groupService.DeleteGroup(group.Name);

                    // Update mods that were in this group
                    foreach (var mod in Mods.Where(m => m.GroupName == group.Name))
                    {
                        mod.GroupName = "N/A";
                    }

                    Groups.Remove(group);
                    StatusText = $"Deleted group: {group.Name}";
                }
            });
        }
    }
}
