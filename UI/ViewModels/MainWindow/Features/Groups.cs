using Avalonia.Controls;
using FactorioModManager.Models;
using ReactiveUI;
using System.Linq;

namespace FactorioModManager.ViewModels.MainWindow
{
    public record DeleteGroupRequest(ModGroupViewModel Group, Window Owner);

    public partial class MainWindowViewModel
    {
        /// <summary>
        /// Creates a new mod group
        /// </summary>
        private void CreateGroup()
        {
            var groupName = $"New Group {Groups.Count + 1}";
            var newGroup = new ModGroup
            {
                Name = groupName,
                Description = "New mod group",
                ModNames = []
            };

            _groupService.AddGroup(newGroup);

            var groupVm = new ModGroupViewModel
            {
                Name = newGroup.Name,
                ModNames = newGroup.ModNames,
                IsRenaming = true,
                EditedName = newGroup.Name
            };
            UpdateGroupStatus(groupVm);
            Groups.Add(groupVm);
            SetStatus($"Created group: {groupName}");
        }

        /// <summary>
        /// Toggles all mods in a group on/off
        /// </summary>
        private void ToggleGroup(ModGroupViewModel? group)
        {
            if (group == null)
                return;

            var enableGroup = group.EnabledCount < group.TotalCount;
            foreach (var modName in group.ModNames)
            {
                var mod = _allMods.FirstOrDefault(m => m.Title == modName);
                if (mod != null && mod.IsEnabled != enableGroup)
                {
                    mod.IsEnabled = enableGroup;
                    _modService.ToggleMod(mod.Name, mod.IsEnabled);
                }
            }

            this.RaisePropertyChanged(nameof(EnabledCountText));
            UpdateGroupStatus(group);
            SetStatus($"Group '{group.Name}' {(enableGroup ? "enabled" : "disabled")}");
        }

        /// <summary>
        /// Adds the selected mod to the selected group
        /// </summary>
        private void AddToGroup()
        {
            if (SelectedMod == null || SelectedGroup == null)
            {
                SetStatus("Select a mod and a group first", LogLevel.Warning);
                return;
            }

            if (SelectedGroup.ModNames.Contains(SelectedMod.Title))
            {
                SetStatus($"'{SelectedMod.Title}' is already in group '{SelectedGroup.Name}'", LogLevel.Warning);
                return;
            }

            SelectedGroup.ModNames.Add(SelectedMod.Title);
            SelectedMod.GroupName = SelectedGroup.Name;
            SaveGroupChanges(SelectedGroup);
            UpdateGroupStatus(SelectedGroup);
            SetStatus($"Added '{SelectedMod.Title}' to group '{SelectedGroup.Name}'");
        }

        /// <summary>
        /// Adds multiple selected mods to the selected group
        /// </summary>
        private void AddMultipleToGroup()
        {
            if (SelectedMods.Count == 0 || SelectedGroup == null)
            {
                SetStatus("Select mods and a group first", LogLevel.Warning);
                return;
            }

            var addedCount = 0;
            foreach (var mod in SelectedMods.ToList())
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
                SaveGroupChanges(SelectedGroup);
                UpdateGroupStatus(SelectedGroup);
                SetStatus($"Added {addedCount} mod(s) to group '{SelectedGroup.Name}'");
            }
            else
            {
                SetStatus("All selected mods are already in this group", LogLevel.Warning);
            }
        }

        /// <summary>
        /// Removes the selected mod from the selected group
        /// </summary>
        private void RemoveFromGroup()
        {
            if (SelectedMod == null || SelectedGroup == null)
            {
                SetStatus("Select a mod and a group first", LogLevel.Warning);
                return;
            }

            if (!SelectedGroup.ModNames.Contains(SelectedMod.Title))
            {
                SetStatus($"'{SelectedMod.Title}' is not in group '{SelectedGroup.Name}'", LogLevel.Warning);
                return;
            }

            SelectedGroup.ModNames.Remove(SelectedMod.Title);
            SelectedMod.GroupName = null;
            SaveGroupChanges(SelectedGroup);
            UpdateGroupStatus(SelectedGroup);
            SetStatus($"Removed '{SelectedMod.Title}' from group '{SelectedGroup.Name}'");
        }

