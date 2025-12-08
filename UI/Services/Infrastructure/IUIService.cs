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

        /// <summary>
        /// Displays a message dialog asynchronously with the specified title and content.
        /// </summary>
        /// <param name="title">The title text to display in the message dialog. Cannot be null or empty.</param>
        /// <param name="message">The message content to display in the dialog. Cannot be null.</param>
        /// <returns>A task that represents the asynchronous operation of showing the message dialog.</returns>
        Task ShowMessageAsync(string title, string message);
    }
}