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
    }
}