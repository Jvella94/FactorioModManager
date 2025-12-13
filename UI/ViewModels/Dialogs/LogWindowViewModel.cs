// ViewModels/LogWindowViewModel.cs
using FactorioModManager.Models;
using FactorioModManager.Services.Infrastructure;
using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace FactorioModManager.ViewModels.Dialogs
{
    public class LogWindowViewModel : ViewModelBase
    {
        private readonly ILogService _logService;
        private readonly IUIService _uiService;
        private readonly CompositeDisposable _disposables = [];

        private ObservableCollection<LogEntry> _logs;
        private ObservableCollection<LogEntry> _filteredLogs;
        private bool _isLoading = true;
        private bool _isFollowing = true;
        private string _selectedLevelFilter = "All";

        public bool IsLoading
        {
            get => _isLoading;
            private set => this.RaiseAndSetIfChanged(ref _isLoading, value);
        }

        public ObservableCollection<LogEntry> Logs
        {
            get => _logs;
            private set => this.RaiseAndSetIfChanged(ref _logs, value);
        }

        public ObservableCollection<LogEntry> FilteredLogs
        {
            get => _filteredLogs;
            private set => this.RaiseAndSetIfChanged(ref _filteredLogs, value);
        }

        public bool IsFollowing
        {
            get => _isFollowing;
            set => this.RaiseAndSetIfChanged(ref _isFollowing, value);
        }

        public ObservableCollection<string> LevelFilters { get; } = new ObservableCollection<string>(new[] { "All", "Info", "Debug", "Warning", "Error" });

        public string SelectedLevelFilter
        {
            get => _selectedLevelFilter;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedLevelFilter, value);
                ApplyFilter();
            }
        }

        public string LogCountText => $"{FilteredLogs.Count} log entries";

        public ReactiveCommand<Unit, Unit> RefreshCommand { get; }
        public ReactiveCommand<Unit, Unit> ClearLogsCommand { get; }
        public ReactiveCommand<Unit, Unit> ArchiveLogsCommand { get; }
        public ReactiveCommand<Unit, Unit> OpenLogFileCommand { get; }
        public ReactiveCommand<Unit, Unit> CopyAllCommand { get; }
        public ReactiveCommand<string, Unit> CopySelectedCommand { get; }

        private readonly IDisposable _logsUpdatedSubscription;

        public LogWindowViewModel(ILogService logService, IUIService uiService)
        {
            _logService = logService;
            _uiService = uiService;
            _logs = [];
            _filteredLogs = [];

            RefreshCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                IsLoading = true;
                try
                {
                    // Load your logs here
                    await Task.Run(() => LoadLogs());
                }
                finally
                {
                    IsLoading = false;
                }
            });
            ClearLogsCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                _logService.ClearLogs();
                LoadLogs();
            });
            ArchiveLogsCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                _logService.ArchiveLogs();
                LoadLogs();
            });

            OpenLogFileCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                try
                {
                    var logFilePath = _logService.GetLogFilePath();
                    if (!File.Exists(logFilePath))
                    {
                        await _uiService.ShowMessageAsync("Error", $"Log file not found: {logFilePath}");
                        return;
                    }

                    _uiService.OpenFile(logFilePath);
                }
                catch (Exception ex)
                {
                    _logService.LogError($"Failed to open log file: {ex.Message}", ex);
                    try { await _uiService.ShowMessageAsync("Error", $"Failed to open log file: {ex.Message}"); } catch { }
                }
            });

            CopyAllCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                var text = string.Join(Environment.NewLine, FilteredLogs.Select(l => $"[{l.Timestamp:yyyy-MM-dd HH:mm:ss}] [{l.Level}] {l.Message}"));
                try
                {
                    await _uiService.SetClipboardTextAsync(text);
                }
                catch { }
            });

            CopySelectedCommand = ReactiveCommand.CreateFromTask<string>(async selectedText =>
            {
                if (string.IsNullOrEmpty(selectedText)) return;
                try
                {
                    await _uiService.SetClipboardTextAsync(selectedText);
                }
                catch { }
            });

            // Subscribe to LogService updates and debounce to avoid rapid UI refreshes
            _logsUpdatedSubscription = Observable
                .FromEventPattern<EventHandler, EventArgs>(h => _logService.LogsUpdated += h, h => _logService.LogsUpdated -= h)
                .Select(_ => Unit.Default)
                .Throttle(TimeSpan.FromMilliseconds(500))
                .ObserveOn(RxApp.MainThreadScheduler)
                .Subscribe(async _ =>
                {
                    await RefreshCommand.Execute();
                    if (IsFollowing)
                    {
                        // request UI thread to scroll to end via UI service
                        _uiService.Post(() =>
                        {
                            // The view will call ScrollToEnd when it sees DataContext refreshed - this is a best effort hook
                        });
                    }
                });

            _disposables.Add(_logsUpdatedSubscription);

        }

        private void LoadLogs()
        {
            var rawLogs = _logService.GetLogs().ToList();

            Logs.Clear();
            foreach (var log in rawLogs)
            {
                Logs.Add(log);
            }

            ApplyFilter();
        }

        private void ApplyFilter()
        {
            var filter = SelectedLevelFilter ?? "All";
            var items = filter == "All"
                ? Logs.ToList()
                : [.. Logs.Where(l => l.Level.ToString() == filter)];

            FilteredLogs.Clear();
            foreach (var it in items)
                FilteredLogs.Add(it);

            this.RaisePropertyChanged(nameof(LogCountText));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _disposables?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}