using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FactorioModManager.Services.Infrastructure
{
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
        /// Shows a simple message dialog
        /// </summary>
        Task ShowMessageAsync(string title, string message, Window? parentWindow = null);

        /// <summary>
        /// Shows a confirmation dialog with Yes/No buttons
        /// </summary>
        Task<bool> ShowConfirmationAsync(string title, string message, Window? parentWindow = null);

        /// <summary>
        /// Shows a customizable confirmation dialog with Yes/No buttons and custom colors
        /// </summary>
        Task<bool> ShowConfirmationAsync(
            string title,
            string message,
            Window? parentWindow = null,
            string yesButtonText = "Yes",
            string noButtonText = "No",
            string? yesButtonColor = null,
            string? noButtonColor = null);

        /// <summary>
        /// Opens a URL in the default browser
        /// </summary>
        void OpenUrl(string url);

        /// <summary>
        /// Opens a folder in the file explorer
        /// </summary>
        void OpenFolder(string path);

        /// <summary>
        /// Gets the main application window
        /// </summary>
        Window? GetMainWindow();

        /// <summary>
        /// Shows the settings dialog
        /// </summary>
        Task<bool> ShowSettingsDialogAsync();

        /// <summary>
        /// Shows the update check dialog
        /// </summary>
        Task<(bool Success, int Hours)> ShowUpdateCheckDialogAsync();

        /// <summary>
        /// Shows the install mod dialog
        /// </summary>
        Task<(bool Success, string? Data, bool IsUrl)> ShowInstallModDialogAsync();

        /// <summary>
        /// Shows the changelog window
        /// </summary>
        Task ShowChangelogAsync(string modTitle, string changelog);

        /// <summary>
        /// Shows the version history window
        /// </summary>
        Task ShowVersionHistoryAsync(string modTitle, string modName,
            List<Models.DTO.ReleaseDTO> releases);
    }
}