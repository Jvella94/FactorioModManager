// Views/LogWindow.axaml.cs
using Avalonia.Controls;
using Avalonia.Interactivity;
using FactorioModManager.Services.Infrastructure;
using FactorioModManager.ViewModels.Dialogs;
using System;
using System.Reactive.Linq;

namespace FactorioModManager.Views
{
    public partial class LogWindow : Window
    {
        private readonly LogWindowViewModel _viewModel;

        public LogWindow(ILogService logService)
        {
            InitializeComponent();
            _viewModel = new LogWindowViewModel(logService);
            DataContext = _viewModel;
        }

        public LogWindow()
            : this(ServiceContainer.Instance.Resolve<ILogService>())
        {
        }

        private async void Window_Loaded(object? sender, RoutedEventArgs e)
        {
            // ✅ Auto-refresh when window opens
            if (DataContext is LogWindowViewModel vm)
            {
                await vm.RefreshCommand.Execute();
                LogScrollViewer.ScrollToEnd();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _viewModel?.Dispose();
            base.OnClosed(e);
        }
    }
}