using System;
using System.Threading;

namespace FactorioModManager.ViewModels.MainWindow
{
    /// <summary>
    /// Small helper to manage a single-shot timer and pending flag for batching UI updates.
    /// Public so multiple viewmodels/features can reuse the same semantics.
    /// </summary>
    public sealed class ProgressTimerHelper : IDisposable
    {
        private readonly TimeSpan _throttle;
        private readonly Action _onElapsed;
        private readonly Timer _timer;
        private volatile bool _pending;

        public ProgressTimerHelper(TimeSpan throttle, Action onElapsed)
        {
            _throttle = throttle;
            _onElapsed = onElapsed ?? throw new ArgumentNullException(nameof(onElapsed));
            _timer = new Timer(_ =>
            {
                if (!_pending) return;
                _pending = false;
                try { _onElapsed(); } catch { }
            }, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }

        public void Schedule()
        {
            _pending = true;
            try { _timer.Change(_throttle, Timeout.InfiniteTimeSpan); } catch { }
        }

        public void FlushIfPending()
        {
            if (!_pending) return;
            _pending = false;
            try { _onElapsed(); } catch { }
        }

        public void Dispose()
        {
            try { _timer.Dispose(); } catch { }
        }
    }
}
