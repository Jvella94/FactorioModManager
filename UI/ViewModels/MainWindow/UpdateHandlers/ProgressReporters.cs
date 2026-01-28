using System.Threading.Tasks;
using FactorioModManager.Models;

namespace FactorioModManager.ViewModels.MainWindow.UpdateHandlers
{
    internal sealed class SingleProgressReporter(IUpdateHost host) : IProgressReporter
    {
        public async Task BeginAsync(int total)
        {
            try { await host.SetStatusAsync($"ProgressReporter: Begin single total={total}", LogLevel.Debug); } catch { }
            try { host.SetDownloadProgressTotal(total); } catch { }
            try { host.SetDownloadProgressCompleted(0); } catch { }
            try { await host.BeginSingleDownloadProgressAsync(); } catch { }
        }

        public void Increment()
        {
            try { host.IncrementDownloadProgressCompleted(); } catch { }
        }

        public async Task EndAsync(bool minimal)
        {
            try { await host.SetStatusAsync($"ProgressReporter: End single minimal={minimal}", LogLevel.Debug); } catch { }
            await host.EndSingleDownloadProgressAsync(minimal);
        }
    }

    internal sealed class BatchProgressReporter(IUpdateHost host) : IProgressReporter
    {
        public async Task BeginAsync(int total)
        {
            try { await host.SetStatusAsync($"ProgressReporter: Begin batch total={total}", LogLevel.Debug); } catch { }
            try { host.SetDownloadProgressTotal(total); } catch { }
            try { host.SetDownloadProgressCompleted(0); } catch { }
            try { host.SetDownloadProgressVisible(true); } catch { }
        }

        public void Increment()
        {
            try { host.IncrementDownloadProgressCompleted(); } catch { }
        }

        public async Task EndAsync(bool minimal)
        {
            try { await host.SetStatusAsync($"ProgressReporter: End batch minimal={minimal}", LogLevel.Debug); } catch { }
            try { host.SetDownloadProgressVisible(false); } catch { }
        }
    }
}