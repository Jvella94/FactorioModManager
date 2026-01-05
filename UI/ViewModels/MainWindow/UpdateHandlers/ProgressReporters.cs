using System.Threading.Tasks;

namespace FactorioModManager.ViewModels.MainWindow.UpdateHandlers
{
    internal sealed class SingleProgressReporter(IUpdateHost host) : IProgressReporter
    {
        public async Task BeginAsync(int total)
        {
            try { host.SetDownloadProgressTotal(total); } catch { }
            try { host.SetDownloadProgressCompleted(0); } catch { }
            try { await host.BeginSingleDownloadProgressAsync(); } catch { }
        }

        public void Increment()
        {
            try { host.IncrementDownloadProgressCompleted(); } catch { }
        }

        public async Task EndAsync(bool minimal) => await host.EndSingleDownloadProgressAsync(minimal);
    }

    internal sealed class BatchProgressReporter(IUpdateHost host) : IProgressReporter
    {
        public async Task BeginAsync(int total)
        {
            try { host.SetDownloadProgressTotal(total); } catch { }
            try { host.SetDownloadProgressCompleted(0); } catch { }
            try { await host.BeginSingleDownloadProgressAsync(); } catch { }
        }

        public void Increment()
        {
            try { host.IncrementDownloadProgressCompleted(); } catch { }
        }

        public Task EndAsync(bool minimal) => Task.CompletedTask;
    }
}