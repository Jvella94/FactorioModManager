using Avalonia.Controls; // Add Avalonia dependency for Window type compatibility
using FactorioModManager.Services.Infrastructure;
using System;
using System.Threading.Tasks;

#nullable enable // Enable nullable annotations

namespace Tests.Mocks
{
    public class MockUIService : IUIService
    {
        public void Post(Action action)
        {
            action();
        }

        public Task InvokeAsync(Action action)
        {
            action();
            return Task.CompletedTask;
        }

        public Task<T> InvokeAsync<T>(Func<T> func)
        {
            return Task.FromResult(func());
        }

        public Task ShowMessageAsync(string title, string message, Window? parentWindow = null)
        {
            return Task.CompletedTask;
        }

        public Task<bool> ShowConfirmationAsync(string title, string message, Window? parentWindow = null)
        {
            return Task.FromResult<bool>(true); // Correct explicit type argument for Task.FromResult
        }

        public Task<bool> ShowConfirmationAsync(string title, string message, Window? parentWindow, string yesButtonText, string noButtonText, string? yesButtonColor, string? noButtonColor)
        {
            return Task.FromResult<bool>(true); // Correct explicit type argument for Task.FromResult
        }

        public void OpenUrl(string url)
        {
        }

        public void OpenFolder(string path)
        {
        }

        public Window? GetMainWindow()
        {
            return null; // Adjust return type to match interface
        }

        public Task<bool> ShowSettingsDialogAsync()
        {
            return Task.FromResult<bool>(true); // Correct explicit type argument for Task.FromResult
        }

        public Task<(bool Success, int Hours)> ShowUpdateCheckDialogAsync()
        {
            return Task.FromResult<(bool, int)>((true, 0)); // Explicitly specify the tuple type for Task.FromResult
        }

        public Task<(bool Success, string? Data, bool IsUrl)> ShowInstallModDialogAsync()
        {
            return Task.FromResult<(bool Success, string? Data, bool IsUrl)>((true, null, false)); // Explicitly specify the tuple type for Task.FromResult
        }

        public Task ShowChangelogAsync(string modTitle, string changelog)
        {
            return Task.CompletedTask;
        }

        public Task ShowVersionHistoryAsync(string modTitle, string modName, System.Collections.Generic.List<FactorioModManager.Models.DTO.ShortReleaseDTO> releases)
        {
            return Task.CompletedTask;
        }
    }
}