namespace FactorioModManager.ViewModels.MainWindow
{
    public partial class MainWindowViewModel
    {
        private void InitializeCommands()
        {
            InitializeModCommands();
            InitializeGroupCommands();
            InitializeNavigationCommands();
            InitializeDialogCommands();
        }
    }
}
