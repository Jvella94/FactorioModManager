using ReactiveUI;
using System.Reactive;

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
        }
    }
}