using FactorioModManager.Models;
using ReactiveUI;
using System;
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
    }
}