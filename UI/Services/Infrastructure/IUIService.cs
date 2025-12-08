using System;
using System.Threading.Tasks;

namespace FactorioModManager.Services.Infrastructure
{
    /// <summary>
    /// Abstracts UI thread operations for testability and maintainability
    /// </summary>
    public interface IUIService
    {
        /// <summary>
        /// Posts an action to the UI thread (fire and forget)
        /// </summary>
        void Post(Action action);

        /// <summary>
        /// Invokes an action on the UI thread and waits for completion
        /// </summary>
        Task InvokeAsync(Action action);

        /// <summary>
        /// Invokes a function on the UI thread and returns the result
        /// </summary>
        Task<T> InvokeAsync<T>(Func<T> func);

        /// <summary>
        /// Invokes an async function on the UI thread
        /// </summary>
        Task InvokeAsync(Func<Task> asyncAction);

        /// <summary>
        /// Checks if the current thread is the UI thread
        /// </summary>
        bool IsOnUIThread { get; }
    }
}