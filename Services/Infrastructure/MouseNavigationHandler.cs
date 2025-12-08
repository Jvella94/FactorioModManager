using Avalonia.Controls;
using Avalonia.Input;
using FactorioModManager.ViewModels.MainWindow;
using ReactiveUI;
using System;
using System.Reactive;

namespace FactorioModManager.Infrastructure
{
    /// <summary>
    /// Handles mouse button navigation (back/forward buttons)
    /// </summary>
    public class MouseNavigationHandler : IDisposable
    {
        private readonly Window _window;
        private bool _disposed;

        public MouseNavigationHandler(Window window)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
            _window.PointerPressed += OnPointerPressed;
        }

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (_window.DataContext is not MainWindowViewModel vm)
                return;

            var properties = e.GetCurrentPoint(_window).Properties;

            if (properties.IsXButton1Pressed && vm.CanNavigateBack)
            {
                vm.NavigateBackCommand.Execute(Unit.Default).Subscribe();
                e.Handled = true;
            }
            else if (properties.IsXButton2Pressed && vm.CanNavigateForward)
            {
                vm.NavigateForwardCommand.Execute(Unit.Default).Subscribe();
                e.Handled = true;
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _window.PointerPressed -= OnPointerPressed;
            _disposed = true;
        }
    }
}
