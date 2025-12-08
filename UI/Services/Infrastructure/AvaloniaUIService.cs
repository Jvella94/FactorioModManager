using Avalonia.Controls;
using Avalonia.Threading;
using System;
using System.Threading.Tasks;

namespace FactorioModManager.Services.Infrastructure
{
    public class AvaloniaUIService : IUIService
    {
        public bool IsOnUIThread => Dispatcher.UIThread.CheckAccess();

        public void Post(Action action)
        {
            ArgumentNullException.ThrowIfNull(action);
            Dispatcher.UIThread.Post(action);
        }

        public Task InvokeAsync(Action action)
        {
            ArgumentNullException.ThrowIfNull(action);
            return Dispatcher.UIThread.InvokeAsync(action).GetTask();
        }

        public Task<T> InvokeAsync<T>(Func<T> func)
        {
            ArgumentNullException.ThrowIfNull(func);
            return Dispatcher.UIThread.InvokeAsync(func).GetTask();
        }

        public async Task InvokeAsync(Func<Task> asyncAction)
        {
            ArgumentNullException.ThrowIfNull(asyncAction);
            await Dispatcher.UIThread.InvokeAsync(asyncAction);
        }

        public async Task ShowMessageAsync(string title, string message)
        {
            var window = new Window
            {
                Title = title,
                Width = 500,
                Height = 300,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new ScrollViewer
                {
                    Content = new TextBlock
                    {
                        Text = message,
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                        Margin = new Avalonia.Thickness(15)
                    }
                }
            };

            await window.ShowDialog(GetMainWindow());
        }

#pragma warning disable CA1822 // Mark members as static
        private Window GetMainWindow()
#pragma warning restore CA1822 // Mark members as static
        {
            return Avalonia.Application.Current?.ApplicationLifetime
                is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow!
                : null!;
        }
    }
}