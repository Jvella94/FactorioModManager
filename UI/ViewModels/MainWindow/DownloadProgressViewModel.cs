using FactorioModManager.Services;
using FactorioModManager.Services.Infrastructure;
using System;
using System.ComponentModel;
using System.Threading;

namespace FactorioModManager.ViewModels.MainWindow
{
    /// <summary>
    /// Encapsulates global download progress UI state and provides helpers
    /// to produce progress reporters used by various download flows.
    /// This allows download UI concerns to be reused across features (updates,
    /// version history installs, manual installs, etc.).
    /// </summary>
    public class DownloadProgressViewModel(IUIService uiService) : IDownloadProgress, IDisposable, INotifyPropertyChanged
    {
        private readonly IUIService _uiService = uiService ?? throw new ArgumentNullException(nameof(uiService));

        // Current callbacks (mutable)
        private Func<bool> _isActive = () => false;

        private Func<int> _getTotal = () => 0;
        private Func<int> _getCompleted = () => 0;
        private Action<string?> _setSpeedText = _ => { };
        private Action<string?> _setProgressText = _ => { };
        private Action<double> _setProgressPercent = _ => { };

        private static readonly TimeSpan _speedUiThrottleInterval = TimeSpan.FromMilliseconds(300);

        // Lock for Initialize to make it thread-safe
        private readonly object _initializeLock = new object();

        private bool _initialized = false;

        // Animation state
        private Timer? _animationTimer;

        private double _currentPercent = 0.0;
        private double _targetPercent = 0.0;
        private static readonly TimeSpan _animationInterval = TimeSpan.FromMilliseconds(40);
        private const double _animationAlpha = 0.25;

        // track whether timer is running
        private readonly object _animationLock = new object();

        // Exposed presentation properties (moved here)
        private double _progressPercent;

        public double ProgressPercent
        {
            get => _progressPercent;
            private set
            {
                if (Math.Abs(_progressPercent - value) > 0.0001)
                {
                    _progressPercent = value;
                    OnPropertyChanged(nameof(ProgressPercent));
                }
            }
        }

        private string? _speedText;

        public string? SpeedText
        {
            get => _speedText;
            private set
            {
                if (_speedText != value)
                {
                    _speedText = value;
                    OnPropertyChanged(nameof(SpeedText));
                }
            }
        }

        private string? _progressText;