        /// <summary>
        /// Removes multiple selected mods from the selected group
        /// </summary>
        private void RemoveMultipleFromGroup()
        {
            if (SelectedMods.Count == 0 || SelectedGroup == null)
            {
                SetStatus("Select mods and a group first", LogLevel.Warning);
                return;
            }

            var removedCount = 0;
            foreach (var mod in SelectedMods.ToList())
            {
                if (SelectedGroup.ModNames.Contains(mod.Title))
                {
                    SelectedGroup.ModNames.Remove(mod.Title);
                    mod.GroupName = null;
                    removedCount++;
                }
            }

            if (removedCount > 0)
            {
                SaveGroupChanges(SelectedGroup);
                UpdateGroupStatus(SelectedGroup);
                SetStatus($"Removed {removedCount} mod(s) from group '{SelectedGroup.Name}'");
            }
            else
            {
                SetStatus("None of the selected mods are in this group", LogLevel.Warning);
            }
        }

        private async void DeleteGroupInternal(ModGroupViewModel group, Window owner)
        {
            var confirm = await _uiService.ShowConfirmationAsync(
                "Delete Group",
                $"Are you sure you want to delete the mod group '{group.Name}'?", owner);

            if (!confirm)
                return;

            _groupService.DeleteGroup(group.Name);
            Groups.Remove(group);

            foreach (var mod in _allMods.Where(m => m.GroupName == group.Name))
                mod.GroupName = null;

            SetStatus($"Deleted group: {group.Name}");
        }

        /// <summary>
        /// Starts renaming a group
        /// </summary>
        private void StartRenameGroup(ModGroupViewModel? group)
        {
            if (group == null)
                return;

            group.IsRenaming = true;
            group.EditedName = group.Name;
        }

        /// <summary>
        /// Confirms renaming a group
        /// </summary>
        private void ConfirmRenameGroup(ModGroupViewModel? group)
        {
            if (group == null || string.IsNullOrWhiteSpace(group.EditedName))
                return;

            var oldName = group.Name;
            var newName = group.EditedName.Trim();

            if (oldName == newName)
            {
                group.IsRenaming = false;
                return;
            }

            if (Groups.Any(g => g.Name.Equals(newName, System.StringComparison.OrdinalIgnoreCase) && g != group))
            {
                SetStatus($"A group named '{newName}' already exists", LogLevel.Warning);
                return;
            }

            var updatedGroup = new Models.ModGroup
            {
                Name = newName,
                Description = "Mod group",
                ModNames = group.ModNames,
                Color = null
            };

            _groupService.UpdateGroup(oldName, updatedGroup);
            group.Name = newName;
            group.IsRenaming = false;

            foreach (var mod in _allMods.Where(m => m.GroupName == oldName))
            {
                mod.GroupName = newName;
            }

            SetStatus($"Renamed group from '{oldName}' to '{newName}'");
        }

        /// <summary>
        /// Updates a group's status (enabled/total count)
        /// </summary>
        private void UpdateGroupStatus(ModGroupViewModel group)
        {
            var modsInGroup = _allMods.Where(m => group.ModNames.Contains(m.Title)).ToList();
            group.TotalCount = modsInGroup.Count;
            group.EnabledCount = modsInGroup.Count(m => m.IsEnabled);
        }

        /// <summary>
        /// ✅ Saves group changes to the service (for mod list changes)
        /// </summary>
        private void SaveGroupChanges(ModGroupViewModel group)
        {
            var modelGroup = new Models.ModGroup
            {
                Name = group.Name,
                Description = "Mod group",
                ModNames = group.ModNames,
                Color = null
            };

            _groupService.UpdateGroup(group.Name, modelGroup);
        }
    }
}