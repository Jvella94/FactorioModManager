using System.Linq;

namespace FactorioModManager.ViewModels.MainWindow
{
    public partial class MainWindowVM
    {
        private void CreateGroup()
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                var groupName = $"New Group {Groups.Count + 1}";
                var newGroup = new Models.ModGroup
                {
                    Name = groupName,
                    Description = "New mod group",
                    ModNames = []
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

                    foreach (var mod in Mods.Where(m => m.GroupName == group.Name))
                    {
                        mod.GroupName = "N/A";
                    }

                    Groups.Remove(group);
                    StatusText = $"Deleted group: {group.Name}";
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
    }
}