        public string? ProgressText
        {
            get => _progressText;
            private set
            {
                if (_progressText != value)
                {
                    _progressText = value;
                    OnPropertyChanged(nameof(ProgressText));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        /// <summary>
        /// Initialize the singleton instance to use the provided callbacks. Safe to call multiple times.
        /// This method is thread-safe. If already initialized, it will be a no-op unless the provided
        /// callbacks are identical to the ones already set. Attempting to re-initialize with different
        /// callbacks is ignored to avoid breaking existing callers.
        /// </summary>
        public void Initialize(
            Func<bool> isActive,
            Func<int> getTotal,
            Func<int> getCompleted,
            Action<string?> setSpeedText,
            Action<string?> setProgressText,
            Action<double> setProgressPercent)
        {
            isActive ??= () => false;
            getTotal ??= () => 0;
            getCompleted ??= () => 0;
            setSpeedText ??= _ => { };
            setProgressText ??= _ => { };
            setProgressPercent ??= _ => { };

            lock (_initializeLock)
            {
                if (!_initialized)
                {
                    _isActive = isActive;
                    _getTotal = getTotal;
                    _getCompleted = getCompleted;
                    _setSpeedText = setSpeedText;
                    _setProgressText = setProgressText;
                    _setProgressPercent = setProgressPercent;
                    _initialized = true;
                    return;
                }

                // Already initialized: allow re-initialize only if callbacks are identical (no-op),
                // otherwise ignore to avoid incompatible changes at runtime.
                bool same = DelegateEquals(_isActive, isActive)
                    && DelegateEquals(_getTotal, getTotal)
                    && DelegateEquals(_getCompleted, getCompleted)
                    && DelegateEquals(_setSpeedText, setSpeedText)
                    && DelegateEquals(_setProgressText, setProgressText)
                    && DelegateEquals(_setProgressPercent, setProgressPercent);

                if (same)
                {
                    // identical; nothing to do
                    return;
                }

                // incompatible re-initialization requested -> no-op guard: ignore silently
                return;
            }
        }

        private static bool DelegateEquals(Delegate? a, Delegate? b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            return a.Equals(b);
        }

        private static string FormatSpeedText(double bytesPerSecond)
        {
            if (bytesPerSecond <= 0)
                return string.Empty;

            const double KB = 1024.0;
            const double MB = KB * 1024.0;

            if (bytesPerSecond < KB)
                return $"{bytesPerSecond:F0} B/s";
            if (bytesPerSecond < MB)
                return $"{(bytesPerSecond / KB):F1} KB/s";
            return $"{(bytesPerSecond / MB):F2} MB/s";
        }

        /// <summary>
        /// Creates a stateful global progress reporter for downloads that updates
        /// the provided speed/progress callbacks. Callers should keep and use
        /// their own reporter instance while downloading.
        /// </summary>
        public IProgress<(long bytesDownloaded, long? totalBytes)> CreateGlobalDownloadProgressReporter()
        {
            DateTime lastTime = DateTime.UtcNow;
            long lastBytes = 0;
            double smoothedBytesPerSec = 0;
            DateTime lastUiUpdate = DateTime.MinValue;
            double lastReportedSpeed = 0;
            const double alpha = 0.2;

            return new Progress<(long bytesDownloaded, long? totalBytes)>(p =>
             {
                 if (!_isActive())
                     return;

                 var now = DateTime.UtcNow;
                 var deltaBytes = p.bytesDownloaded - lastBytes;
                 var deltaSeconds = (now - lastTime).TotalSeconds;
                 // Avoid huge spikes from extremely small intervals by clamping the deltaSeconds
                 var deltaSecondsSafe = Math.Max(deltaSeconds, 0.25);

                 double instBytesPerSec = 0;
                 if (deltaBytes >= 0)
                 {
                     instBytesPerSec = deltaBytes / deltaSecondsSafe;
                     if (smoothedBytesPerSec <= 0)
                         smoothedBytesPerSec = instBytesPerSec;
                     else
                         smoothedBytesPerSec = alpha * instBytesPerSec + (1 - alpha) * smoothedBytesPerSec;
                 }

                 var speedText = smoothedBytesPerSec > 0 ? FormatSpeedText(smoothedBytesPerSec) : null;

                 if ((now - lastUiUpdate) >= _speedUiThrottleInterval)
                 {
                     var speedDelta = Math.Abs(smoothedBytesPerSec - lastReportedSpeed);
                     var minSignificantChange = Math.Max(1024.0, lastReportedSpeed * 0.10);

                     // Update if speed changed significantly or toggled between empty/non-empty
                     if (lastReportedSpeed == 0 || speedDelta >= minSignificantChange || string.IsNullOrEmpty(speedText) != string.IsNullOrEmpty((string?)null))
                     {
                         // Use UI thread posting for safety
                         _uiService.Post(() =>
                         {
                             // Update helper presentation property via provided callback
                             _setSpeedText(speedText);

                             // Keep count text in sync with underlying values
                             var total = _getTotal();
                             var completed = _getCompleted();
                             _setProgressText(total > 0 ? $"{completed}/{total}" : string.Empty);
                         });

                         lastReportedSpeed = smoothedBytesPerSec;
                     }

                     lastUiUpdate = DateTime.UtcNow;
                 }

                 // Also update visible percent when possible
                 try
                 {
                     var total = _getTotal();
                     var completed = _getCompleted();
                     if (total > 0)
                     {
                         // combined percent = (completed + in-flight fraction)/total * 100
                         var fraction = p.totalBytes.HasValue && p.totalBytes.Value > 0
                             ? (double)p.bytesDownloaded / p.totalBytes.Value
                             : 0.0;

                         var combinedPercent = (completed + fraction) / Math.Max(1, total) * 100.0;
                         _uiService.Post(() => _setProgressPercent(combinedPercent));
                     }
                 }
                 catch { }

                 lastBytes = p.bytesDownloaded;
                 lastTime = now;
             });
        }

        // Animation helpers - these expose control to other callers so animation is centralized
        public void SetTargetPercent(double percent)
        {
            _targetPercent = Math.Max(0.0, Math.Min(100.0, percent));
            StartAnimation();
        }

        public void StartAnimation()
        {
            lock (_animationLock)
            {
                if (_animationTimer != null)
                    return;

                _animationTimer = new Timer(_ =>
                {
                    // simple smoothing towards target
                    var current = _currentPercent;
                    var delta = _targetPercent - current;
                    if (Math.Abs(delta) < 0.05)
                    {
                        _currentPercent = _targetPercent;
                        _uiService.Post(() => _setProgressPercent(_currentPercent));
                        StopAnimationTimer();
                        return;
                    }

                    var mag = Math.Abs(delta);
                    double alpha = _animationAlpha;
                    if (mag < 5.0) alpha = _animationAlpha * 0.45;
                    else if (mag > 20.0) alpha = _animationAlpha * 1.4;

                    var next = current + delta * alpha;

                    // Only post if UI is active via callback
                    try
                    {
                        if (!_isActive()) return;
                    }
                    catch { }

                    _currentPercent = next;
                    _uiService.Post(() => _setProgressPercent(_currentPercent));
                }, null, _animationInterval, _animationInterval);
            }
        }

        private void StopAnimationTimer()
        {
            lock (_animationLock)
            {
                try { _animationTimer?.Dispose(); } catch { }
                _animationTimer = null;
            }
        }

        public void StopAndSetPercent(double percent)
        {
            StopAnimationTimer();
            _currentPercent = Math.Max(0.0, Math.Min(100.0, percent));
            _uiService.Post(() => _setProgressPercent(_currentPercent));
        }

        // Helpers for external callbacks to update this viewmodel's exposed properties
        public void UpdateSpeedText(string? text)
        {
            // call on UI thread for safety
            _uiService.Post(() => SpeedText = text);
        }

        public void UpdateProgressText(string? text)
        {
            _uiService.Post(() => ProgressText = text);
        }

        public void UpdateProgressPercent(double percent)
        {
            _uiService.Post(() => ProgressPercent = percent);
        }

        public void Dispose()
        {
            StopAnimationTimer();
            GC.SuppressFinalize(this);
        }
    }
}