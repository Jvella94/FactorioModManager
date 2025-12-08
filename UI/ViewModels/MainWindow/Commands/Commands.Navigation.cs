using ReactiveUI;
using System.Reactive;

namespace FactorioModManager.ViewModels.MainWindow
{
    public partial class MainWindowViewModel
    {
        public ReactiveCommand<string, Unit> NavigateToDependencyCommand { get; private set; } = null!;
        public ReactiveCommand<Unit, Unit> NavigateBackCommand { get; private set; } = null!;
        public ReactiveCommand<Unit, Unit> NavigateForwardCommand { get; private set; } = null!;

        private void InitializeNavigationCommands()
        {
            var canNavigateBack = this.WhenAnyValue(x => x.CanNavigateBack);
            var canNavigateForward = this.WhenAnyValue(x => x.CanNavigateForward);

            NavigateBackCommand = ReactiveCommand.Create(NavigateBack, canNavigateBack);
            NavigateForwardCommand = ReactiveCommand.Create(NavigateForward, canNavigateForward);
            NavigateToDependencyCommand = ReactiveCommand.Create<string>(NavigateToDependency);
        }
    }
}