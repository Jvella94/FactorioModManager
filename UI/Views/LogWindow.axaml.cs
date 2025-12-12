using Avalonia.Controls;
using Avalonia.Interactivity;
using FactorioModManager.Services.Infrastructure;
using FactorioModManager.ViewModels.Dialogs;
using System;
using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia.Threading;

namespace FactorioModManager.Views
{
    public partial class LogWindow : Window
    {
        private readonly LogWindowViewModel _viewModel;
        private NotifyCollectionChangedEventHandler? _logsChangedHandler;
        private PropertyChangedEventHandler? _propChangedHandler;

        public LogWindow(ILogService logService, IUIService uiService)
        {
            InitializeComponent();
            _viewModel = new LogWindowViewModel(logService, uiService);
            DataContext = _viewModel;
        }

        public LogWindow()
            : this(ServiceContainer.Instance.Resolve<ILogService>(), ServiceContainer.Instance.Resolve<IUIService>())
        {
        }

        private void Window_Loaded(object? sender, RoutedEventArgs e)
        {
            if (DataContext is LogWindowViewModel vm)
            {
                vm.RefreshCommand.Execute().Subscribe();
                LogScrollViewer.ScrollToEnd();

                // subscribe to collection changes to auto-scroll when following
                _logsChangedHandler = (s, args) =>
                {
                    if (vm.IsFollowing)
                    {
                        Dispatcher.UIThread.Post(() => LogScrollViewer.ScrollToEnd());
                    }
                };

                vm.Logs.CollectionChanged += _logsChangedHandler;

                // subscribe to property changes for IsFollowing only
                _propChangedHandler = (s, args) =>
                {
                    if (args.PropertyName == nameof(vm.IsFollowing) && vm.IsFollowing)
                    {
                        Dispatcher.UIThread.Post(() => LogScrollViewer.ScrollToEnd());
                    }
                };

                vm.PropertyChanged += _propChangedHandler;
            }
        }

        private async void CopySelected_Click(object? sender, RoutedEventArgs e)
        {
            var top = GetTopLevel(this);
            if (top?.FocusManager?.GetFocusedElement() is TextBox focused)
            {
                var text = focused.Text ?? string.Empty;
                var start = Math.Max(0, focused.SelectionStart);
                var length = Math.Max(0, focused.SelectionEnd - focused.SelectionStart);
                var sel = length > 0 ? text.Substring(start, length) : string.Empty;

                if (!string.IsNullOrEmpty(sel) && DataContext is LogWindowViewModel vm)
                {
                    vm.CopySelectedCommand.Execute(sel).Subscribe();
                }
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            if (_viewModel != null)
            {
                if (_logsChangedHandler != null)
                {
                    _viewModel.Logs.CollectionChanged -= _logsChangedHandler;
                    _logsChangedHandler = null;
                }
                if (_propChangedHandler != null)
                {
                    _viewModel.PropertyChanged -= _propChangedHandler;
                    _propChangedHandler = null;
                }
                _viewModel?.Dispose();
            }

            base.OnClosed(e);
        }
    }
}