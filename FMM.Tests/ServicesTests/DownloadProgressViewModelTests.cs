using FactorioModManager.Services.Infrastructure;
using FactorioModManager.ViewModels.MainWindow;
using FactorioModManager.Models.DTO;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace FMM.Tests.ServicesTests
{
    // Minimal fake UI service that executes posted actions synchronously for tests
    class TestUIService : IUIService
    {
        public void Post(Action action)
        {
            action?.Invoke();
        }

        public Task InvokeAsync(Action action)
        {
            action?.Invoke();
            return Task.CompletedTask;
        }

        public Task<T> InvokeAsync<T>(Func<T> func)
        {
            return Task.FromResult(func());
        }

        public Task ShowMessageAsync(string title, string message, Avalonia.Controls.Window? parentWindow = null) => Task.CompletedTask;
        public Task<bool> ShowConfirmationAsync(string title, string message, Avalonia.Controls.Window? parentWindow = null) => Task.FromResult(true);
        public Task<bool> ShowConfirmationAsync(string title, string message, Avalonia.Controls.Window? parentWindow = null, string yesButtonText = "Yes", string noButtonText = "No", string? yesButtonColor = null, string? noButtonColor = null) => Task.FromResult(true);
        public void OpenUrl(string url) { }
        public void OpenFolder(string path) { }
        public void OpenFile(string path) { }
        public void RevealFile(string path) { }
        public Avalonia.Controls.Window? GetMainWindow() => null;
        public Task<bool> ShowSettingsDialogAsync() => Task.FromResult(false);
        public Task<(bool Success, int Hours)> ShowUpdateCheckDialogAsync() => Task.FromResult((false,0));
        public Task<(bool Success, string? Data, bool IsUrl)> ShowInstallModDialogAsync() => Task.FromResult<(bool,string?,bool)>((false, null, false));
        public Task ShowChangelogAsync(string modTitle, string changelog) => Task.CompletedTask;
        public Task ShowVersionHistoryAsync(string modTitle, string modName, System.Collections.Generic.List<ShortReleaseDTO> releases) => Task.CompletedTask;
        public Task SetClipboardTextAsync(string text) => Task.CompletedTask;
    }

    public class DownloadProgressViewModelTests
    {
        [Fact]
        public void Reporter_ShouldRequireTwoSamples_And_ReportReasonableSpeed()
        {
            var ui = new TestUIService();
            var vm = new DownloadProgressViewModel(ui);

            // Initialize callbacks so reporter is active and updates vm properties via existing helpers
            vm.Initialize(
                isActive: () => true,
                getTotal: () => 1,
                getCompleted: () => 0,
                setSpeedText: s => vm.UpdateSpeedText(s),
                setProgressText: s => vm.UpdateProgressText(s),
                setProgressPercent: d => vm.UpdateProgressPercent(d));

            var reporter = vm.CreateGlobalDownloadProgressReporter();

            // Simulate a download: 0 -> 2_000_000 -> 4_000_000 bytes, spaced by 200ms (clamp is 0.15s)
            reporter.Report((0L, 10000000L));
            // Immediately after first report, speed should not be shown (requires >=2 samples)
            Assert.True(string.IsNullOrEmpty(vm.SpeedText));

            // Wait a bit then report second sample
            Thread.Sleep(220);
            reporter.Report((2_000_000L, 10000000L));

            // Allow UI throttle interval to elapse so speed text updates
            Thread.Sleep(350);

            // After second sample we should have a speed text in MB/s
            Assert.False(string.IsNullOrEmpty(vm.SpeedText));
            Assert.Contains("MB/s", vm.SpeedText ?? string.Empty);

            // Parse numeric part and assert it's in a reasonable range (2 MB/s to 50 MB/s)
            var parts = vm.SpeedText!.Split(' ');
            var numStr = parts.Length > 0 ? parts[0] : "0";
            if (numStr.EndsWith("MB/s")) numStr = numStr.Replace("MB/s", "");
            double.TryParse(numStr, out var value);

            // Value should be > 1 (low) and < 100 (unrealistic), allow generous bounds
            Assert.InRange(value, 1.0, 200.0);

            // Continue reporting more samples to ensure smoothing doesn't drop to zero
            for (int i = 3; i <= 6; i++)
            {
                Thread.Sleep(220);
                reporter.Report(((long)(i - 1) * 2_000_000L, 10000000L));
            }

            Thread.Sleep(350);
            Assert.False(string.IsNullOrEmpty(vm.SpeedText));
            Assert.Contains("MB/s", vm.SpeedText ?? string.Empty);
        }
    }
}
