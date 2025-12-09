// Views/LogWindow.axaml.cs
using Avalonia.Controls;
using Avalonia.Interactivity;
using FactorioModManager.Services.Infrastructure;
using FactorioModManager.ViewModels.Dialogs;
using System;

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

        // ✅ Only platform-specific logic remains
        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);
            // Scroll to bottom after window loads
            LogScrollViewer.ScrollToEnd();
        }

        protected override void OnClosed(EventArgs e)
        {
            _viewModel?.Dispose();
            base.OnClosed(e);
        }
    }
}