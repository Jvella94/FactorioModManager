using Avalonia.Controls;
using System;
using System.Threading.Tasks;

namespace FactorioModManager.Views.Base
{
    /// <summary>
    /// Base class for dialog windows with result handling
    /// </summary>
    /// <typeparam name="TResult">The type of result returned by the dialog. Use nullable types for dialogs that can be canceled.</typeparam>
    public abstract class DialogWindowBase<TResult> : Window, IDisposable
    {
        /// <summary>
        /// The result of the dialog. Will be null if the dialog was canceled.
        /// </summary>
        protected TResult? Result { get; set; }

        private bool _disposed;

        protected DialogWindowBase()
        {
            // Standard dialog settings
            CanResize = false;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = true;

            // Event handlers
            Opened += OnDialogOpened;

            // ESC key to cancel
            KeyDown += (s, e) =>
            {
                if (e.Key == Avalonia.Input.Key.Escape)
                {
                    Cancel();
                }
            };

            Closed += OnDialogClosed;
        }

        private void OnDialogClosed(object? sender, EventArgs e)
        {
            Dispose();
        }

        /// <summary>
        /// Called when the dialog is opened. Override to set initial focus, etc.
        /// </summary>
        protected virtual void OnDialogOpened(object? sender, EventArgs e)
        {
            // Override in derived classes to set focus, etc.
        }

        /// <summary>
        /// Closes the dialog with a successful result
        /// </summary>
        protected void CloseWithResult(TResult result)
        {
            Result = result;
            Close(result);
        }

        /// <summary>
        /// Cancels the dialog without a result
        /// </summary>
        protected void Cancel()
        {
            Close(default(TResult));
        }

        /// <summary>
        /// Shows the dialog and returns the result
        /// </summary>
        public new async Task<TResult?> ShowDialog(Window owner)
        {
            return await ShowDialog<TResult?>(owner);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Dispose DataContext if it implements IDisposable
                    if (DataContext is IDisposable disposableVm)
                    {
                        disposableVm.Dispose();
                    }
                }
                _disposed = true;
            }
        }
    }
}