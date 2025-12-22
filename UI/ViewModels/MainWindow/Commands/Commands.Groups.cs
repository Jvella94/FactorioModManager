using ReactiveUI;
using System.Reactive;
using System;
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